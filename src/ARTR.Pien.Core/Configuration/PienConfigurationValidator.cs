using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Limits;
using ARTR.Pien.Policy;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.Configuration;

/// <summary>
/// Validates and normalizes <see cref="PienConfiguration"/>.
/// </summary>
public static class PienConfigurationValidator
{
    private static readonly HashSet<string> KnownProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ScanProfileNames.Quick,
        ScanProfileNames.Standard,
        ScanProfileNames.Deep,
        ScanProfileNames.Api,
        ScanProfileNames.Ci,
    };

    private static readonly HashSet<string> KnownPolicyPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        PolicyPresets.Balanced,
        PolicyPresets.SecurityFocused,
        PolicyPresets.QualityFocused,
        PolicyPresets.ApiContract,
        PolicyPresets.CiStrict,
    };

    private static readonly HashSet<string> KnownAuthSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "bearer", "basic", "header", "cookie",
    };

    private static readonly HashSet<string> KnownHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE",
    };

    private static readonly HashSet<string> NonIdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    private static readonly HashSet<string> KnownOutputFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "console", "json", "sarif", "junit", "markdown", "html",
    };

    private static readonly HashSet<string> KnownLoggingLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "trace", "debug", "information", "warning", "error", "critical", "none",
    };

    private static readonly HashSet<string> KnownNotificationEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "ScanCompleted", "ScanFailed", "ThresholdExceeded", "NewCriticalFinding", "BaselineChanged",
    };

    private static readonly HashSet<string> KnownJsonAssertionOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "exists", "absent", "eq", "contains", "type", "range", "length", "regex",
    };

    /// <summary>
    /// Validates configuration and returns a scan definition plus network options.
    /// </summary>
    public static ValidatedConfiguration Validate(PienConfiguration configuration, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var results = CollectValidationResults(configuration);
        if (results.Count > 0)
        {
            var message = string.Join(" ", results.Select(r => r.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
            throw new ConfigurationException(message);
        }

        var limits = BuildLimits(configuration);
        configuration.EffectiveLimits = limits;
        var definition = BuildDefinition(configuration, limits);
        _ = TryParseSeverity(configuration.Policies.FailOn, out var failOn);
        var severityOverrides = BuildSeverityOverrides(configuration.Policies.SeverityOverrides);
        var suppressions = configuration.Policies.Suppressions
            .Select(s => new PolicySuppression(s.CheckId.Trim(), s.Fingerprint, s.Reason, s.ExpiresAt))
            .ToArray();

        var policy = Policy.Policy.Create(new Policy.Policy
        {
            Name = configuration.Policies.Name.Trim(),
            Description = "Configuration-derived policy",
            FailOnSeverityAtOrAbove = failOn,
            EnabledCheckIds = configuration.Checks.Enabled,
            DisabledCheckIds = configuration.Checks.Disabled,
            SeverityOverrides = severityOverrides,
            Suppressions = suppressions,
        });

        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = configuration.Network.AllowPrivateNetworks,
            AllowedHosts = configuration.Network.AllowedHosts,
            MaxDnsAddresses = Math.Min(limits.MaxDnsAddresses, HardLimits.MaxDnsAddresses),
            MaxRedirects = Math.Min(configuration.Network.MaxRedirects, HardLimits.MaxRedirects),
        };

        return new ValidatedConfiguration(configuration, definition, policy, network, Path.GetFullPath(workingDirectory));
    }

    /// <summary>
    /// Applies a named profile's defaults onto a configuration object.
    /// </summary>
    public static void ApplyProfileDefaults(PienConfiguration configuration, string profile)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Profile = string.IsNullOrWhiteSpace(profile) ? ScanProfileNames.Standard : profile.Trim().ToLowerInvariant();

        switch (configuration.Profile)
        {
            case ScanProfileNames.Quick:
                configuration.Crawl.MaxPages = Math.Min(configuration.Crawl.MaxPages, 25);
                configuration.Crawl.MaxDepth = Math.Min(configuration.Crawl.MaxDepth, 2);
                break;
            case ScanProfileNames.Deep:
                configuration.Crawl.MaxPages = Math.Max(configuration.Crawl.MaxPages, 500);
                configuration.Crawl.MaxDepth = Math.Max(configuration.Crawl.MaxDepth, 8);
                break;
            case ScanProfileNames.Api:
                configuration.Crawl.MaxPages = Math.Min(configuration.Crawl.MaxPages, 10);
                configuration.Crawl.MaxDepth = Math.Min(configuration.Crawl.MaxDepth, 1);
                break;
            case ScanProfileNames.Ci:
                configuration.Output.Formats = ["console", "json", "sarif", "junit", "markdown"];
                configuration.Policies.FailOn = "high";
                break;
            default:
                break;
        }
    }

    private static List<ValidationResult> CollectValidationResults(PienConfiguration configuration)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(configuration, new ValidationContext(configuration), results, validateAllProperties: true);

        if (configuration.SchemaVersion != 1)
        {
            results.Add(new ValidationResult("schemaVersion must be 1."));
        }

        if (!KnownProfiles.Contains(configuration.Profile))
        {
            results.Add(new ValidationResult("profile must be quick|standard|deep|api|ci."));
        }

        if (configuration.Targets.Count == 0)
        {
            results.Add(new ValidationResult("At least one target is required."));
        }

        ValidateTargets(configuration, results);
        ValidateLimits(configuration, results);
        ValidateChecksAndPolicy(configuration, results);
        ValidateOutput(configuration, results);
        ValidateNotifications(configuration, results);
        ValidateLogging(configuration, results);
        ValidateWatch(configuration, results);
        return results;
    }

    private static void ValidateTargets(PienConfiguration configuration, List<ValidationResult> results)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in configuration.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Id) || !ids.Add(target.Id.Trim()))
            {
                results.Add(new ValidationResult($"Target id '{target.Id}' is missing or duplicated."));
            }

            if (!Uri.TryCreate(target.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                results.Add(new ValidationResult($"Target '{target.Id}' URL must be absolute http or https."));
            }

            if (!target.Authorization.Confirmed)
            {
                throw new AuthorizationException($"Target '{target.Id}' requires authorization.confirmed=true.");
            }

            if (!IsKnownKind(target.Kind))
            {
                results.Add(new ValidationResult($"Target '{target.Id}' kind must be website or api."));
            }

            ValidateAuthentication(target.Authentication, $"Target '{target.Id}' authentication", results);
            ValidateApiCases(target, results);
        }
    }

    private static void ValidateApiCases(PienTargetConfiguration target, List<ValidationResult> results)
    {
        var caseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var apiCase in target.ApiCases)
        {
            if (string.IsNullOrWhiteSpace(apiCase.Id) || !caseIds.Add(apiCase.Id.Trim()))
            {
                results.Add(new ValidationResult($"Target '{target.Id}' api case id '{apiCase.Id}' is missing or duplicated."));
            }

            if (string.IsNullOrWhiteSpace(apiCase.Path))
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' path is required."));
            }

            if (!KnownHttpMethods.Contains(apiCase.Method))
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' method must be GET|HEAD|OPTIONS|POST|PUT|PATCH|DELETE."));
            }
            else if (NonIdempotentMethods.Contains(apiCase.Method) && !apiCase.AllowNonIdempotent)
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' uses {apiCase.Method} and requires allowNonIdempotent=true."));
            }

            if (apiCase.ResponseTimeBudgetMs is <= 0)
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' responseTimeBudgetMs must be >= 1."));
            }

            if (apiCase.MaxBodyBytes is <= 0)
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' maxBodyBytes must be >= 1."));
            }

            if (!string.IsNullOrWhiteSpace(apiCase.BodySecretReference) &&
                !SecretReference.TryParse(apiCase.BodySecretReference, out _))
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' bodySecretReference is not a valid secret:// reference."));
            }

            ValidateAuthentication(apiCase.Authentication, $"API case '{apiCase.Id}' authentication", results);
            ValidateExpectedStatus(apiCase, results);
            ValidateJsonAssertions(apiCase, results);
        }
    }

    private static void ValidateExpectedStatus(PienApiCaseConfiguration apiCase, List<ValidationResult> results)
    {
        if (apiCase.ExpectedStatus is null)
        {
            return;
        }

        var element = apiCase.ExpectedStatus.Value;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var code))
        {
            if (code is < 100 or > 599)
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' expectedStatus must be between 100 and 599."));
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("min", out var minEl) &&
            element.TryGetProperty("max", out var maxEl) &&
            minEl.TryGetInt32(out var min) &&
            maxEl.TryGetInt32(out var max))
        {
            if (min is < 100 or > 599 || max is < 100 or > 599 || min > max)
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' expectedStatus range is invalid."));
            }

            return;
        }

        results.Add(new ValidationResult($"API case '{apiCase.Id}' expectedStatus must be an integer or {{min,max}} object."));
    }

    private static void ValidateJsonAssertions(PienApiCaseConfiguration apiCase, List<ValidationResult> results)
    {
        foreach (var assertion in apiCase.JsonAssertions)
        {
            if (string.IsNullOrWhiteSpace(assertion.Pointer))
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' json assertion pointer is required."));
            }

            if (!KnownJsonAssertionOps.Contains(assertion.Op))
            {
                results.Add(new ValidationResult($"API case '{apiCase.Id}' json assertion op '{assertion.Op}' is unsupported."));
            }
        }
    }

    private static void ValidateAuthentication(
        PienAuthenticationConfiguration? authentication,
        string context,
        List<ValidationResult> results)
    {
        if (authentication is null)
        {
            return;
        }

        if (!KnownAuthSchemes.Contains(authentication.Scheme))
        {
            results.Add(new ValidationResult($"{context} scheme must be none|bearer|basic|header|cookie."));
            return;
        }

        if (authentication.Scheme.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!SecretReference.TryParse(authentication.SecretReference, out _))
        {
            results.Add(new ValidationResult($"{context} requires a valid secretReference."));
        }

        if (authentication.Scheme.Equals("header", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(authentication.HeaderName))
        {
            results.Add(new ValidationResult($"{context} headerName is required when scheme is header."));
        }

        if (!string.IsNullOrWhiteSpace(authentication.UsernameSecretReference) &&
            !SecretReference.TryParse(authentication.UsernameSecretReference, out _))
        {
            results.Add(new ValidationResult($"{context} usernameSecretReference is not a valid secret:// reference."));
        }
    }

    private static void ValidateChecksAndPolicy(PienConfiguration configuration, List<ValidationResult> results)
    {
        foreach (var checkId in configuration.Checks.Enabled.Concat(configuration.Checks.Disabled))
        {
            if (!CheckIds.All.Contains(checkId, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new ValidationResult($"Unknown check id '{checkId}'."));
            }
        }

        if (!KnownPolicyPresets.Contains(configuration.Policies.Name))
        {
            results.Add(new ValidationResult("policies.name must be a known built-in policy preset."));
        }

        if (!TryParseSeverity(configuration.Policies.FailOn, out _))
        {
            results.Add(new ValidationResult("policies.failOn must be info|low|medium|high|critical."));
        }

        foreach (var pair in configuration.Policies.SeverityOverrides)
        {
            if (!CheckIds.All.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new ValidationResult($"Unknown severity override check id '{pair.Key}'."));
            }

            if (!TryParseSeverity(pair.Value, out _))
            {
                results.Add(new ValidationResult($"Invalid severity override '{pair.Value}' for '{pair.Key}'."));
            }
        }

        foreach (var suppression in configuration.Policies.Suppressions)
        {
            if (string.IsNullOrWhiteSpace(suppression.CheckId))
            {
                results.Add(new ValidationResult("policies.suppressions.checkId is required."));
            }
            else if (!CheckIds.All.Contains(suppression.CheckId, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new ValidationResult($"Unknown suppression check id '{suppression.CheckId}'."));
            }
        }
    }

    private static void ValidateOutput(PienConfiguration configuration, List<ValidationResult> results)
    {
        foreach (var format in configuration.Output.Formats)
        {
            if (!KnownOutputFormats.Contains(format))
            {
                results.Add(new ValidationResult($"Unknown output format '{format}'."));
            }
        }
    }

    private static void ValidateNotifications(PienConfiguration configuration, List<ValidationResult> results)
    {
        if (!string.IsNullOrWhiteSpace(configuration.Notifications.WebhookUrl))
        {
            if (!Uri.TryCreate(configuration.Notifications.WebhookUrl, UriKind.Absolute, out var hook) ||
                !string.Equals(hook.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ValidationResult("notifications.webhookUrl must be https."));
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.Notifications.WebhookSecretReference) &&
            !SecretReference.TryParse(configuration.Notifications.WebhookSecretReference, out _))
        {
            results.Add(new ValidationResult("notifications.webhookSecretReference is not a valid secret:// reference."));
        }

        foreach (var evt in configuration.Notifications.Events)
        {
            if (!KnownNotificationEvents.Contains(evt))
            {
                results.Add(new ValidationResult($"Unknown notification event '{evt}'."));
            }
        }
    }

    private static void ValidateLogging(PienConfiguration configuration, List<ValidationResult> results)
    {
        if (!KnownLoggingLevels.Contains(configuration.Logging.Level))
        {
            results.Add(new ValidationResult("logging.level must be trace|debug|information|warning|error|critical|none."));
        }
    }

    private static void ValidateWatch(PienConfiguration configuration, List<ValidationResult> results)
    {
        if (configuration.Watch.IntervalSeconds is < 1 or > 86_400)
        {
            results.Add(new ValidationResult("watch.intervalSeconds must be between 1 and 86400."));
        }
    }

    private static void ValidateLimits(PienConfiguration configuration, List<ValidationResult> results)
    {
        if (configuration.Crawl.MaxPages is < HardLimits.MinPositiveCount or > HardLimits.MaxCrawlPages)
        {
            results.Add(new ValidationResult($"crawl.maxPages must be between {HardLimits.MinPositiveCount} and {HardLimits.MaxCrawlPages}."));
        }

        if (configuration.Crawl.MaxDepth is < 0 or > HardLimits.MaxCrawlDepth)
        {
            results.Add(new ValidationResult($"crawl.maxDepth must be between 0 and {HardLimits.MaxCrawlDepth}."));
        }

        if (configuration.Crawl.MaxLinksPerPage is < HardLimits.MinPositiveCount or > HardLimits.MaxLinksPerPage)
        {
            results.Add(new ValidationResult($"crawl.maxLinksPerPage must be between {HardLimits.MinPositiveCount} and {HardLimits.MaxLinksPerPage}."));
        }

        if (configuration.Network.MaxRedirects is < HardLimits.MinPositiveCount or > HardLimits.MaxRedirects)
        {
            results.Add(new ValidationResult($"network.maxRedirects must be between {HardLimits.MinPositiveCount} and {HardLimits.MaxRedirects}."));
        }

        if (configuration.Network.ConnectTimeoutSeconds is < 1 or > 120)
        {
            results.Add(new ValidationResult("network.connectTimeoutSeconds must be between 1 and 120."));
        }

        if (configuration.Network.RequestTimeoutSeconds is < 1 or > 300)
        {
            results.Add(new ValidationResult("network.requestTimeoutSeconds must be between 1 and 300."));
        }

        if (configuration.Storage.RetainRuns < HardLimits.MinPositiveCount)
        {
            results.Add(new ValidationResult("storage.retainRuns must be >= 1."));
        }
    }

    private static ScanDefinition BuildDefinition(PienConfiguration configuration, ScanLimits limits)
    {
        var targets = configuration.Targets.Select(t => ScanTarget.Create(new ScanTarget
        {
            Id = t.Id.Trim(),
            Kind = ParseKind(t.Kind),
            BaseUrl = new Uri(t.Url, UriKind.Absolute),
            Authorization = new TargetAuthorization(t.Authorization.Confirmed, Notes: t.Authorization.Notes),
            Authentication = t.Authentication,
            OpenApiDocument = string.IsNullOrWhiteSpace(t.OpenApiDocument) ? null : t.OpenApiDocument.Trim(),
            ApiCases = t.ApiCases ?? [],
        })).ToArray();

        return ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = configuration.SchemaVersion,
            ProfileName = configuration.Profile,
            Targets = targets,
            Limits = limits,
            EnabledCheckIds = configuration.Checks.Enabled,
            DisabledCheckIds = configuration.Checks.Disabled,
        });
    }

    private static ScanLimits BuildLimits(PienConfiguration configuration)
        => ScanLimits.Create(ScanLimits.Default with
        {
            MaxCrawlPages = configuration.Crawl.MaxPages,
            MaxCrawlDepth = configuration.Crawl.MaxDepth,
            MaxRedirects = configuration.Network.MaxRedirects,
            MaxLinksPerPage = configuration.Crawl.MaxLinksPerPage,
            ConnectTimeout = TimeSpan.FromSeconds(configuration.Network.ConnectTimeoutSeconds),
            RequestTimeout = TimeSpan.FromSeconds(configuration.Network.RequestTimeoutSeconds),
        });

    private static IReadOnlyDictionary<string, FindingSeverity> BuildSeverityOverrides(
        Dictionary<string, string> overrides)
    {
        var map = new Dictionary<string, FindingSeverity>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides)
        {
            if (TryParseSeverity(pair.Value, out var severity))
            {
                map[pair.Key] = severity;
            }
        }

        return map;
    }

    private static bool IsKnownKind(string kind)
        => kind.Equals("website", StringComparison.OrdinalIgnoreCase) ||
           kind.Equals("api", StringComparison.OrdinalIgnoreCase);

    private static ScanTargetKind ParseKind(string kind)
        => kind.Equals("api", StringComparison.OrdinalIgnoreCase) ? ScanTargetKind.Api : ScanTargetKind.Website;

    private static bool TryParseSeverity(string value, out FindingSeverity severity)
    {
        severity = FindingSeverity.High;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out severity) && Enum.IsDefined(severity);
    }
}

