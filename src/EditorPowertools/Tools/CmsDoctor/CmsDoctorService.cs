using EditorPowertools.Tools.CmsDoctor.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.CmsDoctor;

public class CmsDoctorService
{
    private readonly IEnumerable<IDoctorCheck> _checks;
    private readonly DoctorCheckResultStore _store;
    private readonly ILogger<CmsDoctorService> _logger;

    // In-memory cache loaded from DDS on first access
    private Dictionary<string, DoctorCheckResult>? _cachedResults;
    private DateTime? _lastFullCheck;

    public CmsDoctorService(
        IEnumerable<IDoctorCheck> checks,
        DoctorCheckResultStore store,
        ILogger<CmsDoctorService> logger)
    {
        _checks = checks;
        _store = store;
        _logger = logger;
    }

    private Dictionary<string, DoctorCheckResult> GetCachedResults()
    {
        if (_cachedResults == null)
        {
            try { _cachedResults = _store.LoadAll(); }
            catch { _cachedResults = new Dictionary<string, DoctorCheckResult>(StringComparer.OrdinalIgnoreCase); }
        }
        return _cachedResults;
    }

    public CmsDoctorDashboard GetDashboard()
    {
        var cached = GetCachedResults();

        var results = _checks.Select(c =>
        {
            var key = c.GetType().FullName ?? c.GetType().Name;
            if (cached.TryGetValue(key, out var stored))
            {
                // Merge stored result with live check metadata
                stored.CanFix = c.CanFix;
                stored.Tags = c.Tags;
                return stored;
            }
            return new DoctorCheckResult
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
                .Select(g => new DoctorCheckGroupDto
                {
                    Name = g.Key,
                    Checks = g.OrderBy(c => c.CheckName).ToList()
                })
                .ToList()
        };
    }

    public List<DoctorCheckResult> RunAll()
    {
        var results = new List<DoctorCheckResult>();
        foreach (var check in _checks)
        {
            results.Add(RunSingle(check));
        }
        _lastFullCheck = DateTime.UtcNow;
        return results;
    }

    public DoctorCheckResult? RunCheck(string checkType)
    {
        var check = _checks.FirstOrDefault(c =>
            (c.GetType().FullName ?? c.GetType().Name).Equals(checkType, StringComparison.OrdinalIgnoreCase));
        if (check == null) return null;
        return RunSingle(check);
    }

    public DoctorCheckResult? FixCheck(string checkType)
    {
        var check = _checks.FirstOrDefault(c =>
            (c.GetType().FullName ?? c.GetType().Name).Equals(checkType, StringComparison.OrdinalIgnoreCase));
        if (check == null || !check.CanFix) return null;

        try
        {
            var result = check.Fix();
            if (result != null)
            {
                SaveResult(result);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fix check {CheckType}", checkType);
            var result = new DoctorCheckResult
            {
                CheckName = check.Name,
                CheckType = checkType,
                Group = check.Group,
                Status = HealthStatus.Fault,
                StatusText = $"Fix failed: {ex.Message}",
                Tags = check.Tags
            };
            SaveResult(result);
            return result;
        }
    }

    public string[] GetAllTags()
    {
        return _checks.SelectMany(c => c.Tags).Distinct().OrderBy(t => t).ToArray();
    }

    public void DismissCheck(string checkType)
    {
        try { _store.SetDismissed(checkType, true); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to dismiss check {CheckType}", checkType); }
        _cachedResults = null; // Invalidate cache
    }

    public void RestoreCheck(string checkType)
    {
        try { _store.SetDismissed(checkType, false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to restore check {CheckType}", checkType); }
        _cachedResults = null; // Invalidate cache
    }

    private DoctorCheckResult RunSingle(IDoctorCheck check)
    {
        var key = check.GetType().FullName ?? check.GetType().Name;
        try
        {
            var result = check.PerformCheck();
            // Preserve dismissed state
            var cached = GetCachedResults();
            if (cached.TryGetValue(key, out var prev))
                result.IsDismissed = prev.IsDismissed;

            SaveResult(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Doctor check {Check} failed", check.Name);
            var result = new DoctorCheckResult
            {
                CheckName = check.Name,
                CheckType = key,
                Group = check.Group,
                Status = HealthStatus.Fault,
                StatusText = $"Check threw exception: {ex.Message}",
                Tags = check.Tags,
                CanFix = check.CanFix
            };
            SaveResult(result);
            return result;
        }
    }

    private void SaveResult(DoctorCheckResult result)
    {
        var cached = GetCachedResults();
        cached[result.CheckType] = result;
        try { _store.Save(result); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to save check result to DDS"); }
    }
}
