using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Abstractions;

/// <summary>
/// Provides the current UTC time. Prefer injecting this (or <see cref="TimeProvider"/>) over <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC timestamp.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// <see cref="IClock"/> adapter over <see cref="TimeProvider"/>.
/// </summary>
public sealed class TimeProviderClock : IClock
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeProviderClock"/> class.
    /// </summary>
    /// <param name="timeProvider">Time provider to wrap. Defaults to <see cref="TimeProvider.System"/>.</param>
    public TimeProviderClock(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
}

/// <summary>
/// Exports a <see cref="ReportDocument"/> to a destination stream.
/// </summary>
public interface IReportExporter
{
    /// <summary>Format name (for example <c>json</c> or <c>sarif</c>).</summary>
    string Format { get; }

    /// <summary>
    /// Exports the document.
    /// </summary>
    /// <param name="document">Report document.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when export finishes.</returns>
    Task ExportAsync(ReportDocument document, Stream destination, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists and retrieves baselines.
/// </summary>
public interface IBaselineStore
{
    /// <summary>
    /// Saves a baseline atomically.
    /// </summary>
    /// <param name="baseline">Baseline to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the baseline is saved.</returns>
    Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a baseline by identifier.
    /// </summary>
    /// <param name="baselineId">Baseline identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The baseline, or <see langword="null"/> when not found.</returns>
    Task<Baseline?> GetAsync(string baselineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a baseline by identifier.
    /// </summary>
    /// <param name="baselineId">Baseline identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a baseline was deleted.</returns>
    Task<bool> DeleteAsync(string baselineId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists scan run history snapshots.
/// </summary>
public interface IRunHistoryStore
{
    /// <summary>
    /// Saves a run snapshot.
    /// </summary>
    /// <param name="run">Scan run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the run is saved.</returns>
    Task SaveAsync(ScanRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a run by identifier.
    /// </summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run, or <see langword="null"/> when not found.</returns>
    Task<ScanRun?> GetAsync(ScanRunId runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists recent runs, newest first, bounded by <paramref name="maxCount"/>.
    /// </summary>
    /// <param name="maxCount">Maximum runs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent runs.</returns>
    Task<IReadOnlyList<ScanRun>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends operator notifications. Implementations must never include secrets in payloads.
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Sends a notification.
    /// </summary>
    /// <param name="notification">Notification payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the notification is accepted for delivery.</returns>
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
