using EditorPowertools.Tools.SecurityAudit;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reports pages with no access restrictions.
/// Only flags pages (not blocks/media which are typically unrestricted).
/// Reads from SecurityAuditRepository (data collected by SecurityAuditAnalyzer).
/// </summary>
public class UnrestrictedContentCheck : DoctorCheckBase
{
    private readonly SecurityAuditRepository _repository;

    public UnrestrictedContentCheck(SecurityAuditRepository repository)
    {
        _repository = repository;
    }

    public override string Name => "Unrestricted Content";
    public override string Description => "Reports pages with no access restrictions set.";
    public override string Group => "Security";
    public override int SortOrder => 71;
    public override string[] Tags => new[] { "security", "permissions" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var allRecords = _repository.GetAll();
            if (!allRecords.Any())
                return Ok("No security audit data available. Run the Content Analysis scheduled job first.",
                    "The Security Audit analyzer populates this data during the scheduled job.");

            var unrestricted = _repository.GetAll()
                .Where(r => r.HasNoRestrictions && r.IsPage)
                .ToList();

            if (unrestricted.Count == 0)
                return Ok("All pages have access restrictions.");

            var details = string.Join("\n",
                unrestricted.Take(10).Select(r => $"- {r.ContentName} ({r.Breadcrumb})"));

            if (unrestricted.Count > 10)
                details += $"\n... and {unrestricted.Count - 10} more.";

            return BadPractice(
                $"{unrestricted.Count} page(s) have no access restrictions (wide open).",
                details);
        }
        catch
        {
            return Ok("Security audit data not available yet.");
        }
    }
}
