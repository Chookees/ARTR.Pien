using System.Reflection;

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
    public void Core_references_no_other_pien_assemblies()
    {
        Assert.Empty(GetPienAssemblyReferences(Core));
    }

    [Fact]
    public void Engine_references_only_core_among_pien_assemblies()
    {
        Assert.Equal(["ARTR.Pien.Core"], GetPienAssemblyReferences(Engine));
    }

    [Fact]
    public void Web_references_only_core_among_pien_assemblies()
    {
        Assert.Equal(["ARTR.Pien.Core"], GetPienAssemblyReferences(Web));
    }

    [Fact]
    public void Checks_project_declares_web_reference()
    {
        var refs = GetPienAssemblyReferences(Checks);
        Assert.Contains("ARTR.Pien.Core", refs);
        // Website checks currently use only Core contracts; OpenAPI/HTML helpers may pull Web types later.
        // Enforce the intended project reference via csproj text.
        var root = FindRepoRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "src", "ARTR.Pien.Checks", "ARTR.Pien.Checks.csproj"));
        Assert.Contains("ARTR.Pien.Web.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ARTR.Pien.Cli", refs);
    }

    [Fact]
    public void Reporting_and_storage_reference_only_core()
    {
        Assert.Equal(["ARTR.Pien.Core"], GetPienAssemblyReferences(Reporting));
        Assert.Equal(["ARTR.Pien.Core"], GetPienAssemblyReferences(Storage));
    }

    [Fact]
    public void Hosting_references_engine_web_checks_reporting_storage_core()
    {
        var refs = GetPienAssemblyReferences(Hosting);
        Assert.Contains("ARTR.Pien.Core", refs);
        Assert.Contains("ARTR.Pien.Engine", refs);
        Assert.Contains("ARTR.Pien.Web", refs);
        Assert.Contains("ARTR.Pien.Checks", refs);
        Assert.Contains("ARTR.Pien.Reporting", refs);
        Assert.Contains("ARTR.Pien.Storage", refs);
        Assert.DoesNotContain("ARTR.Pien.Cli", refs);
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

    private static string[] GetPienAssemblyReferences(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("ARTR.Pien.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

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
