using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencyReportConfiguration : IEntityTypeConfiguration<AgencyReport>
{
    public void Configure(EntityTypeBuilder<AgencyReport> builder)
    {
        builder.ToTable("AgencyReports");

        // Not HasAgencyTenant(): a report is arbitrated by the platform and
        // raised by a customer, neither of whom carries a tenant claim (see
        // AgencyReport). Restrict, like a review — an agency with complaints
        // against it must not be deleted out from under them.
        builder.HasOne(r => r.Agency)
               .WithMany(a => a!.Reports)
               .HasForeignKey(r => r.AgencyId)
               .OnDelete(DeleteBehavior.Restrict);

        // The two reads this table has: the platform's triage queue (oldest open
        // first) and one agency's own list.
        builder.HasIndex(r => new { r.Status, r.SubmittedAt });
        builder.HasIndex(r => new { r.AgencyId, r.SubmittedAt });

        // One report per booking: a complaint is arbitrated once, and a second
        // one about the same booking would score the agency twice. Filtered so
        // the two anchors do not collide on their nulls.
        builder.HasIndex(r => r.ReservationId)
               .IsUnique()
               .HasFilter("[ReservationId] IS NOT NULL");

        builder.HasIndex(r => r.RentingId)
               .IsUnique()
               .HasFilter("[RentingId] IS NOT NULL");

        builder.Property(r => r.Message)
               .IsRequired()
               .HasMaxLength(AgencyReport.MaxMessageLength);

        builder.Property(r => r.ResolutionNote).HasMaxLength(AgencyReport.MaxResolutionLength);
        builder.Property(r => r.AgencyCancellationReason).HasMaxLength(1000);
        builder.Property(r => r.BookingSummary).HasMaxLength(256);
        builder.Property(r => r.ReporterUserId).HasMaxLength(450);
        builder.Property(r => r.ResolvedByUserId).HasMaxLength(450);
        builder.Property(r => r.ReporterName).HasMaxLength(256);

        // No navigation to the booking on purpose: both targets are tenant
        // filtered, so an Include from the platform's triage screen would come
        // back empty. The FKs are here for integrity, and what the screen shows
        // is snapshotted on the row.
        builder.HasOne<Reservation>()
               .WithMany()
               .HasForeignKey(r => r.ReservationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Renting>()
               .WithMany()
               .HasForeignKey(r => r.RentingId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Client)
               .WithMany()
               .HasForeignKey(r => r.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

        // Exactly one anchor, enforced where it cannot be forgotten. The factory
        // checks it too; this is what makes it true of every row.
        //
        // Counted rather than written as `(a IS NULL) <> (b IS NULL)`: T-SQL has
        // no boolean type, so a predicate cannot be an operand of <>.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AgencyReports_OneBooking",
            "(CASE WHEN [ReservationId] IS NULL THEN 0 ELSE 1 END" +
            " + CASE WHEN [RentingId] IS NULL THEN 0 ELSE 1 END) = 1"));
    }
}
