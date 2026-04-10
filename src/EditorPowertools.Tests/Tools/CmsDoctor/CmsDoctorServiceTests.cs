using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.CmsDoctor;

#region Stub check implementations (each has a unique type name for the service key)

public class StubCheckOk : IDoctorCheck
{
    public string Name => "OK Check";
    public string Description => "A check that always passes";
    public string Group { get; set; } = "Config";
    public int SortOrder => 0;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }
    public Func<DoctorCheckResult>? PerformCheckFunc { get; set; }
    public Func<DoctorCheckResult?>? FixFunc { get; set; }

    public DoctorCheckResult PerformCheck() =>
        PerformCheckFunc?.Invoke() ?? new DoctorCheckResult
        {
            CheckName = Name, CheckType = GetType().FullName!, Group = Group,
            Status = HealthStatus.OK, StatusText = "OK", Tags = Tags, CanFix = CanFix
        };

    public DoctorCheckResult? Fix() => FixFunc?.Invoke();
}

public class StubCheckWarning : IDoctorCheck
{
    public string Name => "Warning Check";
    public string Description => "A check that returns warning";
    public string Group { get; set; } = "Content";
    public int SortOrder => 1;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }
    public Func<DoctorCheckResult>? PerformCheckFunc { get; set; }
    public Func<DoctorCheckResult?>? FixFunc { get; set; }

    public DoctorCheckResult PerformCheck() =>
        PerformCheckFunc?.Invoke() ?? new DoctorCheckResult
        {
            CheckName = Name, CheckType = GetType().FullName!, Group = Group,
            Status = HealthStatus.Warning, StatusText = "Warning", Tags = Tags, CanFix = CanFix
        };

    public DoctorCheckResult? Fix() => FixFunc?.Invoke();
}

public class StubCheckFault : IDoctorCheck
{
    public string Name => "Fault Check";
    public string Description => "A check that returns fault";
    public string Group { get; set; } = "Config";
    public int SortOrder => 2;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }
    public Func<DoctorCheckResult>? PerformCheckFunc { get; set; }
    public Func<DoctorCheckResult?>? FixFunc { get; set; }

    public DoctorCheckResult PerformCheck() =>
        PerformCheckFunc?.Invoke() ?? new DoctorCheckResult
        {
            CheckName = Name, CheckType = GetType().FullName!, Group = Group,
            Status = HealthStatus.Fault, StatusText = "Fault", Tags = Tags, CanFix = CanFix
        };

    public DoctorCheckResult? Fix() => FixFunc?.Invoke();
}

public class StubCheckBadPractice : IDoctorCheck
{
    public string Name => "Bad Practice Check";
    public string Description => "A check that returns bad practice";
    public string Group { get; set; } = "Content";
    public int SortOrder => 3;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }

    public DoctorCheckResult PerformCheck() => new()
    {
        CheckName = Name, CheckType = GetType().FullName!, Group = Group,
        Status = HealthStatus.BadPractice, StatusText = "Bad Practice", Tags = Tags, CanFix = CanFix
    };

    public DoctorCheckResult? Fix() => null;
}

public class StubCheckPerformance : IDoctorCheck
{
    public string Name => "Performance Check";
    public string Description => "A check that returns performance issue";
    public string Group { get; set; } = "Environment";
    public int SortOrder => 4;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool CanFix { get; set; }

    public DoctorCheckResult PerformCheck() => new()
    {
        CheckName = Name, CheckType = GetType().FullName!, Group = Group,
        Status = HealthStatus.Performance, StatusText = "Performance", Tags = Tags, CanFix = CanFix
    };

    public DoctorCheckResult? Fix() => null;
}

#endregion

public class CmsDoctorServiceTests
{
    private readonly Mock<DoctorCheckResultStore> _storeMock;
    private readonly Mock<ILogger<CmsDoctorService>> _loggerMock;

    public CmsDoctorServiceTests()
    {
        _storeMock = new Mock<DoctorCheckResultStore>();
        _loggerMock = new Mock<ILogger<CmsDoctorService>>();

        // Default: empty store
        _storeMock.Setup(s => s.LoadAll())
            .Returns(new Dictionary<string, DoctorCheckResult>(StringComparer.OrdinalIgnoreCase));
    }

