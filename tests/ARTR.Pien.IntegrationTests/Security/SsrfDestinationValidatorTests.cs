using System.Net;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Hosting;
using ARTR.Pien.Policy;
using ARTR.Pien.Scanning;
using ARTR.Pien.Web.Network;

using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.IntegrationTests.Security;

public sealed class SsrfDestinationValidatorTests
{
    private readonly DestinationValidator _validator = new();

    [Fact]
    public async Task Blocks_private_ip_without_allowlist()
    {
        var options = new NetworkSafetyOptions { AllowPrivateNetworks = false, AllowedHosts = [] };
        await Assert.ThrowsAsync<TargetSafetyException>(() =>
            _validator.ValidateAsync(new Uri("http://127.0.0.1/"), options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Engine_surfaces_TargetSafetyException_for_disallowed_private_target()
    {
        await using var provider = new ServiceCollection()
            .AddPien(o =>
            {
                o.AllowPrivateNetworks = false;
                o.AllowedHosts = [];
                o.StateDirectory = Path.Combine(Path.GetTempPath(), "pien-ssrf-" + Guid.NewGuid().ToString("N"));
            })
            .BuildServiceProvider();

        var engine = provider.GetRequiredService<IScanEngine>();
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets =
            [
                ScanTarget.Create(new ScanTarget
                {
                    Id = "ssrf",
                    Kind = ScanTargetKind.Website,
                    BaseUrl = new Uri("http://127.0.0.1:65530/"),
                    Authorization = new TargetAuthorization(true),
                }),
            ],
            Limits = ScanLimits.Default with { MaxCrawlPages = 1 },
            EnabledCheckIds = [CheckIds.Http001],
        });

        await Assert.ThrowsAsync<TargetSafetyException>(() =>
            engine.RunAsync(
                definition,
                new ScanEngineOptions
                {
                    Policy = Policy.Policy.Create(new Policy.Policy
                    {
                        Name = "balanced",
                        Description = "t",
                        FailOnSeverityAtOrAbove = Findings.FindingSeverity.Critical,
                    }),
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    Network = new NetworkSafetyOptions { AllowPrivateNetworks = false, AllowedHosts = [] },
                },
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Allows_loopback_when_allowlisted_and_private_permitted()
    {
        var options = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
        };
        var endpoint = await _validator.ValidateAsync(new Uri("http://127.0.0.1:18080/"), options, TestContext.Current.CancellationToken);
        Assert.Contains(endpoint.Addresses, IPAddress.IsLoopback);
    }

    [Fact]
    public async Task Rejects_non_http_scheme()
    {
        var options = new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] };
        await Assert.ThrowsAsync<TargetSafetyException>(() =>
            _validator.ValidateAsync(new Uri("file:///etc/passwd"), options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Classifier_marks_rfc1918_and_metadata_restricted()
    {
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("10.0.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("192.168.1.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("169.254.169.254")));
        Assert.True(IpAddressClassifier.IsLoopback(IPAddress.Loopback));
    }
}
