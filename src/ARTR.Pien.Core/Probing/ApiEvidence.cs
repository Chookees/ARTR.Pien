using System.Net;
using ARTR.Pien.Abstractions;

namespace ARTR.Pien.Probing;

/// <summary>
/// Result of executing a configured API test case.
/// </summary>
/// <param name="CaseId">Configured case id.</param>
/// <param name="Method">HTTP method used.</param>
/// <param name="Uri">Final request URI.</param>
/// <param name="Probe">Probe result when executed.</param>
/// <param name="Blocked">True when execution was blocked (non-idempotent without allow).</param>
/// <param name="BlockReason">Reason when blocked.</param>
/// <param name="AssertionFailures">Human-readable assertion failure messages.</param>
/// <param name="SchemaFailures">JSON Schema validation failure messages.</param>
public sealed record ApiCaseExecutionResult(
    string CaseId,
    string Method,
    Uri? Uri,
    ProbeResult? Probe,
    bool Blocked,
    string? BlockReason,
    IReadOnlyList<string> AssertionFailures,
    IReadOnlyList<string> SchemaFailures);

/// <summary>
/// Crawl page with optional probe evidence.
/// </summary>
/// <param name="Page">Crawl page reference.</param>
/// <param name="Probe">Optional probe result for the page.</param>
public sealed record CrawledPageEvidence(CrawlPage Page, ProbeResult? Probe);

/// <summary>
/// Expected HTTP status constraint for an API case.
/// </summary>
public sealed record ExpectedStatusConstraint(int Min, int Max)
{
    /// <summary>Returns whether <paramref name="code"/> is within range.</summary>
    public bool Matches(HttpStatusCode code)
    {
        var value = (int)code;
        return value >= Min && value <= Max;
    }
}
