using UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reports pages with no access restrictions.
/// Only flags pages (not blocks/media which are typically unrestricted).
/// Reads from SecurityAuditRepository (data collected by SecurityAuditAnalyzer).
/// </summary>
public class UnrestrictedContentCheck : DoctorCheckBase
{
    private readonly SecurityAuditRepository _repository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/unrestrictedcontentcheck/";

    public UnrestrictedContentCheck(SecurityAuditRepository repository)
    {
        _repository = repository;
    }

    public override string Name => L(Prefix + "name", "Unrestricted Content");
    public override string Description => L(Prefix + "description", "Reports pages with no access restrictions set.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/security", "Security");
    public override int SortOrder => 71;
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

            var unrestricted = _repository.GetAll()
                .Where(r => r.HasNoRestrictions && r.IsPage)
                .ToList();

            if (unrestricted.Count == 0)
                return Ok(L(Prefix + "ok", "All pages have access restrictions."));

            var details = string.Join("\n",
                unrestricted.Take(10).Select(r => $"- {r.ContentName} ({r.Breadcrumb})"));

            if (unrestricted.Count > 10)
                details += $"\n... and {unrestricted.Count - 10} more.";

            return BadPractice(
                string.Format(L(Prefix + "badpractice", "{0} page(s) have no access restrictions (wide open)."), unrestricted.Count),
                details);
        }
        catch
        {
            return Ok(L(Prefix + "notavailable", "Security audit data not available yet."));
        }
    }
}
