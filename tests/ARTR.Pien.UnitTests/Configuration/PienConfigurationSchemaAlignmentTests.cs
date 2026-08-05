using System.Text.Json;
using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Configuration;

public sealed class PienConfigurationSchemaAlignmentTests
{
    [Fact]
    public async Task Json_loader_deserializes_complete_website_example_shape()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "config", "examples", "complete-website.json");
        var loader = new JsonConfigLoader();
        var configuration = await loader.LoadAsync(new ConfigLoadRequest(path, root), TestContext.Current.CancellationToken);

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal("deep", configuration.Profile);
        Assert.Equal(10, configuration.Network.ConnectTimeoutSeconds);
        Assert.Equal(30, configuration.Network.RequestTimeoutSeconds);
        Assert.Equal(500, configuration.Crawl.MaxLinksPerPage);
        Assert.Equal(PolicyPresets.SecurityFocused, configuration.Policies.Name);
        Assert.Equal("medium", configuration.Policies.FailOn);
        Assert.Equal("high", configuration.Policies.SeverityOverrides["PIEN-HEADERS-001"]);
        Assert.Equal("critical", configuration.Policies.SeverityOverrides["PIEN-TLS-001"]);
        Assert.False(configuration.Baselines.CompareOnScan);
        Assert.Equal("information", configuration.Logging.Level);
        Assert.False(configuration.Logging.IncludeScopes);
        Assert.Contains("html", configuration.Output.Formats);
        Assert.Empty(configuration.Notifications.Events);
    }

    [Fact]
    public async Task Json_loader_deserializes_authenticated_api_example()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "config", "examples", "authenticated-api.json");
        var loader = new JsonConfigLoader();
        var configuration = await loader.LoadAsync(new ConfigLoadRequest(path, root), TestContext.Current.CancellationToken);

        var target = Assert.Single(configuration.Targets);
        Assert.Equal("api", target.Kind);
        Assert.NotNull(target.Authentication);
        Assert.Equal("bearer", target.Authentication!.Scheme);
        Assert.Equal("secret://env/PIEN_SAMPLE_API_TOKEN", target.Authentication.SecretReference);
        Assert.Equal(2, target.ApiCases.Count);
        Assert.Equal("header", target.ApiCases[1].Authentication!.Scheme);
        Assert.Equal("X-Api-Key", target.ApiCases[1].Authentication!.HeaderName);
        Assert.Equal(PolicyPresets.ApiContract, configuration.Policies.Name);
        Assert.Equal("warning", configuration.Logging.Level);
    }

    [Fact]
    public void Validate_maps_timeouts_links_policy_and_baselines()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Network.ConnectTimeoutSeconds = 12;
        configuration.Network.RequestTimeoutSeconds = 45;
        configuration.Crawl.MaxLinksPerPage = 250;
        configuration.Policies.Name = PolicyPresets.CiStrict;
        configuration.Policies.FailOn = "critical";
        configuration.Policies.SeverityOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PIEN-TLS-001"] = "high",
        };
        configuration.Policies.Suppressions =
        [
            new PienSuppressionConfiguration
            {
                CheckId = "PIEN-HEADERS-001",
                Reason = "accepted risk",
            },
        ];
        configuration.Baselines.Id = "base-1";
        configuration.Baselines.CompareOnScan = true;
        configuration.Baselines.FailOnNew = true;
        configuration.Logging.Level = "debug";
        configuration.Notifications.Events = ["ScanCompleted", "BaselineChanged"];

        var validated = PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory());

        Assert.Equal(TimeSpan.FromSeconds(12), validated.Definition.Limits.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(45), validated.Definition.Limits.RequestTimeout);
        Assert.Equal(250, validated.Definition.Limits.MaxLinksPerPage);
        Assert.Equal(PolicyPresets.CiStrict, validated.Policy.Name);
        Assert.Equal(FindingSeverity.Critical, validated.Policy.FailOnSeverityAtOrAbove);
        Assert.Equal(FindingSeverity.High, validated.Policy.SeverityOverrides["PIEN-TLS-001"]);
        Assert.Single(validated.Policy.Suppressions);
        Assert.Equal("base-1", validated.Configuration.Baselines.Id);
        Assert.True(validated.Configuration.Baselines.CompareOnScan);
        Assert.Equal("debug", validated.Configuration.Logging.Level);
        Assert.Equal(2, validated.Configuration.Notifications.Events.Count);
    }

    [Fact]
    public void Validate_accepts_api_cases_with_status_range_and_assertions()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Targets[0].Kind = "api";
        configuration.Targets[0].OpenApiDocument = "openapi.json";
        configuration.Targets[0].Authentication = new PienAuthenticationConfiguration
        {
            Scheme = "bearer",
            SecretReference = "secret://env/TOKEN",
        };
        configuration.Targets[0].ApiCases =
        [
            new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                ExpectedStatus = JsonSerializer.SerializeToElement(new { min = 200, max = 204 }),
                JsonAssertions =
                [
                    new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
                ],
            },
        ];

        var validated = PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory());
        var target = Assert.Single(validated.Definition.Targets);
        Assert.Equal(ScanTargetKind.Api, target.Kind);
        Assert.Equal("openapi.json", target.OpenApiDocument);
        Assert.Single(target.ApiCases);
        Assert.NotNull(target.Authentication);
    }

    [Fact]
    public void Rejects_unknown_policy_preset()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Policies.Name = "not-a-preset";
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_invalid_authentication_scheme()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Targets[0].Authentication = new PienAuthenticationConfiguration
        {
            Scheme = "oauth",
            SecretReference = "secret://env/TOKEN",
        };
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_header_auth_without_header_name()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Targets[0].Authentication = new PienAuthenticationConfiguration
        {
            Scheme = "header",
            SecretReference = "secret://env/KEY",
        };
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_non_idempotent_without_allow_flag()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Targets[0].Kind = "api";
        configuration.Targets[0].ApiCases =
        [
            new PienApiCaseConfiguration
            {
                Id = "create",
                Method = "POST",
                Path = "/items",
                AllowNonIdempotent = false,
            },
        ];
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_invalid_logging_level()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Logging.Level = "verbose";
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_unknown_output_format()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Output.Formats = ["console", "xml"];
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Rejects_connect_timeout_out_of_range()
    {
        var configuration = CreateAuthorizedWebsite();
        configuration.Network.ConnectTimeoutSeconds = 0;
        Assert.Throws<ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    private static PienConfiguration CreateAuthorizedWebsite()
        => new()
        {
            SchemaVersion = 1,
            Targets =
            [
                new PienTargetConfiguration
                {
                    Id = "local",
                    Kind = "website",
                    Url = "http://127.0.0.1:8080/",
                    Authorization = new PienAuthorizationConfiguration { Confirmed = true },
                },
            ],
            Network = new PienNetworkConfiguration
            {
                AllowPrivateNetworks = true,
                AllowedHosts = ["127.0.0.1"],
            },
        };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ARTR.Pien.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate ARTR.Pien.sln from the test working directory.");
    }
}
