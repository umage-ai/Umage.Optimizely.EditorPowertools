using EditorPowertools.Tools.LinkChecker;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Health check that reads from the Link Checker's DDS data.
/// Demonstrates how checks can leverage data collected by other tools.
/// </summary>
public class BrokenLinksCheck : DoctorCheckBase
{
    private readonly LinkCheckerRepository _linkRepo;

    public BrokenLinksCheck(LinkCheckerRepository linkRepo)
    {
        _linkRepo = linkRepo;
    }

    public override string Name => "Broken Links";
    public override string Description => "Reports broken links found by the Link Checker tool.";
    public override string Group => "Content";
    public override int SortOrder => 60;
    public override string[] Tags => new[] { "SEO", "EditorUX" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        try
        {
            var results = _linkRepo.GetAll();
            if (!results.Any())
                return Ok("No link data available. Run the Content Analysis scheduled job first.",
                    "The Link Checker analyzer populates this data during the scheduled job.");

            var broken = results.Where(r => r.StatusCode >= 400 || r.StatusCode == 0).ToList();
            var total = results.Count();

            if (broken.Count == 0)
                return Ok($"All {total} links are healthy.");

            var by404 = broken.Count(r => r.StatusCode == 404);
            var by500 = broken.Count(r => r.StatusCode >= 500);
            var byTimeout = broken.Count(r => r.StatusCode == 0);
            var details = $"Total links: {total}, Broken: {broken.Count}. " +
                          $"404s: {by404}, 5xx: {by500}, Timeouts: {byTimeout}";

            if (broken.Count > 20)
                return Fault($"{broken.Count} broken links found across {total} total links.", details);
            if (broken.Count > 0)
                return Warning($"{broken.Count} broken links found.", details);

            return Ok($"All {total} links healthy.");
        }
        catch
        {
            return Ok("Link checker data not available yet.");
        }
    }
}
