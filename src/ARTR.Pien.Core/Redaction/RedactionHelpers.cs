namespace ARTR.Pien.Redaction;

/// <summary>
/// Standard redaction marker used in logs, reports, and diagnostics.
/// </summary>
public static class RedactionMarkers
{
    /// <summary>Generic redacted placeholder.</summary>
    public const string Redacted = "[REDACTED]";

    /// <summary>Placeholder for redacted authorization material.</summary>
    public const string Authorization = "[REDACTED:authorization]";

    /// <summary>Placeholder for redacted cookie values.</summary>
    public const string Cookie = "[REDACTED:cookie]";

    /// <summary>Placeholder for redacted secret references or values.</summary>
    public const string Secret = "[REDACTED:secret]";

    /// <summary>Placeholder for redacted request/response bodies.</summary>
    public const string Body = "[REDACTED:body]";
}

/// <summary>
/// Contract for redacting sensitive material from text destined for logs or reports.
/// </summary>
public interface IRedactor
{
    /// <summary>
    /// Redacts sensitive material from <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Raw text that may contain secrets.</param>
    /// <returns>A redacted string safe for logs and reports.</returns>
    string Redact(string? input);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="headerName"/> is considered sensitive.
    /// </summary>
    /// <param name="headerName">HTTP header name.</param>
    /// <returns><see langword="true"/> when the header value must be redacted.</returns>
    bool IsSensitiveHeader(string headerName);
}

/// <summary>
/// Pure helper contracts for common redaction decisions. Implementations may wrap these helpers.
/// </summary>
public static class RedactionHelpers
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key",
        "X-Auth-Token",
    };

    /// <summary>
    /// Determines whether an HTTP header name is sensitive by default.
    /// </summary>
    /// <param name="headerName">Header name.</param>
    /// <returns><see langword="true"/> when values must be redacted.</returns>
    public static bool IsSensitiveHeaderName(string? headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        if (SensitiveHeaders.Contains(headerName))
        {
            return true;
        }

        return headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("api-key", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Redacts a header value when the header name is sensitive.
    /// </summary>
    /// <param name="headerName">Header name.</param>
    /// <param name="headerValue">Header value.</param>
    /// <returns>The original value or a redaction marker.</returns>
    public static string RedactHeaderValue(string headerName, string? headerValue)
    {
        if (IsSensitiveHeaderName(headerName))
        {
            if (headerName.Contains("cookie", StringComparison.OrdinalIgnoreCase))
            {
                return RedactionMarkers.Cookie;
            }

            if (headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase))
            {
                return RedactionMarkers.Authorization;
            }

            return RedactionMarkers.Redacted;
        }

        return headerValue ?? string.Empty;
    }

    /// <summary>
    /// Masks a secret reference URI for display when the path itself may be sensitive.
    /// </summary>
    /// <param name="secretUri">Secret reference URI.</param>
    /// <returns>A display-safe representation.</returns>
    public static string MaskSecretReference(string? secretUri)
    {
        if (string.IsNullOrWhiteSpace(secretUri))
        {
            return RedactionMarkers.Secret;
        }

        if (secretUri.StartsWith("secret://env/", StringComparison.OrdinalIgnoreCase))
        {
            return secretUri;
        }

        if (secretUri.StartsWith("secret://file/", StringComparison.OrdinalIgnoreCase))
        {
            return "secret://file/" + RedactionMarkers.Secret;
        }

        return RedactionMarkers.Secret;
    }

    /// <summary>
    /// Truncates text to a maximum length, appending an ellipsis when truncated.
    /// </summary>
    /// <param name="value">Input text.</param>
    /// <param name="maxChars">Maximum characters to retain.</param>
    /// <returns>A truncated string.</returns>
    public static string Truncate(string? value, int maxChars)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxChars ? value : value[..maxChars] + "…";
    }
}
