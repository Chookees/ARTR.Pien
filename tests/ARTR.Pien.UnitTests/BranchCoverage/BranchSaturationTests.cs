using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Scanning;
using ARTR.Pien.Storage;

namespace ARTR.Pien.UnitTests.BranchCoverage;

public sealed class BranchSaturationTests
{
    [Fact]
    public void Validator_rejects_invalid_profiles_formats_and_api_cases()
    {
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(
            new PienConfiguration
            {
                SchemaVersion = 2,
                Profile = "nope",
                Targets =
                [
                    new PienTargetConfiguration
                    {
                        Id = "t",
                        Kind = "website",
                        Url = "http://127.0.0.1/",
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
                    Url = "http://127.0.0.1:5088/",
                    Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                    Authentication = new PienAuthenticationConfiguration { Scheme = "bearer", SecretReference = "not-a-secret" },
                    ApiCases =
                    [
                        new PienApiCaseConfiguration { Id = " ", Method = "GET", Path = "" },
                        new PienApiCaseConfiguration { Id = "bad", Method = "TRACE", Path = "/x" },
                        new PienApiCaseConfiguration { Id = "post", Method = "POST", Path = "/x", AllowNonIdempotent = false },
                        new PienApiCaseConfiguration
                        {
                            Id = "assert",
                            Method = "GET",
                            Path = "/a",
                            ResponseTimeBudgetMs = 0,
                            MaxBodyBytes = 0,
                            BodySecretReference = "bad",
                            ExpectedStatus = JsonSerializer.SerializeToElement(900),
                            JsonAssertions =
                            [
                                new PienJsonAssertionConfiguration { Pointer = "", Op = "nope" },
                            ],
                            Authentication = new PienAuthenticationConfiguration { Scheme = "header", SecretReference = "secret://env/X" },
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "range",
                            Method = "GET",
                            Path = "/r",
                            ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 500, max = 200 }),
                        },
                        new PienApiCaseConfiguration
                        {
                            Id = "status-shape",
                            Method = "GET",
                            Path = "/s",
                            ExpectedStatus = JsonSerializer.SerializeToElement("200"),
                        },
                    ],
                },
            ],
            Checks = new PienChecksConfiguration { Enabled = ["PIEN-NOPE-001"], Disabled = ["PIEN-HTTP-001"] },
            Policies = new PienPolicyConfiguration
            {
                Name = "unknown-policy",
                FailOn = "banana",
                SeverityOverrides = new Dictionary<string, string> { ["PIEN-HTTP-001"] = "nope", ["PIEN-NOPE"] = "high" },
                Suppressions = [new PienSuppressionConfiguration { CheckId = " " }, new PienSuppressionConfiguration { CheckId = "PIEN-NOPE-001" }],
            },
            Output = new PienOutputConfiguration { Formats = ["xml"] },
            Logging = new PienLoggingConfiguration { Level = "loud" },
            Watch = new PienWatchConfiguration { IntervalSeconds = 0 },
            Crawl = new PienCrawlConfiguration { MaxPages = 0, MaxDepth = -1, MaxLinksPerPage = 0 },
            Notifications = new PienNotificationConfiguration
            {
                WebhookUrl = "http://example.com/hook",
                WebhookSecretReference = "bad",
                Events = ["Nope"],
            },
        };
        Assert.Throws<ConfigurationException>(() => PienConfigurationValidator.Validate(cfg, Directory.GetCurrentDirectory()));

