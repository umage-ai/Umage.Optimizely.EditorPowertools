using UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reports content items more permissive than their parent.
/// Reads from SecurityAuditRepository (data collected by SecurityAuditAnalyzer).
/// </summary>
public class InconsistentInheritanceCheck : DoctorCheckBase
{
    private readonly SecurityAuditRepository _repository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/inconsistentinheritancecheck/";

    public InconsistentInheritanceCheck(SecurityAuditRepository repository)
    {
        _repository = repository;
    }

    public override string Name => L(Prefix + "name", "Inconsistent Permission Inheritance");
    public override string Description => L(Prefix + "description", "Reports content that is more permissive than its parent, which may indicate accidental ACL changes.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/security", "Security");
    public override int SortOrder => 72;
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

            var inconsistent = _repository.GetAll()
                .Where(r => r.ChildMorePermissive)
                .ToList();

            if (inconsistent.Count == 0)
                return Ok(L(Prefix + "ok", "No permission inheritance inconsistencies found."));

            var details = string.Join("\n",
                inconsistent.Take(10).Select(r => $"- {r.ContentName} ({r.Breadcrumb})"));

            if (inconsistent.Count > 10)
                details += $"\n... and {inconsistent.Count - 10} more.";

            return Warning(
                string.Format(L(Prefix + "warning", "{0} content item(s) are more permissive than their parent."), inconsistent.Count),
                details);
        }
        catch
        {
            return Ok(L(Prefix + "notavailable", "Security audit data not available yet."));
        }
    }
}
