using System.Net;
using System.Text;

using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class WebsiteChecksTests
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

    private static InspectionEvidence Evidence(string html, HttpStatusCode status = HttpStatusCode.OK, IDictionary<string, string>? headers = null)
    {
        var hdrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "text/html",
        };
        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                hdrs[k] = v;
            }
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
                    StatusCode = status,
                    Headers = hdrs,
                    Body = Encoding.UTF8.GetBytes(html),
                    ContentType = "text/html",
                    Duration = TimeSpan.FromMilliseconds(10),
                }),
            },
        });
    }

    [Fact]
    public async Task Http_availability_passes_for_2xx()
    {
        var result = await new HttpAvailabilityCheck().EvaluateAsync(Context(), Evidence("<html><title>t</title><body></body></html>"), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Http_availability_fails_for_500()
    {
        var result = await new HttpAvailabilityCheck().EvaluateAsync(Context(), Evidence("err", HttpStatusCode.InternalServerError), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Redirect_check_detects_loop()
    {
        var context = Context();
        var uri = context.Target.BaseUrl;
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = uri,
                    StatusCode = HttpStatusCode.OK,
                    Duration = TimeSpan.FromMilliseconds(1),
                    RedirectChain = [uri, uri],
                }),
            },
        });
        var result = await new HttpRedirectCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Security_headers_report_missing()
    {
        var result = await new SecurityHeadersCheck().EvaluateAsync(Context(), Evidence("<html></html>"), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Html_and_a11y_and_seo_pass_on_complete_document()
    {
        const string html = "<!doctype html><html lang=\"en\"><head><title>Hello</title><meta name=\"description\" content=\"d\"/></head><body><img alt=\"x\"/></body></html>";
        var evidence = Evidence(html);
        Assert.Equal(FindingStatus.Pass, (await new HtmlStructureCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new AccessibilityFundamentalsCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new SeoFundamentalsCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Cookie_check_requires_secure_httponly_samesite()
    {
        var evidence = Evidence("<html></html>", headers: new Dictionary<string, string> { ["Set-Cookie"] = "a=b" });
        var result = await new CookieAttributeCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Link_check_flags_javascript_href()
    {
        var evidence = Evidence("<html><body><a href=\"javascript:alert(1)\">x</a></body></html>");
        var result = await new LinkSafetyCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Perf_check_passes_fast_small_body()
    {
        var result = await new PerformanceBudgetCheck().EvaluateAsync(Context(), Evidence("<html></html>"), TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Tls_checks_not_applicable_for_http()
    {
        var evidence = Evidence("<html></html>");
        Assert.Equal(FindingStatus.NotApplicable, (await new TlsFundamentalsCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.NotApplicable, (await new TlsExpirationCheck().EvaluateAsync(Context(), evidence, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Tls_expiration_fails_when_expired()
    {
        var context = Context("https://127.0.0.1/");
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Tls = new Abstractions.TlsProbeResult("Tls12", "TLS_AES_128_GCM_SHA256", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1), "ABC", "CN=x", "CN=y"),
        });
        var result = await new TlsExpirationCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }
}