        foreach (var profile in new[] { "quick", "standard", "deep", "api", "ci" })
        {
            var c = new PienConfiguration();
            PienConfigurationValidator.ApplyProfileDefaults(c, profile);
            Assert.False(string.IsNullOrWhiteSpace(c.Profile));
        }
    }

    [Fact]
    public async Task Json_config_loader_reads_file_and_rejects_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "pien.json");
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "schemaVersion": 1,
                  "profile": "quick",
                  "targets": [{
                    "id": "local",
                    "kind": "website",
                    "url": "http://127.0.0.1:8080/",
                    "authorization": { "confirmed": true }
                  }],
                  "network": { "allowPrivateNetworks": true, "allowedHosts": ["127.0.0.1"] }
                }
                """,
                TestContext.Current.CancellationToken);
            var loader = new JsonConfigLoader();
            var loaded = await loader.LoadAsync(new ConfigLoadRequest(path, root), TestContext.Current.CancellationToken);
            Assert.Equal(1, loaded.SchemaVersion);
            await Assert.ThrowsAsync<ConfigurationException>(() =>
                loader.LoadAsync(new ConfigLoadRequest(Path.Combine(root, "missing.json"), root), TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Storage_cleanup_and_missing_lookups()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-store2-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileScanStore(root);
            Assert.Null(await store.GetAsync(ScanRunId.Create("missing"), TestContext.Current.CancellationToken));
            Assert.Null(await store.GetAsync("missing-baseline", TestContext.Current.CancellationToken));
            Assert.False(await store.DeleteAsync("missing-baseline", TestContext.Current.CancellationToken));
            Assert.Empty(await store.ListIdsAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await store.ListRecentAsync(5, TestContext.Current.CancellationToken));

            for (var i = 0; i < 3; i++)
            {
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
            }

            Assert.Equal(2, await store.CleanAsync(1, TestContext.Current.CancellationToken));
            Assert.Single(await store.ListRecentAsync(10, TestContext.Current.CancellationToken));
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
    public async Task Reporting_covers_severity_map_and_empty_policy()
    {
        var runId = ScanRunId.Create("run-branch");
        var findings = new[]
        {
            FindingSeverity.Critical, FindingSeverity.High, FindingSeverity.Medium, FindingSeverity.Low, FindingSeverity.Info,
        }.Select((severity, i) => Finding.Create(new Finding
        {
            Id = FindingId.Create($"f{i}"),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = $"t| {severity}",
            Summary = "s",
            Explanation = "e",
            Severity = severity,
            Status = severity >= FindingSeverity.Medium ? FindingStatus.Fail : FindingStatus.Pass,
            TargetId = "t",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = runId,
        })).ToArray();

        var document = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = runId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Findings = findings,
        });

        foreach (var exporter in new IReportExporter[]
                 {
                     new JsonReportExporter(),
                     new ConsoleReportExporter(),
                     new SarifReportExporter(),
                     new JUnitReportExporter(),
                     new MarkdownReportExporter(),
                     new HtmlReportExporter(),
                 })
        {
            await using var stream = new MemoryStream();
            await exporter.ExportAsync(document, stream, TestContext.Current.CancellationToken);
            Assert.True(stream.Length > 0);
        }
    }

    [Fact]
    public void Null_arguments_hit_guard_branches()
    {
        Assert.Throws<ArgumentNullException>(() => CheckDefinition.Create(null!));
        Assert.Throws<ArgumentNullException>(() => Finding.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ProbeResult.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ProbeRequest.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ScanDefinition.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ScanTarget.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ScanRun.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ReportDocument.Create(null!));
        Assert.Throws<ArgumentNullException>(() => Policy.Policy.Create(null!));
        Assert.Throws<ArgumentNullException>(() => PolicyResult.Create(null!));
        Assert.Throws<ArgumentNullException>(() => CheckResult.Create(null!));
        Assert.Throws<ArgumentNullException>(() => Notification.Create(null!));
        Assert.Throws<ArgumentNullException>(() => Baseline.Create(null!));
        Assert.Throws<ArgumentNullException>(() => BaselineComparison.Create(null!));
        Assert.Throws<ArgumentNullException>(() => new PolicyEvaluator().Evaluate(null!, []));
        Assert.Throws<ArgumentNullException>(() => new PolicyEvaluator().Evaluate(Policy.Policy.Create(new Policy.Policy { Name = "n", Description = "d" }), null!));
        Assert.Throws<ArgumentNullException>(() => new ScoreCalculator().ScoreByCategory(null!, []));
        Assert.Throws<ArgumentNullException>(() => new ScoreCalculator().ScoreByCategory([], null!));
        Assert.Throws<ArgumentNullException>(() => new CheckCatalog(null!));
    }
}
