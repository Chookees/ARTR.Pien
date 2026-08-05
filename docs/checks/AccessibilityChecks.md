# Accessibility checks

**Status:** Design (v1)  
Parent: [CheckCatalog.md](CheckCatalog.md)

Deterministic **static HTML** fundamentals only. **Not** WCAG certification.

## Checks

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-A11Y-001 | **I** | `html[lang]`, document title, basic img alt presence |
| PIEN-A11Y-002 | **I** | Form controls without associated labels |
| PIEN-A11Y-003 | **I** | Empty links / buttons |
| PIEN-A11Y-004 | **I** | Heading-level jumps |
| PIEN-A11Y-005 | **I** | Positive `tabindex` observation (Info) |

## Limitations (must appear in `pien explain` and docs)

- No claim of accessibility compliance or WCAG conformance.
- JavaScript-rendered UI is out of mandatory v1 scope (no browser automation).
- Manual testing remains required for real a11y assurance.

## HTML report a11y

Separate from these checks: exported HTML reports must meet the a11y bar in [ReportFormats.md](../reporting/ReportFormats.md) / [CliUx.md](../cli/CliUx.md).