    private CmsDoctorService CreateService(params IDoctorCheck[] checks)
    {
        return new CmsDoctorService(checks, _storeMock.Object, _loggerMock.Object);
    }

    // --- GetDashboard tests ---

    [Fact]
    public void GetDashboard_ReturnsAggregatedResultsFromAllChecks()
    {
        var checkOk = new StubCheckOk();
        var checkWarn = new StubCheckWarning();
        var checkFault = new StubCheckFault();
        var sut = CreateService(checkOk, checkWarn, checkFault);

        // Run all first so results are cached
        sut.RunAll();
        var dashboard = sut.GetDashboard();

        dashboard.TotalChecks.Should().Be(3);
        dashboard.OkCount.Should().Be(1);
        dashboard.WarningCount.Should().Be(1);
        dashboard.FaultCount.Should().Be(1);
        dashboard.NotCheckedCount.Should().Be(0);
    }

    [Fact]
    public void GetDashboard_WithNoChecks_ReturnsEmptyDashboard()
    {
        var sut = CreateService();

        var dashboard = sut.GetDashboard();

        dashboard.TotalChecks.Should().Be(0);
        dashboard.OkCount.Should().Be(0);
        dashboard.WarningCount.Should().Be(0);
        dashboard.FaultCount.Should().Be(0);
        dashboard.NotCheckedCount.Should().Be(0);
        dashboard.Groups.Should().BeEmpty();
    }

    [Fact]
    public void GetDashboard_WithNoCachedResults_ReturnsNotCheckedStatus()
    {
        var check = new StubCheckOk();
        var sut = CreateService(check);

        var dashboard = sut.GetDashboard();

        dashboard.NotCheckedCount.Should().Be(1);
        dashboard.Groups.Should().ContainSingle();
        dashboard.Groups[0].Checks[0].Status.Should().Be(HealthStatus.NotChecked);
    }

    [Fact]
    public void GetDashboard_WithCachedResults_MergesStoredAndLiveMetadata()
    {
        var check = new StubCheckOk { Tags = new[] { "Security" }, CanFix = true };
        var checkType = check.GetType().FullName!;

        var storedResults = new Dictionary<string, DoctorCheckResult>(StringComparer.OrdinalIgnoreCase)
        {
            [checkType] = new DoctorCheckResult
            {
                CheckName = "OK Check",
                CheckType = checkType,
                Group = "Config",
                Status = HealthStatus.Warning,
                StatusText = "Needs attention"
            }
        };
        _storeMock.Setup(s => s.LoadAll()).Returns(storedResults);

        var sut = CreateService(check);
        var dashboard = sut.GetDashboard();

        var result = dashboard.Groups.SelectMany(g => g.Checks).Single();
        result.Status.Should().Be(HealthStatus.Warning);
        result.CanFix.Should().BeTrue();
        result.Tags.Should().Contain("Security");
    }

    [Fact]
    public void GetDashboard_GroupsChecksByGroupName()
    {
        var check1 = new StubCheckOk { Group = "Config" };
        var check2 = new StubCheckWarning { Group = "Content" };
        var check3 = new StubCheckFault { Group = "Config" };
        var sut = CreateService(check1, check2, check3);

        sut.RunAll();
        var dashboard = sut.GetDashboard();

        dashboard.Groups.Should().HaveCount(2);
        dashboard.Groups.Select(g => g.Name).Should().BeEquivalentTo("Config", "Content");
    }

    [Fact]
    public void GetDashboard_CountsWarningStatusesCorrectly()
    {
        var check1 = new StubCheckWarning();
        var check2 = new StubCheckBadPractice();
        var check3 = new StubCheckPerformance();
        var check4 = new StubCheckOk();
        var sut = CreateService(check1, check2, check3, check4);

        sut.RunAll();
        var dashboard = sut.GetDashboard();

        // Warning, BadPractice, and Performance all count as "warning"
        dashboard.WarningCount.Should().Be(3);
        dashboard.OkCount.Should().Be(1);
    }

    // --- RunAll tests ---

