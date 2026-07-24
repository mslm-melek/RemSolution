using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Imaging;

// Discards enqueued jobs. Registered only when Hangfire is disabled — the
// NSwag build-time host (placeholder connection string) and any environment
// that turns background processing off — so the DI graph still resolves without
// a job backend.
public sealed class NoOpImageProcessingQueue : IImageProcessingQueue
{
    public ValueTask EnqueueAsync(ImageProcessingJob job, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
