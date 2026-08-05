using System.Net;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Hosting.Notifications;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.UnitTests.Engine;

public sealed class ScanEngineAndWebhookTests
{
    private sealed class FakeTransport : ISafeHttpTransport
    {
        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.OK,
                ContentType = "text/html",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = "text/html",
                    ["Content-Security-Policy"] = "default-src 'self'",
                    ["X-Content-Type-Options"] = "nosniff",
                    ["Referrer-Policy"] = "no-referrer",
                    ["Permissions-Policy"] = "geolocation=()",
                },
                Body = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><head><title>ok</title><meta name=\"description\" content=\"d\"/></head><body><img alt=\"a\"/></body></html>"),
                Duration = TimeSpan.FromMilliseconds(5),
            }));
    }

    private sealed class ThrowingTransport : ISafeHttpTransport
    {
        private readonly Exception _exception;

        public ThrowingTransport(Exception exception) => _exception = exception;

        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
            => Task.FromException<ProbeResult>(_exception);
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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class EnvSecretResolver : ISecretResolver
    {
        public Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolvedSecret.FromString("super-secret"));
    }

    [Fact]
    public async Task Engine_completes_with_fake_transport()
    {
        var catalog = new CheckCatalog([new FakeCheck()]);
        var engine = new ScanEngine(
            new FakeTransport(),
            catalog,
            new PolicyEvaluator(),
            new ScoreCalculator(),
            new TimeProviderClock());
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "local",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("http://127.0.0.1/"),
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default with { MaxCrawlPages = 1 },
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
    }

    [Fact]
    public async Task Webhook_sender_posts_signed_payload()
    {
        var handler = new RecordingHandler();
        var sender = new HmacWebhookNotificationSender(
            "https://example.com/hook",
            "secret://env/HOOK",
            new EnvSecretResolver(),
            handler);
        await sender.SendAsync(
            Notification.Create(new Notification
            {
                Id = "n1",
                Title = "done",
                Message = "scan done",
                Severity = NotificationSeverity.Info,
                CreatedAt = DateTimeOffset.UtcNow,
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Engine_rethrows_TargetSafetyException_instead_of_Failed_run()
    {
        var engine = CreateEngine(new ThrowingTransport(
            new TargetSafetyException("Destination resolves to a restricted address.")));
        var ex = await Assert.ThrowsAsync<TargetSafetyException>(() =>
            engine.RunAsync(
                CreateAuthorizedDefinition(),
                CreateOptions(),
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("restricted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Engine_rethrows_AuthorizationException_instead_of_Failed_run()
    {
        var engine = CreateEngine(new FakeTransport());
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets =
            [
                ScanTarget.Create(new ScanTarget
                {
                    Id = "denied",
                    Kind = ScanTargetKind.Website,
                    BaseUrl = new Uri("http://127.0.0.1/"),
                    Authorization = new TargetAuthorization(false),
                }),
            ],
            Limits = ScanLimits.Default with { MaxCrawlPages = 1 },
            EnabledCheckIds = ["PIEN-HTTP-001"],
        });

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            engine.RunAsync(definition, CreateOptions(), cancellationToken: TestContext.Current.CancellationToken));
    }

    private static ScanEngine CreateEngine(ISafeHttpTransport transport)
        => new(
            transport,
            new CheckCatalog([new FakeCheck()]),
            new PolicyEvaluator(),
            new ScoreCalculator(),
            new TimeProviderClock());

    private static ScanDefinition CreateAuthorizedDefinition()
        => ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets =
            [
                ScanTarget.Create(new ScanTarget
                {
                    Id = "local",
                    Kind = ScanTargetKind.Website,
                    BaseUrl = new Uri("http://127.0.0.1/"),
                    Authorization = new TargetAuthorization(true),
                }),
            ],
            Limits = ScanLimits.Default with { MaxCrawlPages = 1 },
            EnabledCheckIds = ["PIEN-HTTP-001"],
        });

    private static ScanEngineOptions CreateOptions()
        => new()
        {
            Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t" }),
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };
}
