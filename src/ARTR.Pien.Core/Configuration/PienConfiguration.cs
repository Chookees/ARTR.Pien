using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using ARTR.Pien.Scanning;

namespace ARTR.Pien.Configuration;

/// <summary>
/// Request describing how to load configuration.
/// </summary>
/// <param name="ConfigPath">Path to pien.json (optional).</param>
/// <param name="WorkingDirectory">Working directory for relative paths.</param>
/// <param name="ProfileOverride">Optional profile name override.</param>
/// <param name="EnvironmentVariables">Environment bag (defaults to process env).</param>
public sealed record ConfigLoadRequest(
    string? ConfigPath,
    string WorkingDirectory,
    string? ProfileOverride = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null);

/// <summary>
/// Root configuration document for ARTR Pien (<c>schemaVersion: 1</c>).
/// </summary>
public sealed class PienConfiguration
{
    /// <summary>Schema version. Must be 1 for this release.</summary>
    [Range(1, 1)]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Profile name supplying defaults.</summary>
    public string Profile { get; set; } = ScanProfileNames.Standard;

    /// <summary>Scan targets.</summary>
    public List<PienTargetConfiguration> Targets { get; set; } = [];

    /// <summary>Network options.</summary>
    public PienNetworkConfiguration Network { get; set; } = new();

    /// <summary>Crawl options.</summary>
    public PienCrawlConfiguration Crawl { get; set; } = new();

    /// <summary>Check enablement.</summary>
    public PienChecksConfiguration Checks { get; set; } = new();

    /// <summary>Policy options.</summary>
    public PienPolicyConfiguration Policies { get; set; } = new();

    /// <summary>Output options.</summary>
    public PienOutputConfiguration Output { get; set; } = new();

    /// <summary>Storage options.</summary>
    public PienStorageConfiguration Storage { get; set; } = new();

    /// <summary>Watch options.</summary>
    public PienWatchConfiguration Watch { get; set; } = new();

    /// <summary>Notification options.</summary>
    public PienNotificationConfiguration Notifications { get; set; } = new();

    /// <summary>Baseline comparison options.</summary>
    public PienBaselinesConfiguration Baselines { get; set; } = new();

    /// <summary>Logging options.</summary>
    public PienLoggingConfiguration Logging { get; set; } = new();

    /// <summary>Effective scan limits after merge.</summary>
    [JsonIgnore]
    public ScanLimits EffectiveLimits { get; set; } = ScanLimits.Default;
}

/// <summary>Target configuration entry.</summary>
public sealed class PienTargetConfiguration
{
    /// <summary>Target id.</summary>
    [Required]
    public string Id { get; set; } = "";

    /// <summary>website or api.</summary>
    [Required]
    public string Kind { get; set; } = "website";

    /// <summary>Absolute http(s) URL.</summary>
    [Required]
    public string Url { get; set; } = "";

    /// <summary>Authorization block.</summary>
    public PienAuthorizationConfiguration Authorization { get; set; } = new();

    /// <summary>Optional request authentication (secret references only).</summary>
    public PienAuthenticationConfiguration? Authentication { get; set; }

    /// <summary>Local OpenAPI 3.x document path (JSON/YAML).</summary>
    public string? OpenApiDocument { get; set; }

    /// <summary>Explicit API test cases.</summary>
    public List<PienApiCaseConfiguration> ApiCases { get; set; } = [];
}

/// <summary>Authorization acknowledgement.</summary>
public sealed class PienAuthorizationConfiguration
{
    /// <summary>Operator confirmed authorization.</summary>
    public bool Confirmed { get; set; }

    /// <summary>Optional notes (no secrets).</summary>
    public string? Notes { get; set; }
}

/// <summary>Network configuration.</summary>
public sealed class PienNetworkConfiguration
{
    /// <summary>Allow private/link-local when host is allowlisted.</summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>Allowed hosts (exact or trailing-dot FQDN).</summary>
    public List<string> AllowedHosts { get; set; } = [];

    /// <summary>Max redirects.</summary>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>User-Agent header.</summary>
    public string UserAgent { get; set; } = "ARTR-Pien/0.1 (+https://github.com/ARTR-Projects/Pien)";

    /// <summary>TCP/TLS connect timeout in seconds.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>Per-request timeout in seconds.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}

/// <summary>Crawl configuration.</summary>
public sealed class PienCrawlConfiguration
{
    /// <summary>Max pages.</summary>
    public int MaxPages { get; set; } = 100;

    /// <summary>Max depth.</summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>Respect robots.txt.</summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>Use sitemap.xml seeds.</summary>
    public bool UseSitemap { get; set; } = true;

    /// <summary>Same-origin only.</summary>
    public bool SameOriginOnly { get; set; } = true;

    /// <summary>Maximum links processed per page.</summary>
    public int MaxLinksPerPage { get; set; } = 1_000;
}

/// <summary>Check configuration.</summary>
public sealed class PienChecksConfiguration
{
    /// <summary>Explicitly enabled check IDs (empty = all applicable).</summary>
    public List<string> Enabled { get; set; } = [];

    /// <summary>Disabled check IDs.</summary>
    public List<string> Disabled { get; set; } = [];
}

/// <summary>Policy configuration.</summary>
public sealed class PienPolicyConfiguration
{
    /// <summary>Built-in policy preset name.</summary>
    public string Name { get; set; } = Policy.PolicyPresets.Balanced;

    /// <summary>Fail-on severity: info|low|medium|high|critical.</summary>
    public string FailOn { get; set; } = "high";

    /// <summary>Per-check severity overrides keyed by stable check ID.</summary>
    public Dictionary<string, string> SeverityOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Finding suppressions.</summary>
    public List<PienSuppressionConfiguration> Suppressions { get; set; } = [];
}

/// <summary>Output configuration.</summary>
public sealed class PienOutputConfiguration
{
    /// <summary>Output directory.</summary>
    public string Directory { get; set; } = "./artifacts/pien";

    /// <summary>Formats: console,json,sarif,junit,markdown,html.</summary>
    public List<string> Formats { get; set; } = ["console", "json"];
}

/// <summary>Storage configuration.</summary>
public sealed class PienStorageConfiguration
{
    /// <summary>State directory (default .pien).</summary>
    public string StateDirectory { get; set; } = ".pien";

    /// <summary>Max retained runs.</summary>
    public int RetainRuns { get; set; } = 50;
}

/// <summary>Watch configuration.</summary>
public sealed class PienWatchConfiguration
{
    /// <summary>Interval between scans.</summary>
    public int IntervalSeconds { get; set; } = 300;
}

/// <summary>Notification configuration.</summary>
public sealed class PienNotificationConfiguration
{
    /// <summary>Optional webhook URL (https).</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional HMAC secret reference.</summary>
    public string? WebhookSecretReference { get; set; }

    /// <summary>When true, webhook failure yields exit 9.</summary>
    public bool Required { get; set; }

    /// <summary>Events that trigger notifications. Empty means all when webhook URL is set.</summary>
    public List<string> Events { get; set; } = [];
}

/// <summary>Logging configuration.</summary>
public sealed class PienLoggingConfiguration
{
    /// <summary>Log level name.</summary>
    public string Level { get; set; } = "information";

    /// <summary>Whether to include logging scopes.</summary>
    public bool IncludeScopes { get; set; }
}
