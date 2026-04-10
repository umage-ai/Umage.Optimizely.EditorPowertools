namespace UmageAI.Optimizely.EditorPowerTools.Configuration;

public class ContentAuditOptions
{
    /// <summary>
    /// Name of the CMS folder (under Global Assets) where export files are stored.
    /// The folder is created automatically on first export if it does not exist.
    /// </summary>
    public string ReportFolderName { get; set; } = "Internal Reports";
}
