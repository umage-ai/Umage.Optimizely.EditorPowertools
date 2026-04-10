using EPiServer.DataAbstraction;
using EPiServer.Scheduler;
using UmageAI.Optimizely.EditorPowerTools.Tools.ScheduledJobsGantt;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ScheduledJobsGantt;

public class ScheduledJobsGanttServiceTests
{
    private readonly Mock<IScheduledJobRepository> _jobRepo = new();
    private readonly Mock<IScheduledJobLogRepository> _logRepo = new();
    private readonly Mock<ILogger<ScheduledJobsGanttService>> _logger = new();

    private ScheduledJobsGanttService CreateService()
    {
        return new ScheduledJobsGanttService(
            _jobRepo.Object,
            _logRepo.Object,
            _logger.Object);
    }

    private static ScheduledJob CreateJob(
        Guid id, string name,
        bool isEnabled = true, bool isRunning = false,
        ScheduledIntervalType intervalType = ScheduledIntervalType.Hours,
        int intervalLength = 1,
        DateTime? nextExecution = null,
        DateTime? lastExecution = null)
    {
        var job = new ScheduledJob();
        job.ID = id;
        job.Name = name;
        job.IsEnabled = isEnabled;
        job.IsRunning = isRunning;
        job.IntervalType = intervalType;
        job.IntervalLength = intervalLength;
        job.NextExecution = nextExecution ?? DateTime.MinValue;
        job.LastExecution = lastExecution ?? DateTime.MinValue;
        return job;
    }

    private static ScheduledJobLogItem CreateLogItem(
        DateTime completedUtc, TimeSpan? duration,
        ScheduledJobExecutionStatus status = ScheduledJobExecutionStatus.Succeeded,
        string? message = null)
    {
        var item = new ScheduledJobLogItem
        {
            CompletedUtc = completedUtc,
            Status = status,
            Message = message ?? "Completed"
        };

        // Duration has an init-only setter; use reflection to set it
        typeof(ScheduledJobLogItem)
            .GetProperty(nameof(ScheduledJobLogItem.Duration))!
            .SetValue(item, duration);

        return item;
    }

    private void SetupJobs(params ScheduledJob[] jobs)
    {
        _jobRepo.Setup(r => r.List()).Returns(jobs.AsEnumerable());
    }

    private void SetupLogs(Guid jobId, params ScheduledJobLogItem[] items)
    {
        var pagedResult = new PagedScheduledJobLogResult(items, items.Length);
        _logRepo.Setup(r => r.GetAsync(jobId, It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(pagedResult);
    }

    // ===================================================================
    // GetAllJobs
    // ===================================================================

    [Fact]
    public void GetAllJobs_ReturnsAllScheduledJobs()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var job1 = CreateJob(id1, "Job 1", isEnabled: true);
        var job2 = CreateJob(id2, "Job 2", isEnabled: false);

        SetupJobs(job1, job2);

        var svc = CreateService();
        var result = svc.GetAllJobs().ToList();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Job 1");
        result[0].IsEnabled.Should().BeTrue();
        result[1].Name.Should().Be("Job 2");
        result[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void GetAllJobs_EmptyRepository_ReturnsEmpty()
    {
        SetupJobs();

        var svc = CreateService();
        var result = svc.GetAllJobs().ToList();

        result.Should().BeEmpty();
    }

    // ===================================================================
    // GetAllExecutionHistoryAsync - date range filtering
    // ===================================================================

    [Fact]
    public async Task GetAllExecutionHistoryAsync_ReturnsExecutionsWithinRange()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, "Test Job", isRunning: false);
        SetupJobs(job);

        var log1 = CreateLogItem(
            new DateTime(2025, 1, 15, 12, 0, 0),
            TimeSpan.FromMinutes(5));
        var log2 = CreateLogItem(
            new DateTime(2025, 2, 15, 12, 0, 0),
            TimeSpan.FromMinutes(3));

        SetupLogs(jobId, log1, log2);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().HaveCount(1);
        result[0].JobName.Should().Be("Test Job");
        result[0].EndUtc.Should().Be(new DateTime(2025, 1, 15, 12, 0, 0));
    }

    [Fact]
    public async Task GetAllExecutionHistoryAsync_ExcludesOutOfRangeExecutions()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, "Test Job");
        SetupJobs(job);

        var log1 = CreateLogItem(
            new DateTime(2025, 3, 1, 12, 0, 0),
            TimeSpan.FromMinutes(5));

