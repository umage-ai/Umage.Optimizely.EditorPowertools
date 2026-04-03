using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class ScheduledJobsCheck : DoctorCheckBase
{
    private readonly IScheduledJobRepository _jobRepository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/scheduledjobscheck/";

    public ScheduledJobsCheck(IScheduledJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public override string Name => L(Prefix + "name", "Scheduled Jobs Health");
    public override string Description => L(Prefix + "description", "Checks for failed or long-running scheduled jobs.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/environment", "Environment");
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
        if (failedJobs.Count > 0)
            issues.Add(string.Format(L(Prefix + "failedlabel", "Failed: {0}"), string.Join(", ", failedJobs)));
        if (neverRun.Count > 0)
            issues.Add(string.Format(L(Prefix + "neverrunlabel", "Never run: {0}"), string.Join(", ", neverRun)));

        if (failedJobs.Count > 0)
            return Fault(
                string.Format(L(Prefix + "failed", "{0} scheduled job(s) have errors."), failedJobs.Count),
                string.Join(". ", issues));
        if (neverRun.Count > 0)
            return Warning(
                string.Format(L(Prefix + "neverrun", "{0} enabled job(s) have never run."), neverRun.Count),
                string.Join(". ", issues));

        return Ok(string.Format(L(Prefix + "ok", "All {0} enabled jobs are healthy."), jobs.Count(j => j.IsEnabled)));
    }
}
