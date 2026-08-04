using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ARTR.Pien.CodeAnalysis;

/// <summary>
/// Marks this assembly as providing Roslyn analyzers.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class AnalyzerAssemblyMarker
{
    /// <summary>
    /// Returns supported diagnostic ids for discovery smoke tests.
    /// </summary>
    public static ImmutableArray<string> SupportedDiagnosticIds { get; } =
        ImmutableArray.Create(
            "PIEN0001", "PIEN0002", "PIEN0003", "PIEN0004", "PIEN0005",
            "PIEN0006", "PIEN0007", "PIEN0008", "PIEN0009");
}
