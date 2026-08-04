using ARTR.Pien.Limits;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Findings;

/// <summary>
/// Safe location descriptor for a finding (URL, JSON pointer, header name, etc.).
/// </summary>
/// <param name="Kind">Location kind such as <c>url</c>, <c>header</c>, or <c>json-pointer</c>.</param>
/// <param name="Path">Safe location path or pointer that does not embed secrets.</param>
public sealed record FindingLocation(string Kind, string Path)
{
    /// <summary>
    /// Creates a validated location.
    /// </summary>
    /// <param name="kind">Location kind.</param>
    /// <param name="path">Safe path.</param>
    /// <returns>A validated location.</returns>
    public static FindingLocation Create(string kind, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FindingLocation(kind.Trim(), path.Trim());
    }
}

/// <summary>
/// Bounded, redacted excerpt of inspection evidence attached to a finding.
/// </summary>
public sealed record EvidenceExcerpt
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EvidenceExcerpt"/> record.
    /// </summary>
    /// <param name="contentType">Evidence content type or media hint.</param>
    /// <param name="text">Redacted excerpt text.</param>
    /// <param name="truncated">Whether the excerpt was truncated to fit limits.</param>
    public EvidenceExcerpt(string contentType, string text, bool truncated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > HardLimits.MaxEvidenceExcerptBytes)
        {
            throw new ArgumentException(
                $"Evidence excerpt exceeds the hard maximum of {HardLimits.MaxEvidenceExcerptBytes} characters.",
                nameof(text));
        }

        ContentType = contentType.Trim();
        Text = text;
        Truncated = truncated;
    }

    /// <summary>Evidence content type or media hint.</summary>
    public string ContentType { get; init; }

    /// <summary>Redacted excerpt text.</summary>
    public string Text { get; init; }

    /// <summary>Whether the excerpt was truncated.</summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Creates an excerpt, truncating to <paramref name="maxChars"/> when needed.
    /// </summary>
    /// <param name="contentType">Content type.</param>
    /// <param name="text">Raw redacted text.</param>
    /// <param name="maxChars">Maximum characters.</param>
    /// <returns>A bounded excerpt.</returns>
    public static EvidenceExcerpt Create(string contentType, string text, int maxChars = 4 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        maxChars = Math.Min(maxChars, HardLimits.MaxEvidenceExcerptBytes);

        if (text.Length <= maxChars)
        {
            return new EvidenceExcerpt(contentType, text, truncated: false);
        }

        return new EvidenceExcerpt(contentType, text[..maxChars], truncated: true);
    }
}

/// <summary>
/// Immutable finding produced by a check evaluation.
/// </summary>
public sealed record Finding
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Finding"/> record.
    /// Use <see cref="Create"/> for validated construction.
    /// </summary>
    public Finding()
    {
    }

    /// <summary>Unique finding identifier.</summary>
    public required FindingId Id { get; init; }

    /// <summary>Stable check identifier.</summary>
    public required string CheckId { get; init; }

    /// <summary>Check rule version that produced the finding.</summary>
    public required string RuleVersion { get; init; }

    /// <summary>Short human-readable title.</summary>
    public required string Title { get; init; }

    /// <summary>One-line summary.</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed explanation understandable without reading source code.</summary>
    public required string Explanation { get; init; }

    /// <summary>Potential impact severity.</summary>
    public required FindingSeverity Severity { get; init; }

    /// <summary>Evaluation status.</summary>
    public required FindingStatus Status { get; init; }

    /// <summary>Target identifier associated with the finding.</summary>
    public required string TargetId { get; init; }

    /// <summary>Safe location of the observation.</summary>
    public FindingLocation? Location { get; init; }

    /// <summary>Redacted evidence excerpts.</summary>
    public IReadOnlyList<EvidenceExcerpt> Evidence { get; init; } = [];

    /// <summary>Expected condition description.</summary>
    public string? Expected { get; init; }

    /// <summary>Observed condition description.</summary>
    public string? Observed { get; init; }

    /// <summary>Remediation guidance.</summary>
    public string? Remediation { get; init; }

    /// <summary>External references (URLs or document identifiers).</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>UTC timestamp when the finding was produced.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Owning scan run identifier.</summary>
    public required ScanRunId RunId { get; init; }

    /// <summary>Optional baseline comparison classification.</summary>
    public string? BaselineState { get; init; }

    /// <summary>Optional suppression metadata.</summary>
    public string? Suppression { get; init; }

    /// <summary>
    /// Creates a validated finding.
    /// </summary>
    /// <param name="finding">Candidate finding.</param>
    /// <returns>The validated finding.</returns>
    /// <exception cref="ArgumentException">Thrown when required members are invalid.</exception>
    public static Finding Create(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(finding.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.CheckId);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.RuleVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.Summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.Explanation);
        ArgumentException.ThrowIfNullOrWhiteSpace(finding.TargetId);
        ArgumentNullException.ThrowIfNull(finding.RunId);
        ArgumentNullException.ThrowIfNull(finding.Evidence);
        ArgumentNullException.ThrowIfNull(finding.References);

        if (!Enum.IsDefined(finding.Severity))
        {
            throw new ArgumentException($"Unknown severity '{finding.Severity}'.", nameof(finding));
        }

        if (!Enum.IsDefined(finding.Status))
        {
            throw new ArgumentException($"Unknown status '{finding.Status}'.", nameof(finding));
        }

        return finding;
    }
}
