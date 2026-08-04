using ARTR.Pien.Scanning;

namespace ARTR.Pien.Checks;

/// <summary>
/// Metadata describing a registered check.
/// </summary>
public sealed record CheckDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckDefinition"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public CheckDefinition()
    {
    }

    /// <summary>Stable check identifier.</summary>
    public required CheckId Id { get; init; }

    /// <summary>Human-readable check name.</summary>
    public required string Name { get; init; }

    /// <summary>Check description.</summary>
    public required string Description { get; init; }

    /// <summary>Functional category.</summary>
    public required CheckCategory Category { get; init; }

    /// <summary>Default severity when the check fails.</summary>
    public required Findings.FindingSeverity DefaultSeverity { get; init; }

    /// <summary>Semantic version of the check rule.</summary>
    public required string RuleVersion { get; init; }

    /// <summary>
    /// Creates a validated check definition.
    /// </summary>
    /// <param name="definition">Candidate definition.</param>
    /// <returns>The validated definition.</returns>
    public static CheckDefinition Create(CheckDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.RuleVersion);
        if (!Enum.IsDefined(definition.Category))
        {
            throw new ArgumentException($"Unknown category '{definition.Category}'.", nameof(definition));
        }

        if (!Enum.IsDefined(definition.DefaultSeverity))
        {
            throw new ArgumentException($"Unknown severity '{definition.DefaultSeverity}'.", nameof(definition));
        }

        return definition;
    }
}

/// <summary>
/// Result of evaluating a check against inspection evidence.
/// </summary>
public sealed record CheckResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckResult"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public CheckResult()
    {
    }

    /// <summary>Check that produced the result.</summary>
    public required CheckId CheckId { get; init; }

    /// <summary>Findings produced by the check (bounded by scan limits).</summary>
    public IReadOnlyList<Findings.Finding> Findings { get; init; } = [];

    /// <summary>Overall status summarizing the findings.</summary>
    public required Findings.FindingStatus Status { get; init; }

    /// <summary>Optional diagnostic message when status is <see cref="Findings.FindingStatus.Error"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a validated check result.
    /// </summary>
    /// <param name="result">Candidate result.</param>
    /// <returns>The validated result.</returns>
    public static CheckResult Create(CheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.CheckId);
        ArgumentNullException.ThrowIfNull(result.Findings);
        if (!Enum.IsDefined(result.Status))
        {
            throw new ArgumentException($"Unknown status '{result.Status}'.", nameof(result));
        }

        return result;
    }
}

/// <summary>
/// Evaluates a single deterministic check.
/// </summary>
public interface ICheck
{
    /// <summary>Check metadata.</summary>
    CheckDefinition Definition { get; }

    /// <summary>
    /// Evaluates the check against the provided evidence.
    /// </summary>
    /// <param name="context">Active scan context.</param>
    /// <param name="evidence">Inspection evidence gathered by probes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The check result.</returns>
    Task<CheckResult> EvaluateAsync(
        ScanContext context,
        Probing.InspectionEvidence evidence,
        CancellationToken cancellationToken = default);
}
