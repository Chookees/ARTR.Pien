using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Web.Network;

namespace ARTR.Pien.Web.Transport;

/// <summary>
/// SSRF-resistant HTTP transport with explicit redirect handling and ConnectCallback pinning.
/// </summary>
public sealed class SafeHttpTransport : ISafeHttpTransport, IDisposable
{
    private readonly IDestinationValidator _destinationValidator;
    private readonly NetworkSafetyOptions _options;
    private readonly string _userAgent;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeHttpTransport"/> class.
    /// </summary>
    public SafeHttpTransport(
        IDestinationValidator destinationValidator,
        NetworkSafetyOptions options,
        string? userAgent = null)
    {
        _destinationValidator = destinationValidator ?? throw new ArgumentNullException(nameof(destinationValidator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _userAgent = string.IsNullOrWhiteSpace(userAgent)
            ? "ARTR-Pien/0.1 (+https://github.com/ARTR-Projects/Pien)"
            : userAgent;
    }

    /// <inheritdoc />
    public async Task<ProbeResult> SendAsync(
        ProbeRequest request,
        ScanContext context,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        request = ProbeRequest.Create(request);

        var current = request.Uri;
        var original = request.Uri;
        var redirectChain = new List<Uri>();
        ProbeResult? last = null;

        for (var hop = 0; hop <= _options.MaxRedirects; hop++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = await _destinationValidator.ValidateAsync(current, _options, cancellationToken)
                .ConfigureAwait(false);

            last = await SendOnceAsync(request with { Uri = endpoint.Uri }, endpoint, redirectChain, cancellationToken)
                .ConfigureAwait(false);

            if (!IsRedirect(last.StatusCode) || hop == _options.MaxRedirects)
            {
                return last;
            }

            if (!last.Headers.TryGetValue("Location", out var location) || string.IsNullOrWhiteSpace(location))
            {
                throw new Exceptions.HttpProtocolException("Redirect response missing Location header.");
            }

            if (!Uri.TryCreate(endpoint.Uri, location, out var next))
            {
                throw new TargetSafetyException("Redirect Location could not be resolved to an absolute URI.");
            }

            if (string.Equals(endpoint.Uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(next.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            {
                throw new TargetSafetyException("HTTPS to HTTP redirect downgrade is blocked.");
            }

            if (original.UserInfo.Length > 0 &&
                (!string.Equals(original.Host, next.Host, StringComparison.OrdinalIgnoreCase) ||
                 original.Port != next.Port ||
                 !string.Equals(original.Scheme, next.Scheme, StringComparison.OrdinalIgnoreCase)))
            {
                next = new UriBuilder(next) { UserName = string.Empty, Password = string.Empty }.Uri;
            }

            redirectChain.Add(endpoint.Uri);
            current = UriCanonicalizer.Canonicalize(next);
        }

        return last ?? throw new Exceptions.HttpProtocolException("Safe HTTP transport produced no result.");
    }

    private async Task<ProbeResult> SendOnceAsync(
        ProbeRequest request,
        ValidatedEndpoint endpoint,
        IReadOnlyList<Uri> redirectChain,
        CancellationToken cancellationToken)
    {
        var handler = CreateHandler(endpoint, _options);
        using var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        using var message = BuildRequestMessage(request, endpoint);
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token).ConfigureAwait(false);
        stopwatch.Stop();

        var headers = CollectHeaders(response);
        var maxBytes = Math.Max(0, request.MaxResponseBodyBytes);
        var body = Array.Empty<byte>();
        var truncated = false;
        if (response.Content is not null && maxBytes > 0)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            (body, truncated) = await ReadBoundedAsync(stream, maxBytes, timeoutCts.Token).ConfigureAwait(false);
        }

        headers.TryGetValue("Content-Type", out var contentType);
        return ProbeResult.Create(new ProbeResult
        {
            FinalUri = endpoint.Uri,
            StatusCode = response.StatusCode,
            Headers = headers,
            Body = body,
            ContentType = contentType,
            Duration = stopwatch.Elapsed,
            BodyTruncated = truncated,
            RedirectChain = redirectChain.ToArray(),
        });
    }

    private HttpRequestMessage BuildRequestMessage(ProbeRequest request, ValidatedEndpoint endpoint)
    {
        var message = new HttpRequestMessage(ToHttpMethod(request.Method), endpoint.Uri);
        message.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        message.Headers.Host = endpoint.HostHeader;
        foreach (var header in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                message.Content ??= new ByteArrayContent(request.Body.ToArray());
                message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!request.Body.IsEmpty && message.Content is null)
        {
            message.Content = new ByteArrayContent(request.Body.ToArray());
            if (!string.IsNullOrWhiteSpace(request.ContentType))
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
            }
        }

        return message;
    }

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
        }

        return headers;
    }

    private static SocketsHttpHandler CreateHandler(ValidatedEndpoint endpoint, NetworkSafetyOptions options)
    {
        var pinned = endpoint.Addresses[0];
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(pinned, endpoint.Uri.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
            SslOptions =
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                TargetHost = endpoint.HostHeader,
                // Authorized private/loopback scans may use self-signed certificates.
                RemoteCertificateValidationCallback = options.AllowPrivateNetworks
                    ? static (_, _, _, _) => true
                    : null,
            },
        };
    }

    private static async Task<(byte[] Body, bool Truncated)> ReadBoundedAsync(
        Stream stream,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(maxBytes, 64 * 1024)];
        using var ms = new MemoryStream();
        var truncated = false;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + read > maxBytes)
            {
                var remaining = (int)(maxBytes - ms.Length);
                if (remaining > 0)
                {
                    ms.Write(buffer, 0, remaining);
                }

                truncated = true;
                break;
            }

            ms.Write(buffer, 0, read);
        }

        return (ms.ToArray(), truncated);
    }

    private static bool IsRedirect(HttpStatusCode? statusCode)
        => statusCode is not null && (int)statusCode is >= 300 and < 400;

    private static HttpMethod ToHttpMethod(ProbeMethod method)
        => method switch
        {
            ProbeMethod.Get => HttpMethod.Get,
            ProbeMethod.Head => HttpMethod.Head,
            ProbeMethod.Options => HttpMethod.Options,
            ProbeMethod.Post => HttpMethod.Post,
            ProbeMethod.Put => HttpMethod.Put,
            ProbeMethod.Patch => HttpMethod.Patch,
            ProbeMethod.Delete => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

    /// <inheritdoc />
    public void Dispose() => _disposed = true;
}
