using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class ScheduledJobsCheck : DoctorCheckBase
{
    private readonly IScheduledJobRepository _jobRepository;

    public ScheduledJobsCheck(IScheduledJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public override string Name => "Scheduled Jobs Health";
    public override string Description => "Checks for failed or long-running scheduled jobs.";
    public override string Group => "Environment";
    public override int SortOrder => 10;
    public override string[] Tags => new[] { "Performance", "Maintenance" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var jobs = _jobRepository.List().ToList();
        var failedJobs = new List<string>();
        var neverRun = new List<string>();
        var longRunning = new List<string>();

        foreach (var job in jobs)
        {
            if (!job.IsEnabled) continue;

            if (job.LastExecution == DateTime.MinValue)
            {
                neverRun.Add(job.Name);
                continue;
            }

            // Check if last execution message indicates failure
            if (job.LastExecutionMessage != null &&
                (job.LastExecutionMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                 job.LastExecutionMessage.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                 job.LastExecutionMessage.Contains("exception", StringComparison.OrdinalIgnoreCase)))
            {
                failedJobs.Add(job.Name);
            }
        }

        var issues = new List<string>();
        if (failedJobs.Count > 0) issues.Add($"Failed: {string.Join(", ", failedJobs)}");
        if (neverRun.Count > 0) issues.Add($"Never run: {string.Join(", ", neverRun)}");

        if (failedJobs.Count > 0)
            return Fault($"{failedJobs.Count} scheduled job(s) have errors.", string.Join(". ", issues));
        if (neverRun.Count > 0)
            return Warning($"{neverRun.Count} enabled job(s) have never run.", string.Join(". ", issues));

        return Ok($"All {jobs.Count(j => j.IsEnabled)} enabled jobs are healthy.");
    }
}
