namespace RemSolution.Application.Features.Car.DTOs
{
    /// <summary>
    /// One notifiable expense type as it applies to one car. The list has a row per
    /// type, linked or not, which is what the car form draws.
    /// </summary>
    public class CarExpenseScheduleDto
    {
        public int ExpenseTypeId { get; init; }
        public string? ExpenseTypeName { get; init; }

        /// <summary>The car has a row of its own for this type (the form's tick box).</summary>
        public bool IsLinked { get; init; }

        /// <summary>The car's interval departs from the type's.</summary>
        public bool HasOwnInterval { get; init; }

        /// <summary>The type's fleet-wide distance interval; the fallback and the field's placeholder.</summary>
        public int? TypeAfterKilometer { get; init; }

        /// <summary>The type's fleet-wide month interval.</summary>
        public int? TypeAfterMonth { get; init; }

        /// <summary>This car's distance interval; null = it follows the type's.</summary>
        public int? AfterKilometer { get; init; }

        /// <summary>This car's month interval; null = it follows the type's.</summary>
        public int? AfterMonth { get; init; }

        /// <summary>This car's warning window in km; null = the agency's setting.</summary>
        public int? LeadKilometers { get; init; }

        /// <summary>This car's warning window in days; null = the agency's setting.</summary>
        public int? LeadDays { get; init; }

        /// <summary>Odometer when the agency says this was last done, if declared.</summary>
        public int? LastDoneMileage { get; init; }

        /// <summary>When the agency says this was last done, if declared.</summary>
        public DateTime? LastDoneOn { get; init; }

        // Resolved, read-only: computed with the rule the sweep uses.

        /// <summary>The interval in force, this car's or the type's.</summary>
        public int? EffectiveAfterKilometer { get; init; }

        /// <summary>The interval in force, this car's or the type's.</summary>
        public int? EffectiveAfterMonth { get; init; }

        /// <summary>The date the count runs from.</summary>
        public DateTime? BaselineOn { get; init; }

        /// <summary>The odometer the count runs from.</summary>
        public int? BaselineMileage { get; init; }

        /// <summary>When the next one falls due, or null when the date clock cannot run.</summary>
        public DateTime? NextDueOn { get; init; }

        /// <summary>The odometer the next one falls due at, or null when the distance clock cannot run.</summary>
        public int? NextDueAtKilometers { get; init; }

        /// <summary>Past due on either clock, whatever the warning window says.</summary>
        public bool IsOverdue { get; init; }
    }
}
