using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class CatalogCompletenessBranchCoverageTests
{
    private static ScanContext Website(string url = "http://127.0.0.1/")
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

    private static ScanContext Api(params PienApiCaseConfiguration[] cases)
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "api",
            Kind = ScanTargetKind.Api,
            BaseUrl = new Uri("http://127.0.0.1:5088/"),
            Authorization = new TargetAuthorization(true),
            ApiCases = cases,
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "api",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        return new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
    }

    private static InspectionEvidence Ev(
        ScanContext context,
        string body = "<!doctype html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width\"/><meta name=\"description\" content=\"d\"/><title>t</title></head><body><h1>One</h1><h2>Two</h2><form><label for=\"q\">Q</label><input id=\"q\" type=\"text\"/></form><a href=\"/x\">link</a><button>Go</button></body></html>",
        HttpStatusCode status = HttpStatusCode.OK,
        string? contentType = "text/html; charset=utf-8",
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyList<Uri>? redirects = null,
        Uri? finalUri = null,
        IReadOnlyList<CrawledPageEvidence>? crawled = null)
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
            CrawledPages = crawled ?? [],
        });
    }

    private static InspectionEvidence Empty(ScanContext context)
        => InspectionEvidence.Create(new InspectionEvidence { Target = context.Target });

    [Fact]
    public async Task Website_catalog_pass_and_na_branches()
    {
        var http = Website();
        var https = Website("https://127.0.0.1/");
        var goodHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Strict-Transport-Security"] = "max-age=31536000",
            ["X-Frame-Options"] = "DENY",
            ["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'",
            ["Cross-Origin-Opener-Policy"] = "same-origin",
            ["Cross-Origin-Resource-Policy"] = "same-origin",
            ["Cross-Origin-Embedder-Policy"] = "require-corp",
            ["Set-Cookie"] = "__Host-sid=x; Secure; Path=/; HttpOnly; SameSite=Lax",
        };

        Assert.Equal(FindingStatus.NotApplicable, (await new HttpHttpsDowngradeCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpHttpsDowngradeCheck().EvaluateAsync(https, Ev(https), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpHttpsDowngradeCheck().EvaluateAsync(https, Empty(https), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new HttpHttpsDowngradeCheck().EvaluateAsync(
            https,
            Ev(https, redirects: [new Uri("http://127.0.0.1/hop")], finalUri: new Uri("https://127.0.0.1/")),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new HttpExcessiveRedirectsCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpExcessiveRedirectsCheck().EvaluateAsync(http, Empty(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpExcessiveRedirectsCheck().EvaluateAsync(
            http,
            Ev(http, redirects: [new Uri("http://127.0.0.1/a")]),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new HttpServerErrorCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new HttpServerErrorCheck().EvaluateAsync(
            http,
            Ev(
                http,
                crawled:
                [
                    new CrawledPageEvidence(
                        new CrawlPage(new Uri("http://127.0.0.1/x"), 1, http.Target.BaseUrl),
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = new Uri("http://127.0.0.1/x"),
                            StatusCode = HttpStatusCode.BadGateway,
                            Duration = TimeSpan.FromMilliseconds(1),
                        })),
                ]),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new HttpContentTypeCharsetCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpContentTypeCharsetCheck().EvaluateAsync(
            http,
            Ev(http, contentType: "application/json"),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpContentTypeCharsetCheck().EvaluateAsync(http, Empty(http), TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.NotApplicable, (await new HstsHeaderCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HstsHeaderCheck().EvaluateAsync(https, Ev(https, headers: goodHeaders), TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new FrameAncestorsCheck().EvaluateAsync(http, Ev(http, headers: goodHeaders), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new FrameAncestorsCheck().EvaluateAsync(http, Empty(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new FrameAncestorsCheck().EvaluateAsync(
            http,
            Ev(http, headers: new Dictionary<string, string> { ["Content-Security-Policy"] = "frame-ancestors 'none'" }),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new CrossOriginIsolationHeadersCheck().EvaluateAsync(http, Ev(http, headers: goodHeaders), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CrossOriginIsolationHeadersCheck().EvaluateAsync(
            http,
            Ev(http, headers: new Dictionary<string, string> { ["Cross-Origin-Opener-Policy"] = "same-origin" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CrossOriginIsolationHeadersCheck().EvaluateAsync(http, Empty(http), TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new ServerDisclosureHeadersCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new ServerDisclosureHeadersCheck().EvaluateAsync(
            http,
            Ev(http, headers: new Dictionary<string, string> { ["X-Powered-By"] = "aspnet" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new ServerDisclosureHeadersCheck().EvaluateAsync(http, Empty(http), TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.NotApplicable, (await new CookieOverHttpCheck().EvaluateAsync(https, Ev(https), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CookieOverHttpCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new CookieOverHttpCheck().EvaluateAsync(
            http,
            Ev(http, headers: new Dictionary<string, string> { ["Set-Cookie"] = "a=b; Secure; Path=/" }),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new CookiePrefixRulesCheck().EvaluateAsync(https, Ev(https, headers: goodHeaders), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CookiePrefixRulesCheck().EvaluateAsync(https, Ev(https), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new CookiePrefixRulesCheck().EvaluateAsync(
            https,
            Ev(https, headers: new Dictionary<string, string> { ["Set-Cookie"] = "__Secure-x=1; Path=/" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CookiePrefixRulesCheck().EvaluateAsync(
            https,
            Ev(https, headers: new Dictionary<string, string> { ["Set-Cookie"] = "__Secure-x=1; Secure; Path=/" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new CookiePrefixRulesCheck().EvaluateAsync(
            https,
            Ev(https, headers: new Dictionary<string, string> { ["Set-Cookie"] = "__Host-x=1; Secure; Domain=example.com; Path=/" }),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new HtmlMetaViewportCharsetCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HtmlMetaViewportCharsetCheck().EvaluateAsync(
            http,
            Ev(http, contentType: "application/json", body: "{}"),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.NotApplicable, (await new HtmlMixedContentCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HtmlMixedContentCheck().EvaluateAsync(https, Ev(https), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HtmlMixedContentCheck().EvaluateAsync(
            https,
            Ev(https, contentType: "text/plain", body: "x"),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new HtmlDuplicateIdHeadingCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new HtmlDuplicateIdHeadingCheck().EvaluateAsync(
            http,
            Ev(http, body: "<html><body><h1>a</h1><h3>b</h3></body></html>"),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new AccessibilityFormLabelCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new AccessibilityFormLabelCheck().EvaluateAsync(
            http,
            Ev(http, body: "<html><body><input type=\"hidden\" name=\"h\"/><input type=\"submit\" value=\"s\"/><label><input type=\"text\" name=\"wrapped\"/></label><input aria-label=\"named\" type=\"text\"/></body></html>"),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new AccessibilityEmptyControlsCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new AccessibilityEmptyControlsCheck().EvaluateAsync(
            http,
            Ev(http, body: "<html><body><a href=\"/\" aria-label=\"home\"></a><button title=\"go\"></button></body></html>"),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Pass, (await new AccessibilityHeadingJumpCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new AccessibilityPositiveTabindexCheck().EvaluateAsync(http, Ev(http), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new AccessibilityPositiveTabindexCheck().EvaluateAsync(
            http,
            Ev(http, body: "<html><body><a href=\"/\" tabindex=\"0\">x</a><a href=\"/\" tabindex=\"-1\">y</a></body></html>"),
            TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Api_catalog_pass_and_edge_branches()
    {
        var website = Website();
        Assert.Equal(FindingStatus.NotApplicable, (await new ApiExpectedStatusContentTypeCheck().EvaluateAsync(website, Ev(website), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.NotApplicable, (await new ApiJsonAssertionCheck().EvaluateAsync(website, Ev(website), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.NotApplicable, (await new ApiLocalJsonSchemaCheck().EvaluateAsync(website, Ev(website), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.NotApplicable, (await new ApiNonIdempotentGuardCheck().EvaluateAsync(website, Ev(website), TestContext.Current.CancellationToken)).Status);

        var apiCase = new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            ExpectedStatus = JsonDocument.Parse("200").RootElement.Clone(),
            ExpectedContentType = "application/json",
            JsonAssertions =
            [
                new PienJsonAssertionConfiguration { Pointer = "/ok", Op = "eq", Value = JsonDocument.Parse("true").RootElement.Clone() },
            ],
        };
        var context = Api(apiCase);
        var probe = ProbeResult.Create(new ProbeResult
        {
            FinalUri = new Uri("http://127.0.0.1:5088/health"),
            StatusCode = HttpStatusCode.OK,
            ContentType = "application/json",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
            Body = Encoding.UTF8.GetBytes("""{"ok":true}"""),
            Duration = TimeSpan.FromMilliseconds(5),
        });
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
        });
        Assert.Equal(FindingStatus.Pass, (await new ApiExpectedStatusContentTypeCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new ApiJsonAssertionCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new ApiExpectedStatusContentTypeCheck().EvaluateAsync(
            Api(),
            InspectionEvidence.Create(new InspectionEvidence { Target = Api().Target }),
            TestContext.Current.CancellationToken)).Status);

        var statusFail = Api(new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            ExpectedStatus = JsonDocument.Parse("""{"min":200,"max":299}""").RootElement.Clone(),
        });
        var badStatusProbe = ProbeResult.Create(new ProbeResult
        {
            FinalUri = new Uri("http://127.0.0.1:5088/health"),
            StatusCode = HttpStatusCode.NotFound,
            Duration = TimeSpan.FromMilliseconds(1),
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiExpectedStatusContentTypeCheck().EvaluateAsync(
            statusFail,
            InspectionEvidence.Create(new InspectionEvidence
            {
                Target = statusFail.Target,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", badStatusProbe.FinalUri, badStatusProbe, false, null, [], [])],
            }),
            TestContext.Current.CancellationToken)).Status);

        var jsonFailCtx = Api(new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            JsonAssertions = [new PienJsonAssertionConfiguration { Pointer = "/ok", Op = "eq", Value = JsonDocument.Parse("true").RootElement.Clone() }],
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiJsonAssertionCheck().EvaluateAsync(
            jsonFailCtx,
            InspectionEvidence.Create(new InspectionEvidence
            {
                Target = jsonFailCtx.Target,
                ApiCaseResults =
                [
                    new ApiCaseExecutionResult(
                        "health",
                        "GET",
                        probe.FinalUri,
                        ProbeResult.Create(new ProbeResult
                        {
                            FinalUri = probe.FinalUri,
                            StatusCode = HttpStatusCode.OK,
                            Body = Encoding.UTF8.GetBytes("not-json"),
                            Duration = TimeSpan.FromMilliseconds(1),
                        }),
                        false,
                        null,
                        [],
                        []),
                ],
            }),
            TestContext.Current.CancellationToken)).Status);

        var dir = Path.Combine(Path.GetTempPath(), "pien-schema-pass-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "schema.json"),
                """{"type":"object","required":["ok"],"properties":{"ok":{"type":"boolean"}}}""",
                TestContext.Current.CancellationToken);
            var schemaCase = new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                JsonSchemaPath = "schema.json",
            };
            var schemaCtx = Api(schemaCase);
            var schemaEvidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = schemaCtx.Target,
                WorkingDirectory = dir,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            });
            Assert.Equal(FindingStatus.Pass, (await new ApiLocalJsonSchemaCheck().EvaluateAsync(schemaCtx, schemaEvidence, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(FindingStatus.Fail, (await new ApiLocalJsonSchemaCheck().EvaluateAsync(
                Api(new PienApiCaseConfiguration { Id = "health", Method = "GET", Path = "/health", JsonSchemaPath = "missing.json" }),
                InspectionEvidence.Create(new InspectionEvidence
                {
                    Target = schemaCtx.Target,
                    WorkingDirectory = dir,
                    ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
                }),
                TestContext.Current.CancellationToken)).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        var getOnly = Api(new PienApiCaseConfiguration { Id = "health", Method = "GET", Path = "/health" });
        Assert.Equal(FindingStatus.Pass, (await new ApiNonIdempotentGuardCheck().EvaluateAsync(
            getOnly,
            InspectionEvidence.Create(new InspectionEvidence
            {
                Target = getOnly.Target,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            }),
            TestContext.Current.CancellationToken)).Status);

        var allowedPost = Api(new PienApiCaseConfiguration
        {
            Id = "create",
            Method = "POST",
            Path = "/items",
            AllowNonIdempotent = true,
        });
        Assert.Equal(FindingStatus.Pass, (await new ApiNonIdempotentGuardCheck().EvaluateAsync(
            allowedPost,
            InspectionEvidence.Create(new InspectionEvidence
            {
                Target = allowedPost.Target,
                ApiCaseResults = [new ApiCaseExecutionResult("create", "POST", new Uri("http://127.0.0.1:5088/items"), probe, false, null, [], [])],
            }),
            TestContext.Current.CancellationToken)).Status);

        var unallowed = Api(new PienApiCaseConfiguration
        {
            Id = "create",
            Method = "POST",
            Path = "/items",
            AllowNonIdempotent = false,
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiNonIdempotentGuardCheck().EvaluateAsync(
            unallowed,
            InspectionEvidence.Create(new InspectionEvidence { Target = unallowed.Target }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new ApiNonIdempotentGuardCheck().EvaluateAsync(
            unallowed,
            InspectionEvidence.Create(new InspectionEvidence
            {
                Target = unallowed.Target,
                ApiCaseResults = [new ApiCaseExecutionResult("create", "POST", new Uri("http://127.0.0.1:5088/items"), probe, false, null, [], [])],
            }),
            TestContext.Current.CancellationToken)).Status);
    }
}
