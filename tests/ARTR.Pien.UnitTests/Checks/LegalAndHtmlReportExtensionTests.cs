using System.Net;
using System.Text;

using ARTR.Pien.Abstractions;
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
    private static ScanContext Context(string url = "https://shop.example/", ScanLimits? limits = null)
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
            Limits = limits ?? ScanLimits.Default,
        });
        return new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
    }

    private static InspectionEvidence HtmlEvidence(
        ScanContext context,
        string html,
        IReadOnlyList<CrawledPageEvidence>? crawled = null)
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
            CrawledPages = crawled ?? [],
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
    public async Task External_link_check_fails_on_broken_external_and_passes_when_ok()
    {
        var context = Context(limits: ScanLimits.Default with { CheckExternalLinks = true, MaxExternalLinks = 5 });
        var externalBroken = new Uri("https://cdn.example/missing.js");
        var externalOk = new Uri("https://cdn.example/ok.js");
        var sameHost = new Uri("https://shop.example/about");

        var fail = await new LinkExternalBrokenCheck().EvaluateAsync(
            context,
            HtmlEvidence(
                context,
                "<html></html>",
                [
                    new CrawledPageEvidence(
                        new CrawlPage(sameHost, 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = sameHost,
                            StatusCode = HttpStatusCode.NotFound,
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                    new CrawledPageEvidence(
                        new CrawlPage(externalBroken, 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = externalBroken,
                            StatusCode = HttpStatusCode.NotFound,
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, fail.Status);
        Assert.Equal(CheckIds.Link004, fail.CheckId.Value);

        var pass = await new LinkExternalBrokenCheck().EvaluateAsync(
            context,
            HtmlEvidence(
                context,
                "<html></html>",
                [
                    new CrawledPageEvidence(
                        new CrawlPage(externalOk, 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = externalOk,
                            StatusCode = HttpStatusCode.OK,
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, pass.Status);
    }

    [Fact]
    public async Task Impressum_check_fails_when_matched_link_is_broken()
    {
        var context = Context();
        var impressum = new Uri("https://shop.example/impressum");
        var result = await new LegalImpressumCheck().EvaluateAsync(
            context,
            HtmlEvidence(
                context,
                "<html><body><a href='/impressum'>Legal</a></body></html>",
                [
                    new CrawledPageEvidence(
                        new CrawlPage(impressum, 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = impressum,
                            StatusCode = HttpStatusCode.NotFound,
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Contains("broken", result.Findings[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Privacy_check_matches_link_text_and_crawled_html()
    {
        var context = Context();
        var about = new Uri("https://shop.example/about");
        var result = await new LegalPrivacyPolicyCheck().EvaluateAsync(
            context,
            HtmlEvidence(
                context,
                "<html><body><a href='/about'>About</a></body></html>",
                [
                    new CrawledPageEvidence(
                        new CrawlPage(about, 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = about,
                            StatusCode = HttpStatusCode.OK,
                            ContentType = "text/html",
                            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Content-Type"] = "text/html",
                            },
                            Body = Encoding.UTF8.GetBytes("<html><body><a href='/legal/privacy'>Privacy Policy</a></body></html>"),
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                    new CrawledPageEvidence(
                        new CrawlPage(new Uri("https://shop.example/asset.bin"), 1, context.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = new Uri("https://shop.example/asset.bin"),
                            StatusCode = HttpStatusCode.OK,
                            ContentType = "application/octet-stream",
                            Body = new byte[] { 1, 2, 3 },
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                ]),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
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

    [Fact]
    public async Task Html_report_covers_policy_pass_and_baseline_edge_rows()
    {
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.Create("f2"),
            CheckId = CheckIds.Legal002,
            RuleVersion = "1.0.0",
            Title = "ok",
            Summary = "ok",
            Explanation = "ok",
            Severity = FindingSeverity.Info,
            Status = FindingStatus.Pass,
            TargetId = "t1",
            BaselineState = "Unchanged",
            Evidence =
            [
                EvidenceExcerpt.Create("text/plain", "one"),
                EvidenceExcerpt.Create("text/plain", "two"),
            ],
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.Create("run-html-2"),
        });
        var document = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = ScanRunId.Create("run-html-2"),
            GeneratedAt = DateTimeOffset.Parse("2026-08-06T13:00:00Z"),
            Findings = [finding],
            PolicyResult = PolicyResult.Create(new PolicyResult
            {
                PolicyName = "balanced",
                Passed = true,
                Summary = "passed",
            }),
            BaselineComparisons =
            [
                BaselineComparison.Create(new BaselineComparison
                {
                    FindingFingerprint = "ABCDEF0123456789",
                    Kind = BaselineComparisonKind.Changed,
                    CurrentFindingId = finding.Id,
                }),
                BaselineComparison.Create(new BaselineComparison
                {
                    FindingFingerprint = "FEDCBA9876543210",
                    Kind = BaselineComparisonKind.Unchanged,
                }),
            ],
        });

        await using var stream = new MemoryStream();
        await new HtmlReportExporter().ExportAsync(document, stream, TestContext.Current.CancellationToken);
        var html = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("BESTANDEN", html, StringComparison.Ordinal);
        Assert.Contains("policy ok", html, StringComparison.Ordinal);
        Assert.Contains("—", html, StringComparison.Ordinal);
        Assert.Contains("one", html, StringComparison.Ordinal);
        Assert.Contains("two", html, StringComparison.Ordinal);

        var bare = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = ScanRunId.Create("run-html-3"),
            GeneratedAt = DateTimeOffset.UtcNow,
            Findings = [],
        });
        await using var bareStream = new MemoryStream();
        await new HtmlReportExporter().ExportAsync(bare, bareStream, TestContext.Current.CancellationToken);
        var bareHtml = Encoding.UTF8.GetString(bareStream.ToArray());
        Assert.DoesNotContain("Policy", bareHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Baseline-Vergleich", bareHtml, StringComparison.Ordinal);
    }
}
