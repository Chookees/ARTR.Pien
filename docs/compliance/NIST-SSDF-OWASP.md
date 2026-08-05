# NIST SSDF / OWASP mapping (readiness)

This document maps ARTR Pien engineering practices to NIST SSDF and OWASP ASVS themes. It is **not** a certification claim.

## SSDF-oriented practices

| Practice | Pien evidence |
|----------|---------------|
| Secure development | Apache-2.0 deps, CPM, TreatWarningsAsErrors, locked restore |
| Threat modeling | `docs/security/ThreatModel.md`, SSRF ADRs |
| Review | Architecture tests, CodeQL workflow, dependency review |
| Testing | Unit/integration/functional/architecture suites; offline after restore |
| Vulnerability response | `SECURITY.md`, scheduled security workflow |

## OWASP ASVS themes touched by checks

| Theme | Related checks |
|-------|----------------|
| Communication security | PIEN-TLS-001/002, HTTPS downgrade handling in transport |
| HTTP security headers | PIEN-HEADERS-001/002 |
| Cookie attributes | PIEN-COOKIE-001 |
| Input/output encoding in reports | HTML exporter encodes untrusted findings |
| API hygiene | PIEN-API-001, OpenAPI inspect without destructive auto-exec |

## Explicit non-claims

Pien does not claim ASVS level certification, NIST compliance certification, or penetration-test equivalence.
