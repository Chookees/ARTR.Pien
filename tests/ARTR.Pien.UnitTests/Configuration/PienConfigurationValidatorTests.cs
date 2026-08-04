using ARTR.Pien.Configuration;

namespace ARTR.Pien.UnitTests.Configuration;

public sealed class PienConfigurationValidatorTests
{
    [Fact]
    public void Validates_minimal_authorized_target()
    {
        var configuration = new PienConfiguration
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

        var validated = PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory());
        Assert.Equal("local", validated.Definition.Targets[0].Id);
        Assert.True(validated.Network.AllowPrivateNetworks);
    }

    [Fact]
    public void Rejects_unconfirmed_authorization()
    {
        var configuration = new PienConfiguration
        {
            SchemaVersion = 1,
            Targets =
            [
                new PienTargetConfiguration
                {
                    Id = "local",
                    Kind = "website",
                    Url = "https://example.com/",
                    Authorization = new PienAuthorizationConfiguration { Confirmed = false },
                },
            ],
        };

        Assert.Throws<ARTR.Pien.Exceptions.ConfigurationException>(
            () => PienConfigurationValidator.Validate(configuration, Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void Ci_profile_sets_multi_format_output()
    {
        var configuration = new PienConfiguration();
        PienConfigurationValidator.ApplyProfileDefaults(configuration, "ci");
        Assert.Contains("sarif", configuration.Output.Formats);
        Assert.Contains("junit", configuration.Output.Formats);
    }
}
