using System.Net;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class ExtensionWebsiteChecksTests
{
    private static ScanContext Context(string url = "http://127.0.0.1/")
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

    private static InspectionEvidence HtmlEvidence(string html, TimeSpan? duration = null, int? bodyPadBytes = null)
    {
        var body = Encoding.UTF8.GetBytes(html);
        if (bodyPadBytes is int pad && pad > body.Length)
        {
            var padded = new byte[pad];
            Buffer.BlockCopy(body, 0, padded, 0, body.Length);
            body = padded;
        }

        var target = Context().Target;
        return InspectionEvidence.Create(new InspectionEvidence
        {
            Target = target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Content-Type"] = "text/html",
                    },
                    Body = body,
                    ContentType = "text/html",
                    Duration = duration ?? TimeSpan.FromMilliseconds(10),
                }),
            },
        });
    }

    [Fact]
    public async Task Tls003_flags_obsolete_protocol()
    {
        var context = Context("https://127.0.0.1/");
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Tls = new TlsProbeResult("Tls11", "TLS_RSA_WITH_AES_128_CBC_SHA", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), "ABC", "CN=x", "CN=y"),
        });
        var result = await new TlsProtocolStrengthCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Tls003, result.CheckId.Value);
    }

    [Fact]
    public async Task Seo002_flags_overlong_title()
    {
        var title = new string('T', 80);
        var html = $"<!doctype html><html><head><title>{title}</title><meta name=\"description\" content=\"ok desc\"/></head><body></body></html>";
        var result = await new SeoMetadataBudgetCheck().EvaluateAsync(Context(), HtmlEvidence(html), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Seo002, result.CheckId.Value);
    }

    [Fact]
    public async Task Seo003_flags_noindex_with_canonical()
    {
        const string html = """
            <!doctype html><html><head>
            <title>t</title>
            <meta name="description" content="d"/>
            <meta name="robots" content="noindex"/>
            <link rel="canonical" href="http://127.0.0.1/"/>
            </head><body></body></html>
            """;
        var result = await new SeoIndexabilityCheck().EvaluateAsync(Context(), HtmlEvidence(html), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Seo003, result.CheckId.Value);
    }

    [Fact]
    public async Task Seo004_flags_missing_robots_probe()
    {
        var result = await new SeoRobotsSitemapCheck().EvaluateAsync(Context(), HtmlEvidence("<html><title>t</title></html>"), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Seo004, result.CheckId.Value);
    }

    [Fact]
    public async Task Link002_flags_broken_internal_crawl()
    {
        var context = Context();
        var broken = new Uri("http://127.0.0.1/missing");
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = context.Target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "text/html",
                    Body = Encoding.UTF8.GetBytes("<html></html>"),
                    Duration = TimeSpan.FromMilliseconds(1),
                }),
            },
            CrawledPages =
            [
                new CrawledPageEvidence(
                    new CrawlPage(broken, 1, context.Target.BaseUrl),
                    ProbeResult.Create(new ProbeResult
                    {
                        FinalUri = broken,
                        StatusCode = HttpStatusCode.NotFound,
                        Duration = TimeSpan.FromMilliseconds(1),
                    })),
            ],
        });
        var result = await new LinkInternalCrawlCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Link002, result.CheckId.Value);
    }

    [Fact]
    public async Task Link003_flags_empty_anchor_text()
    {
        var evidence = HtmlEvidence("""<html><body><a href="/x"></a></body></html>""");
        var result = await new LinkTextCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Link003, result.CheckId.Value);
    }

    [Fact]
    public async Task Perf002_flags_slow_duration()
    {
        var result = await new PerformanceTimingBudgetCheck().EvaluateAsync(
            Context(),
            HtmlEvidence("<html></html>", duration: TimeSpan.FromSeconds(2)),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Perf002, result.CheckId.Value);
    }

    [Fact]
    public async Task Perf003_flags_large_body()
    {
        var result = await new PerformanceBodySizeBudgetCheck().EvaluateAsync(
            Context(),
            HtmlEvidence("<html></html>", bodyPadBytes: 600_000),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Perf003, result.CheckId.Value);
    }

    [Fact]
    public async Task Change002_notes_volatile_fingerprint_when_baseline_lacks_content()
    {
        var context = Context();
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ContentFingerprint = "AAA",
            Baseline = Baseline.Create(new Baseline
            {
                Id = "b1",
                TargetId = "t1",
                ConfigurationFingerprint = "cfg",
                CreatedAt = DateTimeOffset.UtcNow,
            }),
        });
        var result = await new BaselineVolatilityCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Change002, result.CheckId.Value);
    }
}
