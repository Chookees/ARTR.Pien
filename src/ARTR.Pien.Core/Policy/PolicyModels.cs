using ARTR.Pien.Findings;

namespace ARTR.Pien.Policy;

/// <summary>
/// Immutable policy controlling fail thresholds and check enablement.
/// </summary>
public sealed record Policy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Policy"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public Policy()
    {
    }

    /// <summary>Policy name.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Minimum severity that causes a policy failure. Default: High.</summary>
    public FindingSeverity FailOnSeverityAtOrAbove { get; init; } = FindingSeverity.High;

    /// <summary>Check IDs explicitly enabled; empty means inherit.</summary>
    public IReadOnlyList<string> EnabledCheckIds { get; init; } = [];

    /// <summary>Check IDs explicitly disabled.</summary>
    public IReadOnlyList<string> DisabledCheckIds { get; init; } = [];

    /// <summary>Severity overrides keyed by check ID.</summary>
    public IReadOnlyDictionary<string, FindingSeverity> SeverityOverrides { get; init; } =
        new Dictionary<string, FindingSeverity>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Configured finding suppressions.</summary>
    public IReadOnlyList<PolicySuppression> Suppressions { get; init; } = [];

    /// <summary>
    /// Creates a validated policy.
    /// </summary>
    /// <param name="policy">Candidate policy.</param>
    /// <returns>The validated policy.</returns>
    public static Policy Create(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Description);
        ArgumentNullException.ThrowIfNull(policy.EnabledCheckIds);
        ArgumentNullException.ThrowIfNull(policy.DisabledCheckIds);
        ArgumentNullException.ThrowIfNull(policy.SeverityOverrides);
        ArgumentNullException.ThrowIfNull(policy.Suppressions);
        if (!Enum.IsDefined(policy.FailOnSeverityAtOrAbove))
        {
            throw new ArgumentException($"Unknown severity '{policy.FailOnSeverityAtOrAbove}'.", nameof(policy));
        }

        return policy with { Name = policy.Name.Trim() };
    }
}

/// <summary>
/// A configured finding suppression rule.
/// </summary>
/// <param name="CheckId">Check identifier to suppress.</param>
/// <param name="Fingerprint">Optional finding fingerprint filter.</param>
/// <param name="Reason">Optional non-secret reason.</param>
/// <param name="ExpiresAt">Optional expiry instant.</param>
public sealed record PolicySuppression(
    string CheckId,
    string? Fingerprint = null,
    string? Reason = null,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Result of evaluating a policy against a set of findings.
/// </summary>
public sealed record PolicyResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyResult"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public PolicyResult()
    {
    }

    /// <summary>Policy that was evaluated.</summary>
    public required string PolicyName { get; init; }

    /// <summary>Whether the policy passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Findings that caused policy failure.</summary>
    public IReadOnlyList<Finding> FailedFindings { get; init; } = [];

    /// <summary>Optional summary message.</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Creates a validated policy result.
    /// </summary>
    /// <param name="result">Candidate result.</param>
    /// <returns>The validated result.</returns>
    public static PolicyResult Create(PolicyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.PolicyName);
        ArgumentNullException.ThrowIfNull(result.FailedFindings);
        return result with { PolicyName = result.PolicyName.Trim() };
    }
}

/// <summary>
/// Stored baseline snapshot for change detection.
/// </summary>
public sealed record Baseline
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Baseline"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public Baseline()
    {
    }

    /// <summary>Baseline name or identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Target identity associated with the baseline.</summary>
    public required string TargetId { get; init; }

    /// <summary>Configuration fingerprint.</summary>
    public required string ConfigurationFingerprint { get; init; }

    /// <summary>Normalized finding fingerprints included in the baseline.</summary>
    public IReadOnlyList<string> FindingFingerprints { get; init; } = [];

    /// <summary>Optional TLS certificate fingerprint.</summary>
    public string? TlsCertificateFingerprint { get; init; }

    /// <summary>Optional content fingerprint.</summary>
    public string? ContentFingerprint { get; init; }

    /// <summary>UTC creation time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Creates a validated baseline.
    /// </summary>
    /// <param name="baseline">Candidate baseline.</param>
    /// <returns>The validated baseline.</returns>
    public static Baseline Create(Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseline.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseline.TargetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseline.ConfigurationFingerprint);
        ArgumentNullException.ThrowIfNull(baseline.FindingFingerprints);
        return baseline with
        {
            Id = baseline.Id.Trim(),
            TargetId = baseline.TargetId.Trim(),
        };
    }
}

/// <summary>
/// Comparison of a current finding fingerprint against a baseline.
/// </summary>
public sealed record BaselineComparison
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaselineComparison"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public BaselineComparison()
    {
    }

    /// <summary>Finding fingerprint under comparison.</summary>
    public required string FindingFingerprint { get; init; }

    /// <summary>Comparison classification.</summary>
    public required BaselineComparisonKind Kind { get; init; }

    /// <summary>Optional related finding identifier from the current run.</summary>
    public FindingId? CurrentFindingId { get; init; }

    /// <summary>
    /// Creates a validated baseline comparison.
    /// </summary>
    /// <param name="comparison">Candidate comparison.</param>
    /// <returns>The validated comparison.</returns>
    public static BaselineComparison Create(BaselineComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentException.ThrowIfNullOrWhiteSpace(comparison.FindingFingerprint);
        if (!Enum.IsDefined(comparison.Kind))
        {
            throw new ArgumentException($"Unknown comparison kind '{comparison.Kind}'.", nameof(comparison));
        }

        return comparison with { FindingFingerprint = comparison.FindingFingerprint.Trim() };
    }
}

/// <summary>
/// Built-in policy preset names.
/// </summary>
public static class PolicyPresets
{
    /// <summary>Balanced default policy.</summary>
    public const string Balanced = "balanced";

    /// <summary>Security-focused policy.</summary>
    public const string SecurityFocused = "security-focused";

    /// <summary>Quality-focused policy.</summary>
    public const string QualityFocused = "quality-focused";

    /// <summary>API contract policy.</summary>
    public const string ApiContract = "api-contract";

    /// <summary>CI-strict policy.</summary>
    public const string CiStrict = "ci-strict";
}
