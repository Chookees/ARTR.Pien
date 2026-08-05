using System.CommandLine;
using ARTR.Pien;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.Cli;

internal static class ScanCommandHandler
{
    public static async Task<int> ExecuteAsync(
        ParseResult parseResult,
        Option<string?> config,
        Option<string?> target,
        Option<string?> profile,
        Option<string?> format,
        Option<string?> output,
        Option<string?> failOn,
        Option<int?> maxPages,
        Option<int?> maxDepth,
        Option<string?> baseline,
        Option<bool> quiet,
        Option<bool> confirmAuth,
        CancellationToken cancellationToken)
    {
        try
        {
            var workingDirectory = Directory.GetCurrentDirectory();
            var configuration = await LoadConfigurationAsync(workingDirectory, parseResult, config, profile, cancellationToken)
                .ConfigureAwait(false);
            ApplyOverrides(configuration, parseResult, target, format, output, failOn, maxPages, maxDepth, baseline, confirmAuth);
            var validated = PienConfigurationValidator.Validate(configuration, workingDirectory);
            await using var provider = Program.BuildServices(workingDirectory, configuration);
            var progress = parseResult.GetValue(quiet)
                ? null
                : new Progress<ScanProgress>(p =>
                {
                    var stage = $"[{p.Stage}]".PadRight(12);
                    var percent = p.PercentComplete is int value ? $"{value,3}%" : "   ";
                    var counts = p.TotalChecks > 0 ? $" ({p.CompletedChecks}/{p.TotalChecks})" : string.Empty;
                    Console.Error.WriteLine($"{stage} {p.Message}{counts} {percent}");
                });

            var run = await provider.GetRequiredService<IScanEngine>().RunAsync(
                validated.Definition,
                new ScanEngineOptions
                {
                    Policy = validated.Policy,
                    WorkingDirectory = validated.WorkingDirectory,
                    Network = validated.Network,
                    BaselineId = configuration.Baselines.Id ?? parseResult.GetValue(baseline),
                    CompareBaseline = configuration.Baselines.CompareOnScan || !string.IsNullOrWhiteSpace(parseResult.GetValue(baseline)),
                    Notifications = configuration.Notifications,
                    RetainRuns = configuration.Storage.RetainRuns,
                },
                progress,
                cancellationToken).ConfigureAwait(false);
            await ExportReportsAsync(provider, configuration, run, cancellationToken).ConfigureAwait(false);
            return MapExitCode(provider, validated, run);
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("  hint: Run `pien validate`.");
            return (int)PienExitCode.InvalidConfiguration;
        }
        catch (TargetSafetyException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("  hint: Confirm authorization / allowlist the host.");
            return (int)PienExitCode.TargetRejected;
        }
        catch (AuthorizationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("  hint: Set authorization.confirmed=true or pass --confirm-authorization with --target.");
            return (int)PienExitCode.TargetRejected;
        }
        catch (NotificationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return (int)PienExitCode.NotificationFailed;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return (int)PienExitCode.Cancelled;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return (int)PienExitCode.ScanFailed;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return (int)PienExitCode.ScanFailed;
        }
    }

    private static async Task<PienConfiguration> LoadConfigurationAsync(
        string workingDirectory,
        ParseResult parseResult,
        Option<string?> config,
        Option<string?> profile,
        CancellationToken cancellationToken)
    {
        await using var provider = Program.BuildServices(workingDirectory);
        var loader = provider.GetRequiredService<IConfigLoader>();
        return await loader.LoadAsync(
            new ConfigLoadRequest(parseResult.GetValue(config), workingDirectory, parseResult.GetValue(profile)),
            cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyOverrides(
        PienConfiguration configuration,
        ParseResult parseResult,
        Option<string?> target,
        Option<string?> format,
        Option<string?> output,
        Option<string?> failOn,
        Option<int?> maxPages,
        Option<int?> maxDepth,
        Option<string?> baseline,
        Option<bool> confirmAuth)
    {
        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(target)))
        {
            if (!parseResult.GetValue(confirmAuth))
            {
                throw new AuthorizationException(
                    "Target rejected — --target requires --confirm-authorization (or configure authorization.confirmed in pien.json).");
            }

            configuration.Targets =
            [
                new PienTargetConfiguration
                {
                    Id = "cli",
                    Kind = "website",
                    Url = parseResult.GetValue(target)!,
                    Authorization = new PienAuthorizationConfiguration
                    {
                        Confirmed = true,
                        Notes = "Confirmed via --confirm-authorization",
                    },
                },
            ];
            configuration.Network.AllowPrivateNetworks = true;
            if (configuration.Network.AllowedHosts.Count == 0)
            {
                configuration.Network.AllowedHosts = ["127.0.0.1", "localhost", "::1"];
            }
        }

        if (parseResult.GetValue(maxPages) is int pages)
        {
            configuration.Crawl.MaxPages = pages;
        }

        if (parseResult.GetValue(maxDepth) is int depth)
        {
            configuration.Crawl.MaxDepth = depth;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(failOn)))
        {
            configuration.Policies.FailOn = parseResult.GetValue(failOn)!;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(format)))
        {
            configuration.Output.Formats = parseResult.GetValue(format)!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(f => string.Equals(f, "md", StringComparison.OrdinalIgnoreCase) ? ReportFormats.Markdown : f)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(output)))
        {
            configuration.Output.Directory = parseResult.GetValue(output)!;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(baseline)))
        {
            configuration.Baselines.Id = parseResult.GetValue(baseline);
            configuration.Baselines.CompareOnScan = true;
        }
    }

    private static int MapExitCode(IServiceProvider provider, ValidatedConfiguration validated, ScanRun run)
    {
        if (run.Status == ScanRunStatus.Cancelled)
        {
            return (int)PienExitCode.Cancelled;
        }

        if (run.Status == ScanRunStatus.Failed)
        {
            return (int)PienExitCode.ScanFailed;
        }

        var policy = provider.GetRequiredService<IPolicyEvaluator>().Evaluate(validated.Policy, run.Findings);
        return policy.Passed ? (int)PienExitCode.Success : (int)PienExitCode.PolicyFailed;
    }

    private static async Task ExportReportsAsync(
        IServiceProvider provider,
        PienConfiguration configuration,
        ScanRun run,
        CancellationToken cancellationToken)
    {
        var document = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = run.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            Findings = run.Findings,
        });
        var exporters = provider.GetServices<IReportExporter>()
            .Where(e => configuration.Output.Formats.Contains(e.Format, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var directory = Path.GetFullPath(configuration.Output.Directory);
        Directory.CreateDirectory(directory);
        foreach (var exporter in exporters)
        {
            if (exporter.Format == ReportFormats.Console)
            {
                await exporter.ExportAsync(document, Console.OpenStandardOutput(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var path = Path.Combine(directory, $"{run.Id.Value}.{exporter.Format}");
            await using var stream = File.Create(path);
            await exporter.ExportAsync(document, stream, cancellationToken).ConfigureAwait(false);
        }
    }
}
