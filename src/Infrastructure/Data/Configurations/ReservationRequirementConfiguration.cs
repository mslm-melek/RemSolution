using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ReservationRequirementConfiguration : IEntityTypeConfiguration<ReservationRequirement>
{
    public void Configure(EntityTypeBuilder<ReservationRequirement> builder)
    {
        // Always read as "the requirements of this hold", never scanned on their
        // own, so the tenant index carries the reservation.
        builder.HasAgencyTenant(nameof(ReservationRequirement.ReservationId));

        builder.OwnsMoney(e => e.Amount, "Amount", "AmountCurrency");

        builder.Property(e => e.Label)
               .IsRequired()
               .HasMaxLength(ReservationRequirement.MaxLabelLength);

        builder.Property(e => e.SubmittedNote).HasMaxLength(ReservationRequirement.MaxNoteLength);
        builder.Property(e => e.ReviewNote).HasMaxLength(ReservationRequirement.MaxNoteLength);

        // Cascade: a requirement is part of its hold and says nothing without it.
        // Safe because a reservation is a financial record that is cancelled
        // rather than deleted (see ReservationStatus).
        builder.HasOne(e => e.Reservation)
               .WithMany(r => r.Requirements)
               .HasForeignKey(e => e.ReservationId)
               .OnDelete(DeleteBehavior.Cascade);

        // The customer's answer. Restrict, not cascade: losing the file must not
        // silently take the record of what was asked and answered with it.
        builder.HasOne(e => e.SubmittedFile)
               .WithMany()
               .HasForeignKey(e => e.SubmittedFileId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
