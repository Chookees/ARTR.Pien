using System.Net;
using System.Text;

using ARTR.Pien.Baselines;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class LegalAndHtmlReportExtensionTests
{
    private static ScanContext Context(string url = "https://shop.example/")
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "t1",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri(url),
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "standard",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        return new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
    }

    private static InspectionEvidence HtmlEvidence(ScanContext context, string html)
    {
        return InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = context.Target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Content-Type"] = "text/html",
                    },
                    Body = Encoding.UTF8.GetBytes(html),
                    ContentType = "text/html",
                    Duration = TimeSpan.FromMilliseconds(5),
                }),
            },
        });
    }

    [Fact]
    public async Task Impressum_check_fails_when_link_missing()
    {
        var context = Context();
        var result = await new LegalImpressumCheck().EvaluateAsync(
            context,
            HtmlEvidence(context, "<html><body><a href='/about'>About</a></body></html>"),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.NotNull(Assert.Single(result.Findings).Expected);
    }

    [Fact]
    public async Task Impressum_check_passes_when_link_present()
    {
        var context = Context();
        var result = await new LegalImpressumCheck().EvaluateAsync(
            context,
            HtmlEvidence(context, "<html><body><a href='/impressum'>Impressum</a></body></html>"),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Privacy_check_passes_for_datenschutz_link()
    {
        var context = Context();
        var result = await new LegalPrivacyPolicyCheck().EvaluateAsync(
            context,
            HtmlEvidence(context, "<html><body><a href='/datenschutz'>Datenschutz</a></body></html>"),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
    }

    [Fact]
    public async Task External_link_check_is_not_applicable_when_disabled()
    {
        var context = Context();
        var result = await new LinkExternalBrokenCheck().EvaluateAsync(
            context,
            HtmlEvidence(context, "<html></html>"),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.NotApplicable, result.Status);
    }

    [Fact]
    public void Baseline_comparer_classifies_new_and_resolved()
    {
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.Create("f1"),
            CheckId = CheckIds.Legal001,
            RuleVersion = "1.0.0",
            Title = "Impressum link missing",
            Summary = "Impressum link missing",
            Explanation = "missing",
            Severity = FindingSeverity.Medium,
            Status = FindingStatus.Fail,
            TargetId = "t1",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.Create("run1"),
        });
        var fp = FindingFingerprint.Compute(finding);
        var baseline = Baseline.Create(new Baseline
        {
            Id = "b1",
            TargetId = "t1",
            ConfigurationFingerprint = "cfg",
            FindingFingerprints = ["DEADBEEF"],
            CreatedAt = DateTimeOffset.UtcNow,
        });
        var rows = BaselineComparer.Compare(baseline, [finding]);
        Assert.Contains(rows, r => r.Kind == BaselineComparisonKind.New && r.FindingFingerprint == fp);
        Assert.Contains(rows, r => r.Kind == BaselineComparisonKind.Resolved && r.FindingFingerprint == "DEADBEEF");
    }

    [Fact]
    public async Task Html_report_contains_german_layout_filters_and_baseline()
    {
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.Create("f1"),
            CheckId = CheckIds.Legal001,
            RuleVersion = "1.0.0",
            Title = "Impressum link missing",
            Summary = "Impressum link missing",
            Explanation = "missing",
            Severity = FindingSeverity.Medium,
            Status = FindingStatus.Fail,
            TargetId = "t1",
            Expected = "link present",
            Observed = "missing",
            Remediation = "add link",
            Location = FindingLocation.Create("url", "https://shop.example/"),
            Evidence = [EvidenceExcerpt.Create("text/plain", "no impressum")],
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.Create("run-html"),
        });
        var document = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = ScanRunId.Create("run-html"),
            GeneratedAt = DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
            Findings = [finding],
            PolicyResult = PolicyResult.Create(new PolicyResult
            {
                PolicyName = "balanced",
                Passed = false,
                Summary = "failed",
            }),
            BaselineComparisons =
            [
                BaselineComparison.Create(new BaselineComparison
                {
                    FindingFingerprint = FindingFingerprint.Compute(finding),
                    Kind = BaselineComparisonKind.New,
                    CurrentFindingId = finding.Id,
                }),
            ],
            CategoryScores = new Dictionary<string, double>(StringComparer.Ordinal) { ["Discoverability"] = 40 },
        });

        await using var stream = new MemoryStream();
        await new HtmlReportExporter().ExportAsync(document, stream, TestContext.Current.CancellationToken);
        var html = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("lang=\"de\"", html, StringComparison.Ordinal);
        Assert.Contains("Prüfbericht", html, StringComparison.Ordinal);
        Assert.Contains("sevFilter", html, StringComparison.Ordinal);
        Assert.Contains("Baseline-Vergleich", html, StringComparison.Ordinal);
        Assert.Contains("Impressum link missing", html, StringComparison.Ordinal);
        Assert.Contains("add link", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script src=", html, StringComparison.OrdinalIgnoreCase);
    }
}
