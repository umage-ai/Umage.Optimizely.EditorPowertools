# Extending CMS Doctor with Custom Health Checks

The CMS Doctor is a pluggable health check dashboard. You can create custom checks specific to your site and register them via dependency injection. This guide explains how.

## Architecture Overview

The CMS Doctor discovers all registered `IDoctorCheck` implementations via DI. When the dashboard runs checks, it calls `PerformCheck()` on each one and displays the results grouped and color-coded by status.

There are two types of checks:

1. **Standalone checks** (inherit from `DoctorCheckBase`) -- perform their own logic when called, such as checking memory usage or querying a service.
2. **Analyzer checks** (inherit from `AnalyzerDoctorCheckBase`) -- hook into the unified content analysis scheduled job. They receive every content item during traversal and accumulate data, then evaluate it when the dashboard displays.

For most custom checks, standalone checks (`DoctorCheckBase`) are the right choice.

## The IDoctorCheck Interface

Every health check implements `IDoctorCheck`:

```csharp
public interface IDoctorCheck
{
    /// <summary>Display name shown on the dashboard.</summary>
    string Name { get; }

    /// <summary>Brief description of what this check verifies.</summary>
    string Description { get; }

    /// <summary>Group for dashboard organization (e.g. "Content", "Configuration", "Environment").</summary>
    string Group { get; }

    /// <summary>Sort order within the group (lower = higher).</summary>
    int SortOrder { get; }

    /// <summary>Tags for categorization (e.g. "Security", "Performance", "SEO").</summary>
    string[] Tags { get; }

    /// <summary>Run the check and return the result.</summary>
    DoctorCheckResult PerformCheck();

    /// <summary>If true, this check supports auto-fix.</summary>
    bool CanFix { get; }

    /// <summary>Attempt to auto-fix the issue. Only called if CanFix is true.</summary>
    DoctorCheckResult? Fix();
}
```

## HealthStatus Enum

Each check returns one of these status values:

| Status | Meaning | Dashboard Color |
|--------|---------|----------------|
| `OK` | Everything is healthy | Green |
| `Warning` | Something needs attention but is not critical | Yellow |
| `BadPractice` | A known anti-pattern or suboptimal configuration | Orange |
| `Performance` | A performance concern that may affect site speed | Blue |
| `Fault` | A critical issue that needs immediate attention | Red |
| `NotChecked` | The check has not been run yet (e.g. waiting for scheduled job data) | Gray |

## DoctorCheckResult Structure

```csharp
public class DoctorCheckResult
{
    public string CheckName { get; set; }     // Display name
    public string CheckType { get; set; }     // Full type name (for identification)
    public string Group { get; set; }         // Group name
    public HealthStatus Status { get; set; }  // Health status
    public string StatusText { get; set; }    // Human-readable status message
    public string? Details { get; set; }      // Optional detailed information (shown on expand)
    public string[] Tags { get; set; }        // Tags for filtering
    public bool CanFix { get; set; }          // Whether auto-fix is available
    public bool IsDismissed { get; set; }     // Whether the user dismissed this result
    public DateTime CheckTime { get; set; }   // When the check was run
}
```

## Using DoctorCheckBase

The `DoctorCheckBase` abstract class provides convenience methods so you don't need to manually construct `DoctorCheckResult` objects:

```csharp
public abstract class DoctorCheckBase : IDoctorCheck
{
    // Required overrides
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Group { get; }
    public abstract DoctorCheckResult PerformCheck();

    // Optional overrides (with defaults)
    public virtual int SortOrder => 100;
    public virtual string[] Tags => Array.Empty<string>();
    public virtual bool CanFix => false;
    public virtual DoctorCheckResult? Fix() => null;

    // Helper methods for creating results
    protected DoctorCheckResult Ok(string message, string? details = null);
    protected DoctorCheckResult Warning(string message, string? details = null);
    protected DoctorCheckResult BadPractice(string message, string? details = null);
    protected DoctorCheckResult Fault(string message, string? details = null);
    protected DoctorCheckResult Perf(string message, string? details = null);
}
```

The helper methods (`Ok()`, `Warning()`, `BadPractice()`, `Fault()`, `Perf()`) automatically populate `CheckName`, `CheckType`, `Group`, `Tags`, and `CanFix` from the class properties.

## Example: A Simple Environment Check

Here is a real check from the plugin that verifies memory usage. It has no dependencies and runs instantly:

```csharp
using System.Diagnostics;
using EditorPowertools.Tools.CmsDoctor;
using EditorPowertools.Tools.CmsDoctor.Models;

public class MemoryCheck : DoctorCheckBase
{
    public override string Name => "Memory Usage";
    public override string Description => "Reports current memory usage of the application.";
    public override string Group => "Environment";
    public override int SortOrder => 5;
    public override string[] Tags => new[] { "Performance" };

    public override DoctorCheckResult PerformCheck()
    {
        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / 1024 / 1024;

        if (workingSetMb > 4096)
            return Warning($"High memory usage: {workingSetMb} MB.");

        return Ok($"Memory: {workingSetMb} MB working set.");
    }
}
```

