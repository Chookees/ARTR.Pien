namespace ARTR.Pien.Exceptions;

/// <summary>
/// Base type for expected Pien domain failures.
/// </summary>
public abstract class PienException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PienException"/> class.
    /// </summary>
    /// <param name="message">Safe, non-secret error message.</param>
    protected PienException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PienException"/> class.
    /// </summary>
    /// <param name="message">Safe, non-secret error message.</param>
    /// <param name="innerException">Inner exception preserved for diagnostics.</param>
    protected PienException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when configuration is invalid or unsupported.</summary>
public sealed class ConfigurationException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public ConfigurationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when target authorization acknowledgement is missing or invalid.</summary>
public sealed class AuthorizationException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public AuthorizationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public AuthorizationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a target fails safety validation (scheme, SSRF, private network, etc.).</summary>
public sealed class TargetSafetyException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public TargetSafetyException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public TargetSafetyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when DNS resolution fails or returns no usable addresses.</summary>
public sealed class DnsFailureException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public DnsFailureException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public DnsFailureException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a TCP connection cannot be established.</summary>
public sealed class ConnectionFailureException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public ConnectionFailureException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public ConnectionFailureException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when TLS negotiation or certificate validation fails.</summary>
public sealed class TlsFailureException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public TlsFailureException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public TlsFailureException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when an operation exceeds its configured timeout.</summary>
public sealed class PienTimeoutException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public PienTimeoutException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public PienTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when an HTTP protocol-level error is observed.</summary>
public sealed class HttpProtocolException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public HttpProtocolException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public HttpProtocolException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a response body exceeds the configured inspection limit.</summary>
public sealed class BodyLimitExceededException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public BodyLimitExceededException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public BodyLimitExceededException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when content cannot be parsed within safety bounds.</summary>
public sealed class ParseException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public ParseException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public ParseException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a check fails to execute (distinct from a failing finding).</summary>
public sealed class CheckExecutionException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public CheckExecutionException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public CheckExecutionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when report export fails.</summary>
public sealed class ReportExportException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public ReportExportException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public ReportExportException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when local storage operations fail.</summary>
public sealed class StorageException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public StorageException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public StorageException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a required notification cannot be delivered.</summary>
public sealed class NotificationException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public NotificationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public NotificationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a secret reference is invalid or cannot be resolved.</summary>
public sealed class SecretResolutionException : PienException
{
    /// <inheritdoc cref="PienException(string)"/>
    public SecretResolutionException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="PienException(string, Exception?)"/>
    public SecretResolutionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
