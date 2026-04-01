# Active Editors — Design Spec

## Overview

Real-time editor presence, activity tracking, and ephemeral chat for the CMS editorial team. All components behind an `ActiveEditors` feature toggle (default: true) with sub-toggles for chat.

## Architecture

### SignalR Hub: `ActiveEditorsHub`

Single hub at `/editorpowertools/hubs/active-editors`. Handles:

- **Heartbeat/presence**: Client sends context changes (contentId, contentName, action like "editing"/"viewing"). Server tracks in memory.
- **Presence broadcast**: Server pushes updated editor list to all connected clients on join/leave/context change.
- **Chat messages**: Client sends message text, server broadcasts to all connected clients with sender info and timestamp.
- **Notifications**: When another editor starts editing the same content you're on, you get a notification.

Authorization: Same `codeart:editorpowertools` policy as all other endpoints.

### `ActiveEditorsService` (Singleton)

In-memory state using `ConcurrentDictionary<string, EditorPresence>`:

```csharp
public class EditorPresence
{
    public string ConnectionId { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public int? ContentId { get; set; }
    public string? ContentName { get; set; }
    public string Action { get; set; } // "editing", "viewing", "idle"
    public DateTime LastSeen { get; set; }
    public DateTime ConnectedAt { get; set; }
}
```

- Stale entries cleaned up after 90 seconds of no heartbeat (via check on each hub call, no background timer needed).
- Tracks "today's editors" separately: a `HashSet<string>` of usernames seen today, reset on date change. This powers the "active today" list even after editors disconnect.

### Tracking Script (Dojo Module)

Loaded via the existing `EditorPowertoolsCommandsInitializer` pattern — only when `ActiveEditors` feature is enabled (checked via `/editorpowertools/api/features`).

- Connects to the SignalR hub on CMS shell load
- Sends heartbeat on Optimizely context change (using `_ContextMixin` pattern — same as ContentDetailsWidget)
- Receives presence updates and stores locally
- Receives chat messages and shows notification toast
- Sends heartbeat every 30 seconds to maintain presence
- Graceful disconnect on page unload

### Assets Panel Widget: `ActiveEditorsWidget`

Registered as a `ComponentDefinitionBase` in the `/episerver/cms/assets` area (same pattern as ContentDetailsWidget).

Shows for the currently selected content:
- List of other editors currently viewing/editing this content item
- Their action (editing/viewing) with colored indicator
- "Active today" section showing editors who were active today

### Full-Page Tool: Active Editors Overview

Menu item under EditorPowertools. Shows:
- All currently online editors with what they're working on
- Editors active today (including those now offline)
- Chat panel (if chat enabled)

### Chat

- Ephemeral — messages only exist in memory, not persisted
- Broadcast to all connected editors via the hub
- Recent messages kept in a ring buffer (last 100 messages) in the service so new connections get recent context
- Chat sub-toggle: `ActiveEditorsChat` feature toggle (default: true). When disabled, chat UI hidden, hub ignores chat messages.

### Notifications

When an editor navigates to content that another editor is already editing:
- The arriving editor sees a banner: "Alice is currently editing this page"
- The existing editor gets a toast notification: "Bob just opened this page"
- Uses simple hub callbacks (not Optimizely's notification system — that's for persistent notifications, this is ephemeral)

## Feature Toggles

```csharp
public bool ActiveEditors { get; set; } = true;      // Master toggle
public bool ActiveEditorsChat { get; set; } = true;   // Chat sub-toggle
```

When `ActiveEditors` is false:
- Tracking script not loaded (checked in initializer)
- Hub still mapped but rejects connections (returns empty)
- Widget not registered
- Menu item hidden
- Zero runtime overhead

## Files to Create

### Backend
- `Tools/ActiveEditors/ActiveEditorsHub.cs` — SignalR hub
- `Tools/ActiveEditors/ActiveEditorsService.cs` — In-memory presence + chat state
- `Tools/ActiveEditors/Models/ActiveEditorsDtos.cs` — DTOs
- `Tools/ActiveEditors/ActiveEditorsComponent.cs` — Assets panel widget registration

### Frontend
- `ClientResources/js/ActiveEditorsWidget.js` — Assets panel Dojo widget
- `ClientResources/js/active-editors-tracker.js` — Tracking script (loaded by initializer)
- `ClientResources/js/active-editors-overview.js` — Full-page tool UI

### Views
- `Views/ActiveEditors/Index.cshtml` — Full-page tool view

### Modified Files
- `Configuration/FeatureToggles.cs` — Add toggles
- `Infrastructure/ServiceCollectionExtensions.cs` — Register service + SignalR
- `Infrastructure/ApplicationBuilderExtensions.cs` — Map hub
- `Menu/EditorPowertoolsMenuProvider.cs` — Add menu item
- `Permissions/EditorPowertoolsPermissions.cs` — Add permission
- `Components/FeaturesApiController.cs` — Expose toggles to client
- `commands/EditorPowertoolsCommandsInitializer.js` — Load tracker when enabled
- `module.config` — Register new Dojo module paths

## Data Flow

1. Editor opens CMS → initializer loads → tracker connects to hub
2. Editor navigates to content → tracker sends `UpdateContext(contentId, contentName, "editing")`
3. Hub updates service → broadcasts presence to all clients
4. Other editors on same content see notification
5. Widget shows live list of editors on current content
6. Chat: editor types message → hub broadcasts → all clients show in chat panel
7. Editor closes tab → SignalR `OnDisconnectedAsync` → service removes → broadcast update