/// <summary>
/// Validated configuration bundle ready for engine execution.
/// </summary>
public sealed record ValidatedConfiguration(
    PienConfiguration Configuration,
    ScanDefinition Definition,
    Policy.Policy Policy,
    NetworkSafetyOptions Network,
    string WorkingDirectory);

/// <summary>
/// JSON configuration loader with env overlay support.
/// </summary>
public sealed class JsonConfigLoader : IConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <inheritdoc />
    public async Task<PienConfiguration> LoadAsync(ConfigLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);

        var configuration = new PienConfiguration();
        var explicitPath = !string.IsNullOrWhiteSpace(request.ConfigPath);
        var path = explicitPath
            ? request.ConfigPath!
            : Path.Combine(request.WorkingDirectory, "pien.json");

        if (explicitPath && !File.Exists(path))
        {
            throw new ConfigurationException($"Config file not found: {path}");
        }

        if (File.Exists(path))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                var loaded = await JsonSerializer.DeserializeAsync<PienConfiguration>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (loaded is not null)
                {
                    configuration = loaded;
                }
            }
            catch (JsonException ex)
            {
                throw new ConfigurationException($"Config JSON is invalid: {ex.Message}", ex);
            }
        }

        PienConfigurationValidator.ApplyProfileDefaults(configuration, request.ProfileOverride ?? configuration.Profile);
        ApplyEnvironmentOverrides(configuration, request.EnvironmentVariables);
        return configuration;
    }

    private static void ApplyEnvironmentOverrides(
        PienConfiguration configuration,
        IReadOnlyDictionary<string, string?>? environment)
    {
        string? Get(string key)
        {
            if (environment is not null && environment.TryGetValue(key, out var value))
            {
                return value;
            }

            return Environment.GetEnvironmentVariable(key);
        }

        var failOn = Get("ARTR_PIEN_FAIL_ON");
        if (!string.IsNullOrWhiteSpace(failOn))
        {
            configuration.Policies.FailOn = failOn!;
        }

        if (int.TryParse(Get("ARTR_PIEN_MAX_PAGES"), out var pages))
        {
            configuration.Crawl.MaxPages = pages;
        }

        if (bool.TryParse(Get("ARTR_PIEN_ALLOW_PRIVATE_NETWORKS"), out var allow))
        {
            configuration.Network.AllowPrivateNetworks = allow;
        }
    }
}
