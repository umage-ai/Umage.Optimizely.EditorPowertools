using System.Diagnostics;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class MemoryCheck : DoctorCheckBase
{
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/memorycheck/";

    public override string Name => L(Prefix + "name", "Memory Usage");
    public override string Description => L(Prefix + "description", "Reports current memory usage of the application.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/environment", "Environment");
    public override int SortOrder => 5;
    public override string[] Tags => new[] { "Performance" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / 1024 / 1024;
        var gcTotalMb = GC.GetTotalMemory(false) / 1024 / 1024;

        var details = string.Format(L(Prefix + "details", "Working set: {0} MB, GC heap: {1} MB, 64-bit process: {2}, 64-bit OS: {3}"),
            workingSetMb, gcTotalMb, Environment.Is64BitProcess, Environment.Is64BitOperatingSystem);

        if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
            return Perf(string.Format(L(Prefix + "biton64", "Running as 32-bit on 64-bit OS. Memory: {0} MB."), workingSetMb), details);

        if (workingSetMb > 4096)
            return Warning(string.Format(L(Prefix + "highmemory", "High memory usage: {0} MB."), workingSetMb), details);

        return Ok(string.Format(L(Prefix + "ok", "Memory: {0} MB working set."), workingSetMb), details);
    }
}
