# ADR-008 — Power-of-Ten adaptation

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Scan/crawler code must stay bounded and reviewable. Holzmann’s Power of Ten rules inspire discipline but cannot be claimed as NASA/JPL certification.

## Decision

Adopt a **C# adaptation** documented in `docs/development/PowerOfTen.md`, enforced by:

- Roslyn analyzers **PIEN0001–PIEN0009** (implemented); **PIEN0010–PIEN0012** reserved/deferred per **ADR-030**
- Runtime `HardLimits` / `ScanLimits` / `SafeRegex`
- Architecture tests and code review for rules that are noisy as static analysis

**Explicit non-claim:** Pien is **not** NASA-certified or JPL-approved.

## Consequences

- Methods stay short; no goto; no unsafe; bounded loops on scan paths.
- Some rules (allocation discipline, preprocessor, CT ignore, regex construction) remain procedural/review-enforced (ADR-030).
- Analyzer false positives are fixed in analyzer logic, not silenced repo-wide without justification.
