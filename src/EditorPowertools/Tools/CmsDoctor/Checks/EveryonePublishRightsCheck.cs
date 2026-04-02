using EditorPowertools.Tools.SecurityAudit;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reports content where "Everyone" has Publish or higher access.
/// Reads from SecurityAuditRepository (data collected by SecurityAuditAnalyzer).
/// </summary>
public class EveryonePublishRightsCheck : DoctorCheckBase
{
    private readonly SecurityAuditRepository _repository;

    public EveryonePublishRightsCheck(SecurityAuditRepository repository)
    {
        _repository = repository;
    }

    public override string Name => "Everyone Publish Rights";
    public override string Description => "Reports content where the \"Everyone\" role has Publish or higher access.";
    public override string Group => "Security";
    public override int SortOrder => 70;
    public override string[] Tags => new[] { "security", "permissions" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var allRecords = _repository.GetAll();
            if (!allRecords.Any())
                return Ok("No security audit data available. Run the Content Analysis scheduled job first.",
                    "The Security Audit analyzer populates this data during the scheduled job.");

            var offending = _repository.GetAll()
                .Where(r => r.EveryoneCanPublish)
                .ToList();

            if (offending.Count == 0)
                return Ok("No content grants Publish to the \"Everyone\" role.");

            var details = string.Join("\n",
                offending.Take(10).Select(r => $"- {r.ContentName} ({r.Breadcrumb})"));

            if (offending.Count > 10)
                details += $"\n... and {offending.Count - 10} more.";

            return Warning(
                $"{offending.Count} content item(s) grant Publish or higher to the \"Everyone\" role.",
                details);
        }
        catch
        {
            return Ok("Security audit data not available yet.");
        }
    }
}
