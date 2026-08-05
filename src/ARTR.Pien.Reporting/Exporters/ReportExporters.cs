using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Findings;
using ARTR.Pien.Reporting;

namespace ARTR.Pien.Reporting.Exporters;

/// <summary>JSON report exporter.</summary>
public sealed class JsonReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.Json;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);
        await JsonSerializer.SerializeAsync(
            destination,
            document,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Console text exporter.</summary>
public sealed class ConsoleReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.Console;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);
        var sb = new StringBuilder();
        sb.AppendLine($"Pien report {document.RunId} @ {document.GeneratedAt:O}");
        sb.AppendLine($"Findings: {document.Findings.Count}");
        if (document.PolicyResult is not null)
        {
            sb.AppendLine($"Policy: {(document.PolicyResult.Passed ? "PASS" : "FAIL")} ({document.PolicyResult.Summary})");
        }

        foreach (var finding in document.Findings.Take(50))
        {
            sb.AppendLine($"- [{finding.Severity}] {finding.CheckId}: {finding.Title}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Hand-written SARIF 2.1.0 exporter.</summary>
public sealed class SarifReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.Sarif;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);

        var rules = document.Findings
            .GroupBy(f => f.CheckId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Dictionary<string, object?>
            {
                ["id"] = g.Key,
                ["name"] = g.Key,
                ["shortDescription"] = new Dictionary<string, string> { ["text"] = g.First().Title },
            })
            .ToArray();

        var results = document.Findings.Select(f => new Dictionary<string, object?>
        {
            ["ruleId"] = f.CheckId,
            ["level"] = MapLevel(f.Severity),
            ["message"] = new Dictionary<string, string> { ["text"] = f.Summary },
        }).ToArray();

        var sarif = new Dictionary<string, object?>
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["tool"] = new Dictionary<string, object?>
                    {
                        ["driver"] = new Dictionary<string, object?>
                        {
                            ["name"] = "ARTR Pien",
                            ["informationUri"] = "https://github.com/ARTR-Projects/Pien",
                            ["rules"] = rules,
                        },
                    },
                    ["results"] = results,
                },
            },
        };

        await JsonSerializer.SerializeAsync(destination, sarif, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string MapLevel(FindingSeverity severity)
        => severity switch
        {
            FindingSeverity.Critical or FindingSeverity.High => "error",
            FindingSeverity.Medium => "warning",
            _ => "note",
        };
}

/// <summary>JUnit XML exporter.</summary>
public sealed class JUnitReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.JUnit;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);

        var failures = document.Findings.Count(f => f.Status == FindingStatus.Fail);
        var suite = new XElement(
            "testsuite",
            new XAttribute("name", "ARTR.Pien"),
            new XAttribute("tests", document.Findings.Count),
            new XAttribute("failures", failures),
            document.Findings.Select(f =>
            {
                var test = new XElement(
                    "testcase",
                    new XAttribute("classname", f.CheckId),
                    new XAttribute("name", f.Title));
                if (f.Status == FindingStatus.Fail)
                {
                    test.Add(new XElement("failure", new XAttribute("message", f.Summary), f.Explanation));
                }

                return test;
            }));

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), suite);
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync(xml.ToString()).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Markdown exporter.</summary>
public sealed class MarkdownReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.Markdown;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);
        var sb = new StringBuilder();
        sb.AppendLine("# ARTR Pien Report");
        sb.AppendLine();
        sb.AppendLine($"- Run: `{document.RunId}`");
        sb.AppendLine($"- Generated: {document.GeneratedAt:O}");
        sb.AppendLine($"- Findings: {document.Findings.Count}");
        sb.AppendLine();
        sb.AppendLine("| Severity | Check | Title |");
        sb.AppendLine("|---|---|---|");
        foreach (var finding in document.Findings)
        {
            sb.AppendLine($"| {finding.Severity} | `{WebUtility.HtmlEncode(finding.CheckId)}` | {Escape(finding.Title)} |");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}

/// <summary>Self-contained HTML exporter.</summary>
public sealed class HtmlReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => ReportFormats.Html;

    /// <inheritdoc />
    public async Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        document = ReportDocument.Create(document);
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/><title>ARTR Pien Report</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;margin:2rem;background:#f7f4ef;color:#1b1b1b}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:.5rem;text-align:left}th{background:#e8e0d5}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>ARTR Pien Report</h1>");
        sb.AppendLine($"<p>Run <code>{WebUtility.HtmlEncode(document.RunId.Value)}</code> generated {WebUtility.HtmlEncode(document.GeneratedAt.ToString("O"))}</p>");
        sb.AppendLine("<table><thead><tr><th>Severity</th><th>Check</th><th>Title</th></tr></thead><tbody>");
        foreach (var finding in document.Findings)
        {
            sb.AppendLine($"<tr><td>{finding.Severity}</td><td><code>{WebUtility.HtmlEncode(finding.CheckId)}</code></td><td>{WebUtility.HtmlEncode(finding.Title)}</td></tr>");
        }

        sb.AppendLine("</tbody></table></body></html>");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
