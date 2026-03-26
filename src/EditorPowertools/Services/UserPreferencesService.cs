using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace EditorPowertools.Services;

/// <summary>
/// DDS-persisted record holding per-user, per-tool UI preferences as a JSON string.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_UserPreferences")]
public class UserPreferencesRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();

    [EPiServerDataIndex]
    public string Username { get; set; } = string.Empty;

    [EPiServerDataIndex]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>JSON blob of tool-specific preferences.</summary>
    public string PreferencesJson { get; set; } = "{}";

    public DateTime LastModified { get; set; }
}

/// <summary>
/// Service for reading/writing per-user tool preferences from DDS.
/// </summary>
public class UserPreferencesService
{
    public string? Get(string username, string toolName)
    {
        var store = GetStore();
        var record = store.Items<UserPreferencesRecord>()
            .FirstOrDefault(r => r.Username == username && r.ToolName == toolName);
        return record?.PreferencesJson;
    }

    public void Save(string username, string toolName, string preferencesJson)
    {
        var store = GetStore();
        var existing = store.Items<UserPreferencesRecord>()
            .FirstOrDefault(r => r.Username == username && r.ToolName == toolName);

        if (existing != null)
        {
            existing.PreferencesJson = preferencesJson;
            existing.LastModified = DateTime.UtcNow;
            store.Save(existing);
        }
        else
        {
            store.Save(new UserPreferencesRecord
            {
                Username = username,
                ToolName = toolName,
                PreferencesJson = preferencesJson,
                LastModified = DateTime.UtcNow
            });
        }
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(UserPreferencesRecord));
    }
}
