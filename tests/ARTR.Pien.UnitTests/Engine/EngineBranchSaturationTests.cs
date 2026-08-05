using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Hosting.Notifications;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Storage;

namespace ARTR.Pien.UnitTests.Engine;

public sealed class EngineBranchSaturationTests
{
    private sealed class MapTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = Encoding.UTF8.GetBytes("""{"ok":true}"""),
                Duration = TimeSpan.FromMilliseconds(1),
            }));
    }

    private sealed class FakeCheck : ICheck
    {
        private readonly string _id;

        public FakeCheck(string id = "PIEN-HTTP-001") => _id = id;

        public CheckDefinition Definition { get; private set; } = null!;

        public FakeCheck Init()
        {
            Definition = CheckDefinition.Create(new CheckDefinition
            {
                Id = CheckId.Create(_id),
                Name = _id,
                Description = "t",
                Category = CheckCategory.Reliability,
                DefaultSeverity = FindingSeverity.High,
                RuleVersion = "1.0.0",
            });
            return this;
        }

        public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckResult.Create(new CheckResult { CheckId = Definition.Id, Status = FindingStatus.Pass, Findings = [] }));
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
    }

    private sealed class SecretResolver : ISecretResolver
    {
        public Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolvedSecret.FromString("s"));
    }

    [Fact]
    public async Task Engine_covers_api_methods_baseline_compare_and_required_webhook()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-eng2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FileScanStore(Path.Combine(root, ".pien"));
            var baseline = Baseline.Create(new Baseline
            {
                Id = "b1",
                TargetId = "api",
                ConfigurationFingerprint = "cfg",
                ContentFingerprint = "OLD",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveAsync(baseline, TestContext.Current.CancellationToken);

            var catalog = new CheckCatalog([new FakeCheck("PIEN-API-001").Init(), new FakeCheck("PIEN-CHANGE-001").Init()]);
            var engine = new ScanEngine(
                new MapTransport(),
                catalog,
                new PolicyEvaluator(),
                new ScoreCalculator(),
                new TimeProviderClock(),
                store,
                store,
                notifications: new HmacWebhookNotificationSender("https://example.com/h", null, new SecretResolver(), new FailHandler()));

            var api = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1:5088/"),
                Authorization = new TargetAuthorization(true),
                ApiCases =
                [
                    new PienApiCaseConfiguration { Id = "get", Method = "GET", Path = "http://127.0.0.1:5088/abs" },
                    new PienApiCaseConfiguration { Id = "head", Method = "HEAD", Path = "/h" },
                    new PienApiCaseConfiguration { Id = "options", Method = "OPTIONS", Path = "/o" },
                    new PienApiCaseConfiguration { Id = "post", Method = "POST", Path = "/p", AllowNonIdempotent = true, Body = "{}" },
                    new PienApiCaseConfiguration { Id = "put", Method = "PUT", Path = "/u", AllowNonIdempotent = true },
                    new PienApiCaseConfiguration { Id = "patch", Method = "PATCH", Path = "/x", AllowNonIdempotent = true },
                    new PienApiCaseConfiguration { Id = "delete", Method = "DELETE", Path = "/d", AllowNonIdempotent = true },
                ],
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [api],
                Limits = ScanLimits.Default,
                DisabledCheckIds = ["PIEN-HTTP-001"],
            });

            await Assert.ThrowsAsync<NotificationException>(() => engine.RunAsync(
                definition,
                new ScanEngineOptions
                {
                    Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                    WorkingDirectory = root,
                    BaselineId = "b1",
                    CompareBaseline = true,
                    Notifications = new PienNotificationConfiguration { WebhookUrl = "https://example.com/h", Required = true },
                },
                cancellationToken: TestContext.Current.CancellationToken));

            // Optional webhook path with success sender keeps completed run.
            var engine2 = new ScanEngine(
                new MapTransport(),
                catalog,
                new PolicyEvaluator(),
                new ScoreCalculator(),
                new TimeProviderClock(),
                store,
                store,
                notifications: new HmacWebhookNotificationSender("https://example.com/h", null, new SecretResolver(), new OkHandler()));
            var run = await engine2.RunAsync(
                definition,
                new ScanEngineOptions
                {
                    Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
                    WorkingDirectory = root,
                    Notifications = new PienNotificationConfiguration { WebhookUrl = "https://example.com/h", Required = false },
                },
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(ScanRunStatus.Completed, run.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Openapi_empty_paths_fails()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-oa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "empty.json");
            await File.WriteAllTextAsync(path, """{"openapi":"3.0.3","info":{"title":"t","version":"1"},"paths":{}}""", TestContext.Current.CancellationToken);
            var target = ScanTarget.Create(new ScanTarget
            {
                Id = "api",
                Kind = ScanTargetKind.Api,
                BaseUrl = new Uri("http://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
                OpenApiDocument = "empty.json",
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "api",
                Targets = [target],
                Limits = ScanLimits.Default,
            });
            var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
            var result = await new OpenApiDocumentCheck().EvaluateAsync(
                context,
                InspectionEvidence.Create(new InspectionEvidence { Target = target, WorkingDirectory = root }),
                TestContext.Current.CancellationToken);
            Assert.Equal(FindingStatus.Fail, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
