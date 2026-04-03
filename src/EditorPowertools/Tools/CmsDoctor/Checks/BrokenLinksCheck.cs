using EditorPowertools.Tools.LinkChecker;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reads from the Link Checker's DDS data.
/// Demonstrates how checks can leverage data collected by other tools.
/// </summary>
public class BrokenLinksCheck : DoctorCheckBase
{
    private readonly LinkCheckerRepository _linkRepo;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/brokenlinkscheck/";

    public BrokenLinksCheck(LinkCheckerRepository linkRepo)
    {
        _linkRepo = linkRepo;
    }

    public override string Name => L(Prefix + "name", "Broken Links");
    public override string Description => L(Prefix + "description", "Reports broken links found by the Link Checker tool.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 60;
    public override string[] Tags => new[] { "SEO", "EditorUX" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var results = _linkRepo.GetAll();
            if (!results.Any())
                return Ok(
                    L(Prefix + "nodata", "No link data available. Run the Content Analysis scheduled job first."),
                    L(Prefix + "nodatadetails", "The Link Checker analyzer populates this data during the scheduled job."));

            var broken = results.Where(r => r.StatusCode >= 400 || r.StatusCode == 0).ToList();
            var total = results.Count();

            if (broken.Count == 0)
                return Ok(string.Format(L(Prefix + "allhealthy", "All {0} links are healthy."), total));

            var by404 = broken.Count(r => r.StatusCode == 404);
            var by500 = broken.Count(r => r.StatusCode >= 500);
            var byTimeout = broken.Count(r => r.StatusCode == 0);
            var details = string.Format(
                L(Prefix + "details", "Total links: {0}, Broken: {1}. 404s: {2}, 5xx: {3}, Timeouts: {4}"),
                total, broken.Count, by404, by500, byTimeout);

            if (broken.Count > 20)
                return Fault(
                    string.Format(L(Prefix + "fault", "{0} broken links found across {1} total links."), broken.Count, total),
                    details);
            if (broken.Count > 0)
                return Warning(
                    string.Format(L(Prefix + "warning", "{0} broken links found."), broken.Count),
                    details);

            return Ok(string.Format(L(Prefix + "allhealthy", "All {0} links healthy."), total));
        }
        catch
        {
            return Ok(L(Prefix + "notavailable", "Link checker data not available yet."));
        }
    }
}
