namespace EditorPowertools.Tools.CmsDoctor.Models;

public enum HealthStatus
{
    NotChecked,
    OK,
    Warning,
    BadPractice,
    Fault,
    Performance
}

/// <summary>
/// Interface for pluggable health checks. Implement this interface and register
/// via DI to add custom checks. Third-party packages can add checks by registering
/// IHealthCheck implementations in their service collection extensions.
/// </summary>
public interface IHealthCheck
{
    /// <summary>Display name of the check.</summary>
    string Name { get; }

    /// <summary>Brief description of what this check verifies.</summary>
    string Description { get; }

    /// <summary>Group for dashboard organization (e.g. "Content", "Configuration", "Environment", "Performance").</summary>
    string Group { get; }

    /// <summary>Sort order within the group.</summary>
    int SortOrder { get; }

    /// <summary>Tags for categorizing the check (e.g. "Security", "Performance", "EditorUX", "SEO", "GDPR").</summary>
    string[] Tags { get; }

    /// <summary>Run the check and return the result.</summary>
    HealthCheckResult PerformCheck();

    /// <summary>If true, this check can auto-fix the issue it detects.</summary>
    bool CanFix { get; }

    /// <summary>Attempt to fix the issue. Only called if CanFix is true.</summary>
    HealthCheckResult? Fix();
}

public class HealthCheckResult
{
    public string CheckName { get; set; } = string.Empty;
    public string CheckType { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public HealthStatus Status { get; set; } = HealthStatus.NotChecked;
    public string StatusText { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
}

public class CmsDoctorDashboard
{
    public int TotalChecks { get; set; }
    public int OkCount { get; set; }
    public int WarningCount { get; set; }
    public int FaultCount { get; set; }
    public int NotCheckedCount { get; set; }
    public List<HealthCheckGroupDto> Groups { get; set; } = new();
    public DateTime? LastFullCheck { get; set; }
}

public class HealthCheckGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<HealthCheckResult> Checks { get; set; } = new();
}
