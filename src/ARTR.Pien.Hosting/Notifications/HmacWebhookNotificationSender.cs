using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Notifications;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.Hosting.Notifications;

/// <summary>
/// HMAC-SHA256 webhook sender with at most two retries (three total attempts).
/// </summary>
public sealed class HmacWebhookNotificationSender : INotificationSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _webhookUrl;
    private readonly string? _secretReference;
    private readonly ISecretResolver _secrets;
    private readonly HttpMessageHandler? _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookNotificationSender"/> class.
    /// </summary>
    public HmacWebhookNotificationSender(
        string webhookUrl,
        string? secretReference,
        ISecretResolver secrets,
        HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        _webhookUrl = webhookUrl;
        _secretReference = secretReference;
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _handler = handler;
    }

    /// <inheritdoc />
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        notification = Notification.Create(notification);

        if (!Uri.TryCreate(_webhookUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotificationException("Webhook URL must be https.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            id = notification.Id,
            runId = notification.RunId?.Value,
            title = notification.Title,
            message = notification.Message,
            severity = notification.Severity.ToString(),
            createdAt = notification.CreatedAt,
        }, JsonOptions);
        var body = Encoding.UTF8.GetBytes(payload);
        var signature = await CreateSignatureAsync(body, cancellationToken).ConfigureAwait(false);

        Exception? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SendOnceAsync(uri, body, signature, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is NotificationException or HttpRequestException or TaskCanceledException or IOException)
            {
                last = ex is NotificationException ? ex : new NotificationException("Webhook delivery failed.", ex);
            }

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }

        throw last ?? new NotificationException("Webhook delivery failed.");
    }

    private async Task<string?> CreateSignatureAsync(byte[] body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_secretReference))
        {
            return null;
        }

        var secret = await _secrets.ResolveAsync(SecretReference.Parse(_secretReference), cancellationToken).ConfigureAwait(false);
        using (secret)
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret.Reveal()), body);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    private async Task SendOnceAsync(Uri uri, byte[] body, string? signature, CancellationToken cancellationToken)
    {
        using var client = _handler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(15) }
            : new HttpClient(_handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        if (!string.IsNullOrWhiteSpace(signature))
        {
            request.Headers.TryAddWithoutValidation("X-Pien-Signature", "sha256=" + signature);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new NotificationException($"Webhook returned {(int)response.StatusCode}.");
        }
    }
}
