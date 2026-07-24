using System.Collections.Concurrent;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.FunctionalTests;

// Test double for the image-processing queue. It records what upload handlers
// enqueue (so tests can assert the ingestion contract) instead of dispatching
// to Hangfire, keeping the pipeline deterministic — real derivative generation
// is covered by SkiaImageProcessorTests at the unit level — and preventing
// background writes from racing the per-test database reset.
public sealed class RecordingImageProcessingQueue : IImageProcessingQueue
{
    private static readonly ConcurrentBag<ImageProcessingJob> Jobs = new();

    // Image ids are unique per run, so matching by id is resilient to the queue
    // accumulating jobs across tests (the singleton outlives the DB reset).
    public static bool EnqueuedFor(int carImageId) => Jobs.Any(j => j.CarImageId == carImageId);

    public ValueTask EnqueueAsync(ImageProcessingJob job, CancellationToken cancellationToken = default)
    {
        Jobs.Add(job);
        return ValueTask.CompletedTask;
    }
}
