using ARTR.Pien.Hosting;
using ARTR.Pien.Web.Transport;

using Microsoft.Extensions.Logging;

namespace ARTR.Pien.UnitTests.Hosting;

/// <summary>
/// Hosting ILogger surfacing of the Brotli runtime gate (CVE-2026-111497).
/// </summary>
public sealed class BrotliRuntimeHostDiagnosticsTests
{
    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add($"{logLevel}: {formatter(state, exception)}");
        }
    }

    /// <summary>
    /// When Brotli is off, Hosting logs the exact warning text at Warning level.
    /// </summary>
    [Fact]
    public void LogIfDisabled_logs_warning_for_10_0_13()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 13));
        var logger = new ListLogger();
        BrotliRuntimeHostDiagnostics.LogIfDisabled(logger, status);
        Assert.Single(logger.Messages);
        Assert.Equal("Warning: " + status.Warning, logger.Messages[0]);
    }

    /// <summary>
    /// When Brotli is enabled, Hosting logs nothing.
    /// </summary>
    [Fact]
    public void LogIfDisabled_logs_nothing_for_10_0_14()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 14));
        var logger = new ListLogger();
        BrotliRuntimeHostDiagnostics.LogIfDisabled(logger, status);
        Assert.Empty(logger.Messages);
    }

    /// <summary>
    /// Missing ILoggerFactory/logger is a no-op (hosts that skip AddLogging).
    /// </summary>
    [Fact]
    public void LogIfDisabled_accepts_null_logger()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 13));
        BrotliRuntimeHostDiagnostics.LogIfDisabled(null, status);
    }
}
