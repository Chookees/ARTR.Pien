namespace ARTR.Pien;

/// <summary>
/// Stable process exit codes returned by the Pien CLI and hostable entry points.
/// </summary>
public enum PienExitCode
{
    /// <summary>Scan completed and policy passed.</summary>
    Success = 0,

    /// <summary>Policy threshold failed.</summary>
    PolicyFailed = 1,

    /// <summary>Invalid command-line arguments.</summary>
    InvalidArguments = 2,

    /// <summary>Invalid configuration.</summary>
    InvalidConfiguration = 3,

    /// <summary>Target authorization or safety validation failed.</summary>
    TargetRejected = 4,

    /// <summary>Scan execution failed.</summary>
    ScanFailed = 5,

    /// <summary>Report generation failed.</summary>
    ReportFailed = 6,

    /// <summary>Baseline operation failed.</summary>
    BaselineFailed = 7,

    /// <summary>Local storage failed.</summary>
    StorageFailed = 8,

    /// <summary>Notification failed when configured as required.</summary>
    NotificationFailed = 9,

    /// <summary>Operation cancelled.</summary>
    Cancelled = 10,
}
