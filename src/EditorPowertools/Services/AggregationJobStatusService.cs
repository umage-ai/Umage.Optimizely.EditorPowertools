using EPiServer.DataAbstraction;
using EPiServer.Scheduler;

namespace EditorPowertools.Services;

/// <summary>
/// Checks the status of the aggregation scheduled job.
/// </summary>
public class AggregationJobStatusService
{
    private readonly IScheduledJobRepository _jobRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;

    public AggregationJobStatusService(
        IScheduledJobRepository jobRepository,
        ContentTypeStatisticsRepository statisticsRepository)
    {
        _jobRepository = jobRepository;
        _statisticsRepository = statisticsRepository;
    }

    public AggregationJobStatus GetStatus()
    {
        // Find the job by type name
        var jobs = _jobRepository.List();
        var job = jobs.FirstOrDefault(j =>
            j.TypeName?.Contains("ContentTypeStatisticsJob", StringComparison.OrdinalIgnoreCase) == true);

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
}

public class AggregationJobStatus
{
    public bool HasRun { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public bool IsRunning { get; set; }
    public bool JobExists { get; set; }
    public int TypeCount { get; set; }
}
