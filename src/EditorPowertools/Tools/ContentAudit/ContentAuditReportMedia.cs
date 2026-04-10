using EPiServer.Core;
using EPiServer.DataAnnotations;
using EPiServer.Framework.DataAnnotations;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// CMS media content type used to store Content Audit export files.
/// Registered automatically by Optimizely's content type scanner.
/// Accepts XLSX, CSV, and JSON file extensions.
/// </summary>
[ContentType(
    DisplayName = "Content Audit Report",
    GUID        = "a3f1e2d4-5b6c-4e7a-8f90-1b2c3d4e5f60",
    Description = "Generated content audit export files. Do not create manually.")]
[MediaDescriptor(ExtensionString = "xlsx,csv,json")]
public class ContentAuditReportMedia : MediaData
{
}
