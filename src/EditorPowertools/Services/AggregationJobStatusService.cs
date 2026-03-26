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
    /// Starts the aggregation job. Returns true if started successfully.
    /// </summary>
    public async Task<bool> StartJobAsync()
    {
        var job = FindJob();
        if (job == null)
            return false;

        if (job.IsRunning)
            return false;

        await _jobExecutor.StartAsync(job);
        return true;
    }

    private ScheduledJob? FindJob()
    {
        return _jobRepository.List()
            .FirstOrDefault(j =>
                j.TypeName?.Contains("ContentTypeStatisticsJob", StringComparison.OrdinalIgnoreCase) == true);
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
