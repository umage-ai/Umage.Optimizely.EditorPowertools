# Active Editors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Real-time editor presence tracking, per-content editor awareness widget, full-page overview tool, and ephemeral chat — all via SignalR, all toggleable.

**Architecture:** A single SignalR hub (`ActiveEditorsHub`) handles heartbeats, presence broadcasts, and chat. A singleton `ActiveEditorsService` keeps all state in `ConcurrentDictionary` (no database). A Dojo tracker script runs in the CMS shell and sends context changes. Two UIs consume the data: an assets panel widget and a full-page tool.

**Tech Stack:** ASP.NET Core SignalR (built into the framework), Dojo AMD modules for CMS shell integration, vanilla JS for the full-page tool.

---

## File Map

### New Files — Backend
| File | Responsibility |
|------|---------------|
| `Tools/ActiveEditors/Models/ActiveEditorsDtos.cs` | DTOs for presence, chat messages, hub responses |
| `Tools/ActiveEditors/ActiveEditorsService.cs` | Singleton: ConcurrentDictionary presence state, chat ring buffer, stale cleanup |
| `Tools/ActiveEditors/ActiveEditorsHub.cs` | SignalR hub: heartbeat, context update, chat send/receive, disconnect |
| `Tools/ActiveEditors/ActiveEditorsComponent.cs` | ComponentDefinitionBase registration for assets panel widget |

### New Files — Frontend
| File | Responsibility |
|------|---------------|
| `ClientResources/js/active-editors-tracker.js` | Dojo module: SignalR connection, heartbeat, context forwarding, toast notifications |
| `ClientResources/js/ActiveEditorsWidget.js` | Dojo assets panel widget: shows editors on current content |
| `ClientResources/js/active-editors-overview.js` | Full-page tool: all editors, today's activity, chat panel |

### New Files — Views
| File | Responsibility |
|------|---------------|
| `Views/ActiveEditors/Index.cshtml` | Razor view for full-page tool |

### Modified Files
| File | Change |
|------|--------|
| `Configuration/FeatureToggles.cs` | Add `ActiveEditors` and `ActiveEditorsChat` toggles |
| `Permissions/EditorPowertoolsPermissions.cs` | Add `ActiveEditors` permission |
| `Infrastructure/ServiceCollectionExtensions.cs` | Register `ActiveEditorsService` singleton + `AddSignalR()` |
| `Infrastructure/ApplicationBuilderExtensions.cs` | `MapHub<ActiveEditorsHub>()` |
| `Components/FeaturesApiController.cs` | Expose `ActiveEditors` and `ActiveEditorsChat` to client |
| `Tools/Overview/OverviewController.cs` | Add `ActiveEditors()` action |
| `Menu/EditorPowertoolsMenuProvider.cs` | Add menu item |
| `modules/_protected/EditorPowertools/module.config` | Add `signalr.min.js` client resource |
| `ClientResources/js/commands/EditorPowertoolsCommandsInitializer.js` | Load tracker when feature enabled |

---

## Task 1: Feature Toggles, Permission, and Registration

**Files:**
- Modify: `src/EditorPowertools/Configuration/FeatureToggles.cs:24`
- Modify: `src/EditorPowertools/Permissions/EditorPowertoolsPermissions.cs:61`
- Modify: `src/EditorPowertools/Components/FeaturesApiController.cs:27-35`

- [ ] **Step 1: Add feature toggles**

In `FeatureToggles.cs`, add after the `CmsDoctor` line (line 24):

```csharp
    public bool ActiveEditors { get; set; } = true;
    public bool ActiveEditorsChat { get; set; } = true;
```

- [ ] **Step 2: Add permission type**

In `EditorPowertoolsPermissions.cs`, add after the `CmsDoctor` entry (line 61):

```csharp
    public static PermissionType ActiveEditors { get; } =
        new("EditorPowertools", "ActiveEditors");
```

- [ ] **Step 3: Expose features to client**

In `FeaturesApiController.cs`, add to the anonymous object in `GetFeatures()`:

```csharp
            ActiveEditors = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ActiveEditors),
                EditorPowertoolsPermissions.ActiveEditors),
            ActiveEditorsChat = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ActiveEditorsChat),
                EditorPowertoolsPermissions.ActiveEditors)
```

Note: `ActiveEditorsChat` reuses the `ActiveEditors` permission — it's a sub-toggle, not a separate permission.

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/EditorPowertools/Configuration/FeatureToggles.cs \
        src/EditorPowertools/Permissions/EditorPowertoolsPermissions.cs \
        src/EditorPowertools/Components/FeaturesApiController.cs
git commit -m "Add ActiveEditors feature toggles and permission"
```

---

## Task 2: DTOs and Service

**Files:**
- Create: `src/EditorPowertools/Tools/ActiveEditors/Models/ActiveEditorsDtos.cs`
- Create: `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsService.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create DTOs**

Create `src/EditorPowertools/Tools/ActiveEditors/Models/ActiveEditorsDtos.cs`:

```csharp
namespace EditorPowertools.Tools.ActiveEditors.Models;

public class EditorPresence
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? ContentId { get; set; }
    public string? ContentName { get; set; }
    public string Action { get; set; } = "idle"; // "editing", "viewing", "idle"
    public DateTime LastSeen { get; set; }
    public DateTime ConnectedAt { get; set; }
}

public class ChatMessage
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}

public class PresenceUpdate
{
    public List<EditorPresenceDto> Editors { get; set; } = new();
}

public class EditorPresenceDto
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? ContentId { get; set; }
    public string? ContentName { get; set; }
    public string Action { get; set; } = "idle";
    public DateTime ConnectedAt { get; set; }
}
```

