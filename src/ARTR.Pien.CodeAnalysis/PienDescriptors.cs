using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ARTR.Pien.CodeAnalysis;

internal static class PienDescriptors
{
    private const string Category = "PowerOfTen";

    public static readonly DiagnosticDescriptor MethodTooLong = Create(
        "PIEN0001",
        "Method exceeds the allowed logical length",
        "Method '{0}' has {1} logical lines (limit {2})",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DirectRecursion = Create(
        "PIEN0002",
        "Direct recursion is prohibited",
        "Method '{0}' appears to call itself recursively",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnboundedLoop = Create(
        "PIEN0003",
        "Unbounded loop construct detected",
        "Loop appears unbounded; use an explicit iteration limit or cancellation-checked host loop",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor GotoProhibited = Create(
        "PIEN0004",
        "goto statements are prohibited",
        "goto is prohibited by the Power-of-Ten adaptation",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnsafeProhibited = Create(
        "PIEN0005",
        "Unsafe code or pointer usage is prohibited",
        "Unsafe code and pointer types are prohibited",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor AsyncVoidProhibited = Create(
        "PIEN0006",
        "async void is prohibited",
        "async void method '{0}' is prohibited; return Task or ValueTask",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor SyncOverAsync = Create(
        "PIEN0007",
        "Synchronous blocking of asynchronous work is prohibited",
        "Synchronous blocking via '{0}' is prohibited",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor IgnoredTask = Create(
        "PIEN0008",
        "Task or ValueTask result is ignored",
        "Task/ValueTask result must be awaited or explicitly observed",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor BroadCatch = Create(
        "PIEN0009",
        "Broad exception handling is not justified",
        "catch (Exception) must rethrow, filter, or document justification",
        DiagnosticSeverity.Warning);

    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(
            MethodTooLong,
            DirectRecursion,
            UnboundedLoop,
            GotoProhibited,
            UnsafeProhibited,
            AsyncVoidProhibited,
            SyncOverAsync,
            IgnoredTask,
            BroadCatch);

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity)
        => new(
            id,
            title,
            message,
            Category,
            severity,
            isEnabledByDefault: true,
            description: title,
            helpLinkUri: "https://github.com/ARTR-Projects/Pien/blob/main/docs/development/PowerOfTen.md");
}