    [Fact]
    public void RunAll_ExecutesAllRegisteredChecks()
    {
        var performCount = 0;
        var check1 = new StubCheckOk { PerformCheckFunc = () =>
        {
            performCount++;
            return new DoctorCheckResult
            {
                CheckName = "OK Check", CheckType = typeof(StubCheckOk).FullName!,
                Group = "Config", Status = HealthStatus.OK, StatusText = "OK"
            };
        }};
        var check2 = new StubCheckWarning { PerformCheckFunc = () =>
        {
            performCount++;
            return new DoctorCheckResult
            {
                CheckName = "Warning Check", CheckType = typeof(StubCheckWarning).FullName!,
                Group = "Content", Status = HealthStatus.Warning, StatusText = "Warning"
            };
        }};
        var sut = CreateService(check1, check2);

        var results = sut.RunAll();

        results.Should().HaveCount(2);
        performCount.Should().Be(2);
    }

    [Fact]
    public void RunAll_SavesEachResultToStore()
    {
        var check1 = new StubCheckOk();
        var check2 = new StubCheckWarning();
        var sut = CreateService(check1, check2);

        sut.RunAll();

        _storeMock.Verify(s => s.Save(It.IsAny<DoctorCheckResult>()), Times.Exactly(2));
    }

    [Fact]
    public void RunAll_WithNoChecks_ReturnsEmptyList()
    {
        var sut = CreateService();

        var results = sut.RunAll();

        results.Should().BeEmpty();
    }

    // --- RunCheck tests ---

    [Fact]
    public void RunCheck_RunsSpecificCheckByTypeName()
    {
        var okRan = false;
        var warnRan = false;
        var check1 = new StubCheckOk { PerformCheckFunc = () =>
        {
            okRan = true;
            return new DoctorCheckResult
            {
                CheckName = "OK Check", CheckType = typeof(StubCheckOk).FullName!,
                Group = "Config", Status = HealthStatus.OK, StatusText = "OK"
            };
        }};
        var check2 = new StubCheckWarning { PerformCheckFunc = () =>
        {
            warnRan = true;
            return new DoctorCheckResult
            {
                CheckName = "Warning Check", CheckType = typeof(StubCheckWarning).FullName!,
                Group = "Content", Status = HealthStatus.Warning, StatusText = "Warning"
            };
        }};
        var sut = CreateService(check1, check2);

        var result = sut.RunCheck(typeof(StubCheckOk).FullName!);

        result.Should().NotBeNull();
        okRan.Should().BeTrue();
        warnRan.Should().BeFalse();
    }

    [Fact]
    public void RunCheck_WithUnknownType_ReturnsNull()
    {
        var check = new StubCheckOk();
        var sut = CreateService(check);

        var result = sut.RunCheck("NonExistent.CheckType");

        result.Should().BeNull();
    }

    [Fact]
    public void RunCheck_IsCaseInsensitive()
    {
        var check = new StubCheckOk();
        var checkType = check.GetType().FullName!.ToUpperInvariant();
        var sut = CreateService(check);

        var result = sut.RunCheck(checkType);

        result.Should().NotBeNull();
    }

    // --- FixCheck tests ---

    [Fact]
    public void FixCheck_CallsFixOnTheCheck()
    {
        var fixCalled = false;
        var check = new StubCheckOk
        {
            CanFix = true,
            FixFunc = () =>
            {
                fixCalled = true;
                return new DoctorCheckResult
                {
                    CheckName = "OK Check",
                    CheckType = typeof(StubCheckOk).FullName!,
                    Group = "Config",
                    Status = HealthStatus.OK,
                    StatusText = "Fixed"
                };
            }
        };
        var sut = CreateService(check);

        var result = sut.FixCheck(typeof(StubCheckOk).FullName!);

        result.Should().NotBeNull();
        result!.StatusText.Should().Be("Fixed");
        fixCalled.Should().BeTrue();
    }

    [Fact]
    public void FixCheck_WhenCanFixIsFalse_ReturnsNull()
    {
        var check = new StubCheckOk { CanFix = false };
        var sut = CreateService(check);

        var result = sut.FixCheck(typeof(StubCheckOk).FullName!);

        result.Should().BeNull();
    }

    [Fact]
    public void FixCheck_WithUnknownType_ReturnsNull()
    {
        var sut = CreateService();

        var result = sut.FixCheck("NonExistent.Type");

        result.Should().BeNull();
    }

