using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Expense.DTOs
{
    // A cost the agency booked against one of its cars. Amounts carry the
    // agency's currency (see Money); Outstanding is what is still owed on it.
    public class ExpenseDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public int CarId { get; init; }
        public string? CarMatricule { get; init; }
        public string? CarModelName { get; init; }
        public int ExpenseTypeId { get; init; }
        public string? ExpenseTypeName { get; init; }
        public DateTime ExpenseDate { get; init; }
        public MoneyDto? ExpenseAmount { get; init; }
        public MoneyDto? PaidAmount { get; init; }
        // ExpenseAmount − PaidAmount: what the agency still owes on this expense.
        public MoneyDto? Outstanding { get; init; }
        public string? Description { get; init; }
        // Supplier invoice attached to the expense, as a plain URL like every
        // other file-carrying DTO (see StoredFile); null when none is attached.
        public string? FactureFileUrl { get; init; }
        public string? FactureFileName { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Expense, ExpenseDto>()
                      .Map(dest => dest.CarMatricule, src => src.Car != null ? src.Car.Matricule : null)
                      .Map(dest => dest.FactureFileUrl, src => src.FactureFile != null ? src.FactureFile.Url : null)
                      .Map(dest => dest.FactureFileName,
                           src => src.FactureFile != null ? src.FactureFile.OriginalFileName : null)
                      .Map(dest => dest.CarModelName,
                           src => src.Car != null && src.Car.Model != null ? src.Car.Model.Name : null)
                      .Map(dest => dest.ExpenseTypeName, src => src.ExpenseType != null ? src.ExpenseType.Name : null)
                      // Projected in SQL, so it is expressed over the owned
                      // columns rather than through a Money method call.
                      .Map(dest => dest.Outstanding,
                           src => src.ExpenseAmount == null
                               ? null
                               : new MoneyDto(
                                   src.ExpenseAmount.Amount - (src.PaidAmount == null ? 0m : src.PaidAmount.Amount),
                                   src.ExpenseAmount.Currency));
            }
        }
    }
}
