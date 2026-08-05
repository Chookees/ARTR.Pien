using System.Diagnostics;

using ARTR.Pien;

namespace ARTR.Pien.FunctionalTests;

public sealed class CliExitCodeRegressionTests
{
    [Fact]
    public async Task Explain_invalid_check_id_pattern_returns_InvalidArguments()
    {
        var result = await RunCliAsync(["explain", "NO-SUCH-CHECK"]);
        Assert.Equal((int)PienExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task Validate_malformed_json_returns_InvalidConfiguration()
    {
        var path = Path.Combine(Path.GetTempPath(), "pien-cli-bad-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{ truncated", TestContext.Current.CancellationToken);
        try
        {
            var result = await RunCliAsync(["validate", "--config", path]);
            Assert.Equal((int)PienExitCode.InvalidConfiguration, result.ExitCode);
            Assert.Contains("invalid", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Validate_missing_explicit_config_returns_InvalidConfiguration()
    {
        var missing = Path.Combine(Path.GetTempPath(), "pien-cli-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var result = await RunCliAsync(["validate", "--config", missing]);
        Assert.Equal((int)PienExitCode.InvalidConfiguration, result.ExitCode);
        Assert.Contains("not found", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scan_disallowed_private_target_returns_TargetRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), "pien-cli-ssrf-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 1,
              "profile": "quick",
              "targets": [
                {
                  "id": "ssrf",
                  "kind": "website",
                  "url": "http://127.0.0.1:65530/",
                  "authorization": { "confirmed": true, "notes": "test" }
                }
              ],
              "network": {
                "allowPrivateNetworks": false,
                "allowedHosts": []
              },
              "output": { "formats": ["console"], "directory": "./artifacts/pien" }
            }
            """,
            TestContext.Current.CancellationToken);
        try
        {
            var result = await RunCliAsync(["scan", "--config", path, "--quiet"]);
            Assert.Equal((int)PienExitCode.TargetRejected, result.ExitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Scan_unconfirmed_authorization_returns_TargetRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), "pien-cli-unauth-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 1,
              "profile": "quick",
              "targets": [
                {
                  "id": "unauth",
                  "kind": "website",
                  "url": "http://127.0.0.1:65530/",
                  "authorization": { "confirmed": false, "notes": "test" }
                }
              ],
              "network": {
                "allowPrivateNetworks": true,
                "allowedHosts": ["127.0.0.1"]
              },
              "output": { "formats": ["console"], "directory": "./artifacts/pien" }
            }
            """,
            TestContext.Current.CancellationToken);
        try
        {
            var result = await RunCliAsync(["scan", "--config", path, "--quiet"]);
            Assert.Equal((int)PienExitCode.TargetRejected, result.ExitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<(int ExitCode, string Stderr)> RunCliAsync(string[] args)
    {
        var dll = Path.Combine(FindRepoRoot(), "src", "ARTR.Pien.Cli", "bin", "Release", "net10.0", "pien.dll");
        Assert.True(File.Exists(dll), $"CLI assembly not found at {dll}. Build Release first.");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(dll);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pien CLI.");
        var stderrTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        _ = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, stderr);
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