    [Fact]
    public void FixCheck_WhenFixThrowsException_ReturnsFaultResult()
    {
        var check = new StubCheckOk
        {
            CanFix = true,
            FixFunc = () => throw new InvalidOperationException("Fix failed badly")
        };
        var sut = CreateService(check);

        var result = sut.FixCheck(typeof(StubCheckOk).FullName!);

        result.Should().NotBeNull();
        result!.Status.Should().Be(HealthStatus.Fault);
        result.StatusText.Should().Contain("Fix failed");
        result.StatusText.Should().Contain("Fix failed badly");
    }

    // --- DismissCheck / RestoreCheck tests ---

    [Fact]
    public void DismissCheck_CallsStoreSetDismissedWithTrue()
    {
        var sut = CreateService();

        sut.DismissCheck("SomeCheckType");

        _storeMock.Verify(s => s.SetDismissed("SomeCheckType", true), Times.Once);
    }

    [Fact]
    public void RestoreCheck_CallsStoreSetDismissedWithFalse()
    {
        var sut = CreateService();

        sut.RestoreCheck("SomeCheckType");

        _storeMock.Verify(s => s.SetDismissed("SomeCheckType", false), Times.Once);
    }

    [Fact]
    public void DismissCheck_WhenStoreThrows_DoesNotRethrow()
    {
        _storeMock.Setup(s => s.SetDismissed(It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new Exception("DDS error"));
        var sut = CreateService();

        var act = () => sut.DismissCheck("SomeCheckType");

        act.Should().NotThrow();
    }

    [Fact]
    public void RestoreCheck_WhenStoreThrows_DoesNotRethrow()
    {
        _storeMock.Setup(s => s.SetDismissed(It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new Exception("DDS error"));
        var sut = CreateService();

        var act = () => sut.RestoreCheck("SomeCheckType");

        act.Should().NotThrow();
    }

    // --- Exception handling tests ---

    [Fact]
    public void RunAll_WhenCheckThrowsException_ReturnsFaultResultAndContinues()
    {
        var check1 = new StubCheckOk
        {
            PerformCheckFunc = () => throw new Exception("Boom")
        };
        var check2 = new StubCheckWarning();
        var sut = CreateService(check1, check2);

        var results = sut.RunAll();

        results.Should().HaveCount(2);
        results[0].Status.Should().Be(HealthStatus.Fault);
        results[0].StatusText.Should().Contain("Boom");
        results[1].Status.Should().Be(HealthStatus.Warning);
    }

    [Fact]
    public void RunCheck_WhenCheckThrowsException_ReturnsFaultResult()
    {
        var check = new StubCheckOk
        {
            PerformCheckFunc = () => throw new InvalidOperationException("Something broke")
        };
        var sut = CreateService(check);

        var result = sut.RunCheck(typeof(StubCheckOk).FullName!);

        result.Should().NotBeNull();
        result!.Status.Should().Be(HealthStatus.Fault);
        result.StatusText.Should().Contain("Something broke");
    }

    [Fact]
    public void RunAll_WhenStoreLoadAllThrows_StillRunsChecks()
    {
        _storeMock.Setup(s => s.LoadAll()).Throws(new Exception("DDS unavailable"));
        var check = new StubCheckOk();
        var sut = CreateService(check);

        var results = sut.RunAll();

        results.Should().HaveCount(1);
    }

    [Fact]
    public void RunAll_PreservesDismissedState()
    {
        var check = new StubCheckOk();
        var checkType = check.GetType().FullName!;

        var storedResults = new Dictionary<string, DoctorCheckResult>(StringComparer.OrdinalIgnoreCase)
        {
            [checkType] = new DoctorCheckResult
            {
                CheckName = "OK Check",
                CheckType = checkType,
                Group = "Config",
                Status = HealthStatus.Warning,
                StatusText = "Old result",
                IsDismissed = true
            }
        };
        _storeMock.Setup(s => s.LoadAll()).Returns(storedResults);

        var sut = CreateService(check);
        var results = sut.RunAll();

        results.Single().IsDismissed.Should().BeTrue();
    }
}
