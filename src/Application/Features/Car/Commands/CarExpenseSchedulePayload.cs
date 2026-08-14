using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Car.Commands
{
    /// <summary>
    /// One expense type on the car being saved. Null means "follow the type or the
    /// agency"; maps one to one onto <c>CarExpenseSchedule</c>.
    /// </summary>
    public record CarExpenseScheduleInput
    {
        public int ExpenseTypeId { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
        public int? LeadKilometers { get; init; }
        public int? LeadDays { get; init; }
        public int? LastDoneMileage { get; init; }
        public DateTime? LastDoneOn { get; init; }
    }

    /// <summary>
    /// Shared by create and update. The types existing and being notifiable needs
    /// the database and is checked in <see cref="CarExpenseSchedulePayload"/>.
    /// </summary>
    public class CarExpenseScheduleInputValidator : AbstractValidator<CarExpenseScheduleInput>
    {
        public CarExpenseScheduleInputValidator()
        {
            RuleFor(v => v.ExpenseTypeId).GreaterThan(0);

            // A zero interval would mean "due again the instant it is done".
            RuleFor(v => v.AfterKilometer).GreaterThan(0).When(v => v.AfterKilometer.HasValue);
            RuleFor(v => v.AfterMonth).GreaterThan(0).When(v => v.AfterMonth.HasValue);

            // A zero lead is meaningful: warn on the day, not before.
            RuleFor(v => v.LeadKilometers).GreaterThanOrEqualTo(0).When(v => v.LeadKilometers.HasValue);
            RuleFor(v => v.LeadDays).GreaterThanOrEqualTo(0).When(v => v.LeadDays.HasValue);

            RuleFor(v => v.LastDoneMileage).GreaterThanOrEqualTo(0).When(v => v.LastDoneMileage.HasValue);
        }
    }

    /// <summary>Writes the schedule rows a car form submits, for create and update.</summary>
    public static class CarExpenseSchedulePayload
    {
        /// <summary>
        /// Reconciles the car's rows by expense type, so an unchanged type keeps its
        /// row and audit stamp. Types absent from the submission are removed, and a
        /// row with every figure null is not stored (it says nothing).
        /// </summary>
        public static async Task ApplyAsync(
            IApplicationDbContext context,
            Domain.Entities.Car car,
            IReadOnlyCollection<CarExpenseScheduleInput> inputs,
            CancellationToken cancellationToken)
        {
            var duplicate = inputs
                .GroupBy(i => i.ExpenseTypeId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate is not null)
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new ValidationFailure(
                        nameof(CarExpenseScheduleInput.ExpenseTypeId),
                        $"Expense type {duplicate.Key} is listed more than once.")
                });
            }

            var wanted = inputs.Where(HasSomethingToSay).ToList();
            var wantedIds = wanted.Select(i => i.ExpenseTypeId).ToList();

            if (wantedIds.Count > 0)
            {
                // Only a notifiable type has a schedule to keep; reject the rest
                // rather than silently drop a setting the agency thinks it saved.
                var notifiable = await context.ExpenseTypes
                    .AsNoTracking()
                    .Where(t => wantedIds.Contains(t.Id) && t.IsActive && t.WithNotif)
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);

                var rejected = wantedIds.Except(notifiable).ToList();

                if (rejected.Count > 0)
                {
                    throw new Common.Exceptions.ValidationException(new[]
                    {
                        new ValidationFailure(
                            nameof(CarExpenseScheduleInput.ExpenseTypeId),
                            $"No notifiable expense type with id {string.Join(", ", rejected)}.")
                    });
                }
            }

            var existing = await context.CarExpenseSchedules
                .Where(s => s.CarId == car.Id)
                .ToListAsync(cancellationToken);

            foreach (var stale in existing.Where(s => !wantedIds.Contains(s.ExpenseTypeId)))
            {
                context.CarExpenseSchedules.Remove(stale);
            }

            foreach (var input in wanted)
            {
                var row = existing.FirstOrDefault(s => s.ExpenseTypeId == input.ExpenseTypeId);

                if (row is null)
                {
                    row = new Domain.Entities.CarExpenseSchedule
                    {
                        CarId = car.Id,
                        ExpenseTypeId = input.ExpenseTypeId,
                    };
                    context.CarExpenseSchedules.Add(row);
                }

                row.AfterKilometer = input.AfterKilometer;
                row.AfterMonth = input.AfterMonth;
                row.LeadKilometers = input.LeadKilometers;
                row.LeadDays = input.LeadDays;
                row.LastDoneMileage = input.LastDoneMileage;
                row.LastDoneOn = input.LastDoneOn;
            }
        }

        private static bool HasSomethingToSay(CarExpenseScheduleInput input) =>
            input.AfterKilometer.HasValue
            || input.AfterMonth.HasValue
            || input.LeadKilometers.HasValue
            || input.LeadDays.HasValue
            || input.LastDoneMileage.HasValue
            || input.LastDoneOn.HasValue;
    }
}
