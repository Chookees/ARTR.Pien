> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-020-apache-2.md** and **ADR-021-compliance-boundaries.md** instead. See [README.md](README.md).

# ADR-018 — Apache-2.0 and compliance-readiness boundaries

- **Status:** Superseded (see ADR-020 / ADR-021)
- **Date:** 2026-08-05

## Context

Repo must support future ISO 27001 / SOC 2 programs without claiming certification.

## Decision

- License Apache-2.0 with NOTICE for third-party attributions
- Compliance docs under `docs/compliance/` describe readiness/shared responsibility only
- No invented security email/SLA/company address; use GitHub private vulnerability reporting

## Consequences

Marketing and README must not claim ISO/SOC certification. Control matrices map technical evidence to organizational gaps.
