namespace ARTR.Pien.Findings;

/// <summary>
/// Describes the potential impact of a finding. Severity is independent of certainty.
/// </summary>
public enum FindingSeverity
{
    /// <summary>Informational observation with no direct security or quality impact.</summary>
    Info = 0,

    /// <summary>Low potential impact.</summary>
    Low = 1,

    /// <summary>Moderate potential impact.</summary>
    Medium = 2,

    /// <summary>High potential impact.</summary>
    High = 3,

    /// <summary>Critical potential impact requiring immediate attention.</summary>
    Critical = 4,
}