- [ ] **Step 2: Create ActiveEditorsService**

Create `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsService.cs`:

```csharp
using System.Collections.Concurrent;
using EditorPowertools.Tools.ActiveEditors.Models;

namespace EditorPowertools.Tools.ActiveEditors;

public class ActiveEditorsService
{
    private readonly ConcurrentDictionary<string, EditorPresence> _editors = new();
    private readonly ConcurrentQueue<ChatMessage> _chatHistory = new();
    private readonly ConcurrentDictionary<string, byte> _editorsToday = new();
    private DateTime _todayDate = DateTime.UtcNow.Date;
    private const int MaxChatHistory = 100;
    private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(90);

    public void Connect(string connectionId, string username, string displayName)
    {
        var now = DateTime.UtcNow;
        ResetTodayIfNeeded(now);

        _editors[connectionId] = new EditorPresence
        {
            ConnectionId = connectionId,
            Username = username,
            DisplayName = displayName,
            LastSeen = now,
            ConnectedAt = now
        };

        _editorsToday.TryAdd(username.ToLowerInvariant(), 0);
    }

    public void Disconnect(string connectionId)
    {
        _editors.TryRemove(connectionId, out _);
    }

    public void UpdateContext(string connectionId, int? contentId, string? contentName, string action)
    {
        if (_editors.TryGetValue(connectionId, out var editor))
        {
            editor.ContentId = contentId;
            editor.ContentName = contentName;
            editor.Action = action;
            editor.LastSeen = DateTime.UtcNow;
        }
    }

    public void Heartbeat(string connectionId)
    {
        if (_editors.TryGetValue(connectionId, out var editor))
        {
            editor.LastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Returns editors currently on a specific content item (excluding the caller).
    /// </summary>
    public List<EditorPresenceDto> GetEditorsOnContent(int contentId, string? excludeConnectionId = null)
    {
        CleanupStale();
        return _editors.Values
            .Where(e => e.ContentId == contentId && e.ConnectionId != excludeConnectionId)
            .Select(ToDto)
            .ToList();
    }

    /// <summary>
    /// Returns all currently connected editors.
    /// </summary>
    public List<EditorPresenceDto> GetAllEditors()
    {
        CleanupStale();
        return _editors.Values.Select(ToDto).ToList();
    }

    /// <summary>
    /// Returns usernames of editors who were active today (even if now disconnected).
    /// </summary>
    public List<string> GetEditorNamesToday()
    {
        ResetTodayIfNeeded(DateTime.UtcNow);
        return _editorsToday.Keys.ToList();
    }

    public void AddChatMessage(string username, string displayName, string text)
    {
        var msg = new ChatMessage
        {
            Username = username,
            DisplayName = displayName,
            Text = text,
            TimestampUtc = DateTime.UtcNow
        };

        _chatHistory.Enqueue(msg);

        // Trim to max size
        while (_chatHistory.Count > MaxChatHistory)
            _chatHistory.TryDequeue(out _);
    }

    public List<ChatMessage> GetRecentMessages()
    {
        return _chatHistory.ToList();
    }

    private void CleanupStale()
    {
        var cutoff = DateTime.UtcNow - StaleTimeout;
        var stale = _editors.Where(kvp => kvp.Value.LastSeen < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in stale)
            _editors.TryRemove(key, out _);
    }

    private void ResetTodayIfNeeded(DateTime now)
    {
        var today = now.Date;
        if (today > _todayDate)
        {
            _todayDate = today;
            _editorsToday.Clear();
            // Re-add currently connected editors
            foreach (var editor in _editors.Values)
                _editorsToday.TryAdd(editor.Username.ToLowerInvariant(), 0);
        }
    }

    private static EditorPresenceDto ToDto(EditorPresence e) => new()
    {
        Username = e.Username,
        DisplayName = e.DisplayName,
        ContentId = e.ContentId,
        ContentName = e.ContentName,
        Action = e.Action,
        ConnectedAt = e.ConnectedAt
    };
}
```

- [ ] **Step 3: Register service and SignalR**

In `ServiceCollectionExtensions.cs`, add the using at the top:

```csharp
using EditorPowertools.Tools.ActiveEditors;
```

Add before the `// Register as a protected module` comment (line 135):

```csharp
        // Active Editors (real-time presence + chat)
        services.AddSingleton<ActiveEditorsService>();
        services.AddSignalR();
```

- [ ] **Step 4: Map the hub**

In `ApplicationBuilderExtensions.cs`, replace the entire file:

```csharp
using EditorPowertools.Tools.ActiveEditors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace EditorPowertools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Editor Powertools middleware to the request pipeline.
    /// </summary>
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        // Future: add middleware for static file serving, etc.
        return app;
    }

    /// <summary>
    /// Maps Editor Powertools SignalR hubs. Call after MapControllers/MapRazorPages.
    /// </summary>
    public static IEndpointRouteBuilder MapEditorPowertools(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<ActiveEditorsHub>("/editorpowertools/hubs/active-editors");
        return endpoints;
    }
}
```

Note: The consumer's `Startup.cs` / `Program.cs` will need to call `endpoints.MapEditorPowertools()` after `MapContent()`. Check if the sample site already has an endpoint mapping section, and if so, add the call there.

- [ ] **Step 5: Add MapEditorPowertools to sample site**

