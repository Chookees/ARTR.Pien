using System.Net;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Web.Network;

namespace ARTR.Pien.UnitTests.Network;

public sealed class IpAddressClassifierTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("8.8.8.8", false)]
    public void Classifies_ipv4_addresses(string ip, bool restricted)
    {
        var address = IPAddress.Parse(ip);
        Assert.Equal(restricted, IpAddressClassifier.IsRestricted(address) || IpAddressClassifier.IsLoopback(address));
    }

    [Fact]
    public async Task Destination_validator_rejects_private_without_allowlist()
    {
        var validator = new DestinationValidator();
        var options = new NetworkSafetyOptions { AllowPrivateNetworks = false };
        await Assert.ThrowsAsync<TargetSafetyException>(async () =>
            await validator.ValidateAsync(new Uri("http://127.0.0.1/"), options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Destination_validator_allows_loopback_when_allowlisted()
    {
        var validator = new DestinationValidator();
        var options = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1"],
        };

        var endpoint = await validator.ValidateAsync(
            new Uri("http://127.0.0.1:8080/"),
            options,
            TestContext.Current.CancellationToken);
        Assert.Equal("127.0.0.1", endpoint.HostHeader);
        Assert.Contains(endpoint.Addresses, a => IPAddress.IsLoopback(a));
    }
}
