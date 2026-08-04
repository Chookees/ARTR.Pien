# Power of Ten — C# adaptation for ARTR Pien

This document adapts Gerard J. Holzmann’s *Power of Ten* rules for managed C# used in ARTR Pien.

**Pien is not NASA-certified or JPL-approved.** The adaptation captures engineering intent (bounded work, simple control flow, explicit validation) for a local verification CLI.

## Rules summary

| # | Intent | C# adaptation (Pien) | Enforcement |
|---|--------|----------------------|-------------|
| 1 | Simple control flow | No `goto`; no recursion on scan paths; iterative crawl; nesting ≤4 | Analyzers PIEN0002/0004 + review |
| 2 | Bound loops | Explicit limits for crawl/redirects/body/retries; cancellation-aware host loops | Analyzer PIEN0003 + `HardLimits` / `ScanLimits` |
| 3 | Bound allocation | No unbounded buffers; stream/bound response bodies | Runtime limits + architecture review |
| 4 | Small methods | ≤60 logical lines (prefer ≤40) | Analyzer **PIEN0001** |
| 5 | Assertions / invariants | Validate public boundaries; options at startup | Code review + tests |
| 6 | Minimize scope | Immutable domain records; narrow ownership | Style + review |
| 7 | Validate inputs / observe results | Validate URIs/targets/secrets; observe tasks | Analyzers PIEN0007/0008 + runtime |
| 8 | Restrict preprocessor | Avoid `#if` business logic | Review |
| 9 | No unsafe | `AllowUnsafeBlocks=false`; no pointers | Analyzer **PIEN0005** + build props |
| 10 | Compile with all warnings | `TreatWarningsAsErrors`; analyzers as errors | Build props |

## Analyzer IDs

| Id | Title |
|----|-------|
| PIEN0001 | Method exceeds the allowed logical length |
| PIEN0002 | Direct recursion is prohibited |
| PIEN0003 | Unbounded loop construct detected |
| PIEN0004 | goto statements are prohibited |
| PIEN0005 | Unsafe code or pointer usage is prohibited |
| PIEN0006 | async void is prohibited |
| PIEN0007 | Synchronous blocking of asynchronous work is prohibited |
| PIEN0008 | Task or ValueTask result is ignored |
| PIEN0009 | Broad exception handling is not justified |

Unreliable rules that would create excessive false positives are enforced via centralized APIs, architecture tests, and this document (ADR-008).

## Logical line counting (PIEN0001)

Logical lines exclude:

- XML documentation comments
- Blank lines
- Lines containing only `{` or `}`
- `using` / `namespace` / attribute lines outside the method body

Generated code (`*.g.cs`) is excluded.
