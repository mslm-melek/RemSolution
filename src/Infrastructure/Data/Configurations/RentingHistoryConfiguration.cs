using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class RentingHistoryConfiguration : IEntityTypeConfiguration<RentingHistory>
{
    public void Configure(EntityTypeBuilder<RentingHistory> builder)
    {
        builder.OwnsMoney(e => e.Price, "Price", "PriceCurrency");

        builder.HasOne(c => c.Renting)
               .WithMany(mc => mc.RentingHistories) 
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.SetNull);
        
    }
}
