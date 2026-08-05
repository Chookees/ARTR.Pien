using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Reporting;

public sealed class ReportExporterGoldenTests
{
    private static ReportDocument Sample()
        => ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = ScanRunId.Create("run-golden"),
            GeneratedAt = DateTimeOffset.Parse("2026-08-05T12:00:00Z"),
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
                    Timestamp = DateTimeOffset.Parse("2026-08-05T12:00:00Z"),
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
        await using var stream = new MemoryStream();
        await exporter.ExportAsync(Sample(), stream, TestContext.Current.CancellationToken);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains(marker, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Html_encoder_escapes_untrusted_title()
    {
        var exporter = new HtmlReportExporter();
        await using var stream = new MemoryStream();
        await exporter.ExportAsync(Sample(), stream, TestContext.Current.CancellationToken);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("<script>alert(1)</script>", text, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", text, StringComparison.Ordinal);
    }
}