In the sample site's `Startup.cs`, find the `app.MapContent()` call and add after it:

```csharp
app.MapEditorPowertools();
```

Add the using:
```csharp
using EditorPowertools.Infrastructure;
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/EditorPowertools/Tools/ActiveEditors/ \
        src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs \
        src/EditorPowertools/Infrastructure/ApplicationBuilderExtensions.cs
git commit -m "Add ActiveEditorsService and SignalR registration"
```

---

## Task 3: SignalR Hub

**Files:**
- Create: `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsHub.cs`

- [ ] **Step 1: Create the hub**

Create `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsHub.cs`:

```csharp
using EditorPowertools.Configuration;
using EditorPowertools.Permissions;
using EditorPowertools.Tools.ActiveEditors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace EditorPowertools.Tools.ActiveEditors;

[Authorize(Policy = "codeart:editorpowertools")]
public class ActiveEditorsHub : Hub
{
    private readonly ActiveEditorsService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly IOptions<EditorPowertoolsOptions> _options;

    public ActiveEditorsHub(
        ActiveEditorsService service,
        FeatureAccessChecker accessChecker,
        IOptions<EditorPowertoolsOptions> options)
    {
        _service = service;
        _accessChecker = accessChecker;
        _options = options;
    }

    public override async Task OnConnectedAsync()
    {
        if (!_options.Value.Features.ActiveEditors)
        {
            Context.Abort();
            return;
        }

        var username = Context.User?.Identity?.Name ?? "Unknown";
        var displayName = username; // Could be enhanced with profile lookup
        _service.Connect(Context.ConnectionId, username, displayName);

        await BroadcastPresence();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _service.Disconnect(Context.ConnectionId);
        await BroadcastPresence();
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called when editor navigates to different content.
    /// </summary>
    public async Task UpdateContext(int? contentId, string? contentName, string action)
    {
        // Check who was already on this content before we update
        var previousEditors = contentId.HasValue
            ? _service.GetEditorsOnContent(contentId.Value, Context.ConnectionId)
            : new List<EditorPresenceDto>();

        _service.UpdateContext(Context.ConnectionId, contentId, contentName, action);

        // Notify the arriving editor about who's already here
        if (contentId.HasValue && previousEditors.Count > 0)
        {
            await Clients.Caller.SendAsync("EditorsOnContent", previousEditors);
        }

        // Notify editors already on this content that someone new arrived
        if (contentId.HasValue)
        {
            var username = Context.User?.Identity?.Name ?? "Unknown";
            var arrivedDto = new EditorPresenceDto
            {
                Username = username,
                DisplayName = username,
                ContentId = contentId,
                ContentName = contentName,
                Action = action,
                ConnectedAt = DateTime.UtcNow
            };

            foreach (var editor in previousEditors)
            {
                // Find their connection ID(s) from the service
                var allEditors = _service.GetAllEditors();
                // We need to broadcast to all — the widget will filter
            }
        }

        await BroadcastPresence();
    }

    /// <summary>
    /// Periodic heartbeat to maintain presence.
    /// </summary>
    public void Heartbeat()
    {
        _service.Heartbeat(Context.ConnectionId);
    }

    /// <summary>
    /// Send a chat message to all connected editors.
    /// </summary>
    public async Task SendChat(string text)
    {
        if (!_options.Value.Features.ActiveEditorsChat) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        // Limit message length
        if (text.Length > 500) text = text[..500];

        var username = Context.User?.Identity?.Name ?? "Unknown";
        _service.AddChatMessage(username, username, text);

        await Clients.All.SendAsync("ChatMessage", new ChatMessage
        {
            Username = username,
            DisplayName = username,
            Text = text,
            TimestampUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get recent chat history (called on initial connection).
    /// </summary>
    public List<ChatMessage> GetChatHistory()
    {
        if (!_options.Value.Features.ActiveEditorsChat)
            return new List<ChatMessage>();
        return _service.GetRecentMessages();
    }

    /// <summary>
    /// Get editors active today (including disconnected).
    /// </summary>
    public List<string> GetEditorsToday()
    {
        return _service.GetEditorNamesToday();
    }

    private async Task BroadcastPresence()
    {
        var editors = _service.GetAllEditors();
        await Clients.All.SendAsync("PresenceUpdate", new PresenceUpdate { Editors = editors });
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsHub.cs
git commit -m "Add ActiveEditorsHub SignalR hub"
```

---

## Task 4: Menu Item and Controller Action

**Files:**
- Modify: `src/EditorPowertools/Menu/EditorPowertoolsMenuProvider.cs`
- Modify: `src/EditorPowertools/Tools/Overview/OverviewController.cs`
- Create: `src/EditorPowertools/Views/ActiveEditors/Index.cshtml`

- [ ] **Step 1: Add menu item**

In `EditorPowertoolsMenuProvider.cs`, add after the Link Checker entry (around line 105):

```csharp
        yield return new UrlMenuItem("Active Editors", BaseMenuPath + "/activeeditors",
            GetResourcePath("EditorPowertools/ActiveEditors"))
        {
            SortIndex = 750,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActiveEditors))
        };
```

- [ ] **Step 2: Add controller action**

In `OverviewController.cs` (`EditorPowertoolsController`), add a new action:

```csharp
    [HttpGet]
    public IActionResult ActiveEditors()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActiveEditors),
            EditorPowertoolsPermissions.ActiveEditors))
            return Forbid();

        return View("/Views/ActiveEditors/Index.cshtml");
    }
```

