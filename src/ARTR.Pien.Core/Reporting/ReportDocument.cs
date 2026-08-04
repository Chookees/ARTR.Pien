using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Reporting;

/// <summary>
/// Versioned report document produced from a scan run.
/// </summary>
public sealed record ReportDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDocument"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ReportDocument()
    {
    }

    /// <summary>Report schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Owning scan run identifier.</summary>
    public required ScanRunId RunId { get; init; }

    /// <summary>UTC generation timestamp.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Findings included in the report (already redacted and bounded).</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>Optional policy evaluation result.</summary>
    public PolicyResult? PolicyResult { get; init; }

    /// <summary>Optional baseline comparisons.</summary>
    public IReadOnlyList<BaselineComparison> BaselineComparisons { get; init; } = [];

    /// <summary>Category scores keyed by category name.</summary>
    public IReadOnlyDictionary<string, double> CategoryScores { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Non-secret metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a validated report document.
    /// </summary>
    /// <param name="document">Candidate document.</param>
    /// <returns>The validated document.</returns>
    public static ReportDocument Create(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.RunId);
        ArgumentNullException.ThrowIfNull(document.Findings);
        ArgumentNullException.ThrowIfNull(document.BaselineComparisons);
        ArgumentNullException.ThrowIfNull(document.CategoryScores);
        ArgumentNullException.ThrowIfNull(document.Metadata);

        if (document.SchemaVersion < 1)
        {
            throw new ArgumentException("Schema version must be at least 1.", nameof(document));
        }

        return document;
    }
}

/// <summary>
/// Supported report export formats.
/// </summary>
public static class ReportFormats
{
    /// <summary>Console text.</summary>
    public const string Console = "console";

    /// <summary>JSON.</summary>
    public const string Json = "json";

    /// <summary>SARIF 2.1.0.</summary>
    public const string Sarif = "sarif";

    /// <summary>JUnit XML.</summary>
    public const string JUnit = "junit";

    /// <summary>HTML.</summary>
    public const string Html = "html";

    /// <summary>Markdown.</summary>
    public const string Markdown = "markdown";
}
