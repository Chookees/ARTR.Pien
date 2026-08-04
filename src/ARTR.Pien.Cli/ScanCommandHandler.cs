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
        Option<bool> quiet,
        CancellationToken cancellationToken)
    {
        try
        {
            var workingDirectory = Directory.GetCurrentDirectory();
            await using var provider = Program.BuildServices(workingDirectory);
            var configuration = await LoadConfigurationAsync(provider, parseResult, config, profile, cancellationToken).ConfigureAwait(false);
            ApplyOverrides(configuration, parseResult, target, format, output, failOn, maxPages);
            var validated = PienConfigurationValidator.Validate(configuration, workingDirectory);
            var progress = parseResult.GetValue(quiet)
                ? null
                : new Progress<ScanProgress>(p => Console.WriteLine($"[{p.Stage}] {p.Message}"));
            var run = await provider.GetRequiredService<IScanEngine>().RunAsync(
                validated.Definition,
                new ScanEngineOptions
                {
                    Policy = validated.Policy,
                    WorkingDirectory = validated.WorkingDirectory,
                    Network = validated.Network,
                },
                progress,
                cancellationToken).ConfigureAwait(false);
            await ExportReportsAsync(provider, configuration, run, cancellationToken).ConfigureAwait(false);
            return MapExitCode(provider, validated, run);
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return (int)PienExitCode.InvalidConfiguration;
        }
        catch (TargetSafetyException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return (int)PienExitCode.TargetRejected;
        }
        catch (AuthorizationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return (int)PienExitCode.TargetRejected;
        }
        catch (OperationCanceledException)
        {
            return (int)PienExitCode.Cancelled;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return (int)PienExitCode.ScanFailed;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return (int)PienExitCode.ScanFailed;
        }
    }

    private static async Task<PienConfiguration> LoadConfigurationAsync(
        IServiceProvider provider,
        ParseResult parseResult,
        Option<string?> config,
        Option<string?> profile,
        CancellationToken cancellationToken)
    {
        var loader = provider.GetRequiredService<IConfigLoader>();
        return await loader.LoadAsync(
            new ConfigLoadRequest(parseResult.GetValue(config), Directory.GetCurrentDirectory(), parseResult.GetValue(profile)),
            cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyOverrides(
        PienConfiguration configuration,
        ParseResult parseResult,
        Option<string?> target,
        Option<string?> format,
        Option<string?> output,
        Option<string?> failOn,
        Option<int?> maxPages)
    {
        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(target)))
        {
            configuration.Targets =
            [
                new PienTargetConfiguration
                {
                    Id = "cli",
                    Kind = "website",
                    Url = parseResult.GetValue(target)!,
                    Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                },
            ];
            configuration.Network.AllowPrivateNetworks = true;
            configuration.Network.AllowedHosts = ["127.0.0.1", "localhost", "::1"];
        }

        if (parseResult.GetValue(maxPages) is int pages)
        {
            configuration.Crawl.MaxPages = pages;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(failOn)))
        {
            configuration.Policies.FailOn = parseResult.GetValue(failOn)!;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(format)))
        {
            configuration.Output.Formats = parseResult.GetValue(format)!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(output)))
        {
            configuration.Output.Directory = parseResult.GetValue(output)!;
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
