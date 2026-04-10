using EPiServer.DataAbstraction;
using EPiServer.Scheduler;

namespace EditorPowertools.Services;

/// <summary>
/// Checks the status of the aggregation scheduled job and can trigger it.
/// </summary>
public class AggregationJobStatusService
{
    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobExecutor _jobExecutor;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;

    public AggregationJobStatusService(
        IScheduledJobRepository jobRepository,
        IScheduledJobExecutor jobExecutor,
        ContentTypeStatisticsRepository statisticsRepository)
    {
        _jobRepository = jobRepository;
        _jobExecutor = jobExecutor;
        _statisticsRepository = statisticsRepository;
    }

    public AggregationJobStatus GetStatus()
    {
        var job = FindJob();

        var stats = _statisticsRepository.GetAll().ToList();
        var lastUpdated = stats.Any()
            ? stats.Max(s => s.LastUpdated)
            : (DateTime?)null;

        return new AggregationJobStatus
        {
            HasRun = lastUpdated.HasValue,
            LastRunUtc = lastUpdated,
            IsRunning = job?.IsRunning ?? false,
            JobExists = job != null,
            TypeCount = stats.Count
        };
    }

    /// <summary>
    /// Starts the aggregation job. Returns a JobStartResult indicating success or reason for failure.
    /// </summary>
    public async Task<JobStartResult> StartJobAsync()
    {
        var job = FindJob();
        if (job == null)
            return new JobStartResult(false, "not_found");

        if (job.IsRunning)
            return new JobStartResult(false, "already_running");

        await _jobExecutor.StartAsync(job);
        return new JobStartResult(true);
    }

    private ScheduledJob? FindJob()
    {
        return _jobRepository.List()
            .FirstOrDefault(j =>
                j.TypeName?.Contains("UnifiedContentAnalysisJob", StringComparison.OrdinalIgnoreCase) == true
                || j.TypeName?.Contains("ContentTypeStatisticsJob", StringComparison.OrdinalIgnoreCase) == true);
    }
}

public class AggregationJobStatus
{
    public bool HasRun { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public bool IsRunning { get; set; }
    public bool JobExists { get; set; }
    public int TypeCount { get; set; }
}

public record JobStartResult(bool Started, string? Reason = null);
