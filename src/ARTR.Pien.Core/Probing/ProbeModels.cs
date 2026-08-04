using System.Net;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Probing;

/// <summary>
/// HTTP method allowed for probing. Mutating methods require explicit authorization in higher layers.
/// </summary>
public enum ProbeMethod
{
    /// <summary>HTTP GET.</summary>
    Get = 0,

    /// <summary>HTTP HEAD.</summary>
    Head = 1,

    /// <summary>HTTP OPTIONS.</summary>
    Options = 2,

    /// <summary>HTTP POST (only when explicitly configured).</summary>
    Post = 3,

    /// <summary>HTTP PUT (only when explicitly configured).</summary>
    Put = 4,

    /// <summary>HTTP PATCH (only when explicitly configured).</summary>
    Patch = 5,

    /// <summary>HTTP DELETE (only when explicitly configured).</summary>
    Delete = 6,
}

/// <summary>
/// Immutable request describing a single probe operation.
/// </summary>
public sealed record ProbeRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProbeRequest"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ProbeRequest()
    {
    }

    /// <summary>Absolute request URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>HTTP method.</summary>
    public required ProbeMethod Method { get; init; }

    /// <summary>Approved request headers (names and values must already be validated upstream).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional request body bytes (bounded by scan limits).</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Optional content type for the body.</summary>
    public string? ContentType { get; init; }

    /// <summary>Maximum body bytes to read from the response.</summary>
    public long MaxResponseBodyBytes { get; init; } = 5L * 1024 * 1024;

    /// <summary>
    /// Creates a validated probe request.
    /// </summary>
    /// <param name="request">Candidate request.</param>
    /// <returns>The validated request.</returns>
    public static ProbeRequest Create(ProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Uri);
        ArgumentNullException.ThrowIfNull(request.Headers);

        if (!request.Uri.IsAbsoluteUri)
        {
            throw new ArgumentException("Probe URI must be absolute.", nameof(request));
        }

        if (request.Uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Probe URI scheme must be http or https.", nameof(request));
        }

        if (!Enum.IsDefined(request.Method))
        {
            throw new ArgumentException($"Unknown probe method '{request.Method}'.", nameof(request));
        }

        if (request.MaxResponseBodyBytes < 1)
        {
            throw new ArgumentException("Max response body bytes must be at least 1.", nameof(request));
        }

        return request;
    }
}

/// <summary>
/// Immutable result of a probe operation. Bodies and headers must already be size-bounded and redacted as needed.
/// </summary>
public sealed record ProbeResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProbeResult"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public ProbeResult()
    {
    }

    /// <summary>Final request URI after explicit redirect handling.</summary>
    public required Uri FinalUri { get; init; }

    /// <summary>HTTP status code when a response was received.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>Response headers (bounded and sanitized).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bounded response body bytes.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Response content type, if known.</summary>
    public string? ContentType { get; init; }

    /// <summary>Total elapsed time for the probe.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Whether the body was truncated to the configured limit.</summary>
    public bool BodyTruncated { get; init; }

    /// <summary>Redirect chain URIs (sanitized).</summary>
    public IReadOnlyList<Uri> RedirectChain { get; init; } = [];

    /// <summary>Optional transport error message (never includes secrets).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a validated probe result.
    /// </summary>
    /// <param name="result">Candidate result.</param>
    /// <returns>The validated result.</returns>
    public static ProbeResult Create(ProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.FinalUri);
        ArgumentNullException.ThrowIfNull(result.Headers);
        ArgumentNullException.ThrowIfNull(result.RedirectChain);

        if (result.Duration < TimeSpan.Zero)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(result));
        }

        return result;
    }
}

/// <summary>
/// Aggregated inspection evidence available to checks for a target.
/// </summary>
public sealed record InspectionEvidence
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InspectionEvidence"/> record.
    /// Prefer <see cref="Create"/> for validated instances.
    /// </summary>
    public InspectionEvidence()
    {
    }

    /// <summary>Target that produced the evidence.</summary>
    public required ScanTarget Target { get; init; }

    /// <summary>Primary probe results keyed by logical name.</summary>
    public IReadOnlyDictionary<string, ProbeResult> Probes { get; init; } =
        new Dictionary<string, ProbeResult>(StringComparer.Ordinal);

    /// <summary>Optional TLS certificate fingerprint (SHA-256 hex), if observed.</summary>
    public string? TlsCertificateFingerprint { get; init; }

    /// <summary>Optional content fingerprint for change detection.</summary>
    public string? ContentFingerprint { get; init; }

    /// <summary>Additional non-secret notes.</summary>
    public IReadOnlyDictionary<string, string> Notes { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Creates validated inspection evidence.
    /// </summary>
    /// <param name="evidence">Candidate evidence.</param>
    /// <returns>The validated evidence.</returns>
    public static InspectionEvidence Create(InspectionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(evidence.Target);
        ArgumentNullException.ThrowIfNull(evidence.Probes);
        ArgumentNullException.ThrowIfNull(evidence.Notes);
        ScanTarget.Create(evidence.Target);
        return evidence;
    }
}

/// <summary>
/// Executes a network or transport probe. Implementations live outside Core.
/// </summary>
public interface IProbe
{
    /// <summary>Stable probe name.</summary>
    string Name { get; }

    /// <summary>
    /// Executes the probe.
    /// </summary>
    /// <param name="request">Probe request.</param>
    /// <param name="context">Scan context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Probe result.</returns>
    Task<ProbeResult> ExecuteAsync(
        ProbeRequest request,
        ScanContext context,
        CancellationToken cancellationToken = default);
}
