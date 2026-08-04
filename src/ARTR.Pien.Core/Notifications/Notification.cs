using ARTR.Pien.Scanning;

namespace ARTR.Pien.Notifications;

/// <summary>
/// Notification severity for operator alerts.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Informational notification.</summary>
    Info = 0,

    /// <summary>Warning notification.</summary>
    Warning = 1,

    /// <summary>Error notification.</summary>
    Error = 2,

    /// <summary>Critical notification.</summary>
    Critical = 3,
}

/// <summary>
/// Immutable notification payload. Must never contain secrets.
/// </summary>
public sealed record Notification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Notification"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public Notification()
    {
    }

    /// <summary>Notification identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Related scan run, if any.</summary>
    public ScanRunId? RunId { get; init; }

    /// <summary>Short title.</summary>
    public required string Title { get; init; }

    /// <summary>Message body (redacted).</summary>
    public required string Message { get; init; }

    /// <summary>Notification severity.</summary>
    public required NotificationSeverity Severity { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Creates a validated notification.
    /// </summary>
    /// <param name="notification">Candidate notification.</param>
    /// <returns>The validated notification.</returns>
    public static Notification Create(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Message);
        if (!Enum.IsDefined(notification.Severity))
        {
            throw new ArgumentException($"Unknown severity '{notification.Severity}'.", nameof(notification));
        }

        return notification with
        {
            Id = notification.Id.Trim(),
            Title = notification.Title.Trim(),
        };
    }
}
