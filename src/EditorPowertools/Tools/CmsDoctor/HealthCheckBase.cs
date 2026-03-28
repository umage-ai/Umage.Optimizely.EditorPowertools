using EditorPowertools.Tools.CmsDoctor.Models;

namespace EditorPowertools.Tools.CmsDoctor;

/// <summary>
/// Base class for health checks. Override PerformCheck() to implement your check.
/// Optionally override Fix() if your check can auto-fix issues.
/// Third-party packages: inherit from this, register as IHealthCheck in DI, done.
/// </summary>
public abstract class HealthCheckBase : IHealthCheck
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Group { get; }
    public virtual int SortOrder => 100;
    public virtual string[] Tags => Array.Empty<string>();
    public virtual bool CanFix => false;

    public abstract HealthCheckResult PerformCheck();

    public virtual HealthCheckResult? Fix() => null;

    private HealthCheckResult Result(HealthStatus status, string message, string? details) => new()
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

    protected HealthCheckResult Ok(string message, string? details = null) => Result(HealthStatus.OK, message, details);
    protected HealthCheckResult Warning(string message, string? details = null) => Result(HealthStatus.Warning, message, details);
    protected HealthCheckResult BadPractice(string message, string? details = null) => Result(HealthStatus.BadPractice, message, details);
    protected HealthCheckResult Fault(string message, string? details = null) => Result(HealthStatus.Fault, message, details);
    protected HealthCheckResult Perf(string message, string? details = null) => Result(HealthStatus.Performance, message, details);
}
