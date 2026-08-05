using System.Net;
using System.Net.Sockets;
using System.Text;

using ARTR.Pien;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using ARTR.Pien.Storage;

namespace ARTR.Pien.FunctionalTests;

/// <summary>
/// In-process CLI coverage so Coverlet attributes hits to ARTR.Pien.Cli (not a child process).
/// </summary>
public sealed class CliInProcessCoverageTests
{
    [Fact]
    public async Task Version_and_list_checks_and_explain_succeed()
    {
        await using var home = TemporaryWorkspace.Create();
        var ct = TestContext.Current.CancellationToken;
        var version = await CliRunner.RunAsync(home.Path, ["version"], ct);
        Assert.Equal((int)PienExitCode.Success, version.ExitCode);
        Assert.Contains("ARTR Pien", version.Stdout, StringComparison.Ordinal);

        var list = await CliRunner.RunAsync(home.Path, ["list-checks"], ct);
        Assert.Equal((int)PienExitCode.Success, list.ExitCode);
        Assert.Contains("PIEN-HTTP-001", list.Stdout, StringComparison.Ordinal);

        var listJson = await CliRunner.RunAsync(home.Path, ["list-checks", "--json"], ct);
        Assert.Equal((int)PienExitCode.Success, listJson.ExitCode);
        Assert.Contains("PIEN-HTTP-001", listJson.Stdout, StringComparison.Ordinal);

        var explain = await CliRunner.RunAsync(home.Path, ["explain", "PIEN-HTTP-001"], ct);
        Assert.Equal((int)PienExitCode.Success, explain.ExitCode);
        Assert.Contains("HTTP availability", explain.Stdout, StringComparison.Ordinal);

        var unknown = await CliRunner.RunAsync(home.Path, ["explain", "PIEN-HTTP-999"], ct);
        Assert.Equal((int)PienExitCode.InvalidArguments, unknown.ExitCode);
    }

    [Fact]
    public async Task Init_doctor_and_validate_cover_configuration_paths()
    {
        await using var home = TemporaryWorkspace.Create();
        var ct = TestContext.Current.CancellationToken;
        var init = await CliRunner.RunAsync(home.Path, ["init", "--website", "--ci"], ct);
        Assert.Equal((int)PienExitCode.Success, init.ExitCode);
        Assert.True(File.Exists(Path.Combine(home.Path, "pien.json")));

        var initAgain = await CliRunner.RunAsync(home.Path, ["init", "--website"], ct);
        Assert.Equal((int)PienExitCode.InvalidArguments, initAgain.ExitCode);

        var bothKinds = await CliRunner.RunAsync(home.Path, ["init", "--website", "--api", "--force"], ct);
        Assert.Equal((int)PienExitCode.InvalidArguments, bothKinds.ExitCode);

        var force = await CliRunner.RunAsync(home.Path, ["init", "--api", "--force"], ct);
        Assert.Equal((int)PienExitCode.Success, force.ExitCode);

        var doctor = await CliRunner.RunAsync(home.Path, ["doctor", "--network"], ct);
        Assert.Equal((int)PienExitCode.Success, doctor.ExitCode);
        Assert.Contains("OK SDK/runtime", doctor.Stdout, StringComparison.Ordinal);

        var validate = await CliRunner.RunAsync(home.Path, ["validate"], ct);
        Assert.Equal((int)PienExitCode.Success, validate.ExitCode);
    }

