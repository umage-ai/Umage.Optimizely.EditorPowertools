using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class OrphanedPropertyCheck : DoctorCheckBase
{
    private readonly IContentTypeRepository _contentTypeRepository;

    public OrphanedPropertyCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => "Orphaned Properties";
    public override string Description => "Finds properties defined in the database but not in code.";
    public override string Group => "Content";
    public override int SortOrder => 20;
    public override string[] Tags => new[] { "Maintenance" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var orphanedCount = 0;
        var examples = new List<string>();

        foreach (var ct in _contentTypeRepository.List().Where(ct => ct.ModelType != null))
        {
            foreach (var propDef in ct.PropertyDefinitions)
            {
                if (propDef.ExistsOnModel) continue;

                orphanedCount++;
                if (examples.Count < 5)
                    examples.Add($"{ct.DisplayName ?? ct.Name}.{propDef.Name}");
            }
        }

        if (orphanedCount == 0)
            return Ok("No orphaned properties found.");

        var details = "Examples: " + string.Join(", ", examples);
        if (orphanedCount > 5) details += $" (and {orphanedCount - 5} more)";

        return BadPractice($"{orphanedCount} orphaned properties found in the database that are not defined in code.", details);
    }
}
