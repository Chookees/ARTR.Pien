using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Hosting.Secrets;
using ARTR.Pien.Limits;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Storage;
using ARTR.Pien.Web.Network;

namespace ARTR.Pien.UnitTests.BranchCoverage;

public sealed class BranchCoverageExpansionTests
{
    [Fact]
    public void Json_pointer_ops_cover_all_failure_and_type_edges()
    {
        using var doc = JsonDocument.Parse(
            """{"s":"hello","n":5,"f":1.5,"a":[1,{"x":2}],"o":{"k":true,"~slash":"v"},"z":null,"b":true}""");
        var root = doc.RootElement;
        var limits = ScanLimits.Default;

        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "eq" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "eq", Value = JsonSerializer.SerializeToElement("nope") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "contains", Value = JsonSerializer.SerializeToElement("x") }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/s", Op = "contains", Value = JsonSerializer.SerializeToElement("zzz") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "range", Min = 1, Max = 2 }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Max = 9 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Max = 1 }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "range", Min = 1 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "length", Min = 1 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "length", Min = 10 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/a", Op = "length", Max = 0 }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "regex" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "regex", Pattern = "." }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "regex", Pattern = "^z" }, limits, out _));

        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "", Op = "type", TypeName = "object" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "string" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "type", TypeName = "string" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "type", TypeName = "number" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "number" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/n", Op = "type", TypeName = "integer" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/f", Op = "type", TypeName = "integer" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/b", Op = "type", TypeName = "boolean" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "boolean" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/o", Op = "type", TypeName = "object" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "object" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/a", Op = "type", TypeName = "array" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "array" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/z", Op = "type", TypeName = "null" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "null" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type", TypeName = "weird" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s", Op = "type" }, limits, out _));

        Assert.True(JsonPointerAssertions.TryEvaluate(
            root,
            new PienJsonAssertionConfiguration { Pointer = "/o/~0slash", Op = "eq", Value = JsonSerializer.SerializeToElement("v") },
            limits,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/a/-1", Op = "exists" }, limits, out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/s/0", Op = "exists" }, limits, out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            root, new PienJsonAssertionConfiguration { Pointer = "/a/1/x", Op = "eq", Value = JsonSerializer.SerializeToElement(2) }, limits, out _));
    }

    [Fact]
    public async Task Api_contract_covers_status_schema_and_incomplete_edges()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-api2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var schemaPath = Path.Combine(root, "schema.json");
            await File.WriteAllTextAsync(
                schemaPath,
                """{"type":"object","required":["status"],"properties":{"status":{"type":"string"}}}""",
                TestContext.Current.CancellationToken);

            var apiCase = new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                ExpectedStatus = JsonSerializer.SerializeToElement(200),
                ExpectedContentType = "json",
                JsonAssertions =
                [
                    new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
                ],
                JsonSchemaPath = schemaPath,
            };
            var target = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases = [apiCase],
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [target],
                Limits = ScanLimits.Default,
            });
            var ctx = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);

            var okProbe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/health"),
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
                Duration = TimeSpan.FromMilliseconds(1),
            });
            Assert.Equal(
                FindingStatus.Pass,
                (await new ApiContractCheck().EvaluateAsync(
                    ctx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = target,
                        WorkingDirectory = root,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", okProbe.FinalUri, okProbe, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            var wrongStatus = ProbeResult.Create(new ProbeResult
            {
                FinalUri = okProbe.FinalUri,
                StatusCode = HttpStatusCode.Created,
                ContentType = okProbe.ContentType,
                Headers = okProbe.Headers,
                Body = okProbe.Body.ToArray(),
                Duration = okProbe.Duration,
            });
            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    ctx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = target,
                        WorkingDirectory = root,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", wrongStatus.FinalUri, wrongStatus, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            var rangeCase = new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200, max = 201 }),
                JsonSchemaPath = "missing-schema.json",
            };
            var rangeTarget = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases = [rangeCase],
            });
            var rangeCtx = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, rangeTarget, static () => DateTimeOffset.UtcNow);
            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    rangeCtx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = rangeTarget,
                        WorkingDirectory = root,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", okProbe.FinalUri, okProbe, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            var missProbe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = okProbe.FinalUri,
                StatusCode = HttpStatusCode.NotFound,
                ContentType = okProbe.ContentType,
                Headers = okProbe.Headers,
                Body = okProbe.Body.ToArray(),
                Duration = okProbe.Duration,
            });
            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    rangeCtx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = rangeTarget,
                        WorkingDirectory = root,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", missProbe.FinalUri, missProbe, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    ctx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = target,
                        ApiCaseResults = [new ApiCaseExecutionResult("unknown", "GET", null, null, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    ctx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = target,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", null, null, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            var assertFail = ProbeResult.Create(new ProbeResult
            {
                FinalUri = okProbe.FinalUri,
                StatusCode = HttpStatusCode.OK,
                ContentType = okProbe.ContentType,
                Headers = okProbe.Headers,
                Body = Encoding.UTF8.GetBytes("""{"status":"bad"}"""),
                Duration = okProbe.Duration,
            });
            Assert.Equal(
                FindingStatus.Fail,
                (await new ApiContractCheck().EvaluateAsync(
                    ctx,
                    InspectionEvidence.Create(new InspectionEvidence
                    {
                        Target = target,
                        WorkingDirectory = root,
                        ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", assertFail.FinalUri, assertFail, false, null, [], [])],
                    }),
                    TestContext.Current.CancellationToken)).Status);

            var noCases = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases = [],
            });
            var noCasesCtx = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, noCases, static () => DateTimeOffset.UtcNow);
            var emptyPrimary = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = noCases,
                Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal),
            });
            Assert.Equal(FindingStatus.Fail, (await new ApiContractCheck().EvaluateAsync(noCasesCtx, emptyPrimary, TestContext.Current.CancellationToken)).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Validator_covers_remaining_auth_limit_and_status_edges()
    {
        Assert.Throws<ArgumentNullException>(() => PienConfigurationValidator.Validate(null!, "x"));
        Assert.Throws<ArgumentException>(() => PienConfigurationValidator.Validate(new PienConfiguration(), " "));

        var emptyTargets = new PienConfiguration { SchemaVersion = 1, Profile = "standard", Targets = [] };
        Assert.Throws<ConfigurationException>(() =>
            PienConfigurationValidator.Validate(emptyTargets, Directory.GetCurrentDirectory()));

        var dup = ValidBase();
        dup.Targets =
        [
            new PienTargetConfiguration
            {
                Id = "a",
                Kind = "website",
                Url = "http://127.0.0.1/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
            },
            new PienTargetConfiguration
            {
                Id = "a",
                Kind = "website",
                Url = "http://127.0.0.1/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
            },
        ];
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(dup, Directory.GetCurrentDirectory()));

        var badUrl = ValidBase();
        badUrl.Targets =
        [
            new PienTargetConfiguration
            {
                Id = "t",
                Kind = "website",
                Url = "ftp://127.0.0.1/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
            },
        ];
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(badUrl, Directory.GetCurrentDirectory()));

        var badKind = ValidBase();
        badKind.Targets =
        [
            new PienTargetConfiguration
            {
                Id = "t",
                Kind = "desktop",
                Url = "http://127.0.0.1/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
            },
        ];
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(badKind, Directory.GetCurrentDirectory()));

        var authNone = ValidBase();
        authNone.Targets =
        [
            new PienTargetConfiguration
            {
                Id = "t",
                Kind = "api",
                Url = "http://127.0.0.1/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                Authentication = new PienAuthenticationConfiguration { Scheme = "none" },
                ApiCases =
                [
                    new PienApiCaseConfiguration
                    {
                        Id = "ok",
                        Method = "GET",
                        Path = "/",
                        ExpectedStatus = JsonSerializer.SerializeToElement(50),
                    },
                    new PienApiCaseConfiguration
                    {
                        Id = "range-lo",
                        Method = "GET",
                        Path = "/",
                        ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 50, max = 200 }),
                    },
                    new PienApiCaseConfiguration
                    {
                        Id = "range-hi",
                        Method = "GET",
                        Path = "/",
                        ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200, max = 999 }),
                    },
                    new PienApiCaseConfiguration
                    {
                        Id = "range-flip",
                        Method = "GET",
                        Path = "/",
                        ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 300, max = 200 }),
                    },
                    new PienApiCaseConfiguration
                    {
                        Id = "auth-header",
                        Method = "GET",
                        Path = "/",
                        Authentication = new PienAuthenticationConfiguration
                        {
                            Scheme = "header",
                            SecretReference = "secret://env/X",
                            HeaderName = "X-Api-Key",
                            UsernameSecretReference = "secret://env/USER",
                        },
                    },
                    new PienApiCaseConfiguration
                    {
                        Id = "auth-bad-user",
                        Method = "GET",
                        Path = "/",
                        Authentication = new PienAuthenticationConfiguration
                        {
                            Scheme = "basic",
                            SecretReference = "secret://env/X",
                            UsernameSecretReference = "not-a-secret",
                        },
                    },
                ],
            },
        ];
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(authNone, Directory.GetCurrentDirectory()));

        foreach (var (pages, depth, links, redirects, connect, request, retain) in new[]
                 {
                     (HardLimits.MaxCrawlPages + 1, 1, 10, 5, 5, 5, 1),
                     (10, HardLimits.MaxCrawlDepth + 1, 10, 5, 5, 5, 1),
                     (10, 1, HardLimits.MaxLinksPerPage + 1, 5, 5, 5, 1),
                     (10, 1, 10, HardLimits.MaxRedirects + 1, 5, 5, 1),
                     (10, 1, 10, 5, 0, 5, 1),
                     (10, 1, 10, 5, 5, 0, 1),
                     (10, 1, 10, 5, 121, 5, 1),
                     (10, 1, 10, 5, 5, 301, 1),
                     (10, 1, 10, 5, 5, 5, 0),
                     (10, 1, 10, 0, 5, 5, 1),
                 })
        {
            var cfg = ValidBase();
            cfg.Crawl = new PienCrawlConfiguration { MaxPages = pages, MaxDepth = depth, MaxLinksPerPage = links };
            cfg.Network = new PienNetworkConfiguration
            {
                AllowPrivateNetworks = true,
                AllowedHosts = ["127.0.0.1"],
                MaxRedirects = redirects,
                ConnectTimeoutSeconds = connect,
                RequestTimeoutSeconds = request,
            };
            cfg.Storage = new PienStorageConfiguration { RetainRuns = retain };
            Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(cfg, Directory.GetCurrentDirectory()));
        }

        var goodHttpsHook = ValidBase();
        goodHttpsHook.Notifications = new PienNotificationConfiguration
        {
            WebhookUrl = "https://example.com/hook",
            WebhookSecretReference = "secret://env/HOOK",
            Events = ["ScanCompleted"],
        };
        goodHttpsHook.Watch = new PienWatchConfiguration { IntervalSeconds = 86_400 };
        goodHttpsHook.Policies = new PienPolicyConfiguration
        {
            Name = "balanced",
            FailOn = "high",
            SeverityOverrides = new Dictionary<string, string> { ["PIEN-HTTP-001"] = "low" },
            Suppressions = [new PienSuppressionConfiguration { CheckId = "PIEN-HTTP-001", Reason = "ok" }],
        };
        var validated = PienConfigurationValidator.Validate(goodHttpsHook, Directory.GetCurrentDirectory());
        Assert.Equal(FindingSeverity.Low, validated.Policy.SeverityOverrides["PIEN-HTTP-001"]);

        var blankProfile = new PienConfiguration();
        PienConfigurationValidator.ApplyProfileDefaults(blankProfile, " ");
        Assert.Equal(ScanProfileNames.Standard, blankProfile.Profile);

        Assert.True(new ExpectedStatusConstraint(200, 299).Matches(HttpStatusCode.OK));
        Assert.False(new ExpectedStatusConstraint(200, 299).Matches(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Secret_resolver_covers_absolute_path_and_escape_guards()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-sec2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var inside = Path.Combine(root, "inside.txt");
            await File.WriteAllTextAsync(inside, "abs\n", TestContext.Current.CancellationToken);
            var resolver = new DefaultSecretResolver(root);

            using (var secret = await resolver.ResolveAsync(
                       SecretReference.Parse("secret://file/" + inside.Replace('\\', '/')),
                       TestContext.Current.CancellationToken))
            {
                Assert.Equal("abs", secret.Reveal());
            }

            await Assert.ThrowsAsync<SecretResolutionException>(() =>
                resolver.ResolveAsync(SecretReference.Parse("secret://file/../outside.txt"), TestContext.Current.CancellationToken));

            var ctor = typeof(SecretReference)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 2);
            var bogus = (SecretReference)ctor.Invoke([(SecretScheme)99, "x"]);
            await Assert.ThrowsAsync<SecretResolutionException>(() =>
                resolver.ResolveAsync(bogus, TestContext.Current.CancellationToken));
            Assert.Throws<InvalidOperationException>(() => _ = bogus.Uri);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Engine_covers_blocked_methods_query_paths_and_null_guards()
    {
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(null!, new CheckCatalog([]), new PolicyEvaluator(), new ScoreCalculator(), new TimeProviderClock()));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(new MapTransport(), null!, new PolicyEvaluator(), new ScoreCalculator(), new TimeProviderClock()));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(new MapTransport(), new CheckCatalog([]), null!, new ScoreCalculator(), new TimeProviderClock()));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(new MapTransport(), new CheckCatalog([]), new PolicyEvaluator(), null!, new TimeProviderClock()));
        Assert.Throws<ArgumentNullException>(() => new ScanEngine(new MapTransport(), new CheckCatalog([]), new PolicyEvaluator(), new ScoreCalculator(), null!));

        var check = new StubCheck().Init();
        var catalog = new CheckCatalog([check]);
        var engine = new ScanEngine(new MapTransport(), catalog, new PolicyEvaluator(), new ScoreCalculator(), new TimeProviderClock());
        var api = ScanTarget.Create(new ScanTarget
        {
            Id = "api",
            Kind = ScanTargetKind.Api,
            BaseUrl = new Uri("http://127.0.0.1:5088/"),
            Authorization = new TargetAuthorization(true),
            ApiCases =
            [
                new PienApiCaseConfiguration { Id = "blocked", Method = "POST", Path = "/p", AllowNonIdempotent = false },
                new PienApiCaseConfiguration { Id = "bad-method", Method = "TRACE", Path = "/t" },
                new PienApiCaseConfiguration { Id = "empty-method", Method = " ", Path = "relative" },
                new PienApiCaseConfiguration
                {
                    Id = "query",
                    Method = "GET",
                    Path = "/q",
                    Query = new Dictionary<string, JsonElement> { ["a"] = JsonSerializer.SerializeToElement("1") },
                    Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
                    Body = "",
                },
            ],
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "api",
            Targets = [api],
            Limits = ScanLimits.Default,
            EnabledCheckIds = [check.Definition.Id.Value],
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
    }

    [Fact]
    public async Task Storage_and_network_classifier_edges()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-store3-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileScanStore(root);
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
            await store.SaveAsync(run, TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetAsync(run.Id, TestContext.Current.CancellationToken));
            Assert.Equal(0, await store.CleanAsync(10, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                store.CleanAsync(0, TestContext.Current.CancellationToken));

            var run2 = ScanRun.Create(new ScanRun
            {
                Id = ScanRunId.NewId(),
                Plan = plan,
                Status = ScanRunStatus.Completed,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveAsync(run2, TestContext.Current.CancellationToken);
            Assert.Equal(1, await store.CleanAsync(1, TestContext.Current.CancellationToken));

            var baseline = Baseline.Create(new Baseline
            {
                Id = "b1",
                TargetId = "t",
                ConfigurationFingerprint = "c",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveAsync(baseline, TestContext.Current.CancellationToken);
            Assert.True(await store.DeleteAsync("b1", TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        Assert.True(IpAddressClassifier.IsLoopback(IPAddress.Loopback));
        Assert.True(IpAddressClassifier.IsLoopback(IPAddress.IPv6Loopback));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("172.16.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("224.0.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("::ffff:10.0.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("fc00::1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("fe80::1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Any));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("1.1.1.1")));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("2001:4860:4860::8888")));
    }

    private static PienConfiguration ValidBase() => new()
    {
        SchemaVersion = 1,
        Profile = "standard",
        Targets =
        [
            new PienTargetConfiguration
            {
                Id = "local",
                Kind = "website",
                Url = "http://127.0.0.1:8080/",
                Authorization = new PienAuthorizationConfiguration { Confirmed = true },
            },
        ],
        Network = new PienNetworkConfiguration
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1"],
        },
    };

    private sealed class MapTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json",
                Body = Encoding.UTF8.GetBytes("{}"),
                Duration = TimeSpan.FromMilliseconds(1),
            }));
    }

    private sealed class StubCheck : ICheck
    {
        public CheckDefinition Definition { get; private set; } = null!;

        public StubCheck Init()
        {
            Definition = CheckDefinition.Create(new CheckDefinition
            {
                Id = CheckId.Create("PIEN-API-001"),
                Name = "api",
                Description = "t",
                Category = CheckCategory.ApiContract,
                DefaultSeverity = FindingSeverity.High,
                RuleVersion = "1.0.0",
            });
            return this;
        }

        public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckResult.Create(new CheckResult { CheckId = Definition.Id, Status = FindingStatus.Pass, Findings = [] }));
    }
}