- [ ] **Step 3: Create the view**

Create `src/EditorPowertools/Views/ActiveEditors/Index.cshtml`:

```html
@using EPiServer.Shell
@{
    Layout = "/Views/Shared/_PowertoolsLayout.cshtml";
    ViewData["Title"] = "Active Editors";
    string ActionPath(string path) => Paths.ToResource(typeof(EditorPowertools.Menu.EditorPowertoolsMenuProvider), path);
}

@section NavItems {
    <a href="@ActionPath("EditorPowertools/Overview")">Overview</a>
}

<div class="ept-page-header">
    <h1>Active Editors</h1>
    <p>See who's online and collaborate with your editorial team.</p>
</div>

<div id="ae-stats" class="ept-stats"></div>
<div id="ae-content" class="ae-layout">
    <div id="ae-editors-panel"></div>
    <div id="ae-chat-panel"></div>
</div>

@section Scripts {
    <script src="@(EPiServer.Shell.Paths.ToClientResource(typeof(EditorPowertools.Menu.EditorPowertoolsMenuProvider), "ClientResources/js/signalr.min.js"))"></script>
    <script src="@(EPiServer.Shell.Paths.ToClientResource(typeof(EditorPowertools.Menu.EditorPowertoolsMenuProvider), "ClientResources/js/active-editors-overview.js"))"></script>
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/EditorPowertools/Menu/EditorPowertoolsMenuProvider.cs \
        src/EditorPowertools/Tools/Overview/OverviewController.cs \
        src/EditorPowertools/Views/ActiveEditors/Index.cshtml
git commit -m "Add Active Editors menu item, controller action, and view"
```

---

## Task 5: SignalR Client Library

**Files:**
- Create: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/signalr.min.js`

- [ ] **Step 1: Add the SignalR JavaScript client**

Download or copy the `@microsoft/signalr` browser bundle. This is the official Microsoft SignalR JS client. It needs to be placed as a static file since we're in a protected module context (no npm/CDN).

Run from the project root:

```bash
curl -L -o src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/signalr.min.js \
  "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"
```

If curl fails or the URL changes, manually download the latest `signalr.min.js` for .NET 8 from the Microsoft CDN or npm package `@microsoft/signalr`.

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/signalr.min.js
git commit -m "Add SignalR JavaScript client library"
```

---

## Task 6: Tracker Script (CMS Shell Integration)

**Files:**
- Create: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-tracker.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/commands/EditorPowertoolsCommandsInitializer.js`

- [ ] **Step 1: Create the tracker Dojo module**

Create `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-tracker.js`:

```javascript
define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "epi/shell/_ContextMixin"
], function (declare, lang, when, _ContextMixin) {

    // This module connects to the ActiveEditors SignalR hub,
    // sends context changes as the editor navigates, and handles
    // incoming presence/chat notifications.

    var Tracker = declare([_ContextMixin], {
        _connection: null,
        _currentContentId: null,
        _heartbeatTimer: null,
        _chatEnabled: false,

        constructor: function (options) {
            this._chatEnabled = options && options.chatEnabled !== false;
        },

        start: function () {
            var self = this;

            if (typeof signalR === "undefined") {
                console.warn("[EditorPowertools] SignalR client not loaded, active editors tracking disabled.");
                return;
            }

            this._connection = new signalR.HubConnectionBuilder()
                .withUrl("/editorpowertools/hubs/active-editors")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            // Handle presence updates
            this._connection.on("PresenceUpdate", function (data) {
                // Store globally for widget consumption
                window.__eptActiveEditors = data.editors || [];
                // Dispatch custom event for widgets to pick up
                document.dispatchEvent(new CustomEvent("ept-presence-update", { detail: data }));
            });

            // Handle notification when navigating to content others are editing
            this._connection.on("EditorsOnContent", function (editors) {
                if (editors && editors.length > 0) {
                    var names = editors.map(function (e) { return e.displayName; }).join(", ");
                    self._showToast(names + (editors.length === 1 ? " is" : " are") + " also editing this content");
                }
            });

            // Handle chat messages
            this._connection.on("ChatMessage", function (msg) {
                document.dispatchEvent(new CustomEvent("ept-chat-message", { detail: msg }));
            });

            // Connect
            this._connection.start().then(function () {
                // Send initial context
                when(self.getCurrentContext(), function (context) {
                    self._sendContext(context);
                });

                // Start heartbeat
                self._heartbeatTimer = setInterval(function () {
                    if (self._connection.state === "Connected") {
                        self._connection.invoke("Heartbeat").catch(function () {});
                    }
                }, 30000);
            }).catch(function (err) {
                console.warn("[EditorPowertools] Could not connect to Active Editors hub:", err);
            });

            // Clean disconnect on page unload
            window.addEventListener("beforeunload", function () {
                if (self._heartbeatTimer) clearInterval(self._heartbeatTimer);
                if (self._connection) self._connection.stop();
            });

            // Store connection globally for chat/widget use
            window.__eptHubConnection = this._connection;
        },

        // Called by _ContextMixin when CMS context changes
        contextChanged: function (context, callerData) {
            this.inherited(arguments);
            this._sendContext(context);
        },

        _sendContext: function (ctx) {
            if (!ctx || !ctx.id || !this._connection || this._connection.state !== "Connected") return;

            var contentId = ctx.id;
            if (typeof contentId === "string") {
                contentId = parseInt(contentId.split("_")[0].replace(/[^0-9]/g, ""), 10);
            }
            if (!contentId || contentId === this._currentContentId) return;

            this._currentContentId = contentId;
            var contentName = ctx.name || "";
            var action = "editing";

            this._connection.invoke("UpdateContext", contentId, contentName, action).catch(function (err) {
                console.warn("[EditorPowertools] Failed to update context:", err);
            });
        },

        _showToast: function (message) {
            var toast = document.createElement("div");
            toast.className = "ept-toast";
            toast.textContent = message;
            document.body.appendChild(toast);

            // Animate in
            requestAnimationFrame(function () {
                toast.classList.add("ept-toast--visible");
            });

            // Remove after 5 seconds
            setTimeout(function () {
                toast.classList.remove("ept-toast--visible");
                setTimeout(function () { toast.remove(); }, 300);
            }, 5000);
        }
    });

    // Singleton — only one tracker per CMS shell
    var instance = null;

    return {
        start: function (options) {
            if (!instance) {
                instance = new Tracker(options);
                instance.start();
            }
        }
    };
});
```

- [ ] **Step 2: Update the initializer to load the tracker**

In `EditorPowertoolsCommandsInitializer.js`, update the `define` dependencies and `_registerCommands`:

Replace the entire file:

```javascript
define([
    "dojo/_base/declare",
    "epi/_Module",
    "epi/locator",
    "editorpowertools/commands/ContentTimelineCommand",
    "editorpowertools/commands/ManageChildrenCommand",
    "editorpowertools/active-editors-tracker"
], function (
    declare,
    _Module,
    locator,
    ContentTimelineCommand,
    ManageChildrenCommand,
    ActiveEditorsTracker
) {
    return declare([_Module], {
        initialize: function () {
            this.inherited(arguments);

            // Check which features are enabled before adding commands
            var self = this;
            try {
                var xhr = new XMLHttpRequest();
                xhr.open("GET", "/editorpowertools/api/features", false);
                xhr.send();
                if (xhr.status === 200) {
                    var features = JSON.parse(xhr.responseText);
                    self._registerCommands(features);
                } else {
                    self._registerCommands({});
                }
            } catch (e) {
                self._registerCommands({});
            }
        },

        _registerCommands: function (features) {
            if (features.activityTimeline !== false) {
                locator.add("epi-cms/navigation-tree/commands[]", new ContentTimelineCommand({ order: 500 }));
            }

            if (features.manageChildren !== false) {
                locator.add("epi-cms/navigation-tree/commands[]", new ManageChildrenCommand({ order: 510 }));
            }

            // Start active editors tracking (presence + chat)
            if (features.activeEditors !== false) {
                ActiveEditorsTracker.start({
                    chatEnabled: features.activeEditorsChat !== false
                });
            }
        }
    });
});
```

- [ ] **Step 3: Add signalr.min.js to module.config**

In `module.config`, add the SignalR client resource inside `<clientResources>`:

```xml
        <add name="signalr" path="ClientResources/js/signalr.min.js" resourceType="Script" />
