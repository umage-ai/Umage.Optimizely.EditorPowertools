using EPiServer.DataAbstraction;
using EPiServer.Scheduler;

namespace EditorPowertools.Tools.PersonalizationAudit;

/// <summary>
/// Checks the status of the personalization analysis scheduled job and can trigger it.
/// </summary>
public class PersonalizationJobStatusService
{
    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobExecutor _jobExecutor;
    private readonly PersonalizationUsageRepository _usageRepository;

    public PersonalizationJobStatusService(
        IScheduledJobRepository jobRepository,
        IScheduledJobExecutor jobExecutor,
        PersonalizationUsageRepository usageRepository)
    {
        _jobRepository = jobRepository;
        _jobExecutor = jobExecutor;
        _usageRepository = usageRepository;
    }

    public PersonalizationJobStatus GetStatus()
    {
        var job = FindJob();

        var records = _usageRepository.GetAll().ToList();
        var lastUpdated = records.Any()
            ? records.Max(r => r.LastUpdated)
            : (DateTime?)null;

        return new PersonalizationJobStatus
        {
            HasRun = lastUpdated.HasValue,
            LastRunUtc = lastUpdated,
            IsRunning = job?.IsRunning ?? false,
            JobExists = job != null,
            RecordCount = records.Count
        };
    }

    /// <summary>
    /// Starts the personalization analysis job. Returns true if started successfully.
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
                j.TypeName?.Contains("UnifiedContentAnalysisJob", StringComparison.OrdinalIgnoreCase) == true
                || j.TypeName?.Contains("PersonalizationAnalysisJob", StringComparison.OrdinalIgnoreCase) == true);
    }
}

public class PersonalizationJobStatus
{
    public bool HasRun { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public bool IsRunning { get; set; }
    public bool JobExists { get; set; }
    public int RecordCount { get; set; }
}
