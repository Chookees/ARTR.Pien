# ADR-030 — Deferred analyzers PIEN0010–PIEN0012

- **Status:** Accepted
- **Date:** 2026-08-05
- **Supersedes in part:** the “ship PIEN0001–PIEN0012” wording in ADR-026 / ADR-008 for IDs 0010–0012

## Context

ADR-008 and ADR-026 call for Roslyn diagnostics **PIEN0001–PIEN0012**. IDs **PIEN0001–PIEN0009** are implemented in `ARTR.Pien.CodeAnalysis` with analyzer tests.

**PIEN0010–PIEN0012** target patterns that are either noisy under real C# codebases or better enforced by runtime helpers and review:

| Id | Intent | Why defer |
|----|--------|-----------|
| PIEN0010 | Unsupported / business-logic `#if` preprocessor use | High false-positive rate: SDK multi-TFM, SourceLink, generated files, and legitimate platform guards look identical to “logic `#if`” without fragile heuristics |
| PIEN0011 | CancellationToken ignored on bounded async operations | Incomplete without deep data-flow; many valid fire-and-forget / host-lifetime patterns; overlaps PIEN0008 partially |
| PIEN0012 | User-controlled regex without safe options | Prefer centralized `SafeRegex` + `HardLimits.MaxRegexTimeout` (already used on scan paths) over brittle string/flow analysis |

Shipping unreliable Error-severity analyzers would train contributors to suppress diagnostics and weaken TreatWarningsAsErrors discipline.

## Decision

**Intentionally defer** implementation of **PIEN0010**, **PIEN0011**, and **PIEN0012** in `ARTR.Pien.CodeAnalysis` for v1.

Enforcement substitutes:

- **PIEN0010:** code review + PowerOfTen.md rule 8; avoid `#if` in product business logic
- **PIEN0011:** API review on scan/host loops; prefer `CancellationToken` parameters and host-linked CTS; architecture / unit tests on bounded loops
- **PIEN0012:** `ARTR.Pien.Core.SafeRegex` (and related helpers) + timeout limits; checks must not compile unbounded user regex

IDs **0010–0012** remain reserved. Revisit only if a low-FP, well-tested analyzer design is demonstrated.

## Consequences

- Analyzer package documents **PIEN0001–PIEN0009** as active; **0010–0012** as deferred (see `docs/development/PowerOfTen.md` and `docs/development/Analyzers.md`).
- ADR-026’s “ship PIEN0001–PIEN0012” goal is amended: ship 0001–0009; reserve 0010–0012 per this ADR.
- No claim that static analysis alone enforces preprocessor / CT / regex policy.
