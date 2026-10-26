using ARTR.Pien.Abstractions;
using ARTR.Pien.Hosting;
using ARTR.Pien.UnitTests.Brotli;
using ARTR.Pien.Web.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARTR.Pien.UnitTests.Hosting;

/// <summary>
/// Hosting ILogger surfacing of the Brotli runtime gate (CVE-2026-111497).
/// </summary>
[Collection(nameof(BrotliDiagnosticsCollection))]
public sealed class BrotliRuntimeHostDiagnosticsTests : IDisposable
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

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly Dictionary<string, ListLogger> _loggers = new(StringComparer.Ordinal);

        /// <summary>
        /// Returns the logger for <paramref name="categoryName"/>, creating it on first use.
        /// </summary>
        public ILogger CreateLogger(string categoryName)
        {
            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                logger = new ListLogger();
                _loggers[categoryName] = logger;
            }

            return logger;
        }

        /// <summary>
        /// Messages captured for <paramref name="categoryName"/>, or empty when unused.
        /// </summary>
        public IReadOnlyList<string> MessagesFor(string categoryName)
            => _loggers.TryGetValue(categoryName, out var logger) ? logger.Messages : [];

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Clears Web test seams so AddPien factory cases stay order-independent.
    /// </summary>
    public BrotliRuntimeHostDiagnosticsTests()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
        DecompressionPolicy.WarningEmitter = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
        DecompressionPolicy.WarningEmitter = null;
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

    /// <summary>
    /// <see cref="PienServiceCollectionExtensions.AddPien"/> logs one warning on category
    /// <c>ARTR.Pien.Transport.Brotli</c> when the transport factory runs on seam 10.0.13.
    /// </summary>
    [Fact]
    public void AddPien_transport_factory_logs_one_warning_on_10_0_13()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 13));
        var provider = new CapturingLoggerProvider();
        var root = Path.Combine(Path.GetTempPath(), "pien-host-br-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders().AddProvider(provider));
            services.AddPien(o =>
            {
                o.WorkingDirectory = root;
                o.StateDirectory = Path.Combine(root, ".pien");
            });
            using var sp = services.BuildServiceProvider();
            _ = sp.GetRequiredService<ISafeHttpTransport>();
            var messages = provider.MessagesFor("ARTR.Pien.Transport.Brotli");
            Assert.Single(messages);
            Assert.Equal(
                "Warning: " + DecompressionPolicy.FormatUnpatchedRuntimeWarning(new Version(10, 0, 13)),
                messages[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        }
    }

    /// <summary>
    /// <see cref="PienServiceCollectionExtensions.AddPien"/> logs nothing on category
    /// <c>ARTR.Pien.Transport.Brotli</c> when the transport factory runs on seam 10.0.14.
    /// </summary>
    [Fact]
    public void AddPien_transport_factory_logs_nothing_on_10_0_14()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 14));
        var provider = new CapturingLoggerProvider();
        var root = Path.Combine(Path.GetTempPath(), "pien-host-br-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders().AddProvider(provider));
            services.AddPien(o =>
            {
                o.WorkingDirectory = root;
                o.StateDirectory = Path.Combine(root, ".pien");
            });
            using var sp = services.BuildServiceProvider();
            _ = sp.GetRequiredService<ISafeHttpTransport>();
            Assert.Empty(provider.MessagesFor("ARTR.Pien.Transport.Brotli"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        }
    }
}
