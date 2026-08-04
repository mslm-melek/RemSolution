using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    public class CarDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public string Matricule { get; init; } = string.Empty;
        public int? ModelId { get; init; }
        public string? ModelName { get; init; }
        public int? BranchId { get; init; }
        public string? BranchName { get; init; }
        public CarStatus Status { get; init; }
        public MoneyDto? DailyRate { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }

        /// <summary>
        /// The car's odometer as the agency last saw it (see Car.Mileage). The
        /// booking screen offers it as the pickup reading, so it travels on every
        /// car the picker lists, not just the one being viewed.
        /// </summary>
        public int? Mileage { get; init; }

        /// <summary>
        /// Whether a hire is running on this car right now — the same rule the
        /// fleet's "on rent" figure counts by (see the OnRent filter and the
        /// dashboard). Status above is the administrative one (Active /
        /// Maintenance / Inactive) and says nothing about custody; a screen needs
        /// both to answer "can I rent this out?".
        /// </summary>
        public bool IsOnRent { get; init; }

        /// <summary>Hires this car has had, cancelled ones excluded — its history size.</summary>
        public int RentingCount { get; init; }

        /// <summary>
        /// The hire currently running (null unless <see cref="IsOnRent"/>), so a
        /// list row can offer the return action without opening the booking. The
        /// concurrency token is deliberately absent: a return re-reads the renting
        /// to get a fresh one rather than echoing a token aged by the page.
        /// </summary>
        public CarRentingSummaryDto? CurrentRenting { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Car, CarDto>()
                      .Map(dest => dest.ModelName, src => src.Model != null ? src.Model.Name : string.Empty)
                      .Map(dest => dest.BranchName, src => src.Branch != null ? src.Branch.Name : null)
                      // The photo now lives on a StoredFile; surface the plain
                      // URL so the API contract is unchanged for readers.
                      .Map(dest => dest.ImageUrl, src => src.PhotoFile != null ? src.PhotoFile.Url : null)
                      // Projected in SQL over the car's hires, like RentingDto's
                      // paid/outstanding sums — no second round-trip per row. The
                      // null checks are for the in-memory adapter, which a plain
                      // entity with no collection loaded goes through (see
                      // MappingTests); in SQL the subqueries answer for themselves.
                      .Map(dest => dest.IsOnRent,
                           src => src.Rentings != null
                                  && src.Rentings.Any(r => r.RentingState == RentingState.InProgress))
                      .Map(dest => dest.RentingCount,
                           src => src.Rentings == null
                               ? 0
                               : src.Rentings.Count(r => r.RentingState != RentingState.Cancelled))
                      // The overlap rule (see IAvailabilityChecker) lets at most one
                      // hire run at a time, so "the latest InProgress one" is the
                      // one holding the car; ordering keeps the row deterministic
                      // even if data predating the rule says otherwise.
                      .Map(dest => dest.CurrentRenting,
                           src => src.Rentings == null
                               ? null
                               : src.Rentings
                                     .Where(r => r.RentingState == RentingState.InProgress)
                                     .OrderByDescending(r => r.StartDate)
                                     .Select(r => new CarRentingSummaryDto
                                     {
                                         Id = r.Id,
                                         ClientId = r.ClientId,
                                         ClientName = r.Client != null
                                             ? r.Client.FirstName + " " + r.Client.LastName
                                             : null,
                                         StartDate = r.StartDate,
                                         EndDate = r.EndDate,
                                         StartMileage = r.StartMileage
                                     })
                                     .FirstOrDefault());
            }
        }
    }
}
