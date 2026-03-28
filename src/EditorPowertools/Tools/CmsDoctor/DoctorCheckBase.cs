using EditorPowertools.Tools.CmsDoctor.Models;

namespace EditorPowertools.Tools.CmsDoctor;

/// <summary>
/// Base class for health checks. Override PerformCheck() to implement your check.
/// Optionally override Fix() if your check can auto-fix issues.
/// Third-party packages: inherit from this, register as IDoctorCheck in DI, done.
/// </summary>
public abstract class DoctorCheckBase : IDoctorCheck
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Group { get; }
    public virtual int SortOrder => 100;
    public virtual string[] Tags => Array.Empty<string>();
    public virtual bool CanFix => false;

    public abstract DoctorCheckResult PerformCheck();

    public virtual DoctorCheckResult? Fix() => null;

    private DoctorCheckResult Result(HealthStatus status, string message, string? details) => new()
    {
        CheckName = Name,
        CheckType = GetType().FullName ?? GetType().Name,
        Group = Group,
        Status = status,
        StatusText = message,
        Details = details,
        Tags = Tags,
        CanFix = CanFix
    };

    protected DoctorCheckResult Ok(string message, string? details = null) => Result(HealthStatus.OK, message, details);
    protected DoctorCheckResult Warning(string message, string? details = null) => Result(HealthStatus.Warning, message, details);
    protected DoctorCheckResult BadPractice(string message, string? details = null) => Result(HealthStatus.BadPractice, message, details);
    protected DoctorCheckResult Fault(string message, string? details = null) => Result(HealthStatus.Fault, message, details);
    protected DoctorCheckResult Perf(string message, string? details = null) => Result(HealthStatus.Performance, message, details);
}
