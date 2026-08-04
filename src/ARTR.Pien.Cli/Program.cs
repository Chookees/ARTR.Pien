using System.CommandLine;
using System.Text;
using System.Text.Json;
using ARTR.Pien;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Hosting;
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
            var path = Path.Combine(Directory.GetCurrentDirectory(), "pien.json");
            if (File.Exists(path) && !parseResult.GetValue(force))
            {
                Console.Error.WriteLine("pien.json already exists. Use --force to overwrite.");
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
        var quiet = new Option<bool>("--quiet");
        var command = new Command("scan", "Run a PIEN scan") { config, target, profile, format, output, failOn, maxPages, quiet };
        command.SetAction(async (parseResult, cancellationToken) =>
            await ScanCommandHandler.ExecuteAsync(parseResult, config, target, profile, format, output, failOn, maxPages, quiet, cancellationToken)
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
                Console.Error.WriteLine(ex.Message);
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
                Console.Error.WriteLine($"Unknown check '{id}'.");
                return (int)PienExitCode.InvalidArguments;
            }

            Console.WriteLine(check.Definition.Name);
            Console.WriteLine(check.Definition.Description);
            Console.WriteLine($"Category: {check.Definition.Category}");
            Console.WriteLine($"Default severity: {check.Definition.DefaultSeverity}");
            Console.WriteLine($"Rule version: {check.Definition.RuleVersion}");
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildBaselineCommand()
    {
        var command = new Command("baseline", "Manage baselines");
        command.Subcommands.Add(Stub("create", "Create a baseline"));
        command.Subcommands.Add(Stub("show", "Show a baseline"));
        command.Subcommands.Add(Stub("compare", "Compare against a baseline"));
        command.Subcommands.Add(Stub("remove", "Remove a baseline"));
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
        command.Subcommands.Add(Stub("show", "Show a run"));
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
                Console.Error.WriteLine("Run not found.");
                return (int)PienExitCode.ReportFailed;
            }

            var document = ReportDocument.Create(new ReportDocument { SchemaVersion = 1, RunId = run.Id, GeneratedAt = DateTimeOffset.UtcNow, Findings = run.Findings });
            var exporter = provider.GetServices<IReportExporter>().FirstOrDefault(e => string.Equals(e.Format, parseResult.GetValue(format), StringComparison.OrdinalIgnoreCase));
            if (exporter is null)
            {
                Console.Error.WriteLine("Unknown format.");
                return (int)PienExitCode.ReportFailed;
            }

            await exporter.ExportAsync(document, Console.OpenStandardOutput(), cancellationToken).ConfigureAwait(false);
            return (int)PienExitCode.Success;
        });
        return command;
    }

    private static Command BuildDoctorCommand()
    {
        var command = new Command("doctor", "Diagnose local runtime and configuration");
        command.SetAction((_, _) =>
        {
            Console.WriteLine($"SDK/runtime: {Environment.Version}");
            Console.WriteLine($"Working directory writable: {IsWritable(Directory.GetCurrentDirectory())}");
            Console.WriteLine($"State directory (.pien): {(Directory.Exists(".pien") ? "present" : "absent")}");
            Console.WriteLine("Secrets are never printed by doctor.");
            return Task.FromResult((int)PienExitCode.Success);
        });
        return command;
    }

    private static Command BuildWatchCommand()
    {
        var interval = new Option<int>("--interval") { DefaultValueFactory = _ => 300 };
        var command = new Command("watch", "Run scans on an interval without overlap") { interval };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var seconds = Math.Max(1, parseResult.GetValue(interval));
            Console.WriteLine($"Watching every {seconds}s. Press Ctrl+C to cancel.");
            while (!cancellationToken.IsCancellationRequested)
            {
                var code = await BuildScanCommand().Parse(["scan", "--quiet"]).InvokeAsync(cancellationToken: cancellationToken);
                if (code == (int)PienExitCode.Cancelled)
                {
                    return code;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
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

    private static Command Stub(string name, string description)
    {
        var command = new Command(name, description);
        command.SetAction((_, _) =>
        {
            Console.WriteLine($"{name}: requires stored scan state.");
            return Task.FromResult((int)PienExitCode.Success);
        });
        return command;
    }

    internal static ServiceProvider BuildServices(string workingDirectory)
        => new ServiceCollection()
            .AddPien(options =>
            {
                options.WorkingDirectory = workingDirectory;
                options.StateDirectory = Path.Combine(workingDirectory, ".pien");
                options.AllowPrivateNetworks = true;
                options.AllowedHosts = ["127.0.0.1", "localhost", "::1"];
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
