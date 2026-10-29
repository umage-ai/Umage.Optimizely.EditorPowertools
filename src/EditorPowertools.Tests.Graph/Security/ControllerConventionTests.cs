using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Security;

/// <summary>Pins the repository's controller security conventions for every controller in the Graph add-on.</summary>
public class ControllerConventionTests
{
    private static readonly Assembly GraphAssembly = typeof(GraphFeatureAccessChecker).Assembly;

    private static IReadOnlyList<Type> Controllers() =>
        GraphAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Controller).IsAssignableFrom(t))
            .ToList();

    [Fact]
    public void There_are_controllers_to_check()
    {
        Controllers().Should().NotBeEmpty();
    }

    [Fact]
    public void Every_controller_requires_the_editor_powertools_policy()
    {
        foreach (var type in Controllers())
        {
            var attr = type.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
            attr.Should().NotBeNull($"{type.Name} must be authorized");
            attr!.Policy.Should().Be("codeart:editorpowertools", $"{type.Name} must use the powertools policy");
        }
    }

    [Fact]
    public void Every_api_controller_requires_ajax()
    {
        foreach (var type in Controllers().Where(t => t.Name.EndsWith("ApiController", StringComparison.Ordinal)))
        {
            type.GetCustomAttribute<RequireAjaxAttribute>(inherit: true).Should().NotBeNull($"{type.Name} must be [RequireAjax]");
        }
    }

    [Fact]
    public void Every_controller_takes_the_access_checker()
    {
        foreach (var type in Controllers())
        {
            type.GetConstructors().Should().Contain(
                c => c.GetParameters().Any(p => p.ParameterType == typeof(GraphFeatureAccessChecker)),
                $"{type.Name} must receive GraphFeatureAccessChecker");
        }
    }

    [Fact]
    public void Every_public_action_calls_HasAccess_and_no_controller_leaks_exception_messages()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "EditorPowertools.Graph"));
        Directory.Exists(root).Should().BeTrue($"source root {root} must exist");

        var files = Directory.GetFiles(root, "*Controller.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        files.Should().NotBeEmpty();

        var action = new Regex(@"public\s+(?:async\s+)?(?:Task<IActionResult>|IActionResult)\s+(\w+)\s*\(", RegexOptions.Compiled);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            source.Should().NotContain("ex.Message", $"{Path.GetFileName(file)} must not expose exception messages");

            var matches = action.Matches(source);
            matches.Should().NotBeEmpty($"{Path.GetFileName(file)} should declare actions");
            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : source.Length;
                source[start..end].Should().Contain("HasAccess(",
                    $"{matches[i].Groups[1].Value} in {Path.GetFileName(file)} must check access");
            }
        }
    }
}
