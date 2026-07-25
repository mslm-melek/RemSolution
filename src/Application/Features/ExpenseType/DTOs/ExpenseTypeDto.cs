namespace RemSolution.Application.Features.ExpenseType.DTOs
{
    // Plain fields — Mapster maps by member name, no explicit mapping needed.
    public class ExpenseTypeDto
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public bool IsActive { get; init; }
        // Whether an upcoming due (by kilometre/month threshold) should notify.
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }
}
