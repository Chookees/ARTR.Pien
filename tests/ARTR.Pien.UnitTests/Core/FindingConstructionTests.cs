using ARTR.Pien.Findings;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Core;

public sealed class FindingConstructionTests
{
    [Fact]
    public void Create_builds_validated_finding()
    {
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = "Unexpected status code",
            Summary = "The response status was not successful.",
            Explanation = "A successful availability check expects a 2xx response.",
            Severity = FindingSeverity.High,
            Status = FindingStatus.Fail,
            TargetId = "public-site",
            Location = FindingLocation.Create("url", "https://example.com/"),
            Evidence =
            [
                EvidenceExcerpt.Create("text/plain", "HTTP/1.1 500 Internal Server Error"),
            ],
            Expected = "2xx status",
            Observed = "500",
            Remediation = "Investigate server errors.",
            References = ["https://developer.mozilla.org/en-US/docs/Web/HTTP/Status"],
            Timestamp = DateTimeOffset.Parse("2026-08-05T00:00:00Z"),
            RunId = ScanRunId.NewId(),
        });

        Assert.Equal(FindingSeverity.High, finding.Severity);
        Assert.Equal(FindingStatus.Fail, finding.Status);
        Assert.Equal("PIEN-HTTP-001", finding.CheckId);
        Assert.Single(finding.Evidence);
        Assert.False(finding.Evidence[0].Truncated);
    }

    [Fact]
    public void Create_rejects_missing_title()
    {
        Assert.ThrowsAny<ArgumentException>(() => Finding.Create(new Finding
        {
            Id = FindingId.Create("f1"),
            CheckId = "PIEN-HTTP-001",
            RuleVersion = "1.0.0",
            Title = " ",
            Summary = "summary",
            Explanation = "explanation",
            Severity = FindingSeverity.Low,
            Status = FindingStatus.Warning,
            TargetId = "t1",
            Timestamp = DateTimeOffset.UtcNow,
            RunId = ScanRunId.Create("run1"),
        }));
    }

    [Fact]
    public void Evidence_excerpt_truncates_to_limit()
    {
        var text = new string('a', 100);
        var excerpt = EvidenceExcerpt.Create("text/plain", text, maxChars: 10);

        Assert.True(excerpt.Truncated);
        Assert.Equal(10, excerpt.Text.Length);
    }

    [Fact]
    public void Finding_id_and_run_id_reject_whitespace()
    {
        Assert.ThrowsAny<ArgumentException>(() => FindingId.Create(" "));
        Assert.ThrowsAny<ArgumentException>(() => ScanRunId.Create(""));
    }
}
