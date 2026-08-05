using System.Net;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Limits;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Web.Network;

namespace ARTR.Pien.UnitTests.BranchCoverage;

public sealed class FinalBranchPushTests
{
    [Fact]
    public void Validator_hits_bound_and_auth_edge_branches()
    {
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(
            new PienConfiguration
            {
                SchemaVersion = 1,
                Profile = "standard",
                Targets =
                [
                    new PienTargetConfiguration
                    {
                        Id = " ",
                        Kind = "website",
                        Url = "https://127.0.0.1/",
                        Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                    },
                ],
            },
            Directory.GetCurrentDirectory()));

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
                    Url = "https://127.0.0.1/",
                    Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                    Authentication = new PienAuthenticationConfiguration
                    {
                        Scheme = "cookie",
                        SecretReference = "secret://env/COOKIE",
                    },
                    ApiCases =
                    [
                        new PienApiCaseConfiguration
                        {
                            Id = "s100",
                            Method = "GET",
                            Path = "/",
                            ExpectedStatus = JsonSerializer.SerializeToElement(100),
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "s599",
                            Method = "GET",
                            Path = "/",
                            ExpectedStatus = JsonSerializer.SerializeToElement(599),
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "range-ok",
                            Method = "GET",
                            Path = "/",
                            ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 100, max = 599 }),
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "header-missing-name",
                            Method = "GET",
                            Path = "/",
                            Authentication = new PienAuthenticationConfiguration
                            {
                                Scheme = "header",
                                SecretReference = "secret://env/H",
                                HeaderName = " ",
                            },
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "user-blank",
                            Method = "GET",
                            Path = "/",
                            Authentication = new PienAuthenticationConfiguration
                            {
                                Scheme = "basic",
                                SecretReference = "secret://env/H",
                                UsernameSecretReference = " ",
                            },
                        },
                    ],
                },
            ],
            Watch = new PienWatchConfiguration { IntervalSeconds = 86_401 },
            Crawl = new PienCrawlConfiguration
            {
                MaxPages = HardLimits.MaxCrawlPages,
                MaxDepth = HardLimits.MaxCrawlDepth,
                MaxLinksPerPage = HardLimits.MaxLinksPerPage,
            },
            Network = new PienNetworkConfiguration
            {
                AllowPrivateNetworks = true,
                AllowedHosts = ["127.0.0.1"],
                MaxRedirects = HardLimits.MaxRedirects,
                ConnectTimeoutSeconds = 120,
                RequestTimeoutSeconds = 300,
            },
            Notifications = new PienNotificationConfiguration
            {
                WebhookUrl = "https://hooks.example/h",
            },
            Policies = new PienPolicyConfiguration { Name = "balanced", FailOn = " " },
        };
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(cfg, Directory.GetCurrentDirectory()));

        PienConfigurationValidator.ApplyProfileDefaults(new PienConfiguration(), null!);
    }

    [Fact]
    public void Secret_and_classifier_remaining_edges()
    {
        Assert.False(SecretReference.TryParse(null, out _));
        Assert.False(SecretReference.TryParse("secret://", out _));
        Assert.False(SecretReference.TryParse("secret://env/", out _));
        Assert.False(SecretReference.TryParse("secret://env/a/b", out _));
        Assert.False(SecretReference.TryParse("secret://vault/x", out _));
        Assert.True(SecretReference.TryParse("secret://file//tmp/x", out var fileRef));
        Assert.Equal(SecretScheme.File, fileRef!.Scheme);

        using var secret = ResolvedSecret.FromString("abc");
        Assert.Equal(3, secret.AsSpan().Length);
        Assert.Equal("<redacted-secret>", secret.ToString());
        secret.Dispose();
        secret.Dispose();
        Assert.Throws<ObjectDisposedException>(() => secret.AsSpan());
        Assert.Throws<ObjectDisposedException>(() => secret.Reveal());

        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.IPv6Any));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.None));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("ff02::1")));
        Assert.Throws<ArgumentNullException>(() => IpAddressClassifier.IsRestricted(null!));
        Assert.Throws<TargetSafetyException>(() => UriCanonicalizer.Canonicalize(new Uri("/relative", UriKind.Relative)));
        Assert.Throws<TargetSafetyException>(() => UriCanonicalizer.Canonicalize(new Uri("ftp://example.com/")));
    }

    [Fact]
    public async Task Engine_blocked_unsupported_method_with_allow_flag()
    {
        var engine = new ScanEngine(
            new MapTransport(),
            new CheckCatalog([new PassCheck().Init()]),
            new PolicyEvaluator(),
            new ScoreCalculator(),
            new TimeProviderClock());
        var api = ScanTarget.Create(new ScanTarget
        {
            Id = "api",
            Kind = ScanTargetKind.Api,
            BaseUrl = new Uri("http://127.0.0.1:5088/"),
            Authorization = new TargetAuthorization(true),
            ApiCases =
            [
                new PienApiCaseConfiguration { Id = "trace", Method = "TRACE", Path = "/t", AllowNonIdempotent = true },
                new PienApiCaseConfiguration { Id = "connect", Method = "CONNECT", Path = "/c", AllowNonIdempotent = true },
                new PienApiCaseConfiguration { Id = "get", Method = "GET", Path = "/g", Body = null },
            ],
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "api",
            Targets = [api],
            Limits = ScanLimits.Default,
            EnabledCheckIds = ["PIEN-API-001"],
        });
        var run = await engine.RunAsync(
            definition,
            new ScanEngineOptions
            {
                Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                WorkingDirectory = Directory.GetCurrentDirectory(),
                CompareBaseline = false,
            },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ScanRunStatus.Completed, run.Status);
    }

    private sealed class MapTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                Duration = TimeSpan.FromMilliseconds(1),
            }));
    }

    private sealed class PassCheck : ICheck
    {
        public CheckDefinition Definition { get; private set; } = null!;

        public PassCheck Init()
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
