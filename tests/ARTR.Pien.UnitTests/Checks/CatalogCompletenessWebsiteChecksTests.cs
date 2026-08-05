using System.Net;
using System.Text;

using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class CatalogCompletenessWebsiteChecksTests
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

    private static InspectionEvidence ProbeEvidence(
        ScanContext context,
        HttpStatusCode status = HttpStatusCode.OK,
        string? contentType = "text/html; charset=utf-8",
        string body = "<html></html>",
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyList<Uri>? redirects = null,
        Uri? finalUri = null)
    {
        var hdrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (contentType is not null)
        {
            hdrs["Content-Type"] = contentType;
        }

        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                hdrs[k] = v;
            }
        }

        return InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = finalUri ?? context.Target.BaseUrl,
                    StatusCode = status,
                    Headers = hdrs,
                    Body = Encoding.UTF8.GetBytes(body),
                    ContentType = contentType,
                    Duration = TimeSpan.FromMilliseconds(5),
                    RedirectChain = redirects ?? [],
                }),
            },
        });
    }

    [Fact]
    public async Task Http003_flags_https_to_http_downgrade()
    {
        var context = Context("https://127.0.0.1/");
        var evidence = ProbeEvidence(
            context,
            finalUri: new Uri("http://127.0.0.1/"),
            redirects: [new Uri("http://127.0.0.1/")]);
        var result = await new HttpHttpsDowngradeCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Http003, result.CheckId.Value);
    }

    [Fact]
    public async Task Http004_flags_excessive_redirect_count()
    {
        var context = Context();
        var redirects = Enumerable.Range(0, context.Limits.MaxRedirects + 1)
            .Select(i => new Uri($"http://127.0.0.1/r{i}"))
            .ToArray();
        var result = await new HttpExcessiveRedirectsCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, redirects: redirects),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Http004, result.CheckId.Value);
    }

    [Fact]
    public async Task Http005_flags_server_error()
    {
        var context = Context();
        var result = await new HttpServerErrorCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, status: HttpStatusCode.InternalServerError),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Http005, result.CheckId.Value);
    }

    [Fact]
    public async Task Http006_flags_missing_charset_on_html()
    {
        var context = Context();
        var result = await new HttpContentTypeCharsetCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, contentType: "text/html"),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Http006, result.CheckId.Value);
    }

    [Fact]
    public async Task Headers003_flags_missing_hsts_on_https()
    {
        var context = Context("https://127.0.0.1/");
        var result = await new HstsHeaderCheck().EvaluateAsync(
            context,
            ProbeEvidence(context),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Headers003, result.CheckId.Value);
    }

    [Fact]
    public async Task Headers004_flags_missing_frame_protection()
    {
        var context = Context();
        var result = await new FrameAncestorsCheck().EvaluateAsync(
            context,
            ProbeEvidence(context),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Headers004, result.CheckId.Value);
    }

    [Fact]
    public async Task Headers005_flags_missing_isolation_headers()
    {
        var context = Context();
        var result = await new CrossOriginIsolationHeadersCheck().EvaluateAsync(
            context,
            ProbeEvidence(context),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Headers005, result.CheckId.Value);
    }

    [Fact]
    public async Task Headers006_flags_server_disclosure()
    {
        var context = Context();
        var result = await new ServerDisclosureHeadersCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, headers: new Dictionary<string, string> { ["Server"] = "nginx" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Headers006, result.CheckId.Value);
    }

    [Fact]
    public async Task Cookie002_flags_insecure_cookie_over_http()
    {
        var context = Context("http://127.0.0.1/");
        var result = await new CookieOverHttpCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, headers: new Dictionary<string, string> { ["Set-Cookie"] = "sid=x; Path=/" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Cookie002, result.CheckId.Value);
    }

    [Fact]
    public async Task Cookie003_flags_host_prefix_violations()
    {
        var context = Context("https://127.0.0.1/");
        var result = await new CookiePrefixRulesCheck().EvaluateAsync(
            context,
            ProbeEvidence(
                context,
                headers: new Dictionary<string, string> { ["Set-Cookie"] = "__Host-sid=x; Secure; Path=/app" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Cookie003, result.CheckId.Value);
    }

    [Fact]
    public async Task Html002_flags_missing_viewport_and_charset()
    {
        const string html = "<!doctype html><html><head><title>t</title></head><body></body></html>";
        var context = Context();
        var result = await new HtmlMetaViewportCharsetCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Html002, result.CheckId.Value);
    }

    [Fact]
    public async Task Html003_flags_mixed_content_on_https()
    {
        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><img src="http://cdn.example/x.png"/></body></html>
            """;
        var context = Context("https://127.0.0.1/");
        var result = await new HtmlMixedContentCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Html003, result.CheckId.Value);
    }

    [Fact]
    public async Task Html004_flags_duplicate_ids()
    {
        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><div id="dup"></div><span id="dup"></span></body></html>
            """;
        var context = Context();
        var result = await new HtmlDuplicateIdHeadingCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Html004, result.CheckId.Value);
    }

    [Fact]
    public async Task A11y002_flags_unlabeled_form_controls()
    {
        const string html = """
            <!doctype html><html lang="en"><head><title>t</title></head>
            <body><form><input type="text" name="q"/></form></body></html>
            """;
        var context = Context();
        var result = await new AccessibilityFormLabelCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.A11y002, result.CheckId.Value);
    }

    [Fact]
    public async Task A11y003_flags_empty_links_and_buttons()
    {
        const string html = """
            <!doctype html><html lang="en"><head><title>t</title></head>
            <body><button></button></body></html>
            """;
        var context = Context();
        var result = await new AccessibilityEmptyControlsCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.A11y003, result.CheckId.Value);
    }

    [Fact]
    public async Task A11y004_flags_heading_level_jumps()
    {
        const string html = """
            <!doctype html><html lang="en"><head><title>t</title></head>
            <body><h1>One</h1><h3>Three</h3></body></html>
            """;
        var context = Context();
        var result = await new AccessibilityHeadingJumpCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.A11y004, result.CheckId.Value);
    }

    [Fact]
    public async Task A11y005_flags_positive_tabindex()
    {
        const string html = """
            <!doctype html><html lang="en"><head><title>t</title></head>
            <body><a href="/" tabindex="2">x</a></body></html>
            """;
        var context = Context();
        var result = await new AccessibilityPositiveTabindexCheck().EvaluateAsync(
            context,
            ProbeEvidence(context, body: html),
            TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.A11y005, result.CheckId.Value);
    }
}
