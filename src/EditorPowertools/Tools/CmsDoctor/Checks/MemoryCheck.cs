using System.Diagnostics;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class MemoryCheck : DoctorCheckBase
{
    public override string Name => "Memory Usage";
    public override string Description => "Reports current memory usage of the application.";
    public override string Group => "Environment";
    public override int SortOrder => 5;
    public override string[] Tags => new[] { "Performance" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / 1024 / 1024;
        var gcTotalMb = GC.GetTotalMemory(false) / 1024 / 1024;

        var details = $"Working set: {workingSetMb} MB, GC heap: {gcTotalMb} MB, " +
                      $"64-bit process: {Environment.Is64BitProcess}, 64-bit OS: {Environment.Is64BitOperatingSystem}";

        if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
            return Perf($"Running as 32-bit on 64-bit OS. Memory: {workingSetMb} MB.", details);

        if (workingSetMb > 4096)
            return Warning($"High memory usage: {workingSetMb} MB.", details);

        return Ok($"Memory: {workingSetMb} MB working set.", details);
    }
}
