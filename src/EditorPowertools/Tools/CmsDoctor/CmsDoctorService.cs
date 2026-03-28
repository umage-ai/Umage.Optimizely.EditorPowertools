using EditorPowertools.Tools.CmsDoctor.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.CmsDoctor;

public class CmsDoctorService
{
    private readonly IEnumerable<IHealthCheck> _checks;
    private readonly ILogger<CmsDoctorService> _logger;

    // Cache results in memory (per-instance, singleton service)
    private readonly Dictionary<string, HealthCheckResult> _lastResults = new();
    private DateTime? _lastFullCheck;

    public CmsDoctorService(IEnumerable<IHealthCheck> checks, ILogger<CmsDoctorService> logger)
    {
        _checks = checks;
        _logger = logger;
    }

    public CmsDoctorDashboard GetDashboard()
    {
        var results = _checks.Select(c =>
        {
            var key = c.GetType().FullName ?? c.GetType().Name;
            return _lastResults.TryGetValue(key, out var cached) ? cached : new HealthCheckResult
            {
                CheckName = c.Name,
                CheckType = key,
                Group = c.Group,
                Status = HealthStatus.NotChecked,
                StatusText = "Not checked yet",
                Tags = c.Tags,
                CanFix = c.CanFix
            };
        }).ToList();

        return new CmsDoctorDashboard
        {
            TotalChecks = results.Count,
            OkCount = results.Count(r => r.Status == HealthStatus.OK),
            WarningCount = results.Count(r => r.Status is HealthStatus.Warning or HealthStatus.BadPractice or HealthStatus.Performance),
            FaultCount = results.Count(r => r.Status == HealthStatus.Fault),
            NotCheckedCount = results.Count(r => r.Status == HealthStatus.NotChecked),
            LastFullCheck = _lastFullCheck,
            Groups = results
                .GroupBy(r => r.Group)
                .OrderBy(g => g.Key)
                .Select(g => new HealthCheckGroupDto
                {
                    Name = g.Key,
                    Checks = g.OrderBy(c => c.CheckName).ToList()
                })
                .ToList()
        };
    }

    public List<HealthCheckResult> RunAll()
    {
        var results = new List<HealthCheckResult>();
        foreach (var check in _checks)
        {
            results.Add(RunSingle(check));
        }
        _lastFullCheck = DateTime.UtcNow;
        return results;
    }

    public HealthCheckResult? RunCheck(string checkType)
    {
        var check = _checks.FirstOrDefault(c =>
            (c.GetType().FullName ?? c.GetType().Name).Equals(checkType, StringComparison.OrdinalIgnoreCase));
        if (check == null) return null;
        return RunSingle(check);
    }

    public HealthCheckResult? FixCheck(string checkType)
    {
        var check = _checks.FirstOrDefault(c =>
            (c.GetType().FullName ?? c.GetType().Name).Equals(checkType, StringComparison.OrdinalIgnoreCase));
        if (check == null || !check.CanFix) return null;

        try
        {
            var result = check.Fix();
            if (result != null)
            {
                var key = check.GetType().FullName ?? check.GetType().Name;
                _lastResults[key] = result;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fix check {CheckType}", checkType);
            return new HealthCheckResult
            {
                CheckName = check.Name,
                CheckType = checkType,
                Group = check.Group,
                Status = HealthStatus.Fault,
                StatusText = $"Fix failed: {ex.Message}",
                Tags = check.Tags
            };
        }
    }

    public string[] GetAllTags()
    {
        return _checks.SelectMany(c => c.Tags).Distinct().OrderBy(t => t).ToArray();
    }

    private HealthCheckResult RunSingle(IHealthCheck check)
    {
        var key = check.GetType().FullName ?? check.GetType().Name;
        try
        {
            var result = check.PerformCheck();
            _lastResults[key] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check {Check} failed", check.Name);
            var result = new HealthCheckResult
            {
                CheckName = check.Name,
                CheckType = key,
                Group = check.Group,
                Status = HealthStatus.Fault,
                StatusText = $"Check threw exception: {ex.Message}",
                Tags = check.Tags,
                CanFix = check.CanFix
            };
            _lastResults[key] = result;
            return result;
        }
    }
}
