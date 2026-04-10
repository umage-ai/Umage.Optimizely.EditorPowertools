using System.Reflection;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

public class VersionInfoCheck : DoctorCheckBase
{
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/versioninfocheck/";

    public override string Name => L(Prefix + "name", "CMS Version");
    public override string Description => L(Prefix + "description", "Reports the current Optimizely CMS version.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/environment", "Environment");
    public override int SortOrder => 1;
    public override string[] Tags => new[] { "Security" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var epiAssembly = Assembly.Load("EPiServer");
            var version = epiAssembly.GetName().Version;
            var versionStr = version?.ToString() ?? L(Prefix + "unknown", "Unknown");

            return Ok(
                string.Format(L(Prefix + "ok", "Optimizely CMS {0}"), versionStr),
                string.Format(L(Prefix + "details", "EPiServer assembly version: {0}. .NET {1}"), versionStr, Environment.Version));
        }
        catch
        {
            return Warning(L(Prefix + "couldnotdetermine", "Could not determine CMS version."));
        }
    }
}
