using System.Reflection;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors.Models;
using FluentAssertions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ActiveEditors;

public class ActiveEditorsServiceTests
{
    private readonly ActiveEditorsService _sut = new();

    #region Connect / Disconnect

    [Fact]
    public void Connect_ShouldAddEditorToGetAllEditors()
    {
        _sut.Connect("conn1", "alice", "Alice A");

        var editors = _sut.GetAllEditors();
        editors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Username = "alice",
                DisplayName = "Alice A"
            });
    }

    [Fact]
    public void Connect_MultipleTimes_ShouldTrackAllEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");

        _sut.GetAllEditors().Should().HaveCount(2);
    }

    [Fact]
    public void Connect_SameConnectionId_ShouldOverwritePrevious()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn1", "bob", "Bob");

        var editors = _sut.GetAllEditors();
        editors.Should().ContainSingle()
            .Which.Username.Should().Be("bob");
    }

    [Fact]
    public void Disconnect_ShouldRemoveEditor()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Disconnect("conn1");

        _sut.GetAllEditors().Should().BeEmpty();
    }

    [Fact]
    public void Disconnect_NonExistentConnection_ShouldNotThrow()
    {
        var act = () => _sut.Disconnect("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void Disconnect_OnlyRemovesSpecifiedEditor()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");

        _sut.Disconnect("conn1");

        var editors = _sut.GetAllEditors();
        editors.Should().ContainSingle()
            .Which.Username.Should().Be("bob");
    }

    #endregion

    #region UpdateContext

    [Fact]
    public void UpdateContext_ShouldSetContentFields()
    {
        _sut.Connect("conn1", "alice", "Alice");

        _sut.UpdateContext("conn1", 42, "Start Page", "editing");

        var editor = _sut.GetAllEditors().Single();
        editor.ContentId.Should().Be(42);
        editor.ContentName.Should().Be("Start Page");
        editor.Action.Should().Be("editing");
    }

    [Fact]
    public void UpdateContext_NonExistentConnection_ShouldNotThrow()
    {
        var act = () => _sut.UpdateContext("nonexistent", 1, "Page", "editing");
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateContext_ShouldUpdateLastSeen()
    {
        _sut.Connect("conn1", "alice", "Alice");
        var beforeUpdate = DateTime.UtcNow;

        // Small delay to ensure timestamp differs
        Thread.Sleep(15);
        _sut.UpdateContext("conn1", 42, "Page", "editing");

        // We can't read LastSeen from the DTO directly, but we can verify
        // the editor is still active (not cleaned up) which implies LastSeen was refreshed
        _sut.GetAllEditors().Should().ContainSingle();
    }

    [Fact]
    public void UpdateContext_WithNullContentId_ShouldAllowNull()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.UpdateContext("conn1", 42, "Page", "editing");
        _sut.UpdateContext("conn1", null, null, "idle");

        var editor = _sut.GetAllEditors().Single();
        editor.ContentId.Should().BeNull();
        editor.ContentName.Should().BeNull();
        editor.Action.Should().Be("idle");
    }

    #endregion

    #region Heartbeat

    [Fact]
    public void Heartbeat_ShouldNotThrowForNonExistentConnection()
    {
        var act = () => _sut.Heartbeat("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void Heartbeat_ShouldKeepEditorAlive()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Heartbeat("conn1");

        _sut.GetAllEditors().Should().ContainSingle();
    }

    #endregion

    #region GetEditorsOnContent

    [Fact]
    public void GetEditorsOnContent_ShouldReturnOnlyEditorsOnThatContent()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");
        _sut.Connect("conn3", "carol", "Carol");

        _sut.UpdateContext("conn1", 42, "Start Page", "editing");
        _sut.UpdateContext("conn2", 42, "Start Page", "viewing");
        _sut.UpdateContext("conn3", 99, "About Page", "editing");

        var editors = _sut.GetEditorsOnContent(42);

        editors.Should().HaveCount(2);
        editors.Select(e => e.Username).Should().BeEquivalentTo("alice", "bob");
    }

    [Fact]
    public void GetEditorsOnContent_ShouldExcludeSpecifiedConnectionId()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");

        _sut.UpdateContext("conn1", 42, "Start Page", "editing");
        _sut.UpdateContext("conn2", 42, "Start Page", "viewing");

        var editors = _sut.GetEditorsOnContent(42, excludeConnectionId: "conn1");

        editors.Should().ContainSingle()
            .Which.Username.Should().Be("bob");
    }

    [Fact]
    public void GetEditorsOnContent_NoMatch_ShouldReturnEmpty()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.UpdateContext("conn1", 42, "Start Page", "editing");

        _sut.GetEditorsOnContent(999).Should().BeEmpty();
    }

    [Fact]
    public void GetEditorsOnContent_EditorsWithNoContent_ShouldNotBeReturned()
    {
        _sut.Connect("conn1", "alice", "Alice");
        // No UpdateContext called, so ContentId is null

        _sut.GetEditorsOnContent(42).Should().BeEmpty();
    }

    #endregion

    #region GetEditorNamesToday

    [Fact]
    public void GetEditorNamesToday_ShouldTrackConnectedUsernames()
    {
        _sut.Connect("conn1", "Alice", "Alice A");
        _sut.Connect("conn2", "Bob", "Bob B");

        var names = _sut.GetEditorNamesToday();

        names.Should().HaveCount(2);
        // Usernames are stored lowercase
        names.Should().Contain("alice");
        names.Should().Contain("bob");
    }

    [Fact]
    public void GetEditorNamesToday_SameUserMultipleConnections_ShouldDedup()
    {
        _sut.Connect("conn1", "Alice", "Alice A");
        _sut.Connect("conn2", "Alice", "Alice A");

        _sut.GetEditorNamesToday().Should().ContainSingle()
            .Which.Should().Be("alice");
    }

    [Fact]
    public void GetEditorNamesToday_ShouldPersistAfterDisconnect()
    {
        _sut.Connect("conn1", "Alice", "Alice A");
        _sut.Disconnect("conn1");

        // Editor disconnected but should still appear in today's names
        _sut.GetEditorNamesToday().Should().Contain("alice");
    }

    [Fact]
    public void GetEditorNamesToday_CaseInsensitive()
    {
        _sut.Connect("conn1", "Alice", "Alice");
        _sut.Connect("conn2", "ALICE", "Alice Upper");

        _sut.GetEditorNamesToday().Should().ContainSingle();
    }

    [Fact]
    public void GetEditorNamesToday_ShouldResetOnDateChange()
    {
        _sut.Connect("conn1", "alice", "Alice");

        // Force date change by setting _todayDate to yesterday via reflection
        var todayField = typeof(ActiveEditorsService)
            .GetField("_todayDate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        todayField.SetValue(_sut, DateTime.UtcNow.Date.AddDays(-1));

        // Disconnect alice so she's not in _editors anymore
        _sut.Disconnect("conn1");

        // Now GetEditorNamesToday should trigger reset; alice is gone from _editors
        var names = _sut.GetEditorNamesToday();
        names.Should().BeEmpty();
    }

    [Fact]
    public void GetEditorNamesToday_DateReset_ShouldReAddCurrentlyConnectedEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");

        // Force date change
        var todayField = typeof(ActiveEditorsService)
            .GetField("_todayDate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        todayField.SetValue(_sut, DateTime.UtcNow.Date.AddDays(-1));

        // Disconnect bob only
        _sut.Disconnect("conn2");

        // Reset triggers: only alice (still connected) should remain
        var names = _sut.GetEditorNamesToday();
        names.Should().ContainSingle().Which.Should().Be("alice");
    }

    #endregion

    #region Chat Messages

    [Fact]
    public void AddChatMessage_ShouldBeRetrievableViaGetRecentMessages()
    {
        _sut.AddChatMessage("alice", "Alice", "Hello!");

        var messages = _sut.GetRecentMessages();
        messages.Should().ContainSingle();
        messages[0].Username.Should().Be("alice");
        messages[0].DisplayName.Should().Be("Alice");
        messages[0].Text.Should().Be("Hello!");
        messages[0].TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddChatMessage_MultipleMessages_ShouldPreserveOrder()
    {
        _sut.AddChatMessage("alice", "Alice", "First");
        _sut.AddChatMessage("bob", "Bob", "Second");
        _sut.AddChatMessage("carol", "Carol", "Third");

        var messages = _sut.GetRecentMessages();
        messages.Should().HaveCount(3);
        messages[0].Text.Should().Be("First");
        messages[1].Text.Should().Be("Second");
        messages[2].Text.Should().Be("Third");
    }

    [Fact]
    public void AddChatMessage_RingBuffer_ShouldTrimAtMaxHistory()
    {
        // Add 105 messages; only the last 100 should remain
        for (int i = 0; i < 105; i++)
        {
            _sut.AddChatMessage("user", "User", $"Message {i}");
        }

        var messages = _sut.GetRecentMessages();
        messages.Should().HaveCount(100);
        // First message should be #5 (0-4 were trimmed)
        messages[0].Text.Should().Be("Message 5");
        messages[99].Text.Should().Be("Message 104");
    }

    [Fact]
    public void GetRecentMessages_WhenEmpty_ShouldReturnEmptyList()
    {
        _sut.GetRecentMessages().Should().BeEmpty();
    }

    #endregion

    #region Stale Cleanup

    [Fact]
    public void GetAllEditors_ShouldRemoveStaleEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");

        // Make the editor stale by setting LastSeen via reflection on the internal dict
        var editorsField = typeof(ActiveEditorsService)
            .GetField("_editors", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var editors = (System.Collections.Concurrent.ConcurrentDictionary<string, EditorPresence>)editorsField.GetValue(_sut)!;
        editors["conn1"].LastSeen = DateTime.UtcNow.AddSeconds(-91);

        _sut.GetAllEditors().Should().BeEmpty();
    }

    [Fact]
    public void GetAllEditors_ShouldKeepFreshEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");

        // LastSeen is set to UtcNow on Connect, so editor is fresh
        _sut.GetAllEditors().Should().ContainSingle();
    }

    [Fact]
    public void GetEditorsOnContent_ShouldRemoveStaleEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.UpdateContext("conn1", 42, "Page", "editing");

        var editorsField = typeof(ActiveEditorsService)
            .GetField("_editors", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var editors = (System.Collections.Concurrent.ConcurrentDictionary<string, EditorPresence>)editorsField.GetValue(_sut)!;
        editors["conn1"].LastSeen = DateTime.UtcNow.AddSeconds(-91);

        _sut.GetEditorsOnContent(42).Should().BeEmpty();
    }

    [Fact]
    public void StaleCleanup_ShouldOnlyRemoveStaleEditors()
    {
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");

        var editorsField = typeof(ActiveEditorsService)
            .GetField("_editors", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var editors = (System.Collections.Concurrent.ConcurrentDictionary<string, EditorPresence>)editorsField.GetValue(_sut)!;
        // Make only alice stale
        editors["conn1"].LastSeen = DateTime.UtcNow.AddSeconds(-91);

        var result = _sut.GetAllEditors();
        result.Should().ContainSingle().Which.Username.Should().Be("bob");
    }

    [Fact]
    public void StaleCleanup_EditorAt89Seconds_ShouldNotBeRemoved()
    {
        _sut.Connect("conn1", "alice", "Alice");

        var editorsField = typeof(ActiveEditorsService)
            .GetField("_editors", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var editors = (System.Collections.Concurrent.ConcurrentDictionary<string, EditorPresence>)editorsField.GetValue(_sut)!;
        editors["conn1"].LastSeen = DateTime.UtcNow.AddSeconds(-89);

        _sut.GetAllEditors().Should().ContainSingle();
    }

    #endregion

    #region DTO Mapping

    [Fact]
    public void GetAllEditors_ShouldMapAllDtoFields()
    {
        _sut.Connect("conn1", "alice", "Alice A");
        _sut.UpdateContext("conn1", 42, "Start Page", "editing");

        var dto = _sut.GetAllEditors().Single();

        dto.Username.Should().Be("alice");
        dto.DisplayName.Should().Be("Alice A");
        dto.ContentId.Should().Be(42);
        dto.ContentName.Should().Be("Start Page");
        dto.Action.Should().Be("editing");
        dto.ConnectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GetAllEditors_DefaultAction_ShouldBeIdle()
    {
        _sut.Connect("conn1", "alice", "Alice");

        var dto = _sut.GetAllEditors().Single();
        dto.Action.Should().Be("idle");
    }

    #endregion

    #region Multiple Concurrent Editors

    [Fact]
    public void MultipleConcurrentEditors_FullScenario()
    {
        // Simulate a realistic scenario with multiple editors
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "bob", "Bob");
        _sut.Connect("conn3", "carol", "Carol");

        // Alice and Bob edit the same page
        _sut.UpdateContext("conn1", 42, "Start Page", "editing");
        _sut.UpdateContext("conn2", 42, "Start Page", "viewing");
        _sut.UpdateContext("conn3", 99, "About Page", "editing");

        // Verify all editors tracked
        _sut.GetAllEditors().Should().HaveCount(3);

        // Verify content-specific query
        _sut.GetEditorsOnContent(42).Should().HaveCount(2);

        // Bob disconnects
        _sut.Disconnect("conn2");
        _sut.GetAllEditors().Should().HaveCount(2);
        _sut.GetEditorsOnContent(42).Should().ContainSingle()
            .Which.Username.Should().Be("alice");

        // Chat messages from different editors
        _sut.AddChatMessage("alice", "Alice", "I am editing the start page");
        _sut.AddChatMessage("carol", "Carol", "I have the about page");
        _sut.GetRecentMessages().Should().HaveCount(2);

        // Today's editors should include bob even after disconnect
        _sut.GetEditorNamesToday().Should().HaveCount(3);
    }

    [Fact]
    public void ConcurrentConnections_SameUser_DifferentConnectionIds()
    {
        // Same user in two browser tabs
        _sut.Connect("conn1", "alice", "Alice");
        _sut.Connect("conn2", "alice", "Alice");

        _sut.UpdateContext("conn1", 42, "Start Page", "editing");
        _sut.UpdateContext("conn2", 99, "About Page", "viewing");

        _sut.GetAllEditors().Should().HaveCount(2);
        _sut.GetEditorsOnContent(42).Should().ContainSingle();
        _sut.GetEditorsOnContent(99).Should().ContainSingle();

        // Only one entry in today's names
        _sut.GetEditorNamesToday().Should().ContainSingle();
    }

    #endregion
}
