namespace ARTR.Pien.Findings;

/// <summary>
/// Outcome classification for a check evaluation. Execution errors must never be reported as <see cref="Pass"/>.
/// </summary>
public enum FindingStatus
{
    /// <summary>The check condition was satisfied.</summary>
    Pass = 0,

    /// <summary>The check condition was not satisfied.</summary>
    Fail = 1,

    /// <summary>A non-failing concern was observed.</summary>
    Warning = 2,

    /// <summary>The check does not apply to the inspected target.</summary>
    NotApplicable = 3,

    /// <summary>The check was intentionally not executed.</summary>
    Skipped = 4,

    /// <summary>The check could not complete due to an execution error.</summary>
    Error = 5,

    /// <summary>The finding was suppressed by policy or configuration.</summary>
    Suppressed = 6,
}
