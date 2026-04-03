using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class OrphanedPropertyCheck : DoctorCheckBase
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/orphanedpropertycheck/";

    public OrphanedPropertyCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => L(Prefix + "name", "Orphaned Properties");
    public override string Description => L(Prefix + "description", "Finds properties defined in the database but not in code.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
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
            return Ok(L(Prefix + "ok", "No orphaned properties found."));

        var details = string.Format(L(Prefix + "examples", "Examples: {0}"), string.Join(", ", examples));
        if (orphanedCount > 5)
            details += " " + string.Format(L(Prefix + "andmore", "(and {0} more)"), orphanedCount - 5);

        return BadPractice(
            string.Format(L(Prefix + "found", "{0} orphaned properties found in the database that are not defined in code."), orphanedCount),
            details);
    }
}
