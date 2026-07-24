namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// A car image awaiting derivative generation. Carries its own
/// <see cref="AgencyId"/> because the worker runs with no HTTP context — it
/// pushes this onto <see cref="Tenancy.AmbientTenant"/> so tenant-scoped
/// services behave as they would in a request.
/// </summary>
public record ImageProcessingJob(int CarImageId, int AgencyId);

/// <summary>
/// Hand-off from the upload request to background derivative generation. The
/// request enqueues after committing the original; the job resizes the
/// thumbnail/medium out of band, so upload latency does not grow with image
/// size. Backed by Hangfire (SQL Server storage) in production — see P.10.
/// </summary>
public interface IImageProcessingQueue
{
    ValueTask EnqueueAsync(ImageProcessingJob job, CancellationToken cancellationToken = default);
}