```

And add it as a required resource for the client module:

```xml
        <requiredResources>
            <add name="editorpowertools.css" />
            <add name="signalr" />
        </requiredResources>
```

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-tracker.js \
        src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/commands/EditorPowertoolsCommandsInitializer.js \
        src/EditorPowertools/modules/_protected/EditorPowertools/module.config
git commit -m "Add active editors tracker and CMS shell integration"
```

---

## Task 7: Assets Panel Widget

**Files:**
- Create: `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsComponent.cs`
- Create: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ActiveEditorsWidget.js`

- [ ] **Step 1: Create the component registration**

Create `src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsComponent.cs`:

```csharp
using EPiServer.Shell.ViewComposition;

namespace EditorPowertools.Tools.ActiveEditors;

/// <summary>
/// Registers Active Editors as a component in the CMS assets panel.
/// </summary>
[Component]
public class ActiveEditorsComponent : ComponentDefinitionBase
{
    public ActiveEditorsComponent()
        : base("editorpowertools/ActiveEditorsWidget")
    {
        Categories = new[] { "content" };
        PlugInAreas = new[] { "/episerver/cms/assets" };
        Title = "Active Editors";
        Description = "See who else is editing this content and collaborate with your team.";
        SortOrder = 190; // Just above Content Details (200)
    }
}
```

- [ ] **Step 2: Create the widget**

Create `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ActiveEditorsWidget.js`:

```javascript
define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "dojo/on",
    "dijit/_TemplatedMixin",
    "dijit/layout/_LayoutWidget",
    "epi/shell/_ContextMixin"
], function (
    declare, lang, when, on,
    _TemplatedMixin, _LayoutWidget,
    _ContextMixin
) {
    return declare("editorpowertools.ActiveEditorsWidget", [_LayoutWidget, _TemplatedMixin, _ContextMixin], {
        templateString: '<div class="ept-ae-root">' +
            '<div data-dojo-attach-point="containerNode" class="ept-ae-container">' +
            '<div class="ept-ae-empty">Connecting...</div>' +
            '</div>' +
            '</div>',

        _currentContentId: null,
        _eventHandler: null,

        postCreate: function () {
            this.inherited(arguments);
            var self = this;

            // Listen for presence updates from the tracker
            this._eventHandler = function (e) {
                self._onPresenceUpdate(e.detail);
            };
            document.addEventListener("ept-presence-update", this._eventHandler);

            // Load initial context
            when(this.getCurrentContext(), function (context) {
                self._onContextChanged(context);
            });

            // If we already have data, render it
            if (window.__eptActiveEditors) {
                this._render(window.__eptActiveEditors);
            }
        },

        destroy: function () {
            if (this._eventHandler) {
                document.removeEventListener("ept-presence-update", this._eventHandler);
            }
            this.inherited(arguments);
        },

        contextChanged: function (context, callerData) {
            this.inherited(arguments);
            this._onContextChanged(context);
        },

        _onContextChanged: function (ctx) {
            if (!ctx || !ctx.id) return;
            var contentId = ctx.id;
            if (typeof contentId === "string") {
                contentId = parseInt(contentId.split("_")[0].replace(/[^0-9]/g, ""), 10);
            }
            this._currentContentId = contentId;
            this._render(window.__eptActiveEditors || []);
        },

        _onPresenceUpdate: function (data) {
            this._render(data.editors || []);
        },

        _render: function (allEditors) {
            var container = this.containerNode;
            if (!this._currentContentId) {
                container.innerHTML = '<div class="ept-ae-empty">Select content to see active editors.</div>';
                return;
            }

            // Filter to editors on this content (exclude current user — we know we're here)
            var currentUser = this._getCurrentUsername();
            var onContent = [];
            var otherOnline = [];

            for (var i = 0; i < allEditors.length; i++) {
                var e = allEditors[i];
                if (e.username === currentUser) continue;
                if (e.contentId === this._currentContentId) {
                    onContent.push(e);
                } else {
                    otherOnline.push(e);
                }
            }

            var html = '';

            // Editors on this content
            if (onContent.length > 0) {
                html += '<div class="ept-ae-section">';
                html += '<div class="ept-ae-section-title">On this content</div>';
                for (var j = 0; j < onContent.length; j++) {
                    html += this._renderEditor(onContent[j], true);
                }
                html += '</div>';
            }

            // Other online editors
            if (otherOnline.length > 0) {
                html += '<div class="ept-ae-section">';
                html += '<div class="ept-ae-section-title">Online now</div>';
                for (var k = 0; k < otherOnline.length; k++) {
                    html += this._renderEditor(otherOnline[k], false);
                }
                html += '</div>';
            }

            if (!html) {
                html = '<div class="ept-ae-empty">No other editors online.</div>';
            }

            container.innerHTML = html;
        },

        _renderEditor: function (editor, showAction) {
            var actionClass = "ept-ae-dot--" + (editor.action || "idle");
            var html = '<div class="ept-ae-editor">';
            html += '<span class="ept-ae-dot ' + actionClass + '"></span>';
            html += '<span class="ept-ae-name">' + this._esc(editor.displayName) + '</span>';
            if (showAction) {
                html += ' <span class="ept-ae-action">' + this._esc(editor.action) + '</span>';
            } else if (editor.contentName) {
                html += ' <span class="ept-ae-content">' + this._esc(editor.contentName) + '</span>';
            }
            html += '</div>';
            return html;
        },

        _getCurrentUsername: function () {
            // Best effort — check global or fall back
            if (window.__eptCurrentUser) return window.__eptCurrentUser;
            return "";
        },

        _esc: function (s) {
            if (!s) return "";
            var d = document.createElement("div");
            d.textContent = String(s);
            return d.innerHTML;
        },

        resize: function () {
            this.inherited(arguments);
        }
    });
});
```

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ActiveEditors/ActiveEditorsComponent.cs \
        src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ActiveEditorsWidget.js
git commit -m "Add Active Editors assets panel widget"
```

