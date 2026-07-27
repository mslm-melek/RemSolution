using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        // IX(AgencyId, RentingId): the only list query is "the contracts of this
        // renting", and it is always tenant-scoped by the global query filter.
        builder.HasAgencyTenant(nameof(Contract.RentingId));

        builder.Property(c => c.Number).IsRequired().HasMaxLength(40);
        builder.Property(c => c.Language).IsRequired().HasMaxLength(16);

        // The numbering invariant, enforced by the database rather than trusted
        // from the handler: two contracts in one agency and year can never share
        // a sequence number, so a lock that was skipped or timed out surfaces as
        // a failed insert instead of duplicate paperwork.
        builder.HasIndex(c => new { c.AgencyId, c.Year, c.SequenceNumber }).IsUnique();

        // Legal record: never cascade-deleted with the booking it documents.
        builder.HasOne(c => c.Renting)
               .WithMany(r => r.Contracts)
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.Restrict);

        // The rendered PDF is the contract; deleting the file row out from under
        // it would leave an unretrievable agreement.
        builder.HasOne(c => c.DocumentFile)
               .WithMany()
               .HasForeignKey(c => c.DocumentFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.TemplateName).HasMaxLength(200);

        // Optional: null means the shipped example produced it. Restrict so a
        // template that has issued paperwork cannot be deleted out of the trail
        // (templates are retired via IsActive, not removed).
        builder.HasOne(c => c.DocumentTemplate)
               .WithMany()
               .HasForeignKey(c => c.DocumentTemplateId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
