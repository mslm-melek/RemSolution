using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencyReviewConfiguration : IEntityTypeConfiguration<AgencyReview>
{
    public void Configure(EntityTypeBuilder<AgencyReview> builder)
    {
        builder.ToTable("AgencyReviews");

        // Not HasAgencyTenant(): a review is platform-level public content, not
        // tenant data (see AgencyReview). The Agency FK is still Restrict — an
        // agency with reviews must not be deleted out from under them — and the
        // navigation is wired both ways so the marketplace projections can read
        // an agency's rating straight off Agency.Reviews.
        builder.HasOne(r => r.Agency)
               .WithMany(a => a!.Reviews)
               .HasForeignKey(r => r.AgencyId)
               .OnDelete(DeleteBehavior.Restrict);

        // One review per renting: the rental IS the entitlement to rate, so the
        // rule is a unique index rather than a check in the handler alone.
        builder.HasIndex(r => r.RentingId).IsUnique();

        // Public reads are always "this agency's reviews, newest first".
        builder.HasIndex(r => new { r.AgencyId, r.SubmittedAt });

        builder.Property(r => r.Comment).HasMaxLength(AgencyReview.MaxCommentLength);
        builder.Property(r => r.AuthorUserId).HasMaxLength(450);
        builder.Property(r => r.AuthorName).HasMaxLength(256);
        builder.Property(r => r.CarName).HasMaxLength(256);

        // The 1..5 range is a stored invariant too, not just a validator rule:
        // the average shown on every car card is only meaningful if no row can
        // sit outside the scale.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AgencyReviews_Rating",
            $"[Rating] BETWEEN {AgencyReview.MinRating} AND {AgencyReview.MaxRating}"));

        // Restrict on both: a renting is a financial record that is cancelled
        // rather than removed, and a client is soft-deleted — neither should
        // silently cascade a public review away.
        builder.HasOne(r => r.Renting)
               .WithMany()
               .HasForeignKey(r => r.RentingId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Client)
               .WithMany()
               .HasForeignKey(r => r.ClientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
