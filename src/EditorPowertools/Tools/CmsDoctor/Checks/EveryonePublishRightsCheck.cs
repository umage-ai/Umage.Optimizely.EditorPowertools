using UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reports content where "Everyone" has Publish or higher access.
/// Reads from SecurityAuditRepository (data collected by SecurityAuditAnalyzer).
/// </summary>
public class EveryonePublishRightsCheck : DoctorCheckBase
{
    private readonly SecurityAuditRepository _repository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/everyonepublishrightscheck/";

    public EveryonePublishRightsCheck(SecurityAuditRepository repository)
    {
        _repository = repository;
    }

    public override string Name => L(Prefix + "name", "Everyone Publish Rights");
    public override string Description => L(Prefix + "description", "Reports content where the \"Everyone\" role has Publish or higher access.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/security", "Security");
    public override int SortOrder => 70;
    public override string[] Tags => new[] { "security", "permissions" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var allRecords = _repository.GetAll();
            if (!allRecords.Any())
                return Ok(
                    L(Prefix + "nodata", "No security audit data available. Run the Content Analysis scheduled job first."),
                    L(Prefix + "nodatadetails", "The Security Audit analyzer populates this data during the scheduled job."));

            var offending = _repository.GetAll()
                .Where(r => r.EveryoneCanPublish)
                .ToList();

            if (offending.Count == 0)
                return Ok(L(Prefix + "ok", "No content grants Publish to the \"Everyone\" role."));

            var details = string.Join("\n",
                offending.Take(10).Select(r => $"- {r.ContentName} ({r.Breadcrumb})"));

            if (offending.Count > 10)
                details += $"\n... and {offending.Count - 10} more.";

            return Warning(
                string.Format(L(Prefix + "warning", "{0} content item(s) grant Publish or higher to the \"Everyone\" role."), offending.Count),
                details);
        }
        catch
        {
            return Ok(L(Prefix + "notavailable", "Security audit data not available yet."));
        }
    }
}
