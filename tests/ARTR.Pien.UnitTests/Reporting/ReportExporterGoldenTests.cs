using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Reporting;

public sealed class ReportExporterGoldenTests
{
    private static readonly DateTimeOffset FixedInstant = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

    private static ReportDocument Sample()
        => ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = ScanRunId.Create("run-golden"),
            GeneratedAt = FixedInstant,
            Findings =
            [
                Finding.Create(new Finding
                {
                    Id = FindingId.Create("f1"),
                    CheckId = "PIEN-HTTP-001",
                    RuleVersion = "1.0.0",
                    Title = "<script>alert(1)</script>",
                    Summary = "Unexpected status",
                    Explanation = "detail",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Fail,
                    TargetId = "t1",
                    Timestamp = FixedInstant,
                    RunId = ScanRunId.Create("run-golden"),
                }),
            ],
            PolicyResult = PolicyResult.Create(new PolicyResult
            {
                PolicyName = "balanced",
                Passed = false,
                Summary = "1 finding(s) failed policy.",
            }),
        });

    private static async Task<string> ExportAsync(IReportExporter exporter)
    {
        await using var stream = new MemoryStream();
        await exporter.ExportAsync(Sample(), stream, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Normalize(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        normalized = Regex.Replace(normalized, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})", "TIMESTAMP");
        return normalized;
    }

    [Theory]
    [InlineData(typeof(JsonReportExporter), "run-golden")]
    [InlineData(typeof(ConsoleReportExporter), "Pien report")]
    [InlineData(typeof(SarifReportExporter), "\"version\": \"2.1.0\"")]
    [InlineData(typeof(JUnitReportExporter), "<testsuite")]
    [InlineData(typeof(MarkdownReportExporter), "# ARTR Pien Report")]
    [InlineData(typeof(HtmlReportExporter), "<!DOCTYPE html>")]
    public async Task Exporters_emit_expected_markers(Type exporterType, string marker)
    {
        var exporter = (IReportExporter)Activator.CreateInstance(exporterType)!;
        var text = await ExportAsync(exporter);
        Assert.Contains(marker, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Html_encoder_escapes_untrusted_title()
    {
        var text = await ExportAsync(new HtmlReportExporter());
        Assert.DoesNotContain("<script>alert(1)</script>", text, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_golden_contains_stable_ids_and_policy()
    {
        var text = await ExportAsync(new JsonReportExporter());
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-golden", doc.RootElement.GetProperty("runId").GetProperty("value").GetString());
        Assert.Equal("f1", doc.RootElement.GetProperty("findings")[0].GetProperty("id").GetProperty("value").GetString());
        Assert.Equal("PIEN-HTTP-001", doc.RootElement.GetProperty("findings")[0].GetProperty("checkId").GetString());
        Assert.False(doc.RootElement.GetProperty("policyResult").GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task Sarif_golden_is_version_2_1_0_with_rule_and_result()
    {
        using var doc = JsonDocument.Parse(await ExportAsync(new SarifReportExporter()));
        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
        Assert.Contains("sarif-2.1.0", doc.RootElement.GetProperty("$schema").GetString(), StringComparison.Ordinal);
        var run = doc.RootElement.GetProperty("runs")[0];
        Assert.Equal("ARTR Pien", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Equal("PIEN-HTTP-001", run.GetProperty("tool").GetProperty("driver").GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.Equal("PIEN-HTTP-001", run.GetProperty("results")[0].GetProperty("ruleId").GetString());
        Assert.Equal("error", run.GetProperty("results")[0].GetProperty("level").GetString());
    }

    [Fact]
    public async Task Junit_golden_marks_failure_testcase()
    {
        var xml = XDocument.Parse(await ExportAsync(new JUnitReportExporter()));
        var suite = xml.Root!;
        Assert.Equal("ARTR.Pien", suite.Attribute("name")!.Value);
        Assert.Equal("1", suite.Attribute("tests")!.Value);
        Assert.Equal("1", suite.Attribute("failures")!.Value);
        var test = suite.Element("testcase")!;
        Assert.Equal("PIEN-HTTP-001", test.Attribute("classname")!.Value);
        Assert.NotNull(test.Element("failure"));
    }

    [Fact]
    public async Task Markdown_and_html_goldens_normalize_timestamps()
    {
        var markdown = Normalize(await ExportAsync(new MarkdownReportExporter()));
        Assert.Contains("- Run: `run-golden`", markdown, StringComparison.Ordinal);
        Assert.Contains("- Generated: TIMESTAMP", markdown, StringComparison.Ordinal);
        Assert.Contains("| High | `PIEN-HTTP-001` |", markdown, StringComparison.Ordinal);

        var html = Normalize(await ExportAsync(new HtmlReportExporter()));
        Assert.Contains("Run <code>run-golden</code> generated TIMESTAMP", html, StringComparison.Ordinal);
        Assert.Contains("<code>PIEN-HTTP-001</code>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
    }
}
