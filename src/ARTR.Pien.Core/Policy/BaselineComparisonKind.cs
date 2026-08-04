namespace ARTR.Pien.Policy;

/// <summary>
/// Classification of a finding relative to a stored baseline.
/// </summary>
public enum BaselineComparisonKind
{
    /// <summary>Present in the current run but absent from the baseline.</summary>
    New = 0,

    /// <summary>Present in the baseline but absent from the current run.</summary>
    Resolved = 1,

    /// <summary>Present in both with equivalent normalized content.</summary>
    Unchanged = 2,

    /// <summary>Present in both with a material normalized difference.</summary>
    Changed = 3,
}
