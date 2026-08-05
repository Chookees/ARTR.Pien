using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Hosting.Notifications;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Storage;

namespace ARTR.Pien.UnitTests.Engine;

public sealed class EngineStageCoverageTests
{
    private sealed class MapTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                ContentType = request.Uri.AbsolutePath.Contains("health", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/html",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = request.Uri.AbsolutePath.Contains("health", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/html",
                    ["Content-Security-Policy"] = "default-src 'self'",
                    ["X-Content-Type-Options"] = "nosniff",
                    ["Referrer-Policy"] = "no-referrer",
                    ["Permissions-Policy"] = "geolocation=()",
                },
                Body = request.Uri.AbsolutePath.Contains("health", StringComparison.OrdinalIgnoreCase)
                    ? Encoding.UTF8.GetBytes("""{"status":"ok"}""")
                    : Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><head><title>ok</title><meta name=\"description\" content=\"d\"/></head><body><a href=\"/about\">x</a><img alt=\"a\"/></body></html>"),
                Duration = TimeSpan.FromMilliseconds(5),
            }));
    }

    private sealed class FakeCrawler : ICrawler
    {
        public async IAsyncEnumerable<CrawlPage> CrawlAsync(
            ScanTarget target,
            ScanLimits limits,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new CrawlPage(target.BaseUrl, 0, null);
            yield return new CrawlPage(new Uri(target.BaseUrl, "/about"), 1, target.BaseUrl);
            await Task.CompletedTask;
        }
    }

    private sealed class FakeTls : ITlsProbe
    {
        private readonly bool _fail;

        public FakeTls(bool fail = false) => _fail = fail;

        public Task<TlsProbeResult> ProbeAsync(Uri uri, ScanLimits limits, CancellationToken cancellationToken = default)
        {
            if (_fail)
            {
                throw new TlsFailureException("tls fail");
            }

            return Task.FromResult(new TlsProbeResult(
                "Tls12",
                "TLS_AES_128_GCM_SHA256",
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow.AddDays(60),
                "FINGERPRINT",
                "CN=test",
                "CN=issuer"));
        }
    }

    private sealed class FakeCheck : ICheck
    {
        public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = "HTTP",
            Description = "test",
            Category = CheckCategory.Reliability,
            DefaultSeverity = FindingSeverity.High,
            RuleVersion = "1.0.0",
        });

        public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckResult.Create(new CheckResult
            {
                CheckId = Definition.Id,
                Status = FindingStatus.Pass,
                Findings = [],
            }));
    }

    private sealed class ThrowingCheck : ICheck
    {
        public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTML-001"),
            Name = "HTML",
            Description = "test",
            Category = CheckCategory.ContentQuality,
            DefaultSeverity = FindingSeverity.Low,
            RuleVersion = "1.0.0",
        });

        public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
            => throw new ConfigurationException("check boom");
    }

    private sealed class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
    }

    private sealed class EnvSecretResolver : ISecretResolver
    {
        public Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolvedSecret.FromString("secret"));
    }

    [Fact]
    public async Task Engine_runs_crawl_tls_api_store_and_optional_webhook_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-eng-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FileScanStore(Path.Combine(root, ".pien"));
            var catalog = new CheckCatalog([new FakeCheck(), new ThrowingCheck()]);
            var webhook = new HmacWebhookNotificationSender(
                "https://example.com/hook",
                null,
                new EnvSecretResolver(),
                new FailHandler());
            var engine = new ScanEngine(
                new MapTransport(),
                catalog,
                new PolicyEvaluator(),
                new ScoreCalculator(),
                new TimeProviderClock(),
                store,
                store,
                new FakeCrawler(),
                new FakeTls(),
                webhook);

            var website = ScanTarget.Create(new ScanTarget
            {
                Id = "web",
                Kind = ScanTargetKind.Website,
                BaseUrl = new Uri("https://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
            });
            var api = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases =
                [
                    new PienApiCaseConfiguration { Id = "health", Method = "GET", Path = "/health" },
                    new PienApiCaseConfiguration { Id = "post", Method = "POST", Path = "/x", AllowNonIdempotent = false },
                    new PienApiCaseConfiguration { Id = "weird", Method = "TRACE", Path = "/x" },
                    new PienApiCaseConfiguration
                    {
                        Id = "query",
                        Method = "GET",
                        Path = "/health",
                        Query = new Dictionary<string, JsonElement> { ["q"] = JsonSerializer.SerializeToElement("1") },
                        Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
                        Body = """{"a":1}""",
                        ContentType = "application/json",
                    },
                ],
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "standard",
                Targets = [website, api],
                Limits = ScanLimits.Default with { MaxCrawlPages = 3, MaxCrawlDepth = 2 },
                EnabledCheckIds = ["PIEN-HTTP-001", "PIEN-HTML-001"],
                DisabledCheckIds = ["PIEN-SEO-001"],
            });

            var progress = new List<ScanProgress>();
            var run = await engine.RunAsync(
                definition,
                new ScanEngineOptions
                {
                    Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                    WorkingDirectory = root,
                    Notifications = new PienNotificationConfiguration { WebhookUrl = "https://example.com/hook", Required = false },
                    RetainRuns = 2,
                    CompareBaseline = true,
                    BaselineId = "missing",
                },
                new Progress<ScanProgress>(p => progress.Add(p)),
                TestContext.Current.CancellationToken);

            Assert.Equal(ScanRunStatus.Completed, run.Status);
            Assert.NotEmpty(progress);
            Assert.NotEmpty(await store.ListRecentAsync(10, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Engine_tls_failure_and_unauthorized_target()
    {
        var catalog = new CheckCatalog([new FakeCheck()]);
        var engine = new ScanEngine(
            new MapTransport(),
            catalog,
            new PolicyEvaluator(),
            new ScoreCalculator(),
            new TimeProviderClock(),
            tlsProbe: new FakeTls(fail: true));
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "web",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("https://127.0.0.1/"),
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default,
            EnabledCheckIds = ["PIEN-HTTP-001"],
        });
        var run = await engine.RunAsync(
            definition,
            new ScanEngineOptions
            {
                Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                WorkingDirectory = Directory.GetCurrentDirectory(),
            },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ScanRunStatus.Completed, run.Status);

        var denied = ScanTarget.Create(target with { Authorization = new TargetAuthorization(false) });
        var deniedDefinition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [denied],
            Limits = ScanLimits.Default,
            EnabledCheckIds = ["PIEN-HTTP-001"],
        });
        await Assert.ThrowsAsync<AuthorizationException>(() => engine.RunAsync(
            deniedDefinition,
            new ScanEngineOptions
            {
                Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                WorkingDirectory = Directory.GetCurrentDirectory(),
            },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Policy_and_score_calculator_branches()
    {
        var policy = Policy.Policy.Create(new Policy.Policy
        {
            Name = "strict",
            Description = "t",
            FailOnSeverityAtOrAbove = FindingSeverity.Medium,
        });
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = "x",
            Summary = "x",
            Explanation = "x",
            Severity = FindingSeverity.High,
            Status = FindingStatus.Fail,
            TargetId = "t",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.NewId(),
        });
        var result = new PolicyEvaluator().Evaluate(policy, [finding]);
        Assert.False(result.Passed);

        var scores = new ScoreCalculator().ScoreByCategory(
            [finding],
            [
                CheckResult.Create(new CheckResult { CheckId = CheckId.Create("PIEN-HTTP-001"), Status = FindingStatus.Fail, Findings = [finding] }),
                CheckResult.Create(new CheckResult { CheckId = CheckId.Create("PIEN-HTML-001"), Status = FindingStatus.Pass, Findings = [] }),
            ]);
        Assert.Equal(0, scores["PIEN-HTTP-001"]);
        Assert.Equal(100, scores["PIEN-HTML-001"]);
    }

    [Fact]
    public void Check_catalog_list_and_get()
    {
        var catalog = new CheckCatalog([new FakeCheck()]);
        Assert.Single(catalog.List());
        Assert.NotNull(catalog.Get(CheckId.Create("PIEN-HTTP-001")));
        Assert.Null(catalog.Get(CheckId.Create("PIEN-MISSING-001")));
    }
}
