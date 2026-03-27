using EPiServer.DataAbstraction;
using EPiServer.Scheduler;

namespace EditorPowertools.Tools.LinkChecker;

/// <summary>
/// Checks the status of the link checker scheduled job and can trigger it.
/// </summary>
public class LinkCheckerJobStatusService
{
    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobExecutor _jobExecutor;
    private readonly LinkCheckerRepository _linkCheckerRepository;

    public LinkCheckerJobStatusService(
        IScheduledJobRepository jobRepository,
        IScheduledJobExecutor jobExecutor,
        LinkCheckerRepository linkCheckerRepository)
    {
        _jobRepository = jobRepository;
        _jobExecutor = jobExecutor;
        _linkCheckerRepository = linkCheckerRepository;
    }

    public LinkCheckerJobStatus GetStatus()
    {
        var job = FindJob();

        var records = _linkCheckerRepository.GetAll().ToList();
        var lastChecked = records.Any()
            ? records.Max(r => r.LastChecked)
            : (DateTime?)null;

        return new LinkCheckerJobStatus
        {
            HasRun = lastChecked.HasValue,
            LastRunUtc = lastChecked,
            IsRunning = job?.IsRunning ?? false,
            JobExists = job != null,
            RecordCount = records.Count,
            BrokenCount = records.Count(r => !r.IsValid)
        };
    }

    /// <summary>
    /// Starts the link checker job. Returns true if started successfully.
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
                || j.TypeName?.Contains("LinkCheckerJob", StringComparison.OrdinalIgnoreCase) == true);
    }
}

public class LinkCheckerJobStatus
{
    public bool HasRun { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public bool IsRunning { get; set; }
    public bool JobExists { get; set; }
    public int RecordCount { get; set; }
    public int BrokenCount { get; set; }
}
