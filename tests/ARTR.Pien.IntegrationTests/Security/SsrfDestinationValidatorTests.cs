using System.Net;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Web.Network;

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