---

## Task 8: Full-Page Overview with Chat

**Files:**
- Create: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js`

- [ ] **Step 1: Create the overview JS**

Create `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js`:

```javascript
/**
 * Active Editors Overview - Full-page tool with editor list + chat
 */
(function () {
    var connection = null;
    var editors = [];
    var editorsToday = [];
    var chatMessages = [];
    var chatEnabled = false;
    var currentUser = '';

    function init() {
        EPT.showLoading(document.getElementById('ae-editors-panel'));

        // Check features
        EPT.fetchJson('/editorpowertools/api/features').then(function (features) {
            chatEnabled = features.activeEditorsChat !== false;
            startConnection();
        }).catch(function () {
            chatEnabled = false;
            startConnection();
        });
    }

    function startConnection() {
        if (typeof signalR === 'undefined') {
            document.getElementById('ae-editors-panel').innerHTML =
                '<div class="ept-empty"><p>SignalR client not available.</p></div>';
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/editorpowertools/hubs/active-editors')
            .withAutomaticReconnect()
            .build();

        connection.on('PresenceUpdate', function (data) {
            editors = data.editors || [];
            renderEditors();
            renderStats();
        });

        connection.on('ChatMessage', function (msg) {
            chatMessages.push(msg);
            renderChat();
        });

        connection.start().then(function () {
            // Get initial data
            connection.invoke('GetEditorsToday').then(function (names) {
                editorsToday = names || [];
                renderEditors();
            });

            if (chatEnabled) {
                connection.invoke('GetChatHistory').then(function (messages) {
                    chatMessages = messages || [];
                    renderChat();
                });
            }

            // Determine current user from editors list (we're connected, so we're in it)
            setTimeout(function () {
                // After presence update arrives
                renderEditors();
                renderStats();
                renderChatPanel();
            }, 500);
        }).catch(function (err) {
            document.getElementById('ae-editors-panel').innerHTML =
                '<div class="ept-empty"><p>Could not connect: ' + escHtml(err.message) + '</p></div>';
        });
    }

    function renderStats() {
        var el = document.getElementById('ae-stats');
        var onlineCount = editors.length;
        var todayCount = editorsToday.length;
        var editingCount = editors.filter(function (e) { return e.action === 'editing'; }).length;

        el.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + onlineCount + '</div><div class="ept-stat__label">Online Now</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + editingCount + '</div><div class="ept-stat__label">Currently Editing</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + todayCount + '</div><div class="ept-stat__label">Active Today</div></div>';
    }

    function renderEditors() {
        var panel = document.getElementById('ae-editors-panel');

        if (editors.length === 0) {
            panel.innerHTML = '<div class="ept-empty"><p>No editors currently online.</p></div>';
            return;
        }

        var html = '<div class="ae-editor-list">';
        html += '<h3>Online Now</h3>';

        for (var i = 0; i < editors.length; i++) {
            var e = editors[i];
            var dotClass = 'ae-dot--' + (e.action || 'idle');
            html += '<div class="ae-editor-card">';
            html += '<div class="ae-editor-header">';
            html += '<span class="ae-dot ' + dotClass + '"></span>';
            html += '<strong>' + escHtml(e.displayName) + '</strong>';
            html += '<span class="ae-action-badge ae-action-badge--' + (e.action || 'idle') + '">' + escHtml(e.action || 'idle') + '</span>';
            html += '</div>';
            if (e.contentName) {
                html += '<div class="ae-editor-detail">';
                html += '<span class="ae-content-name">' + escHtml(e.contentName) + '</span>';
                html += '</div>';
            }
            var connTime = formatRelativeTime(e.connectedAt);
            html += '<div class="ae-editor-meta">Connected ' + connTime + '</div>';
            html += '</div>';
        }
        html += '</div>';

        // Editors today section (show those not currently online)
        var onlineNames = {};
        editors.forEach(function (e) { onlineNames[e.username.toLowerCase()] = true; });
        var offlineToday = editorsToday.filter(function (name) { return !onlineNames[name.toLowerCase()]; });

        if (offlineToday.length > 0) {
            html += '<div class="ae-editor-list ae-editor-list--today">';
            html += '<h3>Also Active Today</h3>';
            for (var j = 0; j < offlineToday.length; j++) {
                html += '<div class="ae-editor-card ae-editor-card--offline">';
                html += '<span class="ae-dot ae-dot--offline"></span>';
                html += '<strong>' + escHtml(offlineToday[j]) + '</strong>';
                html += '<span class="ae-action-badge ae-action-badge--offline">offline</span>';
                html += '</div>';
            }
            html += '</div>';
        }

        panel.innerHTML = html;
    }

    function renderChatPanel() {
        var panel = document.getElementById('ae-chat-panel');
        if (!chatEnabled) {
            panel.style.display = 'none';
            return;
        }

        panel.innerHTML =
            '<div class="ae-chat">' +
                '<h3>Team Chat</h3>' +
                '<div class="ae-chat-messages" id="ae-chat-messages"></div>' +
                '<div class="ae-chat-input">' +
                    '<input type="text" id="ae-chat-text" class="ept-input" placeholder="Type a message..." maxlength="500" />' +
                    '<button id="ae-chat-send" class="ept-btn ept-btn--primary">Send</button>' +
                '</div>' +
            '</div>';

        document.getElementById('ae-chat-send').addEventListener('click', sendChat);
        document.getElementById('ae-chat-text').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') sendChat();
        });

        renderChat();
    }

    function renderChat() {
        var el = document.getElementById('ae-chat-messages');
        if (!el) return;

        var html = '';
        for (var i = 0; i < chatMessages.length; i++) {
            var msg = chatMessages[i];
            var time = new Date(msg.timestampUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            html += '<div class="ae-chat-msg">';
            html += '<span class="ae-chat-user">' + escHtml(msg.displayName) + '</span>';
            html += '<span class="ae-chat-time">' + time + '</span>';
            html += '<div class="ae-chat-text">' + escHtml(msg.text) + '</div>';
            html += '</div>';
        }
        el.innerHTML = html;
        el.scrollTop = el.scrollHeight;
    }

    function sendChat() {
        var input = document.getElementById('ae-chat-text');
        var text = input.value.trim();
        if (!text || !connection) return;

        connection.invoke('SendChat', text).catch(function (err) {
            console.warn('Failed to send chat:', err);
        });
        input.value = '';
    }

    function formatRelativeTime(utcStr) {
        var date = new Date(utcStr);
        var now = new Date();
        var diffMin = Math.floor((now - date) / 60000);
        if (diffMin < 1) return 'just now';
        if (diffMin < 60) return diffMin + 'm ago';
        var diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return diffHour + 'h ago';
        return date.toLocaleDateString();
    }

    function escHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // ── Styles ─────────────────────────────────────────────────────
    function injectStyles() {
        var style = document.createElement('style');
        style.textContent =
            '.ae-layout { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }' +
            '@media (max-width: 900px) { .ae-layout { grid-template-columns: 1fr; } }' +

            '.ae-editor-list h3 { font-size: 14px; font-weight: 600; margin: 0 0 12px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; }' +

            '.ae-editor-card { display: flex; flex-direction: column; gap: 4px; padding: 12px 16px; background: #fff; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; margin-bottom: 8px; }' +
            '.ae-editor-card--offline { opacity: 0.6; }' +

            '.ae-editor-header { display: flex; align-items: center; gap: 8px; }' +
            '.ae-editor-detail { font-size: 13px; color: var(--ept-text, #333); padding-left: 20px; }' +
            '.ae-editor-meta { font-size: 11px; color: var(--ept-text-muted, #999); padding-left: 20px; }' +

            '.ae-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }' +
            '.ae-dot--editing { background: #22c55e; box-shadow: 0 0 6px rgba(34,197,94,0.4); }' +
            '.ae-dot--viewing { background: #3b82f6; }' +
            '.ae-dot--idle { background: #9ca3af; }' +
            '.ae-dot--offline { background: #d1d5db; }' +

            '.ae-action-badge { font-size: 11px; padding: 1px 8px; border-radius: 10px; font-weight: 500; margin-left: auto; }' +
            '.ae-action-badge--editing { background: #dcfce7; color: #166534; }' +
            '.ae-action-badge--viewing { background: #dbeafe; color: #1e40af; }' +
            '.ae-action-badge--idle { background: #f3f4f6; color: #6b7280; }' +
            '.ae-action-badge--offline { background: #f3f4f6; color: #9ca3af; }' +

            '.ae-content-name::before { content: ""; display: inline-block; width: 12px; height: 12px; margin-right: 4px; vertical-align: -1px; background: currentColor; mask-size: contain; -webkit-mask-size: contain; }' +

            /* Chat */
            '.ae-chat { display: flex; flex-direction: column; height: 100%; }' +
            '.ae-chat h3 { font-size: 14px; font-weight: 600; margin: 0 0 12px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; }' +
            '.ae-chat-messages { flex: 1; min-height: 300px; max-height: 500px; overflow-y: auto; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; padding: 12px; margin-bottom: 12px; background: #fafafa; }' +
            '.ae-chat-msg { margin-bottom: 10px; }' +
            '.ae-chat-user { font-weight: 600; font-size: 13px; margin-right: 6px; }' +
            '.ae-chat-time { font-size: 11px; color: var(--ept-text-muted, #999); }' +
            '.ae-chat-text { font-size: 13px; margin-top: 2px; color: var(--ept-text, #333); }' +
            '.ae-chat-input { display: flex; gap: 8px; }' +
            '.ae-chat-input .ept-input { flex: 1; }' +

            /* Toast for tracker notifications */
            '.ept-toast { position: fixed; bottom: 20px; right: 20px; background: #1e293b; color: #fff; padding: 12px 20px; border-radius: 8px; font-size: 13px; z-index: 99999; opacity: 0; transform: translateY(10px); transition: opacity 0.3s, transform 0.3s; box-shadow: 0 4px 12px rgba(0,0,0,0.2); }' +
            '.ept-toast--visible { opacity: 1; transform: translateY(0); }' +

            /* Widget styles */
            '.ept-ae-root { font-size: 12px; }' +
            '.ept-ae-container { padding: 8px; }' +
            '.ept-ae-empty { color: var(--ept-text-muted, #999); font-style: italic; padding: 12px 0; }' +
            '.ept-ae-section { margin-bottom: 12px; }' +
            '.ept-ae-section-title { font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--ept-text-muted, #999); margin-bottom: 6px; }' +
            '.ept-ae-editor { display: flex; align-items: center; gap: 6px; padding: 4px 0; }' +
            '.ept-ae-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }' +
            '.ept-ae-dot--editing { background: #22c55e; }' +
            '.ept-ae-dot--viewing { background: #3b82f6; }' +
            '.ept-ae-dot--idle { background: #9ca3af; }' +
            '.ept-ae-name { font-weight: 600; }' +
            '.ept-ae-action { font-size: 10px; color: var(--ept-text-muted, #999); }' +
            '.ept-ae-content { font-size: 10px; color: var(--ept-text-muted, #999); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 120px; }';

        document.head.appendChild(style);
    }

    injectStyles();
    init();
})();
```

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js
git commit -m "Add Active Editors full-page overview with chat"
```

