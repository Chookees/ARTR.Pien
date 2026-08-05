using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;
using ARTR.Pien.Text;

namespace ARTR.Pien.UnitTests.Core;

public sealed class ModelAndExceptionCoverageTests
{
    [Fact]
    public void Exception_types_construct()
    {
        _ = new ConfigurationException("x");
        _ = new ConfigurationException("x", new InvalidOperationException());
        _ = new AuthorizationException("x");
        _ = new AuthorizationException("x", new InvalidOperationException());
        _ = new TargetSafetyException("x");
        _ = new TargetSafetyException("x", new InvalidOperationException());
        _ = new DnsFailureException("x");
        _ = new DnsFailureException("x", new InvalidOperationException());
        _ = new ConnectionFailureException("x");
        _ = new ConnectionFailureException("x", new InvalidOperationException());
        _ = new ARTR.Pien.Exceptions.HttpProtocolException("x");
        _ = new ARTR.Pien.Exceptions.HttpProtocolException("x", new InvalidOperationException());
        _ = new TlsFailureException("x");
        _ = new TlsFailureException("x", new InvalidOperationException());
        _ = new BodyLimitExceededException("x");
        _ = new BodyLimitExceededException("x", new InvalidOperationException());
        _ = new PienTimeoutException("x");
        _ = new PienTimeoutException("x", new TimeoutException());
        _ = new NotificationException("x");
        _ = new NotificationException("x", new HttpRequestException());
        _ = new SecretResolutionException("x");
        _ = new SecretResolutionException("x", new InvalidOperationException());
        _ = new CheckExecutionException("x");
        _ = new CheckExecutionException("x", new InvalidOperationException());
        _ = new ReportExportException("x");
        _ = new ReportExportException("x", new InvalidOperationException());
        _ = new StorageException("x");
        _ = new StorageException("x", new InvalidOperationException());
        _ = new ParseException("x");
        _ = new ParseException("x", new InvalidOperationException());
    }

    [Fact]
    public void Create_helpers_cover_validation_branches()
    {
        Assert.Throws<ArgumentException>(() => CheckId.Create(" "));
        Assert.Throws<ArgumentException>(() => FindingId.Create(" "));
        Assert.Throws<ArgumentException>(() => ScanRunId.Create(" "));
        Assert.ThrowsAny<Exception>(() => CheckDefinition.Create(new CheckDefinition
        {
            Id = CheckId.Create("PIEN-HTTP-001"),
            Name = " ",
            Description = "d",
            Category = CheckCategory.Reliability,
            DefaultSeverity = FindingSeverity.High,
            RuleVersion = "1.0.0",
        }));
        Assert.ThrowsAny<Exception>(() => ProbeRequest.Create(new ProbeRequest
        {
            Uri = new Uri("http://127.0.0.1/"),
            Method = ProbeMethod.Get,
            MaxResponseBodyBytes = -1,
        }));
        var notification = Notification.Create(new Notification
        {
            Id = "n1",
            Title = "t",
            Message = "m",
            Severity = NotificationSeverity.Info,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        Assert.Equal("n1", notification.Id);
        var suppression = new PolicySuppression("PIEN-HTTP-001", "fp", "reason", null);
        Assert.Equal("PIEN-HTTP-001", suppression.CheckId);
        Assert.Equal(ScanProfileNames.Quick, ScanProfileNames.Quick);
        Assert.NotNull(new ScanProgress(ScanStage.Testing, "x", 1));
        Assert.NotNull(new TlsProbeResult("Tls12", "c", null, null, null, null, null));
        Assert.NotNull(new ApiCaseExecutionResult("id", "GET", null, null, true, "blocked", [], []));
        using var secret = ResolvedSecret.FromString("abc");
        Assert.Equal("abc", secret.Reveal());
        Assert.True(SafeRegex.IsMatch("a", "a", ScanLimits.Default));
        _ = EvidenceExcerpt.Create("text/plain", "body", 8);
        Assert.ThrowsAny<Exception>(() => EvidenceExcerpt.Create(" ", "body", 8));
    }

    [Fact]
    public void Report_and_finding_create_cover_optional_paths()
    {
        var runId = ScanRunId.NewId();
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = "t",
            Summary = "s",
            Explanation = "e",
            Severity = FindingSeverity.Low,
            Status = FindingStatus.Pass,
            TargetId = "t1",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = runId,
            Evidence = [EvidenceExcerpt.Create("text/plain", "x", 16)],
        });
        var report = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = runId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Findings = [finding],
            BaselineComparisons =
            [
                BaselineComparison.Create(new BaselineComparison
                {
                    FindingFingerprint = "fp",
                    Kind = BaselineComparisonKind.Unchanged,
                    CurrentFindingId = finding.Id,
                }),
            ],
        });
        Assert.Single(report.Findings);
        Assert.Single(report.BaselineComparisons);
    }
}