## Example: A Check That Queries Content

This check injects Optimizely services to inspect content. It scans for stale drafts:

```csharp
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EditorPowertools.Tools.CmsDoctor;
using EditorPowertools.Tools.CmsDoctor.Models;

public class DraftContentCheck : DoctorCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;

    public DraftContentCheck(
        IContentRepository contentRepository,
        IContentLoader contentLoader)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
    }

    public override string Name => "Stale Drafts";
    public override string Description => "Checks for draft content that has never been published.";
    public override string Group => "Content";
    public override string[] Tags => new[] { "EditorUX", "Maintenance" };

    public override DoctorCheckResult PerformCheck()
    {
        var allContent = _contentRepository.GetDescendents(ContentReference.RootPage)
            .Take(1000).ToList();
        var neverPublished = 0;

        foreach (var contentRef in allContent)
        {
            if (!_contentLoader.TryGet<IContent>(contentRef, out var content)) continue;
            if (content is not IVersionable versionable) continue;

            if (versionable.Status != VersionStatus.Published &&
                versionable.StartPublish == null)
            {
                neverPublished++;
            }
        }

        if (neverPublished == 0)
            return Ok("No stale drafts found.");

        if (neverPublished > 50)
            return Warning($"{neverPublished} never-published items found. Consider cleaning up.");

        return Ok($"{neverPublished} unpublished items found (minor).");
    }
}
```

## Example: A Custom Check (Pages Without Meta Description)

Here is a complete example of a custom check you might write for your own site. It checks whether published pages are missing a meta description property:

```csharp
using EPiServer;
using EPiServer.Core;
using EditorPowertools.Tools.CmsDoctor;
using EditorPowertools.Tools.CmsDoctor.Models;

public class MetaDescriptionCheck : DoctorCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;

    public MetaDescriptionCheck(
        IContentRepository contentRepository,
        IContentLoader contentLoader)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
    }

    public override string Name => "Missing Meta Descriptions";
    public override string Description => "Checks for published pages that are missing a meta description.";
    public override string Group => "Content";
    public override int SortOrder => 50;
    public override string[] Tags => new[] { "SEO" };

    public override DoctorCheckResult PerformCheck()
    {
        var descendants = _contentRepository
            .GetDescendents(ContentReference.StartPage)
            .Take(2000)
            .ToList();

        var missing = new List<string>();

        foreach (var contentRef in descendants)
        {
            if (!_contentLoader.TryGet<PageData>(contentRef, out var page)) continue;

            // Only check published pages
            if (page is not IVersionable v || v.Status != VersionStatus.Published) continue;

            // Check for a "MetaDescription" property (adjust name to match your model)
            var metaDesc = page.Property["MetaDescription"]?.Value as string;
            if (string.IsNullOrWhiteSpace(metaDesc))
            {
                missing.Add($"{page.Name} (ID: {page.ContentLink.ID})");
            }
        }

        if (missing.Count == 0)
            return Ok("All published pages have a meta description.");

        var details = string.Join("\n", missing.Take(20));
        if (missing.Count > 20)
            details += $"\n... and {missing.Count - 20} more";

        if (missing.Count > 50)
            return Warning(
                $"{missing.Count} published pages are missing a meta description.",
                details);

        return BadPractice(
            $"{missing.Count} published pages are missing a meta description.",
            details);
    }
}
```

## Registering Your Custom Check

Register your check in `Startup.cs` (or wherever you configure services) as a transient `IDoctorCheck`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddEditorPowertools();

    // Register your custom health checks
    services.AddTransient<IDoctorCheck, MetaDescriptionCheck>();
    services.AddTransient<IDoctorCheck, MyOtherCustomCheck>();
}
```

That is all that's needed. The CMS Doctor will automatically discover and display your check alongside the built-in checks.

## Implementing Auto-Fix

If your check can automatically remediate the issue it detects, override `CanFix` and `Fix()`:

```csharp
public class DefaultLanguageCheck : DoctorCheckBase
{
    private readonly ISiteDefinitionRepository _siteRepo;

    public DefaultLanguageCheck(ISiteDefinitionRepository siteRepo)
    {
        _siteRepo = siteRepo;
    }

    public override string Name => "Default Language Configuration";
    public override string Description => "Verifies the default language is set correctly.";
    public override string Group => "Configuration";
    public override string[] Tags => new[] { "i18n" };

    // Enable the Fix button on the dashboard
    public override bool CanFix => true;

    public override DoctorCheckResult PerformCheck()
    {
        // ... your check logic ...
        return Warning("Default language is not configured optimally.");
    }

