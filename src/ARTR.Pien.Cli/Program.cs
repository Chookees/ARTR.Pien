using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ARTR.Pien;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Hosting;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;

using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.Cli;

/// <summary>
/// CLI entry point for ARTR Pien.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("ARTR Pien — Test. Inspect. Examine. Report.");
        root.Subcommands.Add(BuildInitCommand());
        root.Subcommands.Add(BuildScanCommand());
        root.Subcommands.Add(BuildValidateCommand());
        root.Subcommands.Add(BuildListChecksCommand());
        root.Subcommands.Add(BuildExplainCommand());
        root.Subcommands.Add(BuildBaselineCommand());
        root.Subcommands.Add(BuildHistoryCommand());
        root.Subcommands.Add(BuildReportCommand());
        root.Subcommands.Add(BuildDoctorCommand());
        root.Subcommands.Add(BuildWatchCommand());
        root.Subcommands.Add(BuildVersionCommand());
        return await root.Parse(args).InvokeAsync();
    }

    private static Command BuildInitCommand()
    {
        var website = new Option<bool>("--website");
        var api = new Option<bool>("--api");
        var ci = new Option<bool>("--ci");
        var force = new Option<bool>("--force");
        var command = new Command("init", "Create a starter pien.json") { website, api, ci, force };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            if (parseResult.GetValue(website) && parseResult.GetValue(api))
            {
                Console.Error.WriteLine("error: Choose either --website or --api, not both.");
                Console.Error.WriteLine("  hint: See `pien init --help`.");
                return (int)PienExitCode.InvalidArguments;
            }

            var path = Path.Combine(Directory.GetCurrentDirectory(), "pien.json");
            if (File.Exists(path) && !parseResult.GetValue(force))
            {
                Console.Error.WriteLine("error: pien.json already exists.");
                Console.Error.WriteLine("  hint: Use --force to overwrite.");
                return (int)PienExitCode.InvalidArguments;
            }

            var kind = parseResult.GetValue(api) ? "api" : "website";
            var profile = parseResult.GetValue(ci) ? "ci" : "standard";
            var json =
                "{\n  \"schemaVersion\": 1,\n  \"profile\": \"" + profile + "\",\n  \"targets\": [{\n" +
                "    \"id\": \"local\",\n    \"kind\": \"" + kind + "\",\n    \"url\": \"http://127.0.0.1:8080/\",\n" +
                "    \"authorization\": { \"confirmed\": true, \"notes\": \"local development only\" }\n  }],\n" +
                "  \"network\": { \"allowPrivateNetworks\": true, \"allowedHosts\": [\"127.0.0.1\", \"localhost\"] }\n}\n";
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Wrote {path}");
            Console.WriteLine("Next: edit targets, then run `pien validate` and `pien scan`.");
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildScanCommand()
    {
        var config = new Option<string?>("--config");
        var target = new Option<string?>("--target");
        var profile = new Option<string?>("--profile");
        var format = new Option<string?>("--format");
        var output = new Option<string?>("--output");
        var failOn = new Option<string?>("--fail-on");
        var maxPages = new Option<int?>("--max-pages");
        var maxDepth = new Option<int?>("--max-depth");
        var baseline = new Option<string?>("--baseline");
        var quiet = new Option<bool>("--quiet");
        var confirmAuth = new Option<bool>("--confirm-authorization");
        var command = new Command("scan", "Run a PIEN scan")
        {
            config, target, profile, format, output, failOn, maxPages, maxDepth, baseline, quiet, confirmAuth,
        };
        command.SetAction(async (parseResult, cancellationToken) =>
            await ScanCommandHandler.ExecuteAsync(
                    parseResult, config, target, profile, format, output, failOn, maxPages, maxDepth, baseline, quiet, confirmAuth, cancellationToken)
                .ConfigureAwait(false));
        return command;
    }

    private static Command BuildValidateCommand()
    {
        var config = new Option<string?>("--config");
        var command = new Command("validate", "Validate configuration") { config };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var workingDirectory = Directory.GetCurrentDirectory();
                await using var provider = BuildServices(workingDirectory);
                var loader = provider.GetRequiredService<IConfigLoader>();
                var configuration = await loader.LoadAsync(new ConfigLoadRequest(parseResult.GetValue(config), workingDirectory), cancellationToken).ConfigureAwait(false);
                _ = PienConfigurationValidator.Validate(configuration, workingDirectory);
                Console.WriteLine("Configuration is valid.");
                return (int)PienExitCode.Success;
            }
            catch (ConfigurationException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Console.Error.WriteLine("  hint: Fix pien.json.");
                return (int)PienExitCode.InvalidConfiguration;
            }
        });
        return command;
    }

    private static Command BuildListChecksCommand()
    {
        var json = new Option<bool>("--json");
        var command = new Command("list-checks", "List built-in checks") { json };
        command.SetAction(async (parseResult, _) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var checks = provider.GetRequiredService<ICheckCatalog>().List();
            if (parseResult.GetValue(json))
            {
                Console.WriteLine(JsonSerializer.Serialize(checks.Select(c => new { id = c.Id.Value, name = c.Name, category = c.Category.ToString(), severity = c.DefaultSeverity.ToString(), version = c.RuleVersion }), new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                foreach (var check in checks)
                {
                    Console.WriteLine($"{check.Id.Value,-18} {check.Category,-16} {check.DefaultSeverity,-8} {check.Name}");
                }
            }

            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildExplainCommand()
    {
        var idArg = new Argument<string>("check-id");
        var command = new Command("explain", "Explain a check") { idArg };
        command.SetAction(async (parseResult, _) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var id = parseResult.GetValue(idArg)!;
            var check = provider.GetRequiredService<ICheckCatalog>().Get(CheckId.Create(id));
            if (check is null)
            {
                Console.Error.WriteLine($"error: Unknown check '{id}'.");
                Console.Error.WriteLine("  hint: Run `pien list-checks`.");
                return (int)PienExitCode.InvalidArguments;
            }

            var definition = check.Definition;
            Console.WriteLine(definition.Name);
            Console.WriteLine(definition.Description);
            Console.WriteLine($"Category: {definition.Category}");
            Console.WriteLine($"Default severity: {definition.DefaultSeverity}");
            Console.WriteLine($"Rule version: {definition.RuleVersion}");
            Console.WriteLine("Remediation: Address the observed finding according to the check description and your security/quality policy.");
            Console.WriteLine("Limitations: Checks are deterministic HTTP/TLS/HTML/API inspections; they are not a browser, exploit, or certification suite.");
            Console.WriteLine("References: docs/checks/CheckCatalog.md");
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildBaselineCommand()
    {
        var command = new Command("baseline", "Manage baselines");
        command.Subcommands.Add(BuildBaselineCreateCommand());
        command.Subcommands.Add(BuildBaselineShowCommand());
        command.Subcommands.Add(BuildBaselineCompareCommand());
        command.Subcommands.Add(BuildBaselineRemoveCommand());
        return command;
    }

    private static Command BuildBaselineCreateCommand()
    {
        var id = new Option<string>("--id") { Required = true };
        var runId = new Option<string>("--run-id") { Required = true };
        var command = new Command("create", "Create a baseline from a stored run") { id, runId };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                await using var provider = BuildServices(Directory.GetCurrentDirectory());
                var run = await provider.GetRequiredService<IRunHistoryStore>()
                    .GetAsync(ScanRunId.Create(parseResult.GetValue(runId)!), cancellationToken)
                    .ConfigureAwait(false);
                if (run is null)
                {
                    Console.Error.WriteLine("error: Run not found.");
                    return (int)PienExitCode.BaselineFailed;
                }

                var targetId = run.Plan.Definition.Targets.FirstOrDefault()?.Id ?? "unknown";
                var fingerprints = run.Findings
                    .Select(f => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{f.CheckId}|{f.Title}|{f.Status}"))))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();
                var baseline = Baseline.Create(new Baseline
                {
                    Id = parseResult.GetValue(id)!,
                    TargetId = targetId,
                    ConfigurationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(run.Plan.Definition.ProfileName))),
                    FindingFingerprints = fingerprints,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await provider.GetRequiredService<IBaselineStore>().SaveAsync(baseline, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"Created baseline '{baseline.Id}' from run {run.Id.Value}.");
                return (int)PienExitCode.Success;
            }
            catch (Exception ex) when (ex is StorageException or IOException or ArgumentException)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return (int)PienExitCode.BaselineFailed;
            }
        });
        return command;
    }

    private static Command BuildBaselineShowCommand()
    {
        var id = new Option<string>("--id") { Required = true };
        var command = new Command("show", "Show a baseline") { id };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var baseline = await provider.GetRequiredService<IBaselineStore>()
                .GetAsync(parseResult.GetValue(id)!, cancellationToken)
                .ConfigureAwait(false);
            if (baseline is null)
            {
                Console.Error.WriteLine("error: Baseline not found.");
                return (int)PienExitCode.BaselineFailed;
            }

            Console.WriteLine(JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }));
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildBaselineCompareCommand()
    {
        var id = new Option<string>("--id") { Required = true };
        var runId = new Option<string>("--run-id") { Required = true };
        var command = new Command("compare", "Compare a run against a baseline") { id, runId };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var baseline = await provider.GetRequiredService<IBaselineStore>()
                .GetAsync(parseResult.GetValue(id)!, cancellationToken)
                .ConfigureAwait(false);
            var run = await provider.GetRequiredService<IRunHistoryStore>()
                .GetAsync(ScanRunId.Create(parseResult.GetValue(runId)!), cancellationToken)
                .ConfigureAwait(false);
            if (baseline is null || run is null)
            {
                Console.Error.WriteLine("error: Baseline or run not found.");
                return (int)PienExitCode.BaselineFailed;
            }

            var current = run.Findings
                .Select(f => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{f.CheckId}|{f.Title}|{f.Status}"))))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var baselineSet = baseline.FindingFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"New: {current.Except(baselineSet, StringComparer.OrdinalIgnoreCase).Count()}");
            Console.WriteLine($"Resolved: {baselineSet.Except(current, StringComparer.OrdinalIgnoreCase).Count()}");
            Console.WriteLine($"Unchanged: {current.Intersect(baselineSet, StringComparer.OrdinalIgnoreCase).Count()}");
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildBaselineRemoveCommand()
    {
        var id = new Option<string>("--id") { Required = true };
        var command = new Command("remove", "Remove a baseline") { id };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var removed = await provider.GetRequiredService<IBaselineStore>()
                .DeleteAsync(parseResult.GetValue(id)!, cancellationToken)
                .ConfigureAwait(false);
            if (!removed)
            {
                Console.Error.WriteLine("error: Baseline not found.");
                return (int)PienExitCode.BaselineFailed;
            }

            Console.WriteLine($"Removed baseline '{parseResult.GetValue(id)}'.");
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildHistoryCommand()
    {
        var command = new Command("history", "Inspect local run history");
        var list = new Command("list", "List recent runs");
        list.SetAction(async (_, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            foreach (var run in await provider.GetRequiredService<IRunHistoryStore>().ListRecentAsync(20, cancellationToken).ConfigureAwait(false))
            {
                Console.WriteLine($"{run.Id.Value} {run.Status} findings={run.Findings.Count}");
            }

            return (int)PienExitCode.Success;
        });
        command.Subcommands.Add(list);

        var show = new Command("show", "Show a run");
        var runId = new Option<string>("--run-id") { Required = true };
        show.Options.Add(runId);
        show.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var run = await provider.GetRequiredService<IRunHistoryStore>()
                .GetAsync(ScanRunId.Create(parseResult.GetValue(runId)!), cancellationToken)
                .ConfigureAwait(false);
            if (run is null)
            {
                Console.Error.WriteLine("error: Run not found.");
                return (int)PienExitCode.StorageFailed;
            }

            Console.WriteLine(JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
            return (int)PienExitCode.Success;
        });
        command.Subcommands.Add(show);

        var clean = new Command("clean", "Apply retention");
        clean.SetAction(async (_, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var removed = await provider.GetRequiredService<IRunHistoryStore>().CleanAsync(50, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Removed {removed} run(s).");
            return (int)PienExitCode.Success;
        });
        command.Subcommands.Add(clean);
        return command;
    }

    private static Command BuildReportCommand()
    {
        var runId = new Option<string>("--run-id") { Required = true };
        var format = new Option<string>("--format") { DefaultValueFactory = _ => "json" };
        var command = new Command("report", "Export a stored run report") { runId, format };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildServices(Directory.GetCurrentDirectory());
            var run = await provider.GetRequiredService<IRunHistoryStore>().GetAsync(ScanRunId.Create(parseResult.GetValue(runId)!), cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                Console.Error.WriteLine("error: Run not found.");
                return (int)PienExitCode.ReportFailed;
            }

            var requested = parseResult.GetValue(format)!;
            if (string.Equals(requested, "md", StringComparison.OrdinalIgnoreCase))
            {
                requested = ReportFormats.Markdown;
            }

            var document = ReportDocument.Create(new ReportDocument { SchemaVersion = 1, RunId = run.Id, GeneratedAt = DateTimeOffset.UtcNow, Findings = run.Findings });
            var exporter = provider.GetServices<IReportExporter>().FirstOrDefault(e => string.Equals(e.Format, requested, StringComparison.OrdinalIgnoreCase));
            if (exporter is null)
            {
                Console.Error.WriteLine("error: Unknown format.");
                return (int)PienExitCode.ReportFailed;
            }

            await exporter.ExportAsync(document, Console.OpenStandardOutput(), cancellationToken).ConfigureAwait(false);
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildDoctorCommand()
    {
        var network = new Option<bool>("--network");
        var command = new Command("doctor", "Diagnose local runtime and configuration") { network };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            Console.WriteLine($"OK SDK/runtime: {Environment.Version}");
            Console.WriteLine($"OK Working directory writable: {IsWritable(Directory.GetCurrentDirectory())}");
            Console.WriteLine($"OK State directory (.pien): {(Directory.Exists(".pien") ? "present" : "absent")}");
            Console.WriteLine("OK Secrets are never printed by doctor.");
            if (File.Exists("pien.json"))
            {
                try
                {
                    await using var provider = BuildServices(Directory.GetCurrentDirectory());
                    var configuration = await provider.GetRequiredService<IConfigLoader>()
                        .LoadAsync(new ConfigLoadRequest("pien.json", Directory.GetCurrentDirectory()), cancellationToken)
                        .ConfigureAwait(false);
                    _ = PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory());
                    Console.WriteLine("OK Configuration validates.");
                }
                catch (ConfigurationException ex)
                {
                    Console.WriteLine($"ERR Configuration: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("WARN pien.json absent.");
            }

            if (parseResult.GetValue(network))
            {
                Console.WriteLine("OK Network opt-in: loopback DNS resolve only.");
                try
                {
                    _ = await System.Net.Dns.GetHostAddressesAsync("127.0.0.1", cancellationToken).ConfigureAwait(false);
                    Console.WriteLine("OK Loopback DNS usable.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"ERR Loopback DNS: {ex.Message}");
                }
            }

            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildWatchCommand()
    {
        var interval = new Option<int>("--interval") { DefaultValueFactory = _ => 300 };
        var command = new Command("watch", "Run scans on an interval without overlap") { interval };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var seconds = Math.Clamp(parseResult.GetValue(interval), 5, 86_400);
            Console.Error.WriteLine($"Watching every {seconds}s. Press Ctrl+C to cancel.");
            var cycle = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                cycle++;
                Console.Error.WriteLine($"── watch cycle {cycle} @ {DateTimeOffset.UtcNow:O} ──");
                var next = DateTimeOffset.UtcNow.AddSeconds(seconds);
                var code = await BuildScanCommand().Parse(["scan", "--quiet"]).InvokeAsync(cancellationToken: cancellationToken);
                if (code == (int)PienExitCode.Cancelled)
                {
                    return code;
                }

                Console.Error.WriteLine($"Next run at {next:O}");
                var delay = next - DateTimeOffset.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.FromSeconds(seconds);
                }

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return (int)PienExitCode.Cancelled;
                }
            }

            return (int)PienExitCode.Cancelled;
        });
        return command;
    }

    private static Command BuildVersionCommand()
    {
        var command = new Command("version", "Show version information");
        command.SetAction((_, _) =>
        {
            Console.WriteLine("ARTR Pien 0.1.0");
            return Task.FromResult((int)PienExitCode.Success);
        });
        return command;
    }

    internal static ServiceProvider BuildServices(string workingDirectory, PienConfiguration? configuration = null)
        => new ServiceCollection()
            .AddPien(options =>
            {
                options.WorkingDirectory = workingDirectory;
                options.StateDirectory = Path.Combine(workingDirectory, configuration?.Storage.StateDirectory.TrimStart('.', '/', '\\') is { Length: > 0 } relative
                    ? configuration!.Storage.StateDirectory
                    : ".pien");
                if (!Path.IsPathRooted(options.StateDirectory))
                {
                    options.StateDirectory = Path.Combine(workingDirectory, configuration?.Storage.StateDirectory ?? ".pien");
                }

                options.AllowPrivateNetworks = configuration?.Network.AllowPrivateNetworks ?? true;
                options.AllowedHosts = configuration?.Network.AllowedHosts is { Count: > 0 } hosts
                    ? hosts
                    : ["127.0.0.1", "localhost", "::1"];
                options.WebhookUrl = configuration?.Notifications.WebhookUrl;
                options.WebhookSecretReference = configuration?.Notifications.WebhookSecretReference;
            })
            .BuildServiceProvider();

    private static bool IsWritable(string path)
    {
        try
        {
            var probe = Path.Combine(path, ".pien-write-probe");
            File.WriteAllText(probe, "ok", Encoding.UTF8);
            File.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
