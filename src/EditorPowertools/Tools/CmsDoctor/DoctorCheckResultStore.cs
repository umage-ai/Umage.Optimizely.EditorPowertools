using EPiServer.Data;
using EPiServer.Data.Dynamic;
using EditorPowertools.Tools.CmsDoctor.Models;

namespace EditorPowertools.Tools.CmsDoctor;

[EPiServerDataStore(AutomaticallyRemapStore = true, StoreName = "EditorPowertools_DoctorCheckResults")]
public class DoctorCheckResultEntry : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public string CheckType { get; set; } = string.Empty;
    public string CheckName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string TagsCsv { get; set; } = string.Empty;
    public bool CanFix { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime CheckTime { get; set; }
}

public class DoctorCheckResultStore
{
    private DynamicDataStore GetStore() =>
        DynamicDataStoreFactory.Instance.CreateStore(typeof(DoctorCheckResultEntry));

    public void Save(DoctorCheckResult result)
    {
        var store = GetStore();
        var existing = store.Items<DoctorCheckResultEntry>()
            .FirstOrDefault(e => e.CheckType == result.CheckType);

        if (existing != null)
        {
            existing.CheckName = result.CheckName;
            existing.Group = result.Group;
            existing.Status = (int)result.Status;
            existing.StatusText = result.StatusText;
            existing.Details = result.Details;
            existing.TagsCsv = string.Join(",", result.Tags ?? Array.Empty<string>());
            existing.CanFix = result.CanFix;
            existing.IsDismissed = result.IsDismissed;
            existing.CheckTime = result.CheckTime;
            store.Save(existing);
        }
        else
        {
            store.Save(new DoctorCheckResultEntry
            {
                CheckType = result.CheckType,
                CheckName = result.CheckName,
                Group = result.Group,
                Status = (int)result.Status,
                StatusText = result.StatusText,
                Details = result.Details,
                TagsCsv = string.Join(",", result.Tags ?? Array.Empty<string>()),
                CanFix = result.CanFix,
                IsDismissed = result.IsDismissed,
                CheckTime = result.CheckTime
            });
        }
    }

    public DoctorCheckResult? Load(string checkType)
    {
        var store = GetStore();
        var entry = store.Items<DoctorCheckResultEntry>()
            .FirstOrDefault(e => e.CheckType == checkType);
        return entry == null ? null : ToResult(entry);
    }

    public Dictionary<string, DoctorCheckResult> LoadAll()
    {
        var store = GetStore();
        return store.Items<DoctorCheckResultEntry>()
            .ToDictionary(
                e => e.CheckType,
                e => ToResult(e),
                StringComparer.OrdinalIgnoreCase);
    }

    public void SetDismissed(string checkType, bool dismissed)
    {
        var store = GetStore();
        var existing = store.Items<DoctorCheckResultEntry>()
            .FirstOrDefault(e => e.CheckType == checkType);
        if (existing != null)
        {
            existing.IsDismissed = dismissed;
            store.Save(existing);
        }
        else
        {
            // Create a placeholder entry for dismiss tracking
            store.Save(new DoctorCheckResultEntry
            {
                CheckType = checkType,
                Status = (int)HealthStatus.NotChecked,
                IsDismissed = dismissed,
                CheckTime = DateTime.UtcNow
            });
        }
    }

    private static DoctorCheckResult ToResult(DoctorCheckResultEntry entry) => new()
    {
        CheckType = entry.CheckType,
        CheckName = entry.CheckName,
        Group = entry.Group,
        Status = (HealthStatus)entry.Status,
        StatusText = entry.StatusText,
        Details = entry.Details,
        Tags = string.IsNullOrEmpty(entry.TagsCsv) ? Array.Empty<string>() : entry.TagsCsv.Split(','),
        CanFix = entry.CanFix,
        IsDismissed = entry.IsDismissed,
        CheckTime = entry.CheckTime
    };
}
