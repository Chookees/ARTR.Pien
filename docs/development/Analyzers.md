# ARTR.Pien.CodeAnalysis

Roslyn analyzers adapting Holzmann’s Power of Ten rules for ARTR Pien (see `docs/development/PowerOfTen.md`).

## Active diagnostics (implemented)

| Id | Severity | Title |
|----|----------|-------|
| PIEN0001 | Error | Method exceeds the allowed logical length |
| PIEN0002 | Error | Direct recursion is prohibited |
| PIEN0003 | Warning | Unbounded loop construct detected |
| PIEN0004 | Error | goto statements are prohibited |
| PIEN0005 | Error | Unsafe code or pointer usage is prohibited |
| PIEN0006 | Error | async void is prohibited |
| PIEN0007 | Error | Synchronous blocking of asynchronous work is prohibited |
| PIEN0008 | Warning | Task or ValueTask result is ignored |
| PIEN0009 | Warning | Broad exception handling is not justified |

Production projects reference this assembly as an analyzer via `Directory.Build.props` when `EnablePienAnalyzers` is true.

## Deferred diagnostics (reserved)

| Id | Title | Status |
|----|-------|--------|
| PIEN0010 | Unsupported preprocessor directive detected | Deferred — see [ADR-030](../architecture/adr/ADR-030-deferred-analyzers.md) |
| PIEN0011 | Cancellation token ignored in bounded async operation | Deferred — ADR-030 |
| PIEN0012 | User-controlled regex lacks safe options | Deferred — prefer `SafeRegex` + ADR-030 |

Do **not** implement 0010–0012 unless a low false-positive design with tests is accepted in a follow-up ADR.

## Tests

`tests/ARTR.Pien.CodeAnalysis.Tests` uses `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.
