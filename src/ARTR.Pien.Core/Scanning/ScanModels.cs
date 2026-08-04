using ARTR.Pien.Checks;
using ARTR.Pien.Findings;

namespace ARTR.Pien.Scanning;

/// <summary>
/// Kind of scan target.
/// </summary>
public enum ScanTargetKind
{
    /// <summary>Website / HTML surface.</summary>
    Website = 0,

    /// <summary>HTTP API surface.</summary>
    Api = 1,
}

/// <summary>
/// Explicit authorization acknowledgement required before a target may be scanned.
/// </summary>
/// <param name="Confirmed">Whether the operator confirmed authorization to scan the target.</param>
/// <param name="ConfirmedAt">Optional UTC confirmation timestamp.</param>
/// <param name="Notes">Optional operator notes (must not contain secrets).</param>
public sealed record TargetAuthorization(bool Confirmed, DateTimeOffset? ConfirmedAt = null, string? Notes = null);

/// <summary>
/// Immutable scan target definition.
/// </summary>
public sealed record ScanTarget
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanTarget"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ScanTarget()
    {
    }

    /// <summary>Unique target identifier within a scan definition.</summary>
    public required string Id { get; init; }

    /// <summary>Target kind.</summary>
    public required ScanTargetKind Kind { get; init; }

    /// <summary>Absolute base URL using http or https.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Authorization acknowledgement.</summary>
    public required TargetAuthorization Authorization { get; init; }

    /// <summary>
    /// Creates a validated scan target.
    /// </summary>
    /// <param name="target">Candidate target.</param>
    /// <returns>The validated target.</returns>
    public static ScanTarget Create(ScanTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Id);
        ArgumentNullException.ThrowIfNull(target.BaseUrl);
        ArgumentNullException.ThrowIfNull(target.Authorization);

        if (!target.BaseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Target base URL must be absolute.", nameof(target));
        }

        if (target.BaseUrl.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Target base URL scheme must be http or https.", nameof(target));
        }

        if (!Enum.IsDefined(target.Kind))
        {
            throw new ArgumentException($"Unknown target kind '{target.Kind}'.", nameof(target));
        }

        return target with { Id = target.Id.Trim() };
    }
}

/// <summary>
/// Built-in scan profile names that supply defaults only.
/// </summary>
public static class ScanProfileNames
{
    /// <summary>Fast, low-depth scan.</summary>
    public const string Quick = "quick";

    /// <summary>Balanced default profile.</summary>
    public const string Standard = "standard";

    /// <summary>Deeper crawl and broader checks.</summary>
    public const string Deep = "deep";

    /// <summary>API-focused profile.</summary>
    public const string Api = "api";

    /// <summary>CI-oriented strict profile.</summary>
    public const string Ci = "ci";
}

/// <summary>
/// Immutable named profile providing default limits and enabled checks.
/// </summary>
public sealed record ScanProfile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanProfile"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ScanProfile()
    {
    }

    /// <summary>Profile name.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Default limits applied by the profile.</summary>
    public required ScanLimits Limits { get; init; }

    /// <summary>Check IDs enabled by default for this profile; empty means engine defaults.</summary>
    public IReadOnlyList<string> EnabledCheckIds { get; init; } = [];

    /// <summary>
    /// Creates a validated profile.
    /// </summary>
    /// <param name="profile">Candidate profile.</param>
    /// <returns>The validated profile.</returns>
    public static ScanProfile Create(ScanProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Description);
        ArgumentNullException.ThrowIfNull(profile.Limits);
        ArgumentNullException.ThrowIfNull(profile.EnabledCheckIds);
        profile.Limits.Validate();
        return profile with { Name = profile.Name.Trim() };
    }
}

