namespace UmageAI.Optimizely.EditorPowerTools.Tools.ScheduledJobsGantt.Models;

public class ScheduledJobDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsRunning { get; set; }
    public DateTime? NextExecution { get; set; }
    public string? IntervalType { get; set; }
    public int IntervalLength { get; set; }
}

public class ExecutionDto
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public double DurationSeconds { get; set; }
    public bool Succeeded { get; set; }
    public bool IsRunning { get; set; }
    public bool IsPlanned { get; set; }
    public string? Message { get; set; }
}

public class GanttDataResponse
{
    public List<ScheduledJobDto> Jobs { get; set; } = new();
    public List<ExecutionDto> Executions { get; set; } = new();
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}
