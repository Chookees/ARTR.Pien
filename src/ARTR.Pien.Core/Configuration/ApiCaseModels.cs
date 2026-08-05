using System.Text.Json;

namespace ARTR.Pien.Configuration;

/// <summary>
/// Configured API test case from <c>pien.json</c>.
/// </summary>
public sealed class PienApiCaseConfiguration
{
    /// <summary>Stable case id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Optional display name.</summary>
    public string? Name { get; set; }

    /// <summary>HTTP method (GET, HEAD, OPTIONS, POST, PUT, PATCH, DELETE).</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Absolute URL or path relative to the target base URL.</summary>
    public string Path { get; set; } = "/";

    /// <summary>Optional query parameters.</summary>
    public Dictionary<string, JsonElement>? Query { get; set; }

    /// <summary>Non-secret request headers.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Optional per-case authentication override.</summary>
    public PienAuthenticationConfiguration? Authentication { get; set; }

    /// <summary>Optional request body text.</summary>
    public string? Body { get; set; }

    /// <summary>Optional request body secret reference.</summary>
    public string? BodySecretReference { get; set; }

    /// <summary>Optional request content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Expected status (int) or range object with min/max.</summary>
    public JsonElement? ExpectedStatus { get; set; }

    /// <summary>Expected Content-Type substring.</summary>
    public string? ExpectedContentType { get; set; }

    /// <summary>Optional response time budget in milliseconds.</summary>
    public int? ResponseTimeBudgetMs { get; set; }

    /// <summary>Optional per-case body byte limit.</summary>
    public long? MaxBodyBytes { get; set; }

    /// <summary>Required response header names.</summary>
    public List<string> RequiredHeaders { get; set; } = [];

    /// <summary>Forbidden response header names.</summary>
    public List<string> ForbiddenHeaders { get; set; } = [];

    /// <summary>JSON Pointer assertions.</summary>
    public List<PienJsonAssertionConfiguration> JsonAssertions { get; set; } = [];

    /// <summary>Local JSON Schema path relative to the working directory.</summary>
    public string? JsonSchemaPath { get; set; }

    /// <summary>When true, POST/PUT/PATCH/DELETE are permitted.</summary>
    public bool AllowNonIdempotent { get; set; }
}

/// <summary>
/// JSON Pointer assertion configuration.
/// </summary>
public sealed class PienJsonAssertionConfiguration
{
    /// <summary>RFC 6901 JSON Pointer.</summary>
    public string Pointer { get; set; } = "";

    /// <summary>Assertion operator.</summary>
    public string Op { get; set; } = "exists";

    /// <summary>Comparison value for eq/contains.</summary>
    public JsonElement? Value { get; set; }

    /// <summary>Minimum for range/length.</summary>
    public double? Min { get; set; }

    /// <summary>Maximum for range/length.</summary>
    public double? Max { get; set; }

    /// <summary>Expected JSON type name.</summary>
    public string? TypeName { get; set; }

    /// <summary>Regex pattern for regex op.</summary>
    public string? Pattern { get; set; }
}

/// <summary>
/// Request authentication configuration (secret references only).
/// </summary>
public sealed class PienAuthenticationConfiguration
{
    /// <summary>Authentication scheme: none|bearer|basic|header|cookie.</summary>
    public string Scheme { get; set; } = "none";

    /// <summary>Secret reference for token/password/header/cookie value.</summary>
    public string? SecretReference { get; set; }

    /// <summary>Required when scheme is header.</summary>
    public string? HeaderName { get; set; }

    /// <summary>Optional username secret reference for basic auth.</summary>
    public string? UsernameSecretReference { get; set; }
}

/// <summary>
/// Baseline comparison options.
/// </summary>
public sealed class PienBaselinesConfiguration
{
    /// <summary>Baseline id to compare during scan.</summary>
    public string? Id { get; set; }

    /// <summary>When true, compare against baseline during scan.</summary>
    public bool CompareOnScan { get; set; }

    /// <summary>When true, new Fail findings vs baseline contribute to policy failure.</summary>
    public bool FailOnNew { get; set; }
}

/// <summary>
/// Finding suppression configuration entry.
/// </summary>
public sealed class PienSuppressionConfiguration
{
    /// <summary>Check id to suppress.</summary>
    public string CheckId { get; set; } = "";

    /// <summary>Optional finding fingerprint.</summary>
    public string? Fingerprint { get; set; }

    /// <summary>Optional human reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional expiry (ISO-8601).</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