---

## Task 9: Wire Up Sample Site and Test

**Files:**
- Modify: `src/EditorPowertools.SampleSite/Startup.cs`

- [ ] **Step 1: Update sample site Startup.cs**

Add the `MapEditorPowertools()` call. Find the endpoint mapping section (likely `app.MapContent()` or similar) and add:

```csharp
app.MapEditorPowertools();
```

Ensure the using is present:
```csharp
using EditorPowertools.Infrastructure;
```

- [ ] **Step 2: Build the entire solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run and manually test**

Run: `dotnet run --project src/EditorPowertools.SampleSite`

Test checklist:
1. Open CMS edit mode — check that no errors in browser console from SignalR connection
2. Navigate between pages — check Network tab for SignalR WebSocket messages
3. Open the Active Editors menu item — verify the overview page loads
4. Check the Assets panel — verify "Active Editors" widget appears
5. Open a second browser/incognito window — verify presence appears in both
6. Send a chat message — verify it appears in both windows
7. Close one window — verify the editor disappears from the other

- [ ] **Step 4: Commit all remaining changes**

```bash
git add -A
git commit -m "Wire up Active Editors in sample site and verify integration"
```

---

## Task 10: Update Backlog

**Files:**
- Modify: `docs/backlog.md`

- [ ] **Step 1: Mark Active Editors Widget as done**

In `docs/backlog.md`, change:
```
- [ ] **Active Editors Widget**
```
to:
```
- [x] **Active Editors Widget**
```

- [ ] **Step 2: Commit**

```bash
git add docs/backlog.md
git commit -m "Mark Active Editors Widget as done in backlog"
```