/// <summary>
/// Versioned scan definition assembled from configuration.
/// </summary>
public sealed record ScanDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanDefinition"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ScanDefinition()
    {
    }

    /// <summary>Configuration schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Selected profile name.</summary>
    public required string ProfileName { get; init; }

    /// <summary>Targets to scan.</summary>
    public required IReadOnlyList<ScanTarget> Targets { get; init; }

    /// <summary>Effective limits after merging defaults and configuration.</summary>
    public required ScanLimits Limits { get; init; }

    /// <summary>Explicitly enabled check IDs; empty means all registered defaults.</summary>
    public IReadOnlyList<string> EnabledCheckIds { get; init; } = [];

    /// <summary>Explicitly disabled check IDs.</summary>
    public IReadOnlyList<string> DisabledCheckIds { get; init; } = [];

    /// <summary>
    /// Creates a validated scan definition.
    /// </summary>
    /// <param name="definition">Candidate definition.</param>
    /// <returns>The validated definition.</returns>
    public static ScanDefinition Create(ScanDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.ProfileName);
        ArgumentNullException.ThrowIfNull(definition.Targets);
        ArgumentNullException.ThrowIfNull(definition.Limits);
        ArgumentNullException.ThrowIfNull(definition.EnabledCheckIds);
        ArgumentNullException.ThrowIfNull(definition.DisabledCheckIds);

        if (definition.SchemaVersion < 1)
        {
            throw new ArgumentException("Schema version must be at least 1.", nameof(definition));
        }

        if (definition.Targets.Count == 0)
        {
            throw new ArgumentException("At least one target is required.", nameof(definition));
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in definition.Targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            ScanTarget.Create(target);
            if (!ids.Add(target.Id))
            {
                throw new ArgumentException($"Duplicate target id '{target.Id}'.", nameof(definition));
            }
        }

        definition.Limits.Validate();
        return definition with { ProfileName = definition.ProfileName.Trim() };
    }
}

/// <summary>
/// Planned work items for a scan run.
/// </summary>
public sealed record ScanPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanPlan"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ScanPlan()
    {
    }

    /// <summary>Source scan definition.</summary>
    public required ScanDefinition Definition { get; init; }

    /// <summary>Ordered check identifiers selected for execution.</summary>
    public required IReadOnlyList<CheckId> SelectedChecks { get; init; }

    /// <summary>Effective limits for the plan.</summary>
    public required ScanLimits Limits { get; init; }

    /// <summary>
    /// Creates a validated scan plan.
    /// </summary>
    /// <param name="plan">Candidate plan.</param>
    /// <returns>The validated plan.</returns>
    public static ScanPlan Create(ScanPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Definition);
        ArgumentNullException.ThrowIfNull(plan.SelectedChecks);
        ArgumentNullException.ThrowIfNull(plan.Limits);
        ScanDefinition.Create(plan.Definition);
        plan.Limits.Validate();
        return plan;
    }
}

/// <summary>
/// Mutable-lifecycle immutable snapshot of a scan run.
/// </summary>
public sealed record ScanRun
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanRun"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ScanRun()
    {
    }

    /// <summary>Run identifier.</summary>
    public required ScanRunId Id { get; init; }

    /// <summary>Plan executed by the run.</summary>
    public required ScanPlan Plan { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required ScanRunStatus Status { get; init; }

    /// <summary>UTC start time, if started.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>UTC completion time, if finished.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Findings aggregated so far.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>
    /// Creates a validated scan run snapshot.
    /// </summary>
    /// <param name="run">Candidate run.</param>
    /// <returns>The validated run.</returns>
    public static ScanRun Create(ScanRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(run.Id);
        ArgumentNullException.ThrowIfNull(run.Plan);
        ArgumentNullException.ThrowIfNull(run.Findings);
        if (!Enum.IsDefined(run.Status))
        {
            throw new ArgumentException($"Unknown status '{run.Status}'.", nameof(run));
        }

        ScanPlan.Create(run.Plan);
        return run;
    }
}

/// <summary>
/// Runtime context available to probes and checks during a scan.
/// </summary>
public sealed class ScanContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanContext"/> class.
    /// </summary>
    /// <param name="runId">Active run identifier.</param>
    /// <param name="definition">Active scan definition.</param>
    /// <param name="limits">Effective limits.</param>
    /// <param name="target">Current target.</param>
    /// <param name="utcNow">UTC clock callback.</param>
    /// <param name="properties">Optional bag of non-secret contextual properties.</param>
    public ScanContext(
        ScanRunId runId,
        ScanDefinition definition,
        ScanLimits limits,
        ScanTarget target,
        Func<DateTimeOffset> utcNow,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(utcNow);
        limits.Validate();

        RunId = runId;
        Definition = definition;
        Limits = limits;
        Target = target;
        UtcNow = utcNow;
        Properties = properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Active run identifier.</summary>
    public ScanRunId RunId { get; }

    /// <summary>Active scan definition.</summary>
    public ScanDefinition Definition { get; }

    /// <summary>Effective limits.</summary>
    public ScanLimits Limits { get; }

    /// <summary>Current target.</summary>
    public ScanTarget Target { get; }

    /// <summary>UTC clock callback.</summary>
    public Func<DateTimeOffset> UtcNow { get; }

    /// <summary>Non-secret contextual properties.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; }
}
