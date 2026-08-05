using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class CheckBranchCoverageTests
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
            Limits = ScanLimits.Default with { MaxRedirects = 2 },
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
            OpenApiDocument = null,
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
        string? html = null,
        HttpStatusCode status = HttpStatusCode.OK,
        IDictionary<string, string>? headers = null,
        TimeSpan? duration = null,
        byte[]? body = null,
        IReadOnlyList<Uri>? redirects = null,
        TlsProbeResult? tls = null,
        IReadOnlyList<CrawledPageEvidence>? crawled = null)
    {
        var hdrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                hdrs[k] = v;
            }
        }

        if (html is not null)
        {
            hdrs["Content-Type"] = "text/html";
        }

        var payload = body ?? (html is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(html));
        return InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            Tls = tls,
            CrawledPages = crawled ?? [],
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = context.Target.BaseUrl,
                    StatusCode = status,
                    Headers = hdrs,
                    Body = payload,
                    ContentType = hdrs.GetValueOrDefault("Content-Type"),
                    Duration = duration ?? TimeSpan.FromMilliseconds(5),
                    RedirectChain = redirects ?? [],
                }),
            },
        });
    }

    [Fact]
    public async Task Website_check_branches_cover_pass_fail_na()
    {
        var ctx = Website();
        var noProbes = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal),
        });
        Assert.Equal(FindingStatus.Fail, (await new HttpAvailabilityCheck().EvaluateAsync(ctx, noProbes, TestContext.Current.CancellationToken)).Status);

        var emptyProbe = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = ctx.Target.BaseUrl,
                    Duration = TimeSpan.Zero,
                }),
            },
        });
        Assert.Equal(FindingStatus.Fail, (await new HttpAvailabilityCheck().EvaluateAsync(ctx, emptyProbe, TestContext.Current.CancellationToken)).Status);

        var longRedirects = Ev(ctx, "<html></html>", redirects: [new Uri("http://127.0.0.1/a"), new Uri("http://127.0.0.1/b"), new Uri("http://127.0.0.1/c")]);
        Assert.Equal(FindingStatus.Fail, (await new HttpRedirectCheck().EvaluateAsync(ctx, longRedirects, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpRedirectCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>"), TestContext.Current.CancellationToken)).Status);

        var goodHeaders = new Dictionary<string, string>
        {
            ["Content-Security-Policy"] = "default-src 'self'",
            ["X-Content-Type-Options"] = "nosniff",
            ["Referrer-Policy"] = "no-referrer",
            ["Permissions-Policy"] = "geolocation=()",
        };
        Assert.Equal(FindingStatus.Pass, (await new SecurityHeadersCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>", headers: goodHeaders), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new ContentSecurityPolicyCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>", headers: goodHeaders), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new ContentSecurityPolicyCheck().EvaluateAsync(
            ctx,
            Ev(ctx, "<html></html>", headers: new Dictionary<string, string> { ["Content-Security-Policy"] = "script-src 'unsafe-inline'" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new ContentSecurityPolicyCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>"), TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Fail, (await new HtmlStructureCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HtmlStructureCheck().EvaluateAsync(
            ctx,
            Ev(ctx, body: Encoding.UTF8.GetBytes("{}"), headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" }),
            TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Fail, (await new AccessibilityFundamentalsCheck().EvaluateAsync(ctx, Ev(ctx, "<html><head><title>t</title></head><body><img/></body></html>"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CookieAttributeCheck().EvaluateAsync(ctx, Ev(ctx, "<html></html>"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new CookieAttributeCheck().EvaluateAsync(
            ctx,
            Ev(ctx, "<html></html>", headers: new Dictionary<string, string> { ["Set-Cookie"] = "a=b; Secure; HttpOnly; SameSite=Lax" }),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new SeoFundamentalsCheck().EvaluateAsync(ctx, Ev(ctx, "<html><head><title>t</title></head><body></body></html>"), TestContext.Current.CancellationToken)).Status);

        var crawledFail = Ev(
            ctx,
            "<html></html>",
            crawled:
            [
                new CrawledPageEvidence(
                    new CrawlPage(new Uri("http://127.0.0.1/broken"), 1, null),
                    ProbeResult.Create(new ProbeResult
                    {
                        FinalUri = new Uri("http://127.0.0.1/broken"),
                        StatusCode = HttpStatusCode.NotFound,
                        Duration = TimeSpan.FromMilliseconds(1),
                    })),
            ]);
        Assert.Equal(FindingStatus.Fail, (await new LinkSafetyCheck().EvaluateAsync(ctx, crawledFail, TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Fail, (await new PerformanceBudgetCheck().EvaluateAsync(
            ctx,
            Ev(ctx, "<html></html>", duration: TimeSpan.FromSeconds(4)),
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new PerformanceBudgetCheck().EvaluateAsync(
            ctx,
            Ev(ctx, body: new byte[(2 * 1024 * 1024) + 10], headers: new Dictionary<string, string> { ["Content-Type"] = "application/octet-stream" }),
            TestContext.Current.CancellationToken)).Status);

        var https = Website("https://127.0.0.1/");
        Assert.Equal(FindingStatus.Fail, (await new TlsFundamentalsCheck().EvaluateAsync(https, Ev(https), TestContext.Current.CancellationToken)).Status);
        var tlsOk = Ev(https, tls: new TlsProbeResult("Tls12", "TLS_AES_128_GCM_SHA256", DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(60), "ABC", "CN=x", "CN=y"));
        Assert.Equal(FindingStatus.Pass, (await new TlsFundamentalsCheck().EvaluateAsync(https, tlsOk, TestContext.Current.CancellationToken)).Status);
        var tlsErr = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = https.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["tls-error"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = https.Target.BaseUrl,
                    Duration = TimeSpan.Zero,
                    ErrorMessage = "boom",
                }),
            },
        });
        Assert.Equal(FindingStatus.Fail, (await new TlsFundamentalsCheck().EvaluateAsync(https, tlsErr, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new TlsExpirationCheck().EvaluateAsync(https, tlsOk, TestContext.Current.CancellationToken)).Status);
        var soon = Ev(https, tls: new TlsProbeResult("Tls12", "c", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7), "ABC", "CN=x", "CN=y"));
        Assert.Equal(FindingStatus.Fail, (await new TlsExpirationCheck().EvaluateAsync(https, soon, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public void Json_pointer_covers_ops_and_paths()
    {
        using var doc = JsonDocument.Parse("""{"s":"hello","n":5,"a":[1,2],"o":{"k":true},"z":null}""");
        var root = doc.RootElement;
        var limits = ScanLimits.Default;
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/missing", Op = "absent" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "absent" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "exists" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "contains", Value = JsonSerializer.SerializeToElement("ell") }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Min = 1, Max = 9 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Min = 10, Max = 20 }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "length", Min = 1, Max = 10 }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/a", Op = "length", Min = 2, Max = 2 }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "regex", Pattern = "^h" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "nope" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "s", Op = "exists" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/a/1", Op = "eq", Value = JsonSerializer.SerializeToElement(2) }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/a/9", Op = "exists" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/o/k", Op = "type", TypeName = "boolean" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/z", Op = "type", TypeName = "null" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/", Op = "type", TypeName = "object" }, limits, out _));
    }

    [Fact]
    public async Task Api_contract_and_openapi_and_change_branches()
    {
        var website = Website();
        Assert.Equal(FindingStatus.NotApplicable, (await new ApiContractCheck().EvaluateAsync(website, Ev(website), TestContext.Current.CancellationToken)).Status);

        var apiNoCases = Api();
        var failPrimary = Ev(apiNoCases, status: HttpStatusCode.InternalServerError);
        Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(apiNoCases, failPrimary, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new ApiContractCheck().EvaluateAsync(apiNoCases, Ev(apiNoCases, status: HttpStatusCode.OK), TestContext.Current.CancellationToken)).Status);

        var root = Path.Combine(Path.GetTempPath(), "pien-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var schemaPath = Path.Combine(root, "schema.json");
            await File.WriteAllTextAsync(schemaPath, """{"type":"object","required":["status"],"properties":{"status":{"type":"string"}}}""", TestContext.Current.CancellationToken);
            var openApiPath = Path.Combine(root, "openapi.json");
            await File.WriteAllTextAsync(openApiPath, """{"openapi":"3.0.3","info":{"title":"t","version":"1"},"paths":{"/health":{"get":{"responses":{"200":{"description":"ok"}}}}}}""", TestContext.Current.CancellationToken);
            var yamlPath = Path.Combine(root, "openapi.yaml");
            await File.WriteAllTextAsync(yamlPath, "openapi: 3.0.3\ninfo:\n  title: t\n  version: '1'\npaths:\n  /x:\n    get:\n      responses:\n        '200':\n          description: ok\n", TestContext.Current.CancellationToken);

            var apiCase = new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200, max = 299 }),
                ExpectedContentType = "json",
                ResponseTimeBudgetMs = 1000,
                RequiredHeaders = ["X-Request-Id"],
                ForbiddenHeaders = ["X-Debug"],
                JsonAssertions =
                [
                    new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
                ],
                JsonSchemaPath = "schema.json",
            };
            var ctx = Api(apiCase);
            var probe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/health"),
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = "application/json",
                    ["X-Request-Id"] = "1",
                },
                Body = Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
                Duration = TimeSpan.FromMilliseconds(5),
            });
            var evidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = ctx.Target,
                WorkingDirectory = root,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            });
            Assert.Equal(FindingStatus.Pass, (await new ApiContractCheck().EvaluateAsync(ctx, evidence, TestContext.Current.CancellationToken)).Status);

            var badProbe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = probe.FinalUri,
                StatusCode = HttpStatusCode.OK,
                ContentType = "text/plain",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Debug"] = "1" },
                Body = Encoding.UTF8.GetBytes("not-json"),
                Duration = TimeSpan.FromMilliseconds(2000),
            });
            var badEvidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = ctx.Target,
                WorkingDirectory = root,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", badProbe.FinalUri, badProbe, false, null, [], [])],
            });
            Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(ctx, badEvidence, TestContext.Current.CancellationToken)).Status);

            var openTarget = ScanTarget.Create(ctx.Target with { OpenApiDocument = "openapi.json" });
            var openDefinition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [openTarget],
                Limits = ScanLimits.Default,
            });
            var openContext = new ScanContext(ScanRunId.NewId(), openDefinition, openDefinition.Limits, openTarget, static () => DateTimeOffset.UtcNow);
            var openEvidence = InspectionEvidence.Create(new InspectionEvidence { Target = openTarget, WorkingDirectory = root });
            Assert.Equal(FindingStatus.Pass, (await new OpenApiDocumentCheck().EvaluateAsync(openContext, openEvidence, TestContext.Current.CancellationToken)).Status);

            var yamlTarget = ScanTarget.Create(openTarget with { OpenApiDocument = "openapi.yaml" });
            var yamlContext = new ScanContext(ScanRunId.NewId(), openDefinition, openDefinition.Limits, yamlTarget, static () => DateTimeOffset.UtcNow);
            Assert.Equal(FindingStatus.Pass, (await new OpenApiDocumentCheck().EvaluateAsync(
                yamlContext,
                InspectionEvidence.Create(new InspectionEvidence { Target = yamlTarget, WorkingDirectory = root }),
                TestContext.Current.CancellationToken)).Status);

            var missingTarget = ScanTarget.Create(openTarget with { OpenApiDocument = "missing.json" });
            var missingContext = new ScanContext(ScanRunId.NewId(), openDefinition, openDefinition.Limits, missingTarget, static () => DateTimeOffset.UtcNow);
            Assert.Equal(FindingStatus.Fail, (await new OpenApiDocumentCheck().EvaluateAsync(
                missingContext,
                InspectionEvidence.Create(new InspectionEvidence { Target = missingTarget, WorkingDirectory = root }),
                TestContext.Current.CancellationToken)).Status);

            Assert.Equal(FindingStatus.NotApplicable, (await new BaselineChangeCheck().EvaluateAsync(ctx, evidence, TestContext.Current.CancellationToken)).Status);
            var changePass = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = ctx.Target,
                ContentFingerprint = "AAA",
                TlsCertificateFingerprint = "TLS",
                Baseline = Baseline.Create(new Baseline
                {
                    Id = "b1",
                    TargetId = "api",
                    ConfigurationFingerprint = "cfg",
                    ContentFingerprint = "AAA",
                    TlsCertificateFingerprint = "TLS",
                    CreatedAt = DateTimeOffset.UtcNow,
                }),
            });
            Assert.Equal(FindingStatus.Pass, (await new BaselineChangeCheck().EvaluateAsync(ctx, changePass, TestContext.Current.CancellationToken)).Status);
            var tlsDrift = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = ctx.Target,
                ContentFingerprint = "AAA",
                TlsCertificateFingerprint = "OTHER",
                Baseline = changePass.Baseline,
            });
            Assert.Equal(FindingStatus.Fail, (await new BaselineChangeCheck().EvaluateAsync(ctx, tlsDrift, TestContext.Current.CancellationToken)).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
