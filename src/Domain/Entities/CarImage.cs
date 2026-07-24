namespace RemSolution.Domain.Entities
{
    // One image in a car's gallery. Supersedes the single Car.PhotoFileId: a car
    // can have many images, ordered by SortOrder, with exactly one IsPrimary.
    // Each image keeps its full-resolution original plus generated thumbnail and
    // medium derivatives (produced asynchronously — see ImageProcessingStatus),
    // all as StoredFile rows so hashing/dedup/audit apply uniformly.
    public class CarImage : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int CarId { get; set; }
        public virtual Car? Car { get; set; }

        // Full-resolution upload; required.
        public int OriginalFileId { get; set; }
        public virtual StoredFile? OriginalFile { get; set; }

        // Derivatives; null until the background pipeline generates them.
        public int? ThumbnailFileId { get; set; }
        public virtual StoredFile? ThumbnailFile { get; set; }
        public int? MediumFileId { get; set; }
        public virtual StoredFile? MediumFile { get; set; }

        // Gallery order within the car; primary image is the card/hero.
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }

        public ImageProcessingStatus ProcessingStatus { get; set; } = ImageProcessingStatus.Pending;
    }
}
