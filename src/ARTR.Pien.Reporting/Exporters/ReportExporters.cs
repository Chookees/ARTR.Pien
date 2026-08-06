using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
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
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            },
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

/// <summary>Self-contained HTML exporter with German customer-facing layout, filters, and baseline diff.</summary>
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
        var sb = new StringBuilder(capacity: 16_384);
        AppendDocument(sb, document);
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static void AppendDocument(StringBuilder sb, ReportDocument document)
    {
        AppendHead(sb, document);
        AppendSummary(sb, document);
        if (document.BaselineComparisons.Count > 0)
        {
            AppendBaseline(sb, document);
        }

        AppendFindings(sb, document);
        sb.AppendLine("<footer><p>ARTR Pien erzeugt Beobachtungen für autorisierte Ziele. Kein Zertifikat, kein Pen-Test, keine Rechtsberatung.</p></footer>");
        sb.AppendLine("<script>");
        sb.AppendLine(Js);
        sb.AppendLine("</script></body></html>");
    }

    private static void AppendHead(StringBuilder sb, ReportDocument document)
    {
        var policy = document.PolicyResult;
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"de\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.AppendLine("<title>ARTR Pien — Prüfbericht</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<header class=\"hero\">");
        sb.AppendLine("<p class=\"brand\">ARTR Pien</p>");
        sb.AppendLine("<h1>Prüfbericht</h1>");
        sb.AppendLine("<p class=\"tagline\">Test. Inspect. Examine. Report.</p>");
        sb.AppendLine($"<p class=\"meta\">Lauf <code>{E(document.RunId.Value)}</code> · {E(document.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC</p>");
        if (policy is not null)
        {
            sb.AppendLine($"<p class=\"policy {(policy.Passed ? "ok" : "bad")}\">Policy <strong>{E(policy.PolicyName)}</strong>: {(policy.Passed ? "BESTANDEN" : "NICHT BESTANDEN")}</p>");
        }

        sb.AppendLine("</header>");
    }

    private static void AppendSummary(StringBuilder sb, ReportDocument document)
    {
        var findings = document.Findings;
        var counts = findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.Count());
        var failCount = findings.Count(f => f.Status == FindingStatus.Fail);
        sb.AppendLine("<section class=\"summary\" aria-label=\"Zusammenfassung\">");
        sb.AppendLine("<h2>Zusammenfassung</h2>");
        sb.AppendLine("<div class=\"cards\">");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{findings.Count}</span><span class=\"l\">Findings</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{failCount}</span><span class=\"l\">Fehler</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{counts.GetValueOrDefault(FindingSeverity.Critical)}</span><span class=\"l\">Kritisch</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{counts.GetValueOrDefault(FindingSeverity.High)}</span><span class=\"l\">Hoch</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{counts.GetValueOrDefault(FindingSeverity.Medium)}</span><span class=\"l\">Mittel</span></div>");
        sb.AppendLine("</div>");
        if (document.CategoryScores.Count > 0)
        {
            sb.AppendLine("<h3>Kategorie-Scores</h3><ul class=\"scores\">");
            foreach (var (category, score) in document.CategoryScores.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"<li><span>{E(category)}</span><strong>{score:0.#}</strong></li>");
            }

            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</section>");
    }

    private static void AppendFindings(StringBuilder sb, ReportDocument document)
    {
        sb.AppendLine("<section class=\"findings\" aria-label=\"Findings\">");
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine("<h2>Findings</h2>");
        sb.AppendLine("<label>Schwere <select id=\"sevFilter\"><option value=\"\">Alle</option><option>Critical</option><option>High</option><option>Medium</option><option>Low</option><option>Info</option></select></label>");
        sb.AppendLine("<label>Status <select id=\"statusFilter\"><option value=\"\">Alle</option><option>Fail</option><option>Warning</option><option>Pass</option><option>Error</option></select></label>");
        sb.AppendLine("<label>Suche <input id=\"q\" type=\"search\" placeholder=\"Check, Titel, Ziel…\"/></label>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table id=\"findingsTable\"><thead><tr>");
        sb.AppendLine("<th>Schwere</th><th>Status</th><th>Check</th><th>Titel</th><th>Ort</th><th>Baseline</th>");
        sb.AppendLine("</tr></thead><tbody>");
        AppendFindingRows(sb, document);
        sb.AppendLine("</tbody></table></section>");
    }

    private static void AppendFindingRows(StringBuilder sb, ReportDocument document)
    {
        var baselineByFinding = document.BaselineComparisons
            .Where(c => c.CurrentFindingId is not null)
            .ToDictionary(c => c.CurrentFindingId!.Value, c => c.Kind.ToString(), StringComparer.OrdinalIgnoreCase);

        foreach (var finding in document.Findings.OrderByDescending(f => f.Severity).ThenBy(f => f.CheckId, StringComparer.Ordinal))
        {
            var baselineKind = finding.BaselineState
                ?? (baselineByFinding.TryGetValue(finding.Id.Value, out var kind) ? kind : string.Empty);
            var query = (finding.CheckId + " " + finding.Title + " " + finding.TargetId).ToLowerInvariant();
            sb.AppendLine($"<tr data-sev=\"{finding.Severity}\" data-status=\"{finding.Status}\" data-q=\"{E(query)}\">");
            sb.AppendLine($"<td><span class=\"sev {finding.Severity}\">{finding.Severity}</span></td>");
            sb.AppendLine($"<td>{finding.Status}</td>");
            sb.AppendLine($"<td><code>{E(finding.CheckId)}</code></td>");
            sb.AppendLine($"<td><button type=\"button\" class=\"linkish\" onclick=\"toggle('{EJs(finding.Id.Value)}')\">{E(finding.Title)}</button></td>");
            sb.AppendLine($"<td>{E(finding.Location?.Path ?? "—")}</td>");
            sb.AppendLine($"<td>{E(string.IsNullOrWhiteSpace(baselineKind) ? "—" : baselineKind)}</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine($"<tr class=\"detail\" id=\"d-{E(finding.Id.Value)}\" hidden><td colspan=\"6\">");
            sb.AppendLine("<dl>");
            sb.AppendLine($"<dt>Erklärung</dt><dd>{E(finding.Explanation)}</dd>");
            sb.AppendLine($"<dt>Erwartet</dt><dd>{E(finding.Expected ?? "—")}</dd>");
            sb.AppendLine($"<dt>Beobachtet</dt><dd>{E(finding.Observed ?? "—")}</dd>");
            sb.AppendLine($"<dt>Evidence</dt><dd><pre>{E(string.Join("\n---\n", finding.Evidence.Select(e => e.Text)))}</pre></dd>");
            sb.AppendLine($"<dt>Maßnahme</dt><dd>{E(finding.Remediation ?? "—")}</dd>");
            sb.AppendLine("</dl></td></tr>");
        }
    }

    private static void AppendBaseline(StringBuilder sb, ReportDocument document)
    {
        var groups = document.BaselineComparisons.GroupBy(c => c.Kind).ToDictionary(g => g.Key, g => g.Count());
        sb.AppendLine("<section class=\"baseline\" aria-label=\"Baseline-Vergleich\">");
        sb.AppendLine("<h2>Baseline-Vergleich</h2>");
        sb.AppendLine("<div class=\"cards\">");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{groups.GetValueOrDefault(BaselineComparisonKind.New)}</span><span class=\"l\">Neu</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{groups.GetValueOrDefault(BaselineComparisonKind.Resolved)}</span><span class=\"l\">Behoben</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{groups.GetValueOrDefault(BaselineComparisonKind.Changed)}</span><span class=\"l\">Geändert</span></div>");
        sb.AppendLine($"<div class=\"card\"><span class=\"n\">{groups.GetValueOrDefault(BaselineComparisonKind.Unchanged)}</span><span class=\"l\">Unverändert</span></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table><thead><tr><th>Art</th><th>Fingerprint</th><th>Finding</th></tr></thead><tbody>");
        foreach (var row in document.BaselineComparisons.OrderBy(r => r.Kind).ThenBy(r => r.FindingFingerprint, StringComparer.Ordinal))
        {
            sb.AppendLine($"<tr><td>{row.Kind}</td><td><code>{E(row.FindingFingerprint[..Math.Min(16, row.FindingFingerprint.Length)])}…</code></td><td>{E(row.CurrentFindingId?.Value ?? "—")}</td></tr>");
        }

        sb.AppendLine("</tbody></table></section>");
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string EJs(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private const string Css = """
:root{--ink:#1a1c1a;--paper:#f4f1ea;--line:#d9d2c5;--brand:#0f3d2e;--accent:#c45c26;--ok:#2f6b4f;--bad:#8b1e1e}
*{box-sizing:border-box}body{margin:0;font:16px/1.5 "Source Serif 4",Georgia,serif;background:linear-gradient(180deg,#ebe6dc,#f7f4ef 28%);color:var(--ink)}
.hero{padding:2.5rem clamp(1rem,4vw,3rem) 1.5rem;background:radial-gradient(1200px 400px at 10% -20%,#dfece5,transparent),var(--brand);color:#f5f7f4}
.brand{font:700 1.4rem/1 "Segoe UI",system-ui,sans-serif;letter-spacing:.04em;margin:0}
.hero h1{font:700 clamp(1.8rem,4vw,2.6rem)/1.1 "Segoe UI",system-ui,sans-serif;margin:.4rem 0}
.tagline{opacity:.85;margin:0 0 1rem}.meta,.policy{font:14px/1.4 "Segoe UI",system-ui,sans-serif}
.policy.ok{color:#b7e0c8}.policy.bad{color:#ffd0c8}
section{padding:1.5rem clamp(1rem,4vw,3rem)}h2{font:650 1.25rem/1.2 "Segoe UI",system-ui,sans-serif;margin:0 0 1rem}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(7.5rem,1fr));gap:.75rem;margin-bottom:1rem}
.card{background:#fff;border:1px solid var(--line);border-radius:10px;padding:.9rem;display:flex;flex-direction:column}
.card .n{font:700 1.5rem/1 "Segoe UI",system-ui,sans-serif}.card .l{color:#5c584f;font:12px/1.2 "Segoe UI",system-ui,sans-serif;margin-top:.35rem}
.scores{list-style:none;padding:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(12rem,1fr));gap:.4rem}
.scores li{display:flex;justify-content:space-between;background:#fff;border:1px solid var(--line);border-radius:8px;padding:.5rem .7rem;font:14px "Segoe UI",system-ui,sans-serif}
.toolbar{display:flex;flex-wrap:wrap;gap:.75rem 1.25rem;align-items:end;margin-bottom:1rem}
.toolbar label{font:13px "Segoe UI",system-ui,sans-serif;display:flex;flex-direction:column;gap:.25rem}
input,select{font:14px "Segoe UI",system-ui,sans-serif;padding:.4rem .55rem;border:1px solid var(--line);border-radius:6px;background:#fff}
table{width:100%;border-collapse:collapse;background:#fff;border:1px solid var(--line);border-radius:10px;overflow:hidden}
th,td{padding:.55rem .7rem;border-bottom:1px solid var(--line);text-align:left;vertical-align:top;font:14px/1.4 "Segoe UI",system-ui,sans-serif}
th{background:#ebe4d8}tr.detail td{background:#faf8f4}
.sev{display:inline-block;padding:.1rem .45rem;border-radius:999px;font-size:12px;font-weight:650}
.sev.Critical,.sev.High{background:#f3d2d0}.sev.Medium{background:#f6e2c6}.sev.Low,.sev.Info{background:#dde8df}
.linkish{background:none;border:0;padding:0;color:var(--accent);font:inherit;cursor:pointer;text-align:left;text-decoration:underline}
pre{white-space:pre-wrap;word-break:break-word;background:#f0ebe3;padding:.75rem;border-radius:8px;margin:0;font:12px/1.4 ui-monospace,Consolas,monospace}
dl{display:grid;grid-template-columns:8rem 1fr;gap:.35rem .75rem;margin:0}dt{font-weight:650;color:#5c584f}dd{margin:0}
footer{padding:1rem clamp(1rem,4vw,3rem) 2.5rem;color:#5c584f;font:13px/1.45 "Segoe UI",system-ui,sans-serif}
@media print{body{background:#fff}.hero{background:#fff;color:#000;border-bottom:2px solid #000}.toolbar{display:none}.linkish{color:#000;text-decoration:none}tr.detail{display:table-row!important}}
""";

    private const string Js = """
const table=document.getElementById('findingsTable');
const sev=document.getElementById('sevFilter');
const st=document.getElementById('statusFilter');
const q=document.getElementById('q');
function apply(){const s=(sev.value||'').toLowerCase();const u=(st.value||'').toLowerCase();const query=(q.value||'').toLowerCase();
for(const row of table.tBodies[0].rows){if(row.classList.contains('detail'))continue;
const show=(!s||row.dataset.sev.toLowerCase()===s)&&(!u||row.dataset.status.toLowerCase()===u)&&(!query||(row.dataset.q||'').includes(query));
row.style.display=show?'':'none';const detail=row.nextElementSibling;if(detail&&detail.classList.contains('detail')&&!show)detail.hidden=true;}}
sev.addEventListener('change',apply);st.addEventListener('change',apply);q.addEventListener('input',apply);
function toggle(id){const el=document.getElementById('d-'+id);if(el)el.hidden=!el.hidden;}
""";
}
