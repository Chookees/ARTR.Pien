namespace ARTR.Pien.UnitTests.Brotli;

/// <summary>
/// Serializes tests that mutate Brotli process latches, WarningEmitter, runtime overrides, or Console.Error.
/// </summary>
[CollectionDefinition(nameof(BrotliDiagnosticsCollection), DisableParallelization = true)]
public sealed class BrotliDiagnosticsCollection;