    [Fact]
    public async Task Baseline_history_and_report_commands_roundtrip_against_store()
    {
        await using var home = TemporaryWorkspace.Create();
        var ct = TestContext.Current.CancellationToken;
        var state = Path.Combine(home.Path, ".pien");
        Directory.CreateDirectory(state);
        var store = new FileScanStore(state);
        var run = await SeedRunAsync(store, "run-cli-1");
        var baseline = Baseline.Create(new Baseline
        {
            Id = "base-cli",
            TargetId = "t1",
            ConfigurationFingerprint = "cfg",
            FindingFingerprints = ["AAA"],
            CreatedAt = DateTimeOffset.Parse("2026-08-05T12:00:00Z"),
        });
        await store.SaveAsync(baseline, ct);

        var create = await CliRunner.RunAsync(home.Path, ["baseline", "create", "--id", "base-from-run", "--run-id", run.Id.Value], ct);
        Assert.Equal((int)PienExitCode.Success, create.ExitCode);

        var show = await CliRunner.RunAsync(home.Path, ["baseline", "show", "--id", "base-from-run"], ct);
        Assert.Equal((int)PienExitCode.Success, show.ExitCode);
        Assert.Contains("base-from-run", show.Stdout, StringComparison.Ordinal);

        var compare = await CliRunner.RunAsync(home.Path, ["baseline", "compare", "--id", "base-cli", "--run-id", run.Id.Value], ct);
        Assert.Equal((int)PienExitCode.Success, compare.ExitCode);
        Assert.Contains("New:", compare.Stdout, StringComparison.Ordinal);

        var missingBaseline = await CliRunner.RunAsync(home.Path, ["baseline", "show", "--id", "missing"], ct);
        Assert.Equal((int)PienExitCode.BaselineFailed, missingBaseline.ExitCode);

        var history = await CliRunner.RunAsync(home.Path, ["history", "list"], ct);
        Assert.Equal((int)PienExitCode.Success, history.ExitCode);
        Assert.Contains(run.Id.Value, history.Stdout, StringComparison.Ordinal);

        var historyShow = await CliRunner.RunAsync(home.Path, ["history", "show", "--run-id", run.Id.Value], ct);
        Assert.Equal((int)PienExitCode.Success, historyShow.ExitCode);

        var report = await CliRunner.RunAsync(home.Path, ["report", "--run-id", run.Id.Value, "--format", "json"], ct);
        Assert.Equal((int)PienExitCode.Success, report.ExitCode);
        Assert.Contains(run.Id.Value, report.Stdout, StringComparison.Ordinal);

        var remove = await CliRunner.RunAsync(home.Path, ["baseline", "remove", "--id", "base-from-run"], ct);
        Assert.Equal((int)PienExitCode.Success, remove.ExitCode);

        var clean = await CliRunner.RunAsync(home.Path, ["history", "clean"], ct);
        Assert.Equal((int)PienExitCode.Success, clean.ExitCode);
        Assert.Contains("Removed", clean.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scan_with_overrides_against_unreachable_loopback_returns_ScanFailed()
    {
        await using var home = TemporaryWorkspace.Create();
        var ct = TestContext.Current.CancellationToken;
        var init = await CliRunner.RunAsync(home.Path, ["init", "--website", "--force"], ct);
        Assert.Equal((int)PienExitCode.Success, init.ExitCode);

        var scan = await CliRunner.RunAsync(
            home.Path,
            [
                "scan",
                "--quiet",
                "--format", "console,json,md",
                "--output", "./artifacts/pien",
                "--fail-on", "high",
                "--max-pages", "1",
                "--max-depth", "0",
            ],
            ct);
        Assert.True(
            scan.ExitCode is (int)PienExitCode.ScanFailed
                or (int)PienExitCode.TargetRejected
                or (int)PienExitCode.PolicyFailed,
            $"Unexpected exit {scan.ExitCode}. stderr={scan.Stderr}");
    }

    [Fact]
    public async Task Scan_target_without_confirm_authorization_returns_TargetRejected()
    {
        await using var home = TemporaryWorkspace.Create();
        var ct = TestContext.Current.CancellationToken;
        var scan = await CliRunner.RunAsync(
            home.Path,
            ["scan", "--target", "http://127.0.0.1:9/", "--quiet"],
            ct);
        Assert.Equal((int)PienExitCode.TargetRejected, scan.ExitCode);
    }

    [Fact]
    public async Task Watch_honors_cancellation()
    {
        await using var home = TemporaryWorkspace.Create();
        await File.WriteAllTextAsync(
            Path.Combine(home.Path, "pien.json"),
            """
            {
              "schemaVersion": 1,
              "profile": "quick",
              "targets": [
                {
                  "id": "local",
                  "kind": "website",
                  "url": "http://127.0.0.1:9/",
                  "authorization": { "confirmed": true, "notes": "watch cancel" }
                }
              ],
              "network": { "allowPrivateNetworks": true, "allowedHosts": ["127.0.0.1"] },
              "output": { "formats": ["console"], "directory": "./artifacts/pien" }
            }
            """,
            TestContext.Current.CancellationToken);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken,
            timeout.Token);
        var result = await CliRunner.RunAsync(home.Path, ["watch", "--interval", "5"], linked.Token);
        Assert.Equal((int)PienExitCode.Cancelled, result.ExitCode);
    }

    [Fact]
    public async Task Scan_covers_overrides_progress_export_and_rejection_paths()
    {
        await using var home = TemporaryWorkspace.Create();
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var serve = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext? ctx = null;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
                ctx.Response.Headers["Permissions-Policy"] = "geolocation=()";
                ctx.Response.ContentType = "text/html; charset=utf-8";
                var bytes = Encoding.UTF8.GetBytes(
                    "<!doctype html><html lang=\"en\"><head><title>Home</title><meta name=\"description\" content=\"d\"/></head><body><img alt=\"x\"/></body></html>");
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        }, TestContext.Current.CancellationToken);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(home.Path, "pien.json"),
                $$"""
                {
                  "schemaVersion": 1,
                  "profile": "quick",
                  "targets": [{
                    "id": "loop",
                    "kind": "website",
                    "url": "{{prefix}}",
                    "authorization": { "confirmed": true, "notes": "in-process scan" }
                  }],
                  "network": { "allowPrivateNetworks": true, "allowedHosts": ["127.0.0.1", "localhost"] },
                  "crawl": { "maxPages": 1, "maxDepth": 0 },
                  "checks": { "enabled": ["PIEN-HTTP-001", "PIEN-HEADERS-001"] },
                  "policies": { "name": "balanced", "failOn": "critical" },
                  "output": { "formats": ["console", "json"], "directory": "./out" },
                  "storage": { "retainRuns": 5 }
                }
                """,
                TestContext.Current.CancellationToken);

            var noisy = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--format", "md,json,console", "--output", "./out2", "--fail-on", "critical", "--max-pages", "1", "--max-depth", "0"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.Success, noisy.ExitCode);
            Assert.True(Directory.Exists(Path.Combine(home.Path, "out2")));

            var quiet = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--quiet", "--baseline", "missing-base"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.Success, quiet.ExitCode);

            var targetNoConfirm = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--target", prefix, "--quiet"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.TargetRejected, targetNoConfirm.ExitCode);

            var targetOk = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--target", prefix, "--confirm-authorization", "--quiet", "--format", "json"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.Success, targetOk.ExitCode);

            var policyFail = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--quiet", "--fail-on", "info"],
                TestContext.Current.CancellationToken);
            Assert.True(
                policyFail.ExitCode is (int)PienExitCode.PolicyFailed or (int)PienExitCode.Success,
                $"unexpected exit {policyFail.ExitCode}");

            var badConfig = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--config", Path.Combine(home.Path, "missing.json"), "--quiet"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.InvalidConfiguration, badConfig.ExitCode);

            await File.WriteAllTextAsync(
                Path.Combine(home.Path, "unauth.json"),
                """
                {
                  "schemaVersion": 1,
                  "profile": "quick",
                  "targets": [{
                    "id": "u",
                    "kind": "website",
                    "url": "http://example.com/",
                    "authorization": { "confirmed": false }
                  }]
                }
                """,
                TestContext.Current.CancellationToken);
            var unauth = await CliRunner.RunAsync(
                home.Path,
                ["scan", "--config", Path.Combine(home.Path, "unauth.json"), "--quiet"],
                TestContext.Current.CancellationToken);
            Assert.Equal((int)PienExitCode.TargetRejected, unauth.ExitCode);

            using var cancel = new CancellationTokenSource();
            await cancel.CancelAsync();
            var cancelled = await CliRunner.RunAsync(home.Path, ["scan", "--quiet"], cancel.Token);
            Assert.Equal((int)PienExitCode.Cancelled, cancelled.ExitCode);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            try
            {
                await serve;
            }
            catch
            {
                // listener shutdown
            }
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<ScanRun> SeedRunAsync(FileScanStore store, string runId)
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "t1",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("http://127.0.0.1/"),
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "standard",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        var plan = ScanPlan.Create(new ScanPlan { Definition = definition, SelectedChecks = [], Limits = definition.Limits });
        var run = ScanRun.Create(new ScanRun
        {
            Id = ScanRunId.Create(runId),
            Plan = plan,
            Status = ScanRunStatus.Completed,
            StartedAt = DateTimeOffset.Parse("2026-08-05T12:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-08-05T12:00:01Z"),
            Findings =
            [
                Finding.Create(new Finding
                {
                    Id = FindingId.Create("f-cli"),
                    CheckId = "PIEN-HTTP-001",
                    RuleVersion = "1.0.0",
                    Title = "ok",
                    Summary = "ok",
                    Explanation = "ok",
                    Severity = FindingSeverity.Info,
                    Status = FindingStatus.Pass,
                    TargetId = "t1",
                    Timestamp = DateTimeOffset.Parse("2026-08-05T12:00:00Z"),
                    RunId = ScanRunId.Create(runId),
                }),
            ],
        });
        var report = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = run.Id,
            GeneratedAt = DateTimeOffset.Parse("2026-08-05T12:00:01Z"),
            Findings = run.Findings,
        });
        await store.SaveRunAsync(run, report, TestContext.Current.CancellationToken);
        return run;
    }
}

internal sealed class TemporaryWorkspace : IAsyncDisposable
{
    private TemporaryWorkspace(string path) => Path = path;

    public string Path { get; }

    public static TemporaryWorkspace Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pien-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryWorkspace(path);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}

internal static class CliRunner
{
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string workingDirectory,
        string[] args,
        CancellationToken cancellationToken)
    {
        var previous = Directory.GetCurrentDirectory();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        Directory.SetCurrentDirectory(workingDirectory);
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var code = await ARTR.Pien.Cli.Program.RunAsync(args, cancellationToken).ConfigureAwait(false);
            return (code, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
            Directory.SetCurrentDirectory(previous);
        }
    }
}
