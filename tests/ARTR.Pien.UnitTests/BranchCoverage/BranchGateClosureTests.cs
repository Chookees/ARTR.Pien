using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Hosting;
using ARTR.Pien.Hosting.Notifications;
using ARTR.Pien.Hosting.Secrets;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Storage;
using ARTR.Pien.Text;
using ARTR.Pien.Web.Crawl;
using ARTR.Pien.Web.Network;
using ARTR.Pien.Web.Tls;
using ARTR.Pien.Web.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.UnitTests.BranchCoverage;

public sealed class BranchGateClosureTests
{
    private static ScanContext WebsiteContext(string url = "http://127.0.0.1/")
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

    private static ScanContext ApiContext(params PienApiCaseConfiguration[] cases)
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

    [Fact]
    public void Json_pointer_failure_and_type_branches()
    {
        using var doc = JsonDocument.Parse("""{"s":"hello","n":5.5,"a":[1],"o":{"~":1},"x":true}""");
        var root = doc.RootElement;
        var limits = ScanLimits.Default;

        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "eq" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "eq", Value = JsonSerializer.SerializeToElement("nope") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "contains", Value = JsonSerializer.SerializeToElement("x") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "contains", Value = JsonSerializer.SerializeToElement("zzz") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "range", Min = 1, Max = 2 },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Max = 1 },
            limits,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Min = 1 },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "length", Min = 1 },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "length", Min = 50 },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/a", Op = "length", Max = 0 },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "regex", Pattern = "x" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "regex" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "regex", Pattern = "^z" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "number" },
            limits,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "string" },
            limits,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "type", TypeName = "number" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/n", Op = "type", TypeName = "integer" },
            limits,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/a", Op = "type", TypeName = "array" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "weird" },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/a/x", Op = "exists" },
            limits,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/o/~0", Op = "exists" },
            limits,
            out _));
        Assert.Throws<ArgumentNullException>(() =>
            JsonPointerAssertions.TryEvaluate(root, null!, limits, out _));
        Assert.Throws<ArgumentNullException>(() =>
            JsonPointerAssertions.TryEvaluate(root, new PienJsonAssertionConfiguration { Pointer = "/", Op = "exists" }, null!, out _));
    }

    [Fact]
    public async Task Api_contract_incomplete_schema_and_status_branches()
    {
        var apiCase = new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            ExpectedStatus = JsonSerializer.SerializeToElement(201),
            ExpectedContentType = "application/json",
            JsonSchemaPath = "missing-schema.json",
            JsonAssertions =
            [
                new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
            ],
        };
        var ctx = ApiContext(apiCase);
        var probe = ProbeResult.Create(new ProbeResult
        {
            FinalUri = new Uri("http://127.0.0.1:5088/health"),
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "text/plain",
            },
            Body = Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
            Duration = TimeSpan.FromMilliseconds(1),
        });
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            WorkingDirectory = Path.GetTempPath(),
            ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(ctx, evidence, TestContext.Current.CancellationToken)).Status);

        var incomplete = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            ApiCaseResults = [new ApiCaseExecutionResult("unknown", "GET", null, null, false, null, [], [])],
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(ctx, incomplete, TestContext.Current.CancellationToken)).Status);

        var blockedNullReason = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            ApiCaseResults = [new ApiCaseExecutionResult("health", "POST", null, null, true, null, [], [])],
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(ctx, blockedNullReason, TestContext.Current.CancellationToken)).Status);

        var noPrimary = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ApiContext().Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal),
        });
        Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(ApiContext(), noPrimary, TestContext.Current.CancellationToken)).Status);

        var root = Path.Combine(Path.GetTempPath(), "pien-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var schemaPath = Path.Combine(root, "schema.json");
            await File.WriteAllTextAsync(schemaPath, """{"type":"object"}""", TestContext.Current.CancellationToken);
            var rootedCase = new PienApiCaseConfiguration
            {
                Id = "rooted",
                Method = "GET",
                Path = "/",
                JsonSchemaPath = schemaPath,
                ExpectedStatus = JsonSerializer.SerializeToElement(new { foo = 1 }),
            };
            var rootedCtx = ApiContext(rootedCase);
            var okProbe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/"),
                StatusCode = HttpStatusCode.OK,
                Body = Encoding.UTF8.GetBytes("{}"),
                Duration = TimeSpan.FromMilliseconds(1),
            });
            var rootedEvidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = rootedCtx.Target,
                ApiCaseResults = [new ApiCaseExecutionResult("rooted", "GET", okProbe.FinalUri, okProbe, false, null, [], [])],
            });
            Assert.Equal(FindingStatus.Pass, (await new ApiContractCheck().EvaluateAsync(rootedCtx, rootedEvidence, TestContext.Current.CancellationToken)).Status);

            var errors = await LocalJsonSchemaValidator.ValidateAsync("{", "{}", TestContext.Current.CancellationToken);
            Assert.NotEmpty(errors);
            await Assert.ThrowsAsync<ArgumentException>(() => LocalJsonSchemaValidator.ValidateAsync(" ", "{}", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Core_models_null_nested_and_expected_status()
    {
        Assert.True(new ExpectedStatusConstraint(200, 299).Matches(HttpStatusCode.OK));
        Assert.False(new ExpectedStatusConstraint(200, 299).Matches(HttpStatusCode.NotFound));

        Assert.ThrowsAny<Exception>(() => CheckDefinition.Create(new CheckDefinition
        {
            Id = null!,
            Name = "n",
            Description = "d",
            Category = CheckCategory.Reliability,
            DefaultSeverity = FindingSeverity.Low,
            RuleVersion = "1.0.0",
        }));
        Assert.ThrowsAny<Exception>(() => CheckResult.Create(new CheckResult
        {
            CheckId = null!,
            Status = FindingStatus.Pass,
            Findings = [],
        }));
        Assert.ThrowsAny<Exception>(() => CheckResult.Create(new CheckResult
        {
            CheckId = CheckId.Create("PIEN-HTTP-001"),
            Status = FindingStatus.Pass,
            Findings = null!,
        }));
        Assert.ThrowsAny<Exception>(() => ProbeRequest.Create(new ProbeRequest
        {
            Uri = null!,
            Method = ProbeMethod.Get,
        }));
        Assert.ThrowsAny<Exception>(() => ProbeRequest.Create(new ProbeRequest
        {
            Uri = new Uri("http://127.0.0.1/"),
            Method = ProbeMethod.Get,
            Headers = null!,
        }));
        Assert.ThrowsAny<Exception>(() => ProbeRequest.Create(new ProbeRequest
        {
            Uri = new Uri("/relative", UriKind.Relative),
            Method = ProbeMethod.Get,
        }));
        Assert.ThrowsAny<Exception>(() => ScanTarget.Create(new ScanTarget
        {
            Id = "t",
            Kind = ScanTargetKind.Website,
            BaseUrl = null!,
            Authorization = new TargetAuthorization(true),
        }));
        Assert.ThrowsAny<Exception>(() => ScanTarget.Create(new ScanTarget
        {
            Id = "t",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("http://127.0.0.1/"),
            Authorization = null!,
        }));
        Assert.ThrowsAny<Exception>(() => ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = null!,
            Limits = ScanLimits.Default,
        }));
        Assert.ThrowsAny<Exception>(() => Policy.Policy.Create(new Policy.Policy
        {
            Name = "n",
            Description = "d",
            EnabledCheckIds = null!,
        }));
        Assert.ThrowsAny<Exception>(() => Baseline.Create(new Baseline
        {
            Id = "b",
            TargetId = "t",
            ConfigurationFingerprint = "c",
            ContentFingerprint = "x",
            FindingFingerprints = null!,
            CreatedAt = DateTimeOffset.UtcNow,
        }));
        Assert.ThrowsAny<Exception>(() => PolicyResult.Create(new PolicyResult
        {
            PolicyName = "p",
            Passed = true,
            FailedFindings = null!,
        }));
        Assert.Throws<ArgumentNullException>(() => SafeRegex.IsMatch(null!, "a", ScanLimits.Default));
        Assert.Throws<ArgumentNullException>(() => SafeRegex.IsMatch("a", "a", null!));
        Assert.Throws<ArgumentNullException>(() => PienConfigurationValidator.Validate(null!, "."));
        _ = new TimeProviderClock();
        Assert.True(DateTimeOffset.UtcNow - new TimeProviderClock().UtcNow < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Engine_ctor_null_guards_and_score_paths()
    {
        var transport = new StubTransport();
        var catalog = new CheckCatalog([]);
        var policy = new PolicyEvaluator();
        var score = new ScoreCalculator();
        var clock = new TimeProviderClock();

        Assert.Throws<ArgumentNullException>(() => new ScanEngine(null!, catalog, policy, score, clock));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(transport, null!, policy, score, clock));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(transport, catalog, null!, score, clock));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(transport, catalog, policy, null!, clock));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(transport, catalog, policy, score, null!));

        var def = CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = "n",
            Description = "d",
            Category = CheckCategory.Reliability,
            DefaultSeverity = FindingSeverity.High,
            RuleVersion = "1.0.0",
        });
        var pass = CheckResult.Create(new CheckResult { CheckId = def.Id, Status = FindingStatus.Pass, Findings = [] });
        var na = CheckResult.Create(new CheckResult { CheckId = def.Id, Status = FindingStatus.NotApplicable, Findings = [] });
        var skip = CheckResult.Create(new CheckResult { CheckId = def.Id, Status = FindingStatus.Skipped, Findings = [] });
        var fail = CheckResult.Create(new CheckResult { CheckId = def.Id, Status = FindingStatus.Fail, Findings = [] });
        var scores = score.ScoreByCategory([], [pass, na, skip, fail]);
        Assert.True(scores["PIEN-HTTP-001"] < 100);
    }

    [Fact]
    public async Task Storage_missing_dirs_and_report_save()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-store-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileScanStore(root);
            Directory.Delete(Path.Combine(root, "runs"), recursive: true);
            Directory.Delete(Path.Combine(root, "baselines"), recursive: true);
            Assert.Empty(await store.ListRecentAsync(3, TestContext.Current.CancellationToken));
            Assert.Equal(0, await store.CleanAsync(1, TestContext.Current.CancellationToken));
            Assert.Empty(await store.ListIdsAsync(TestContext.Current.CancellationToken));

            var target = ScanTarget.Create(new ScanTarget
            {
                Id = "t",
                Kind = ScanTargetKind.Website,
                BaseUrl = new Uri("http://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "quick",
                Targets = [target],
                Limits = ScanLimits.Default,
            });
            var plan = ScanPlan.Create(new ScanPlan { Definition = definition, SelectedChecks = [], Limits = definition.Limits });
            var run = ScanRun.Create(new ScanRun
            {
                Id = ScanRunId.NewId(),
                Plan = plan,
                Status = ScanRunStatus.Completed,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            var report = ReportDocument.Create(new ReportDocument
            {
                SchemaVersion = 1,
                RunId = run.Id,
                GeneratedAt = DateTimeOffset.UtcNow,
                Findings = [],
                PolicyResult = PolicyResult.Create(new PolicyResult
                {
                    PolicyName = "balanced",
                    Passed = false,
                    FailedFindings = [],
                    Summary = "fail",
                }),
            });
            await store.SaveRunAsync(run, report, TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetRunAsync(run.Id, TestContext.Current.CancellationToken));

            var baseline = Baseline.Create(new Baseline
            {
                Id = "b1",
                TargetId = "t",
                ConfigurationFingerprint = "c",
                ContentFingerprint = "x",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveAsync(baseline, TestContext.Current.CancellationToken);
            Assert.True(await store.DeleteAsync("b1", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveRunAsync(null!, null, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Web_classifier_transport_crawler_and_tls_guards()
    {
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Any));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.IPv6Any));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.None));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("172.15.0.1")));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("172.32.0.1")));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("192.167.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("ff02::1")));
        Assert.Throws<ArgumentNullException>(() => IpAddressClassifier.IsRestricted(null!));

        Assert.Throws<ArgumentNullException>(() => new SafeHttpTransport(null!, new NetworkSafetyOptions()));
        Assert.Throws<ArgumentNullException>(() => new SafeHttpTransport(new DestinationValidator(), null!));
        using (var transport = new SafeHttpTransport(new DestinationValidator(), new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] }, " "))
        {
            var ctx = WebsiteContext();
            await Assert.ThrowsAsync<ArgumentNullException>(() => transport.SendAsync(null!, ctx, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest { Uri = new Uri("http://127.0.0.1/"), Method = ProbeMethod.Get }),
                null!,
                TestContext.Current.CancellationToken));
        }

        Assert.Throws<ArgumentNullException>(() => new TlsProbe(null!));
        var tls = new TlsProbe(new DestinationValidator());
        await Assert.ThrowsAsync<ArgumentNullException>(() => tls.ProbeAsync(null!, ScanLimits.Default, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => tls.ProbeAsync(new Uri("https://127.0.0.1/"), null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TlsFailureException>(() => tls.ProbeAsync(new Uri("http://127.0.0.1/"), ScanLimits.Default, TestContext.Current.CancellationToken));

        var validator = new DestinationValidator();
        var endpoint = await validator.ValidateAsync(
            new Uri("http://localhost/"),
            new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["localhost", "127.0.0.1"] },
            TestContext.Current.CancellationToken);
        Assert.NotEmpty(endpoint.Addresses);

        await Assert.ThrowsAsync<TargetSafetyException>(() => validator.ValidateAsync(
            new Uri("http://127.0.0.1/"),
            new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["other"] },
            TestContext.Current.CancellationToken));

        var map = new MapTransport(new Dictionary<string, Func<ProbeResult>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/robots.txt"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1/robots.txt"),
                StatusCode = HttpStatusCode.NotFound,
                Duration = TimeSpan.FromMilliseconds(1),
            }),
            ["/sitemap.xml"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1/sitemap.xml"),
                StatusCode = HttpStatusCode.InternalServerError,
                Duration = TimeSpan.FromMilliseconds(1),
            }),
            ["/"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1/"),
                StatusCode = HttpStatusCode.OK,
                ContentType = "text/html",
                Body = Encoding.UTF8.GetBytes("""<html><body><a href="">empty</a><a href="javascript:alert(1)">x</a><a href="mailto:a@b.c">m</a><a href="/next">n</a></body></html>"""),
                Duration = TimeSpan.FromMilliseconds(1),
            }),
            ["/next"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1/next"),
                StatusCode = HttpStatusCode.OK,
                ContentType = "text/html",
                Body = Encoding.UTF8.GetBytes("<html></html>"),
                Duration = TimeSpan.FromMilliseconds(1),
            }),
        });
        var crawler = new WebsiteCrawler(map, new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] });
        var pages = new List<CrawlPage>();
        await foreach (var page in crawler.CrawlAsync(
            ScanTarget.Create(new ScanTarget
            {
                Id = "site",
                Kind = ScanTargetKind.Website,
                BaseUrl = new Uri("http://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
            }),
            ScanLimits.Default with { MaxCrawlPages = 3, MaxCrawlDepth = 1, MaxLinksPerPage = 2 },
            TestContext.Current.CancellationToken))
        {
            pages.Add(page);
        }

        Assert.NotEmpty(pages);
    }

    [Fact]
    public async Task Hosting_configure_null_and_webhook_signature_success()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddPien());
        var services = new ServiceCollection();
        services.AddPien();
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IScanEngine>());

        var handler = new OkHandler();
        var sender = new HmacWebhookNotificationSender(
            "https://example.com/hook",
            "secret://env/PIEN_WEBHOOK_TEST",
            new EnvSecretResolver("tok"),
            handler);
        Environment.SetEnvironmentVariable("PIEN_WEBHOOK_TEST", "tok");
        try
        {
            await sender.SendAsync(
                Notification.Create(new Notification
                {
                    Id = "n",
                    Title = "t",
                    Message = "m",
                    Severity = NotificationSeverity.Info,
                    CreatedAt = DateTimeOffset.UtcNow,
                    RunId = ScanRunId.NewId(),
                }),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, handler.Calls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIEN_WEBHOOK_TEST", null);
        }

        Assert.Throws<ArgumentNullException>(() => new HmacWebhookNotificationSender("https://example.com/h", null, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sender.SendAsync(null!, TestContext.Current.CancellationToken));

        var root = Path.Combine(Path.GetTempPath(), "pien-sec2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "abs.txt");
            await File.WriteAllTextAsync(file, "abs\n", TestContext.Current.CancellationToken);
            var resolver = new DefaultSecretResolver(root);
            await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!, TestContext.Current.CancellationToken));
            using var secret = await resolver.ResolveAsync(SecretReference.Parse("secret://file/" + file.Replace('\\', '/')), TestContext.Current.CancellationToken);
            Assert.Equal("abs", secret.Reveal());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reporting_console_policy_and_website_helper_evidence()
    {
        var runId = ScanRunId.Create("run-console");
        var document = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = runId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Findings =
            [
                Finding.Create(new Finding
                {
                    Id = FindingId.Create("f1"),
                    CheckId = "PIEN-HTTP-001",
                    RuleVersion = "1.0.0",
                    Title = "t",
                    Summary = "s",
                    Explanation = "e",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Fail,
                    TargetId = "t",
                    Timestamp = DateTimeOffset.UtcNow,
                    RunId = runId,
                }),
            ],
            PolicyResult = PolicyResult.Create(new PolicyResult
            {
                PolicyName = "balanced",
                Passed = false,
                FailedFindings = [],
                Summary = "policy failed",
            }),
        });
        await using var stream = new MemoryStream();
        await new ConsoleReportExporter().ExportAsync(document, stream, TestContext.Current.CancellationToken);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("FAIL", text, StringComparison.Ordinal);

        var ctx = WebsiteContext();
        var htmlFail = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = ctx.Target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "text/html",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "text/html" },
                    Body = Encoding.UTF8.GetBytes("<html><head></head><body><a href=\"http://127.0.0.1/x\">x</a></body></html>"),
                    Duration = TimeSpan.FromMilliseconds(5000),
                }),
            },
            CrawledPages =
            [
                new CrawledPageEvidence(
                    new CrawlPage(new Uri("http://127.0.0.1/x"), 1, new Uri("http://127.0.0.1/")),
                    ProbeResult.Create(new ProbeResult
                    {
                        FinalUri = new Uri("http://127.0.0.1/x"),
                        StatusCode = HttpStatusCode.NotFound,
                        Duration = TimeSpan.FromMilliseconds(1),
                    })),
            ],
        });
        Assert.Equal(FindingStatus.Fail, (await new SeoFundamentalsCheck().EvaluateAsync(ctx, htmlFail, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new LinkSafetyCheck().EvaluateAsync(ctx, htmlFail, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new PerformanceBudgetCheck().EvaluateAsync(ctx, htmlFail, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new AccessibilityFundamentalsCheck().EvaluateAsync(ctx, htmlFail, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Fail, (await new SecurityHeadersCheck().EvaluateAsync(ctx, htmlFail, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public void Validator_additional_reject_branches()
    {
        var cfg = new PienConfiguration
        {
            SchemaVersion = 1,
            Profile = "standard",
            Targets =
            [
                new PienTargetConfiguration
                {
                    Id = "api",
                    Kind = "api",
                    Url = "http://127.0.0.1/",
                    Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                    Authentication = new PienAuthenticationConfiguration { Scheme = "basic", SecretReference = "secret://env/X" },
                    ApiCases =
                    [
                        new PienApiCaseConfiguration
                        {
                            Id = "h",
                            Method = "GET",
                            Path = "/h",
                            Headers = new Dictionary<string, string> { ["Authorization"] = "raw" },
                            Query = new Dictionary<string, JsonElement>
                            {
                                ["token"] = JsonSerializer.SerializeToElement("raw"),
                            },
                            BodySecretReference = "secret://env/BODY",
                            ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200 }),
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "put",
                            Method = "PUT",
                            Path = "/p",
                            AllowNonIdempotent = true,
                            JsonAssertions =
                            [
                                new PienJsonAssertionConfiguration { Pointer = "/a", Op = "eq", Value = JsonSerializer.SerializeToElement(1) },
                            ],
                        },
                    ],
                },
            ],
            Network = new PienNetworkConfiguration
            {
                AllowPrivateNetworks = true,
                AllowedHosts = ["127.0.0.1"],
                ConnectTimeoutSeconds = 0,
                RequestTimeoutSeconds = 0,
                MaxRedirects = -1,
            },
            Notifications = new PienNotificationConfiguration
            {
                WebhookUrl = "https://example.com/h",
                WebhookSecretReference = "secret://env/H",
                Required = true,
                Events = ["ScanCompleted", "PolicyFailed"],
            },
            Baselines = new PienBaselinesConfiguration { Id = "b", CompareOnScan = true, FailOnNew = true },
            Storage = new PienStorageConfiguration { StateDirectory = ".pien", RetainRuns = 0 },
        };
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(cfg, Directory.GetCurrentDirectory()));
    }

    private sealed class StubTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                Duration = TimeSpan.FromMilliseconds(1),
            }));
    }

    private sealed class MapTransport : ISafeHttpTransport
    {
        private readonly Dictionary<string, Func<ProbeResult>> _map;

        public MapTransport(Dictionary<string, Func<ProbeResult>> map) => _map = map;

        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
        {
            var key = request.Uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (key.Length == 0)
            {
                key = "/";
            }

            return Task.FromResult(_map.TryGetValue(key, out var factory)
                ? factory()
                : ProbeResult.Create(new ProbeResult
                {
                    FinalUri = request.Uri,
                    StatusCode = HttpStatusCode.NotFound,
                    Duration = TimeSpan.FromMilliseconds(1),
                }));
        }
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.True(request.Headers.Contains("X-Pien-Signature"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class EnvSecretResolver : ISecretResolver
    {
        private readonly string _value;

        public EnvSecretResolver(string value) => _value = value;

        public Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolvedSecret.FromString(_value));
    }

    [Fact]
    public async Task Core_invalid_enums_excerpt_and_env_overrides()
    {
        Assert.ThrowsAny<Exception>(() => CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = "n",
            Description = "d",
            Category = (CheckCategory)999,
            DefaultSeverity = FindingSeverity.Low,
            RuleVersion = "1.0.0",
        }));
        Assert.ThrowsAny<Exception>(() => CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = "n",
            Description = "d",
            Category = CheckCategory.Reliability,
            DefaultSeverity = (FindingSeverity)999,
            RuleVersion = "1.0.0",
        }));
        Assert.ThrowsAny<Exception>(() => CheckResult.Create(new CheckResult
        {
            CheckId = CheckId.Create("PIEN-HTTP-001"),
            Status = (FindingStatus)999,
            Findings = [],
        }));
        Assert.ThrowsAny<Exception>(() => Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = "t",
            Summary = "s",
            Explanation = "e",
            Severity = (FindingSeverity)999,
            Status = FindingStatus.Pass,
            TargetId = "t",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.NewId(),
        }));
        Assert.ThrowsAny<Exception>(() => Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = "t",
            Summary = "s",
            Explanation = "e",
            Severity = FindingSeverity.Low,
            Status = (FindingStatus)999,
            TargetId = "t",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.NewId(),
        }));
        Assert.ThrowsAny<Exception>(() => Notification.Create(new Notification
        {
            Id = "n",
            Title = "t",
            Message = "m",
            Severity = (NotificationSeverity)999,
            CreatedAt = DateTimeOffset.UtcNow,
        }));
        Assert.Throws<ArgumentNullException>(() => new EvidenceExcerpt("text/plain", null!, false));
        Assert.False(new ExpectedStatusConstraint(200, 299).Matches(HttpStatusCode.BadRequest));

        var root = Path.Combine(Path.GetTempPath(), "pien-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "pien.json"), """{"schemaVersion":1,"profile":"quick","targets":[]}""", TestContext.Current.CancellationToken);
            var loader = new JsonConfigLoader();
            var loaded = await loader.LoadAsync(
                new ConfigLoadRequest(
                    Path.Combine(root, "pien.json"),
                    root,
                    null,
                    new Dictionary<string, string?>
                    {
                        ["ARTR_PIEN_FAIL_ON"] = "medium",
                        ["ARTR_PIEN_MAX_PAGES"] = "12",
                        ["ARTR_PIEN_ALLOW_PRIVATE_NETWORKS"] = "true",
                    }),
                TestContext.Current.CancellationToken);
            Assert.Equal("medium", loaded.Policies.FailOn);
            Assert.Equal(12, loaded.Crawl.MaxPages);
            Assert.True(loaded.Network.AllowPrivateNetworks);

            _ = await loader.LoadAsync(
                new ConfigLoadRequest(
                    Path.Combine(root, "pien.json"),
                    root,
                    null,
                    new Dictionary<string, string?>
                    {
                        ["ARTR_PIEN_FAIL_ON"] = " ",
                        ["ARTR_PIEN_MAX_PAGES"] = "nope",
                        ["ARTR_PIEN_ALLOW_PRIVATE_NETWORKS"] = "nope",
                    }),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Engine_api_methods_query_and_optional_webhook_failure()
    {
        var transport = new RecordingTransport();
        var catalog = new CheckCatalog([new PassCheck()]);
        var storeRoot = Path.Combine(Path.GetTempPath(), "pien-eng-gate-" + Guid.NewGuid().ToString("N"));
        var sender = new ThrowingWebhook();
        try
        {
            var engine = new ScanEngine(
                transport,
                catalog,
                new PolicyEvaluator(),
                new ScoreCalculator(),
                new TimeProviderClock(),
                store: new FileScanStore(storeRoot),
                notifications: sender);

            var api = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases =
                [
                    new PienApiCaseConfiguration { Id = "empty-method", Method = " ", Path = "/a" },
                    new PienApiCaseConfiguration { Id = "head", Method = "HEAD", Path = "/h" },
                    new PienApiCaseConfiguration { Id = "options", Method = "OPTIONS", Path = "/o" },
                    new PienApiCaseConfiguration { Id = "put", Method = "PUT", Path = "/p", AllowNonIdempotent = true, Body = "{}" },
                    new PienApiCaseConfiguration { Id = "patch", Method = "PATCH", Path = "/p", AllowNonIdempotent = true },
                    new PienApiCaseConfiguration { Id = "delete", Method = "DELETE", Path = "/d", AllowNonIdempotent = true },
                    new PienApiCaseConfiguration { Id = "trace", Method = "TRACE", Path = "/t" },
                    new PienApiCaseConfiguration
                    {
                        Id = "query",
                        Method = "GET",
                        Path = "relative",
                        Query = new Dictionary<string, JsonElement> { ["q"] = JsonSerializer.SerializeToElement(1) },
                        Headers = null,
                    },
                ],
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [api],
                Limits = ScanLimits.Default,
                EnabledCheckIds = ["PIEN-HTTP-001"],
                DisabledCheckIds = ["PIEN-SEO-001"],
            });
            var run = await engine.RunAsync(
                definition,
                new ScanEngineOptions
                {
                    Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    Notifications = new PienNotificationConfiguration
                    {
                        WebhookUrl = "https://example.com/hook",
                        Required = false,
                    },
                },
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(ScanRunStatus.Completed, run.Status);
            Assert.Contains(transport.Methods, m => m == ProbeMethod.Head);
            Assert.Contains(transport.Methods, m => m == ProbeMethod.Options);
            Assert.Contains(transport.Methods, m => m == ProbeMethod.Put);
            Assert.Contains(transport.Methods, m => m == ProbeMethod.Patch);
            Assert.Contains(transport.Methods, m => m == ProbeMethod.Delete);
            Assert.Equal(1, sender.Calls);
        }
        finally
        {
            if (Directory.Exists(storeRoot))
            {
                Directory.Delete(storeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Checks_perf_link_openapi_empty_paths()
    {
        var ctx = WebsiteContext();
        var noPrimary = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal),
        });
        Assert.Equal(FindingStatus.Pass, (await new PerformanceBudgetCheck().EvaluateAsync(ctx, noPrimary, TestContext.Current.CancellationToken)).Status);

        var large = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = ctx.Target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "text/html",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "text/html" },
                    Body = new byte[(2 * 1024 * 1024) + 1],
                    Duration = TimeSpan.FromMilliseconds(1),
                }),
            },
        });
        Assert.Equal(FindingStatus.Fail, (await new PerformanceBudgetCheck().EvaluateAsync(ctx, large, TestContext.Current.CancellationToken)).Status);

        var js = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = ctx.Target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
            {
                ["primary"] = ProbeResult.Create(new ProbeResult
                {
                    FinalUri = ctx.Target.BaseUrl,
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "text/html",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "text/html" },
                    Body = Encoding.UTF8.GetBytes("""<html><body><a href="javascript:alert(1)">x</a></body></html>"""),
                    Duration = TimeSpan.FromMilliseconds(1),
                }),
            },
        });
        Assert.Equal(FindingStatus.Fail, (await new LinkSafetyCheck().EvaluateAsync(ctx, js, TestContext.Current.CancellationToken)).Status);

        Assert.Equal(FindingStatus.Fail, (await new SecurityHeadersCheck().EvaluateAsync(ctx, noPrimary, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FindingStatus.Pass, (await new HttpRedirectCheck().EvaluateAsync(ctx, noPrimary, TestContext.Current.CancellationToken)).Status);

        var root = Path.Combine(Path.GetTempPath(), "pien-oa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var emptyPaths = Path.Combine(root, "empty.json");
            await File.WriteAllTextAsync(emptyPaths, """{"openapi":"3.0.3","info":{"title":"t","version":"1"},"paths":{}}""", TestContext.Current.CancellationToken);
            var target = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
                OpenApiDocument = emptyPaths,
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [target],
                Limits = ScanLimits.Default,
            });
            var openCtx = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
            Assert.Equal(FindingStatus.Fail, (await new OpenApiDocumentCheck().EvaluateAsync(
                openCtx,
                InspectionEvidence.Create(new InspectionEvidence { Target = target, WorkingDirectory = root }),
                TestContext.Current.CancellationToken)).Status);

            var bad = Path.Combine(root, "bad.json");
            await File.WriteAllTextAsync(bad, "{not-json", TestContext.Current.CancellationToken);
            var badTarget = ScanTarget.Create(target with { OpenApiDocument = bad });
            var badCtx = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, badTarget, static () => DateTimeOffset.UtcNow);
            Assert.Equal(FindingStatus.Fail, (await new OpenApiDocumentCheck().EvaluateAsync(
                badCtx,
                InspectionEvidence.Create(new InspectionEvidence { Target = badTarget, WorkingDirectory = root }),
                TestContext.Current.CancellationToken)).Status);

            var rangeCase = new PienApiCaseConfiguration
            {
                Id = "r",
                Method = "GET",
                Path = "/",
                ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200, max = 201 }),
            };
            var rangeCtx = ApiContext(rangeCase);
            var probe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/"),
                StatusCode = HttpStatusCode.NotFound,
                Body = Encoding.UTF8.GetBytes("{}"),
                Duration = TimeSpan.FromMilliseconds(1),
            });
            Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(
                rangeCtx,
                InspectionEvidence.Create(new InspectionEvidence
                {
                    Target = rangeCtx.Target,
                    ApiCaseResults = [new ApiCaseExecutionResult("r", "GET", probe.FinalUri, probe, false, null, [], [])],
                }),
                TestContext.Current.CancellationToken)).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Hosting_webhook_http_exception_and_secret_escape()
    {
        var handler = new ThrowingHttpHandler();
        var sender = new HmacWebhookNotificationSender(
            "https://example.com/hook",
            null,
            new EnvSecretResolver("x"),
            handler);
        await Assert.ThrowsAsync<NotificationException>(() => sender.SendAsync(
            Notification.Create(new Notification
            {
                Id = "n",
                Title = "t",
                Message = "m",
                Severity = NotificationSeverity.Info,
                CreatedAt = DateTimeOffset.UtcNow,
            }),
            TestContext.Current.CancellationToken));
        Assert.Equal(3, handler.Calls);

        var root = Path.Combine(Path.GetTempPath(), "pien-esc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var resolver = new DefaultSecretResolver(root);
            await Assert.ThrowsAsync<SecretResolutionException>(() =>
                resolver.ResolveAsync(SecretReference.Parse("secret://file/../outside.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Crawler_null_guards_and_robots_comments()
    {
        Assert.Throws<ArgumentNullException>(() => new WebsiteCrawler(null!, new NetworkSafetyOptions()));
        Assert.Throws<ArgumentNullException>(() => new WebsiteCrawler(new StubTransport(), null!));
        Assert.Throws<ArgumentNullException>(() => UriCanonicalizer.Canonicalize(null!));
        Assert.Equal("https://example.com/", UriCanonicalizer.Canonicalize(new Uri("https://example.com:443/")).AbsoluteUri);
    }

    private sealed class RecordingTransport : ISafeHttpTransport
    {
        public List<ProbeMethod> Methods { get; } = [];

        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
        {
            Methods.Add(request.Method);
            return Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                Body = Encoding.UTF8.GetBytes("{}"),
                ContentType = "application/json",
                Duration = TimeSpan.FromMilliseconds(1),
            }));
        }
    }

    private sealed class PassCheck : ICheck
    {
        public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = "n",
            Description = "d",
            Category = CheckCategory.Reliability,
            DefaultSeverity = FindingSeverity.High,
            RuleVersion = "1.0.0",
        });

        public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckResult.Create(new CheckResult { CheckId = Definition.Id, Status = FindingStatus.Pass, Findings = [] }));
    }

    private sealed class ThrowingWebhook : INotificationSender
    {
        public int Calls { get; private set; }

        public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new NotificationException("boom");
        }
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("network down");
        }
    }
}
