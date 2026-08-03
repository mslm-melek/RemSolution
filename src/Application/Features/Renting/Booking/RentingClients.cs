using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Domain.Constants;
using FluentValidation.Results;
using ClientEntity = RemSolution.Domain.Entities.Client;

namespace RemSolution.Application.Features.Renting.Booking
{
    /// <summary>
    /// The dependencies <see cref="RentingClients"/> needs, bundled so the call
    /// sites stay readable — the same trade-off <see cref="Entitlements"/> makes
    /// with its long parameter list, one step further because two commands now
    /// make the same call.
    /// </summary>
    public sealed record RentingClientContext(
        IApplicationDbContext Context,
        IUser User,
        IIdentityService IdentityService,
        ITenantProvider Tenant,
        TimeProvider DateTime);

    /// <summary>
    /// Turns "who is on this booking" into <see cref="ClientEntity"/> rows: a picked
    /// client, or one created from an inline payload.
    /// <para>
    /// Shared by the renting create and update handlers, and within each of them by
    /// the renter and the second driver, so the gates and the dedup rule are stated
    /// once. Every inline creation applies the three the standalone
    /// <c>CreateClientCommand</c> does — Client.Create, the Clients feature, and the
    /// plan's MaxClients quota — plus the CIN/passport dedup rule from
    /// <c>ConvertReservationCommand</c>: if the agency already holds a client with
    /// that document, the booking links to THEM rather than adding a duplicate for
    /// the same person.
    /// </para>
    /// <para>
    /// Callers run this inside their own transaction and write lock: it calls
    /// SaveChanges to obtain the new client's key, which is not durable until the
    /// caller commits. A booking that then fails takes the inline client with it.
    /// </para>
    /// </summary>
    public static class RentingClients
    {
        /// <summary>
        /// The renter. Exactly one of <paramref name="clientId"/> /
        /// <paramref name="payload"/> is expected (the commands' validators enforce
        /// it); the record is returned rather than a bare id because the callers
        /// also provision the client's portal account from it.
        /// <para>
        /// Throws <see cref="ArgumentException"/> if given neither — that is a
        /// caller bug, not bad input, and a booking with nobody on it must not be
        /// expressible. Use <see cref="ResolveSecondDriverAsync"/> where "nobody" is
        /// a legitimate answer.
        /// </para>
        /// </summary>
        public static async Task<ClientEntity> ResolveAsync(
            RentingClientContext ctx,
            int? clientId,
            NewRentingClient? payload,
            CancellationToken cancellationToken)
        {
            if (payload is null)
            {
                if (clientId is not int id)
                {
                    throw new ArgumentException(
                        "A client id or an inline client payload is required.", nameof(clientId));
                }

                var picked = await ctx.Context.Clients
                    .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

                Guard.Against.NotFound(id, picked);

                return picked;
            }

            await EnsureMayCreateAsync(ctx, cancellationToken);

            var cin = Trimmed(payload.CIN);
            var passport = Trimmed(payload.PasseportNumber);

            if (cin is not null || passport is not null)
            {
                var existing = await ctx.Context.Clients
                    .FirstOrDefaultAsync(
                        c => (cin != null && c.CIN == cin) || (passport != null && c.PasseportNumber == passport),
                        cancellationToken);

                if (existing is not null)
                {
                    // Fill blanks only: the stored record is the agency's own and
                    // must not be overwritten by whatever was typed at the counter.
                    Enrich(existing, payload);
                    return existing;
                }
            }

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                ctx.Context, ctx.Tenant, ctx.DateTime, ctx.Context.Clients,
                p => p.MaxClients, "clients", cancellationToken);

            var client = new ClientEntity
            {
                FirstName = payload.FirstName,
                LastName = payload.LastName,
                Email = Trimmed(payload.Email),
                BirthDate = payload.BirthDate,
                BirthPlace = payload.BirthPlace,
                BirthCountryId = payload.BirthCountryId,
                CIN = cin,
                CINDeliveranceDate = payload.CINDeliveranceDate,
                CINDeliverancePlace = payload.CINDeliverancePlace,
                CINDeliveranceCountryId = payload.CINDeliveranceCountryId,
                PasseportNumber = passport,
                PasseportDeliveranceDate = payload.PasseportDeliveranceDate,
                PasseportDeliverancePlace = payload.PasseportDeliverancePlace,
                PasseportDeliveranceCountryId = payload.PasseportDeliveranceCountryId,
                DrivingLicenceNumber = Trimmed(payload.DrivingLicenceNumber),
                DrivingLicenceDeliveranceDate = payload.DrivingLicenceDeliveranceDate,
                DrivingLicenceDeliverancePlace = payload.DrivingLicenceDeliverancePlace,
                DrivingLicenceDeliveranceCountryId = payload.DrivingLicenceDeliveranceCountryId,
                Description = payload.Description
                // AgencyId is stamped by TenantEntityInterceptor on insert.
            };

            ctx.Context.Clients.Add(client);

            // The booking needs the client's key. Inside the caller's transaction,
            // so this save is not yet durable either.
            await ctx.Context.SaveChangesAsync(cancellationToken);

            return client;
        }

        /// <summary>
        /// The second driver, or null when the booking has none. Same rules as
        /// <see cref="ResolveAsync"/>, plus the one that only applies here: the
        /// second driver must be a different person from
        /// <paramref name="renter"/>.
        /// <para>
        /// That check has to happen after resolving rather than on the ids alone,
        /// because the dedup rule above can land on the renter — typing the renter's
        /// own CIN into the second-driver form would otherwise silently produce a
        /// booking whose two drivers are one row.
        /// </para>
        /// </summary>
        public static async Task<ClientEntity?> ResolveSecondDriverAsync(
            RentingClientContext ctx,
            ClientEntity renter,
            int? secondClientId,
            NewRentingClient? secondPayload,
            CancellationToken cancellationToken)
        {
            if (secondClientId is null && secondPayload is null)
            {
                return null;
            }

            var second = await ResolveAsync(ctx, secondClientId, secondPayload, cancellationToken);

            if (second.Id == renter.Id)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure("SecondClientId",
                        "The second driver must be a different person from the renter.")
                });
            }

            return second;
        }

        private static Task EnsureMayCreateAsync(
            RentingClientContext ctx, CancellationToken cancellationToken) =>
            Entitlements.EnsureAsync(
                ctx.User, ctx.IdentityService, ctx.Context, ctx.Tenant, ctx.DateTime,
                Permissions.ClientCreate, FeatureFlags.Clients, cancellationToken);

        private static void Enrich(ClientEntity client, NewRentingClient payload)
        {
            // Including the email: a client the agency has known offline for years
            // gets a login the first time somebody types their address at the
            // counter. Still blanks-only — an address already on file is the one
            // their account is keyed to.
            if (string.IsNullOrWhiteSpace(client.Email))
            {
                client.Email = Trimmed(payload.Email);
            }

            if (string.IsNullOrWhiteSpace(client.CIN))
            {
                client.CIN = Trimmed(payload.CIN);
            }

            if (string.IsNullOrWhiteSpace(client.PasseportNumber))
            {
                client.PasseportNumber = Trimmed(payload.PasseportNumber);
            }

            if (string.IsNullOrWhiteSpace(client.DrivingLicenceNumber))
            {
                client.DrivingLicenceNumber = Trimmed(payload.DrivingLicenceNumber);
            }
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
