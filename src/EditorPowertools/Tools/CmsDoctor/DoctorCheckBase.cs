using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;
using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;

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

    /// <summary>
    /// Resolves a localized string with fallback to the provided default.
    /// Uses the lazy-loaded LocalizationService from ServiceLocator since
    /// doctor checks are instantiated via DI and not all have constructor injection support.
    /// </summary>
    private LocalizationService? _localization;
    protected LocalizationService Localization =>
        _localization ??= ServiceLocator.Current.GetInstance<LocalizationService>();

    protected string L(string path, string fallback) =>
        Localization.GetStringByCulture(path, fallback, System.Globalization.CultureInfo.CurrentUICulture);

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
