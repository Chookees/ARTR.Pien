using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Web.Tls;

/// <summary>
/// Performs a bounded TLS handshake probe without sending an HTTP request body.
/// </summary>
public sealed class TlsProbe : ITlsProbe
{
    private readonly IDestinationValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TlsProbe"/> class.
    /// </summary>
    public TlsProbe(IDestinationValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public async Task<TlsProbeResult> ProbeAsync(Uri uri, ScanLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(limits);

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new TlsFailureException("TLS probe requires an https URI.");
        }

        var options = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = [uri.IdnHost, "127.0.0.1", "::1", "localhost"],
            MaxDnsAddresses = limits.MaxDnsAddresses,
        };
        var endpoint = await _validator.ValidateAsync(uri, options, cancellationToken).ConfigureAwait(false);
        var address = endpoint.Addresses[0];

        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(limits.ConnectTimeout);
        await socket.ConnectAsync(address, endpoint.Uri.Port, timeoutCts.Token).ConfigureAwait(false);
        await using var network = new NetworkStream(socket, ownsSocket: false);
        await using var ssl = new SslStream(network, leaveInnerStreamOpen: true);

        try
        {
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = endpoint.HostHeader,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TlsFailureException("TLS negotiation failed.", ex);
        }

        using var cert = ssl.RemoteCertificate is null
            ? null
            : new X509Certificate2(ssl.RemoteCertificate);
        string? fingerprint = null;
        if (cert is not null)
        {
            fingerprint = Convert.ToHexString(SHA256.HashData(cert.RawData));
        }

        return new TlsProbeResult(
            Protocol: ssl.SslProtocol.ToString(),
            CipherAlgorithm: ssl.NegotiatedCipherSuite.ToString(),
            CertificateNotBefore: cert?.NotBefore.ToUniversalTime(),
            CertificateNotAfter: cert?.NotAfter.ToUniversalTime(),
            CertificateFingerprintSha256: fingerprint,
            Subject: cert?.Subject,
            Issuer: cert?.Issuer);
    }
}
