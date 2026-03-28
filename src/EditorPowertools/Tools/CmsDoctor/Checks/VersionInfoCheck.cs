using System.Reflection;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class VersionInfoCheck : DoctorCheckBase
{
    public override string Name => "CMS Version";
    public override string Description => "Reports the current Optimizely CMS version.";
    public override string Group => "Environment";
    public override int SortOrder => 1;
    public override string[] Tags => new[] { "Security" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var epiAssembly = Assembly.Load("EPiServer");
            var version = epiAssembly.GetName().Version;
            var versionStr = version?.ToString() ?? "Unknown";

            return Ok($"Optimizely CMS {versionStr}", $"EPiServer assembly version: {versionStr}. .NET {Environment.Version}");
        }
        catch
        {
            return Warning("Could not determine CMS version.");
        }
    }
}
