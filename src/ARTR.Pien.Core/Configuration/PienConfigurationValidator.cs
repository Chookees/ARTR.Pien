using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Limits;
using ARTR.Pien.Scanning;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.Configuration;

/// <summary>
/// Validates and normalizes <see cref="PienConfiguration"/>.
/// </summary>
public static class PienConfigurationValidator
{
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
        var policy = Policy.Policy.Create(new Policy.Policy
        {
            Name = "default",
            Description = "Configuration-derived policy",
            FailOnSeverityAtOrAbove = failOn,
            EnabledCheckIds = configuration.Checks.Enabled,
            DisabledCheckIds = configuration.Checks.Disabled,
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

        if (configuration.Targets.Count == 0)
        {
            results.Add(new ValidationResult("At least one target is required."));
        }

        ValidateTargets(configuration, results);
        ValidateLimits(configuration, results);
        ValidateChecksAndPolicy(configuration, results);
        ValidateNotifications(configuration, results);
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
                results.Add(new ValidationResult($"Target '{target.Id}' requires authorization.confirmed=true."));
            }

            if (!IsKnownKind(target.Kind))
            {
                results.Add(new ValidationResult($"Target '{target.Id}' kind must be website or api."));
            }
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

        if (!TryParseSeverity(configuration.Policies.FailOn, out _))
        {
            results.Add(new ValidationResult("policies.failOn must be info|low|medium|high|critical."));
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

        if (configuration.Network.MaxRedirects is < HardLimits.MinPositiveCount or > HardLimits.MaxRedirects)
        {
            results.Add(new ValidationResult($"network.maxRedirects must be between {HardLimits.MinPositiveCount} and {HardLimits.MaxRedirects}."));
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
        });

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
        var path = string.IsNullOrWhiteSpace(request.ConfigPath)
            ? Path.Combine(request.WorkingDirectory, "pien.json")
            : request.ConfigPath;

        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            var loaded = await JsonSerializer.DeserializeAsync<PienConfiguration>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            if (loaded is not null)
            {
                configuration = loaded;
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
