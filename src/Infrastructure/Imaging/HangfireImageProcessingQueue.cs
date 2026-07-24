using Hangfire;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Imaging;

// Enqueues derivative generation as a Hangfire job. Enqueue writes the job to
// SQL Server storage and returns immediately; a Hangfire worker later resolves
// CarImageProcessingJob and runs it. Registered as the IImageProcessingQueue
// whenever Hangfire is enabled (see DependencyInjection).
public sealed class HangfireImageProcessingQueue : IImageProcessingQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireImageProcessingQueue(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public ValueTask EnqueueAsync(ImageProcessingJob job, CancellationToken cancellationToken = default)
    {
        _jobs.Enqueue<CarImageProcessingJob>(j => j.ProcessAsync(job.CarImageId, job.AgencyId));
        return ValueTask.CompletedTask;
    }
}
