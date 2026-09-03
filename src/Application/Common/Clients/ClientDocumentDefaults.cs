using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Settings;
using ClientEntity = RemSolution.Domain.Entities.Client;

namespace RemSolution.Application.Common.Clients;

/// <summary>
/// Fills in the identity-document expiry dates nobody typed, from the issue date
/// and the agency's default validity. Every path that writes a client's documents
/// goes through here — the agency screen, the booking form's new client — because
/// a derived expiry that only some paths apply is worse than none: the licence
/// check at booking time would then pass or fail depending on which screen
/// created the client.
/// </summary>
public static class ClientDocumentDefaults
{
    public static Task ApplyAsync(
        ClientEntity client,
        IAgencySettingsProvider settings,
        ITenantProvider tenant,
        CancellationToken cancellationToken) =>
        // No tenant (platform admin outside a workspace, a background job): no
        // settings to read, so nothing is derived and the dates stay as typed.
        tenant.AgencyId is int agencyId
            ? ApplyAsync(client, settings, agencyId, cancellationToken)
            : Task.CompletedTask;

    public static async Task ApplyAsync(
        ClientEntity client,
        IAgencySettingsProvider settings,
        int agencyId,
        CancellationToken cancellationToken)
    {
        var snapshot = await settings.GetAsync(agencyId, cancellationToken);

        client.ApplyDefaultDocumentExpiries(
            snapshot.CINValidityYears,
            snapshot.PasseportValidityYears,
            snapshot.DrivingLicenceValidityYears);
    }
}
