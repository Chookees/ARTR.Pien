using ARTR.Pien.Checks;
using ARTR.Pien.Findings;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;

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
    /// <summary>Saves a baseline atomically.</summary>
    Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default);

    /// <summary>Loads a baseline by identifier.</summary>
    Task<Baseline?> GetAsync(string baselineId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a baseline by identifier.</summary>
    Task<bool> DeleteAsync(string baselineId, CancellationToken cancellationToken = default);

    /// <summary>Lists baseline identifiers.</summary>
    Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists scan run history snapshots.
/// </summary>
public interface IRunHistoryStore
{
    /// <summary>Saves a run snapshot.</summary>
    Task SaveAsync(ScanRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a run by identifier.</summary>
    Task<ScanRun?> GetAsync(ScanRunId runId, CancellationToken cancellationToken = default);

    /// <summary>Lists recent runs, newest first.</summary>
    Task<IReadOnlyList<ScanRun>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>Deletes runs older than the retention policy.</summary>
    Task<int> CleanAsync(int keepCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends operator notifications. Implementations must never include secrets in payloads.
/// </summary>
public interface INotificationSender
{
    /// <summary>Sends a notification.</summary>
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// English pipeline stage names used for progress and UX (never Latin in CLI).
/// </summary>
public enum ScanStage
{
    /// <summary>Test / Proba.</summary>
    Testing = 0,

    /// <summary>Inspect / Inspice.</summary>
    Inspecting = 1,

    /// <summary>Examine / Examina.</summary>
    Examining = 2,

    /// <summary>Report / Nuntia.</summary>
    Reporting = 3,
}

/// <summary>
/// Progress snapshot for console UX.
/// </summary>
/// <param name="Stage">Current PIEN stage (English).</param>
/// <param name="Message">Human-readable status line.</param>
/// <param name="PercentComplete">Optional percent 0–100.</param>
/// <param name="CompletedChecks">Checks completed so far.</param>
/// <param name="TotalChecks">Total checks planned.</param>
public sealed record ScanProgress(
    ScanStage Stage,
    string Message,
    int? PercentComplete = null,
    int CompletedChecks = 0,
    int TotalChecks = 0);

/// <summary>
/// SSRF-safe outbound HTTP transport used by all scan probes.
/// </summary>
public interface ISafeHttpTransport
{
    /// <summary>Sends a probe request through validated destinations and explicit redirects.</summary>
    Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates destinations before connect (DNS + IP classification).
/// </summary>
public interface IDestinationValidator
{
    /// <summary>Validates a URI against network options.</summary>
    Task<ValidatedEndpoint> ValidateAsync(Uri uri, NetworkSafetyOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated endpoint binding host, ports, and allowed addresses.
/// </summary>
/// <param name="Uri">Canonical absolute URI.</param>
/// <param name="Addresses">Approved remote addresses.</param>
/// <param name="HostHeader">Host header / SNI name.</param>
public sealed record ValidatedEndpoint(Uri Uri, IReadOnlyList<System.Net.IPAddress> Addresses, string HostHeader);

/// <summary>
/// Network safety options for destination validation.
/// </summary>
public sealed record NetworkSafetyOptions
{
    /// <summary>When true, private/link-local addresses may be used if allowlisted.</summary>
    public bool AllowPrivateNetworks { get; init; }

    /// <summary>Explicit host allowlist (for loopback tests and approved private targets).</summary>
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    /// <summary>Maximum DNS addresses considered.</summary>
    public int MaxDnsAddresses { get; init; } = 16;

    /// <summary>Maximum redirect hops.</summary>
    public int MaxRedirects { get; init; } = 10;
}

/// <summary>
/// Iterative website crawler.
/// </summary>
public interface ICrawler
{
    /// <summary>Crawls a target within scan limits.</summary>
    IAsyncEnumerable<CrawlPage> CrawlAsync(ScanTarget target, ScanLimits limits, CancellationToken cancellationToken = default);
}

/// <summary>
/// A crawled page reference.
/// </summary>
/// <param name="Uri">Page URI.</param>
/// <param name="Depth">Depth from the seed.</param>
/// <param name="Referrer">Optional referrer URI.</param>
public sealed record CrawlPage(Uri Uri, int Depth, Uri? Referrer = null);

/// <summary>
/// TLS probe.
/// </summary>
public interface ITlsProbe
{
    /// <summary>Probes TLS for a URI.</summary>
    Task<TlsProbeResult> ProbeAsync(Uri uri, ScanLimits limits, CancellationToken cancellationToken = default);
}

/// <summary>
/// TLS probe result (non-secret).
/// </summary>
/// <param name="Protocol">Negotiated protocol string.</param>
/// <param name="CipherAlgorithm">Cipher algorithm name when available.</param>
/// <param name="CertificateNotBefore">Certificate not-before UTC.</param>
/// <param name="CertificateNotAfter">Certificate not-after UTC.</param>
/// <param name="CertificateFingerprintSha256">SHA-256 fingerprint hex.</param>
/// <param name="Subject">Certificate subject.</param>
/// <param name="Issuer">Certificate issuer.</param>
public sealed record TlsProbeResult(
    string Protocol,
    string? CipherAlgorithm,
    DateTimeOffset? CertificateNotBefore,
    DateTimeOffset? CertificateNotAfter,
    string? CertificateFingerprintSha256,
    string? Subject,
    string? Issuer);

/// <summary>
/// Loads and merges Pien configuration.
/// </summary>
public interface IConfigLoader
{
    /// <summary>Loads configuration from the given request.</summary>
    Task<Configuration.PienConfiguration> LoadAsync(Configuration.ConfigLoadRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the four-stage PIEN pipeline.
/// </summary>
public interface IScanEngine
{
    /// <summary>Runs a scan.</summary>
    Task<ScanRun> RunAsync(
        ScanDefinition definition,
        ScanEngineOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for a scan engine execution.
/// </summary>
public sealed record ScanEngineOptions
{
    /// <summary>Policy to evaluate.</summary>
    public required Policy.Policy Policy { get; init; }

    /// <summary>Working directory for relative paths.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Optional baseline id for comparison.</summary>
    public string? BaselineId { get; init; }

    /// <summary>When true, compare findings against the baseline during the scan.</summary>
    public bool CompareBaseline { get; init; }

    /// <summary>Network safety options.</summary>
    public NetworkSafetyOptions Network { get; init; } = new();

    /// <summary>Optional notification configuration snapshot.</summary>
    public Configuration.PienNotificationConfiguration? Notifications { get; init; }

    /// <summary>Optional storage retention count.</summary>
    public int RetainRuns { get; init; } = 50;
}

/// <summary>
/// Evaluates policy against findings.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>Evaluates policy.</summary>
    PolicyResult Evaluate(Policy.Policy policy, IReadOnlyList<Finding> findings);
}

/// <summary>
/// Computes advisory category scores.
/// </summary>
public interface IScoreCalculator
{
    /// <summary>Scores findings by category. Advisory only — never certification language.</summary>
    IReadOnlyDictionary<string, double> ScoreByCategory(IReadOnlyList<Finding> findings, IReadOnlyList<CheckResult> results);
}

/// <summary>
/// Facade over history, artifacts, and locks.
/// </summary>
public interface IScanStore
{
    /// <summary>Persists a run and optional report atomically.</summary>
    Task SaveRunAsync(ScanRun run, ReportDocument? report, CancellationToken cancellationToken = default);

    /// <summary>Loads a run.</summary>
    Task<ScanRun?> GetRunAsync(ScanRunId id, CancellationToken cancellationToken = default);

    /// <summary>Applies retention.</summary>
    Task CleanupAsync(int keepCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Catalog of registered checks.
/// </summary>
public interface ICheckCatalog
{
    /// <summary>Lists check definitions.</summary>
    IReadOnlyList<CheckDefinition> List();

    /// <summary>Gets a check by id.</summary>
    ICheck? Get(CheckId id);
}
