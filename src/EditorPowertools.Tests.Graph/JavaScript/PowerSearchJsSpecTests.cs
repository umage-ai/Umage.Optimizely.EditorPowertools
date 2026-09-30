using System.Diagnostics;
using FluentAssertions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.JavaScript;

/// <summary>
/// Runs the Node specs for the PowerSearch client script so the facet/badge render rules and the
/// "typing survives a re-render" behaviour are covered by <c>dotnet test</c>. The repo has no JS
/// toolchain; the specs use only Node's built-in runner. Without Node on PATH the test is a no-op
/// locally, but fails on CI (where Node is expected) so the coverage cannot silently disappear.
/// </summary>
public class PowerSearchJsSpecTests
{
    [Fact]
    public void PowerSearch_client_specs_pass()
    {
        var spec = FindUp("src/EditorPowertools.Tests.Graph/JavaScript/powersearch.spec.js");

        var psi = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--test");
        psi.ArgumentList.Add(spec);

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Node.js is required on CI to run the PowerSearch client specs.");
            }

            return;
        }

        using (process!)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit(60_000).Should().BeTrue("the Node specs should finish quickly");
            process.ExitCode.Should().Be(0, stdout.Result + stderr.Result);
        }
    }

    private static string FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not locate " + relative + " above " + AppContext.BaseDirectory);
    }
}
