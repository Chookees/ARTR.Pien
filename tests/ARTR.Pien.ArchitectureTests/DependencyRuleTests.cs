using System.Reflection;
using NetArchTest.Rules;

namespace ARTR.Pien.ArchitectureTests;

public sealed class DependencyRuleTests
{
    private static readonly Assembly Core = typeof(ARTR.Pien.PienExitCode).Assembly;
    private static readonly Assembly Engine = typeof(ARTR.Pien.Engine.ScanEngine).Assembly;
    private static readonly Assembly Web = typeof(ARTR.Pien.Web.Network.DestinationValidator).Assembly;
    private static readonly Assembly Checks = typeof(ARTR.Pien.Checks.Website.HttpAvailabilityCheck).Assembly;
    private static readonly Assembly Reporting = typeof(ARTR.Pien.Reporting.Exporters.JsonReportExporter).Assembly;
    private static readonly Assembly Storage = typeof(ARTR.Pien.Storage.FileScanStore).Assembly;
    private static readonly Assembly Hosting = typeof(ARTR.Pien.Hosting.PienServiceCollectionExtensions).Assembly;

    [Fact]
    public void Core_has_no_project_dependencies_on_other_pien_assemblies()
    {
        var result = Types.InAssembly(Core)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ARTR.Pien.Engine",
                "ARTR.Pien.Web",
                "ARTR.Pien.Checks",
                "ARTR.Pien.Reporting",
                "ARTR.Pien.Storage",
                "ARTR.Pien.Hosting",
                "ARTR.Pien.Cli")
            .GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Engine_depends_only_on_core()
    {
        var result = Types.InAssembly(Engine)
            .ShouldNot()
            .HaveDependencyOnAny("ARTR.Pien.Web", "ARTR.Pien.Checks", "ARTR.Pien.Cli", "ARTR.Pien.Hosting")
            .GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Forbidden_packages_are_absent_from_directory_packages()
    {
        var root = FindRepoRoot();
        var packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        Assert.DoesNotContain("Testcontainers", packages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackExchange.Redis", packages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JsonSchema.Net", packages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sarif.Sdk", packages, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(root, "Dockerfile")));
        Assert.False(Directory.Exists(Path.Combine(root, "docker")));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ARTR.Pien.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
