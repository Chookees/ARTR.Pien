namespace ARTR.Pien.Scanning;

/// <summary>
/// Lifecycle state of a scan run.
/// </summary>
public enum ScanRunStatus
{
    /// <summary>The run has been created but has not started.</summary>
    Pending = 0,

    /// <summary>The run is actively executing.</summary>
    Running = 1,

    /// <summary>The run completed successfully (policy outcome is separate).</summary>
    Completed = 2,

    /// <summary>The run failed due to an execution error.</summary>
    Failed = 3,

    /// <summary>The run was cancelled.</summary>
    Cancelled = 4,
}
