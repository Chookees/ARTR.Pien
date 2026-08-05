using System.Diagnostics.CodeAnalysis;

using ARTR.Pien.Exceptions;

namespace ARTR.Pien.Secrets;

/// <summary>
/// Supported secret reference schemes.
/// </summary>
public enum SecretScheme
{
    /// <summary>Environment-variable backed secret (<c>secret://env/...</c>).</summary>
    Environment = 0,

    /// <summary>File-backed secret (<c>secret://file/...</c>).</summary>
    File = 1,
}

/// <summary>
/// Parsed, immutable secret reference. Never stores resolved secret material.
/// </summary>
public sealed record SecretReference
{
    private SecretReference(SecretScheme scheme, string path)
    {
        Scheme = scheme;
        Path = path;
    }

    /// <summary>Secret provider scheme.</summary>
    public SecretScheme Scheme { get; }

    /// <summary>
    /// Scheme-specific path. For environment secrets this is the variable name;
    /// for file secrets this is the absolute or configured-relative path.
    /// </summary>
    public string Path { get; }

    /// <summary>Original normalized reference URI without resolving the secret.</summary>
    public string Uri
    {
        get
        {
            return Scheme switch
            {
                SecretScheme.Environment => $"secret://env/{Path}",
                SecretScheme.File => $"secret://file/{Path}",
                _ => throw new InvalidOperationException($"Unknown secret scheme '{Scheme}'."),
            };
        }
    }

    /// <summary>
    /// Parses a <c>secret://</c> reference.
    /// </summary>
    /// <param name="value">Reference text such as <c>secret://env/PIEN_TEST_API_KEY</c>.</param>
    /// <returns>A parsed secret reference.</returns>
    /// <exception cref="SecretResolutionException">Thrown when the reference is malformed.</exception>
    public static SecretReference Parse(string value)
    {
        if (!TryParse(value, out var reference, out var error))
        {
            throw new SecretResolutionException(error ?? "Invalid secret reference.");
        }

        return reference;
    }

    /// <summary>
    /// Attempts to parse a secret reference.
    /// </summary>
    /// <param name="value">Candidate reference text.</param>
    /// <param name="reference">Parsed reference when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SecretReference? reference)
        => TryParse(value, out reference, out _);

    /// <summary>
    /// Attempts to parse a secret reference and returns an error message on failure.
    /// </summary>
    /// <param name="value">Candidate reference text.</param>
    /// <param name="reference">Parsed reference when successful.</param>
    /// <param name="error">Error description when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(
        string? value,
        [NotNullWhen(true)] out SecretReference? reference,
        [NotNullWhen(false)] out string? error)
    {
        reference = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Secret reference must not be null or whitespace.";
            return false;
        }

        var trimmed = value.Trim();
        const string prefix = "secret://";
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "Secret reference must start with 'secret://'.";
            return false;
        }

        var remainder = trimmed[prefix.Length..];
        var slashIndex = remainder.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == remainder.Length - 1)
        {
            error = "Secret reference must include a scheme and a non-empty path.";
            return false;
        }

        var schemeText = remainder[..slashIndex];
        var path = remainder[(slashIndex + 1)..];
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Secret reference path must not be empty.";
            return false;
        }

        if (schemeText.Equals("env", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains('/') || path.Contains('\\') || path.Contains('\0'))
            {
                error = "Environment secret names must not contain path separators or null characters.";
                return false;
            }

            reference = new SecretReference(SecretScheme.Environment, path);
            return true;
        }

        if (schemeText.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            // Unix absolute paths are encoded as secret://file//etc/... (empty segment before absolute path).
            reference = new SecretReference(SecretScheme.File, path);
            return true;
        }

        error = $"Unsupported secret scheme '{schemeText}'. Supported schemes: env, file.";
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Uri;
}

/// <summary>
/// Opaque handle to a resolved secret value. <see cref="ToString"/> never returns the secret.
/// </summary>
public sealed class ResolvedSecret : IDisposable
{
    private char[]? _buffer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedSecret"/> class.
    /// </summary>
    /// <param name="value">Resolved secret characters. Ownership transfers to this instance.</param>
    public ResolvedSecret(char[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _buffer = value;
    }

    /// <summary>
    /// Creates a resolved secret from a string. The string should be cleared by the caller when practical.
    /// </summary>
    /// <param name="value">Secret string.</param>
    /// <returns>A resolved secret owning a copied buffer.</returns>
    public static ResolvedSecret FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ResolvedSecret(value.ToCharArray());
    }

    /// <summary>
    /// Provides a span over the secret buffer for temporary use. Do not retain the span.
    /// </summary>
    /// <returns>A read-only span of secret characters.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the secret has been disposed.</exception>
    public ReadOnlySpan<char> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed || _buffer is null, this);
        return _buffer;
    }

    /// <summary>
    /// Returns the secret as a string. Prefer <see cref="AsSpan"/> when possible.
    /// </summary>
    /// <returns>A new string containing the secret.</returns>
    public string Reveal()
    {
        ObjectDisposedException.ThrowIf(_disposed || _buffer is null, this);
        return new string(_buffer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer is not null)
        {
            Array.Clear(_buffer);
            _buffer = null;
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public override string ToString() => "<redacted-secret>";
}

/// <summary>
/// Resolves <see cref="SecretReference"/> values from approved providers.
/// Implementations must never log or report resolved material.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves a secret reference.
    /// </summary>
    /// <param name="reference">Parsed secret reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable resolved secret.</returns>
    Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default);
}
