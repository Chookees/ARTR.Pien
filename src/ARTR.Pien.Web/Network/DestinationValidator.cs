using System.Net;
using System.Net.Sockets;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;

namespace ARTR.Pien.Web.Network;

/// <summary>
/// Classifies IP addresses for SSRF resistance.
/// </summary>
public static class IpAddressClassifier
{
    /// <summary>
    /// Returns true when the address is loopback.
    /// </summary>
    public static bool IsLoopback(IPAddress address)
        => IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Loopback);

    /// <summary>
    /// Returns true when the address is link-local, unique-local, private, metadata, multicast, or unspecified.
    /// </summary>
    public static bool IsRestricted(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 10)
            {
                return true;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // Cloud metadata / special-use.
            if (bytes[0] == 169 && bytes[1] == 254 && bytes[2] == 169 && bytes[3] == 254)
            {
                return true;
            }

            if (bytes[0] >= 224)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            // Unique local fc00::/7
            if ((bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Canonicalizes URIs for scanning.
/// </summary>
public static class UriCanonicalizer
{
    /// <summary>
    /// Canonicalizes an absolute http(s) URI.
    /// </summary>
    public static Uri Canonicalize(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new TargetSafetyException("URI must be absolute.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new TargetSafetyException("Only http and https URIs are allowed.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new TargetSafetyException("URI host is required.");
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Host = uri.IdnHost,
        };

        if ((uri.Scheme == "http" && uri.Port == 80) || (uri.Scheme == "https" && uri.Port == 443))
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }
}

/// <summary>
/// Default destination validator with DNS resolution and IP classification.
/// </summary>
public sealed class DestinationValidator : IDestinationValidator
{
    /// <inheritdoc />
    public async Task<ValidatedEndpoint> ValidateAsync(
        Uri uri,
        NetworkSafetyOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(options);

        var canonical = UriCanonicalizer.Canonicalize(uri);
        var host = canonical.IdnHost;
        IPAddress[] addresses;

        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            addresses = resolved.Take(Math.Max(1, options.MaxDnsAddresses)).ToArray();
        }

        if (addresses.Length == 0)
        {
            throw new DnsFailureException($"DNS resolution produced no addresses for '{host}'.");
        }

        foreach (var address in addresses)
        {
            EnsureAddressAllowed(address, host, options);
        }

        return new ValidatedEndpoint(canonical, addresses, host);
    }

    private static void EnsureAddressAllowed(IPAddress address, string host, NetworkSafetyOptions options)
    {
        var restricted = IpAddressClassifier.IsRestricted(address);
        var loopback = IpAddressClassifier.IsLoopback(address);
        if (!restricted && !loopback)
        {
            return;
        }

        var allowlisted = options.AllowedHosts.Any(h =>
            string.Equals(h, host, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(h, address.ToString(), StringComparison.OrdinalIgnoreCase));

        if (options.AllowPrivateNetworks && allowlisted)
        {
            return;
        }

        throw new TargetSafetyException(
            $"Destination address '{address}' for host '{host}' is not allowed by network policy.");
    }
}