    public override DoctorCheckResult? Fix()
    {
        try
        {
            // Perform the remediation
            // ... fix logic ...

            return Ok("Default language configuration has been corrected.");
        }
        catch (Exception ex)
        {
            return Fault($"Auto-fix failed: could not update configuration.");
        }
    }
}
```

When `CanFix` returns `true`, the dashboard shows a "Fix" button next to the check result. Clicking it calls `Fix()` and displays the returned result.

Guidelines for auto-fix:

- Only implement `Fix()` for issues that can be safely and reversibly corrected.
- Return an `Ok` result on success with a description of what was fixed.
- Return a `Fault` result if the fix fails.
- Never make destructive changes (deleting content, removing users) in `Fix()`.
- Log all fix actions for audit purposes.

## Tags and Grouping

### Groups

The `Group` property determines which section of the dashboard your check appears in. Use one of the established groups for consistency:

| Group | Use for |
|-------|---------|
| `Content` | Checks that inspect content items (drafts, orphaned content, missing fields) |
| `Configuration` | Checks that verify CMS or site configuration |
| `Environment` | Checks about the runtime environment (memory, CPU, disk, .NET version) |
| `Performance` | Checks focused on performance concerns (caching, query performance) |
| `Security` | Checks related to access control, permissions, and security settings |

You can also create your own groups -- any string value works, and the dashboard will create a new section for it.

### Tags

Tags provide a second dimension of categorization. Checks can be filtered by tag in the dashboard UI. Common tags include:

- `Performance` -- affects site speed
- `Security` -- security-related
- `SEO` -- search engine optimization
- `EditorUX` -- affects the editorial experience
- `Maintenance` -- housekeeping and cleanup
- `GDPR` -- data protection and privacy
- `i18n` -- internationalization and language

A single check can have multiple tags.

### Sort Order

The `SortOrder` property (default: 100) controls the display order within a group. Lower numbers appear first. Built-in checks use values from 5 to 100, so use values above 100 to appear after them, or lower values to appear before.

## Advanced: Analyzer-Based Checks

For checks that need to inspect every content item on the site (e.g., finding all pages with a specific problem), consider using `AnalyzerDoctorCheckBase`. This hooks into the unified content analysis scheduled job, so your check piggybacks on the existing content traversal instead of doing its own.

```csharp
using EPiServer.Core;
using EditorPowertools.Tools.CmsDoctor;
using EditorPowertools.Tools.CmsDoctor.Models;

public class LargePageCheck : AnalyzerDoctorCheckBase
{
    private int _largePageCount;
    private readonly List<string> _largePages = new();

    public override string Name => "Large Pages";
    public override string Description => "Finds pages with excessive content that may affect performance.";
    public override string Group => "Performance";
    public override string[] Tags => new[] { "Performance" };

    protected override void OnInitialize()
    {
        _largePageCount = 0;
        _largePages.Clear();
    }

    protected override void OnAnalyze(IContent content, ContentReference contentRef)
    {
        if (content is not PageData page) return;

        // Count properties with large string values
        var totalLength = page.Property
            .Where(p => p.Value is string)
            .Sum(p => ((string)p.Value).Length);

        if (totalLength > 100_000)
        {
            _largePageCount++;
            if (_largePages.Count < 10)
                _largePages.Add($"{page.Name} (ID: {page.ContentLink.ID}, {totalLength:N0} chars)");
        }
    }

    protected override DoctorCheckResult EvaluateResults()
    {
        if (_largePageCount == 0)
            return Ok("No excessively large pages found.");

        var details = string.Join("\n", _largePages);
        return Perf($"{_largePageCount} pages have more than 100K characters of content.", details);
    }
}
```

Register analyzer checks as both `IDoctorCheck` and `IContentAnalyzer`:

```csharp
services.AddTransient<LargePageCheck>();
services.AddTransient<IDoctorCheck>(sp => sp.GetRequiredService<LargePageCheck>());
services.AddTransient<IContentAnalyzer>(sp => sp.GetRequiredService<LargePageCheck>());
```

Analyzer checks show "Not Checked" until the **[EditorPowertools] Content Analysis** scheduled job runs. After the job completes, the accumulated data is available and `EvaluateResults()` is called on the dashboard.

## Built-in Checks Reference

The plugin ships with these checks:

| Check | Group | Description |
|-------|-------|-------------|
| Memory Usage | Environment | Reports application memory consumption |
| Version Info | Environment | Checks .NET and CMS version information |
| Stale Drafts | Content | Finds never-published and old draft content |
| Unused Content Types | Content | Detects content types with zero usage |
| Orphaned Properties | Content | Finds properties on content types that no longer exist in code |
| Broken Links | Content | Reports broken internal and external links (requires scheduled job) |
| Missing Alt Text | Content | Finds images without alt text (requires scheduled job) |
| Scheduled Jobs | Configuration | Checks for failed or long-running scheduled jobs |
| Unused Content | Content | Detects content not referenced from anywhere (requires scheduled job) |
