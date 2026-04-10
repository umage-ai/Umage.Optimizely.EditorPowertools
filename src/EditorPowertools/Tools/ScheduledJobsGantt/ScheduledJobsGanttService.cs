using EPiServer.DataAbstraction;
using EPiServer.Scheduler;
using UmageAI.Optimizely.EditorPowerTools.Tools.ScheduledJobsGantt.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ScheduledJobsGantt;

public class ScheduledJobsGanttService
{
    private const int MaxLogItems = 500;

    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobLogRepository _logRepository;
    private readonly ILogger<ScheduledJobsGanttService> _logger;

    public ScheduledJobsGanttService(
        IScheduledJobRepository jobRepository,
        IScheduledJobLogRepository logRepository,
        ILogger<ScheduledJobsGanttService> logger)
    {
        _jobRepository = jobRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all scheduled jobs as DTOs.
    /// </summary>
    public IEnumerable<ScheduledJobDto> GetAllJobs()
    {
        return _jobRepository.List().Select(job => new ScheduledJobDto
        {
            Id = job.ID,
            Name = job.Name,
            IsEnabled = job.IsEnabled,
            IsRunning = job.IsRunning,
            NextExecution = job.NextExecution,
            IntervalType = job.IntervalType.ToString(),
            IntervalLength = job.IntervalLength
        });
    }

    /// <summary>
    /// Gets execution history for all jobs within a date range.
    /// </summary>
    public async Task<List<ExecutionDto>> GetAllExecutionHistoryAsync(DateTime fromUtc, DateTime toUtc)
    {
        var jobs = _jobRepository.List().ToList();
        var executions = new List<ExecutionDto>();

        foreach (var job in jobs)
        {
            try
            {
                var logs = await GetJobLogsAsync(job.ID);
                foreach (var log in logs)
                {
                    var endUtc = log.CompletedUtc;
                    var duration = log.Duration ?? TimeSpan.Zero;
                    var startUtc = endUtc - duration;

                    // Filter to requested range
                    if (endUtc < fromUtc || startUtc > toUtc)
                        continue;

                    executions.Add(new ExecutionDto
                    {
                        JobId = job.ID,
                        JobName = job.Name,
                        StartUtc = startUtc,
                        EndUtc = endUtc,
                        DurationSeconds = duration.TotalSeconds,
                        Succeeded = log.Status == ScheduledJobExecutionStatus.Succeeded,
                        IsRunning = false,
                        IsPlanned = false,
                        Message = log.Message
                    });
                }

                // Add currently running execution if applicable
                if (job.IsRunning)
                {
                    var runningStart = job.LastExecution;
                    if (runningStart >= fromUtc && runningStart <= toUtc)
                    {
                        executions.Add(new ExecutionDto
                        {
                            JobId = job.ID,
                            JobName = job.Name,
                            StartUtc = runningStart,
                            EndUtc = null,
                            DurationSeconds = (DateTime.UtcNow - runningStart).TotalSeconds,
                            Succeeded = true,
                            IsRunning = true,
                            IsPlanned = false,
                            Message = "Currently running..."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading execution history for job {JobName} ({JobId})", job.Name, job.ID);
            }
        }

        return executions;
    }

    /// <summary>
    /// Gets combined Gantt data: jobs, historical executions, and planned future executions.
    /// </summary>
    public async Task<GanttDataResponse> GetGanttDataAsync(DateTime fromUtc, DateTime toUtc)
    {
        var jobs = GetAllJobs().ToList();
        var executions = await GetAllExecutionHistoryAsync(fromUtc, toUtc);

        // Add planned future executions
        var allJobs = _jobRepository.List().ToList();
        foreach (var job in allJobs)
        {
            if (!job.IsEnabled)
                continue;

            var nextExec = job.NextExecution;
            if (nextExec == DateTime.MinValue || nextExec < DateTime.UtcNow)
                continue;

            var estimatedDuration = await GetEstimatedDurationAsync(job.ID);
            var planned = GeneratePlannedExecutions(job, estimatedDuration, fromUtc, toUtc);
            executions.AddRange(planned);
        }

        return new GanttDataResponse
        {
            Jobs = jobs,
            Executions = executions,
            FromUtc = fromUtc,
            ToUtc = toUtc
        };
    }

    /// <summary>
    /// Calculates average execution duration from the last 10 completed runs.
    /// </summary>
    private async Task<TimeSpan> GetEstimatedDurationAsync(Guid jobId)
    {
        try
        {
            var logs = await GetJobLogsAsync(jobId, 10);
            var durations = logs
                .Where(l => l.Duration.HasValue && l.Duration.Value.TotalSeconds > 0)
                .Select(l => l.Duration!.Value.TotalSeconds)
                .ToList();

            if (durations.Count == 0)
                return TimeSpan.FromMinutes(1); // Default estimate

            return TimeSpan.FromSeconds(durations.Average());
        }
        catch
        {
            return TimeSpan.FromMinutes(1);
        }
    }

    /// <summary>
    /// Generates planned future execution entries based on job schedule.
    /// </summary>
    private static List<ExecutionDto> GeneratePlannedExecutions(
        ScheduledJob job, TimeSpan estimatedDuration,
        DateTime fromUtc, DateTime toUtc)
    {
        var planned = new List<ExecutionDto>();
        var next = job.NextExecution;

        if (next == DateTime.MinValue || next < DateTime.UtcNow)
            return planned;

        // Generate up to 50 planned executions within the range
        var count = 0;
        while (next <= toUtc && count < 50)
        {
            if (next >= fromUtc)
            {
                planned.Add(new ExecutionDto
                {
                    JobId = job.ID,
                    JobName = job.Name,
                    StartUtc = next,
                    EndUtc = next + estimatedDuration,
                    DurationSeconds = estimatedDuration.TotalSeconds,
                    Succeeded = true,
                    IsRunning = false,
                    IsPlanned = true,
                    Message = "Planned execution (estimated)"
                });
            }

            next = GetNextOccurrence(next, job.IntervalType, job.IntervalLength);
            if (next == DateTime.MaxValue)
                break;

            count++;
        }

        return planned;
    }

    /// <summary>
    /// Calculates the next occurrence based on interval type and length.
    /// </summary>
    private static DateTime GetNextOccurrence(DateTime current, ScheduledIntervalType intervalType, int intervalLength)
    {
        if (intervalLength <= 0)
            return DateTime.MaxValue;

        return intervalType switch
        {
            ScheduledIntervalType.Minutes => current.AddMinutes(intervalLength),
            ScheduledIntervalType.Hours => current.AddHours(intervalLength),
            ScheduledIntervalType.Days => current.AddDays(intervalLength),
            ScheduledIntervalType.Weeks => current.AddDays(intervalLength * 7),
            ScheduledIntervalType.Months => current.AddMonths(intervalLength),
            _ => DateTime.MaxValue
        };
    }

    /// <summary>
    /// Gets log items for a scheduled job using the paged API.
    /// </summary>
    private async Task<IEnumerable<ScheduledJobLogItem>> GetJobLogsAsync(Guid jobId, int maxItems = MaxLogItems)
    {
        var result = await _logRepository.GetAsync(jobId, 0, maxItems);
        return result.PagedResult;
    }
}
