# Severity and status model

**Status:** Design (v1)

Aligned with `FindingSeverity` and `FindingStatus` in Core.

## Severity (`FindingSeverity`)

| Value | Rank | Meaning |
|-------|------|---------|
| `Info` | 0 | Observation; no direct impact |
| `Low` | 1 | Low potential impact |
| `Medium` | 2 | Moderate impact |
| `High` | 3 | High impact |
| `Critical` | 4 | Immediate attention |

CLI/config tokens are lowercase (`info`…`critical`). Console may Title Case. Color never sole signal.

Policy `failOn` compares ordinal: any **Fail** with severity ≥ threshold → exit **1**.

## Status (`FindingStatus`)

| Value | Meaning | Policy |
|-------|---------|--------|
| `Pass` | Condition satisfied | Ignore for fail-on |
| `Fail` | Condition not satisfied | Subject to fail-on |
| `Warning` | Soft concern | Does not fail unless product later maps it (v1: no) |
| `NotApplicable` | Check does not apply | Ignore |
| `Skipped` | Intentionally not run (disabled) | Ignore |
| `Error` | Execution error (never report as Pass) | `ci-strict`: treat as fail |
| `Suppressed` | Policy/config suppression | Ignore for fail-on |

## Rules

1. Execution failures → `Error`, never `Pass`.
2. Missing evidence where required → `Error` or `Fail` with clear remediation — prefer `Error` when probe infrastructure failed, `Fail` when evidence present but condition unmet.
3. Suppressions set status `Suppressed` and retain audit reason.
4. SARIF level mapping: Critical/High → `error`; Medium → `warning`; Low/Info → `note`.

## Console prefixes

`[CRITICAL]` `[HIGH]` `[MEDIUM]` `[LOW]` `[INFO]` plus `PASS` / `FAIL` / `WARN`.