        SetupLogs(jobId, log1);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().BeEmpty();
    }

    // ===================================================================
    // Duration calculations
    // ===================================================================

    [Fact]
    public async Task GetAllExecutionHistoryAsync_CalculatesDurationCorrectly()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, "Test Job");
        SetupJobs(job);

        var duration = TimeSpan.FromMinutes(7.5);
        var completedAt = new DateTime(2025, 1, 15, 12, 0, 0);
        var log = CreateLogItem(completedAt, duration);

        SetupLogs(jobId, log);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().HaveCount(1);
        result[0].DurationSeconds.Should().Be(450); // 7.5 minutes
        result[0].StartUtc.Should().Be(completedAt - duration);
        result[0].EndUtc.Should().Be(completedAt);
    }

    [Fact]
    public async Task GetAllExecutionHistoryAsync_NullDuration_TreatsAsZero()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, "Test Job");
        SetupJobs(job);

        var completedAt = new DateTime(2025, 1, 15, 12, 0, 0);
        var log = CreateLogItem(completedAt, null);

        SetupLogs(jobId, log);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().HaveCount(1);
        result[0].DurationSeconds.Should().Be(0);
        result[0].StartUtc.Should().Be(completedAt);
    }

    // ===================================================================
    // GetAllExecutionHistoryAsync - succeeded/failed status
    // ===================================================================

    [Fact]
    public async Task GetAllExecutionHistoryAsync_MapsSucceededStatus()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, "Test Job");
        SetupJobs(job);

        var successLog = CreateLogItem(
            new DateTime(2025, 1, 15, 12, 0, 0),
            TimeSpan.FromMinutes(1),
            ScheduledJobExecutionStatus.Succeeded);

        var failLog = CreateLogItem(
            new DateTime(2025, 1, 16, 12, 0, 0),
            TimeSpan.FromMinutes(2),
            ScheduledJobExecutionStatus.Failed,
            "Error occurred");

        SetupLogs(jobId, successLog, failLog);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().HaveCount(2);
        var success = result.First(e => e.EndUtc == new DateTime(2025, 1, 15, 12, 0, 0));
        var fail = result.First(e => e.EndUtc == new DateTime(2025, 1, 16, 12, 0, 0));

        success.Succeeded.Should().BeTrue();
        fail.Succeeded.Should().BeFalse();
        fail.Message.Should().Be("Error occurred");
    }

    // ===================================================================
    // GetAllExecutionHistoryAsync - currently running job
    // ===================================================================

    [Fact]
    public async Task GetAllExecutionHistoryAsync_RunningJob_IncludesRunningEntry()
    {
        var jobId = Guid.NewGuid();
        var lastExec = DateTime.UtcNow.AddMinutes(-5);
        var job = CreateJob(jobId, "Running Job", isRunning: true, lastExecution: lastExec);
        SetupJobs(job);
        SetupLogs(jobId); // no historical logs

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));

        result.Should().HaveCount(1);
        result[0].IsRunning.Should().BeTrue();
        result[0].JobName.Should().Be("Running Job");
        result[0].Message.Should().Be("Currently running...");
    }

    // ===================================================================
    // GetGanttDataAsync
    // ===================================================================

    [Fact]
    public async Task GetGanttDataAsync_ReturnsJobsAndExecutions()
    {
        var jobId = Guid.NewGuid();
        // Disabled job so no planned executions are generated
        var job = CreateJob(jobId, "Test Job", isEnabled: false);
        SetupJobs(job);

        var log = CreateLogItem(
            new DateTime(2025, 1, 15, 12, 0, 0),
            TimeSpan.FromMinutes(5));

        SetupLogs(jobId, log);

        var svc = CreateService();
        var result = await svc.GetGanttDataAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Jobs.Should().HaveCount(1);
        result.Jobs[0].Name.Should().Be("Test Job");
        result.Executions.Should().HaveCount(1);
        result.FromUtc.Should().Be(new DateTime(2025, 1, 1));
        result.ToUtc.Should().Be(new DateTime(2025, 1, 31));
    }

    // ===================================================================
    // GetAllExecutionHistoryAsync - multiple jobs
    // ===================================================================

    [Fact]
    public async Task GetAllExecutionHistoryAsync_MultipleJobs_AggregatesAll()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var job1 = CreateJob(id1, "Job 1");
        var job2 = CreateJob(id2, "Job 2");
        SetupJobs(job1, job2);

        var log1 = CreateLogItem(new DateTime(2025, 1, 10, 12, 0, 0), TimeSpan.FromMinutes(2));
        var log2 = CreateLogItem(new DateTime(2025, 1, 20, 12, 0, 0), TimeSpan.FromMinutes(3));

        SetupLogs(id1, log1);
        SetupLogs(id2, log2);

        var svc = CreateService();
        var result = await svc.GetAllExecutionHistoryAsync(
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        result.Should().HaveCount(2);
        result.Select(e => e.JobName).Should().Contain("Job 1").And.Contain("Job 2");
    }
}
