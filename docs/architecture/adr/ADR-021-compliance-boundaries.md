# ADR-021 — Compliance boundaries

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Operators ask about SOC 2 / ISO 27001. Shipping “certification” language would be inaccurate and legally risky.

## Decision

- Maintain **readiness mapping** docs under `docs/compliance/` (SOC2, ISO27001).
- These documents are **not** certifications, attestations, audit reports, or guarantees.
- Product scores and reports must use **advisory** wording only.
- Security claims limited to implemented controls (SSRF, redaction, authorization gate).

## Consequences

- Marketing and CLI copy must avoid “certified”, “compliant”, “passes SOC 2”.
- Compliance docs can evolve as controls mature without implying external audit.
