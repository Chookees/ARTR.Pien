# ISO 27001 readiness mapping (not a certification)

This document maps ARTR Pien engineering practices to common ISO/IEC 27001 control themes to support **readiness discussions**.

**This is not an ISO 27001 certification, audit opinion, or attestation.**

| Theme | Pien evidence |
|-------|----------------|
| Secure development | TreatWarningsAsErrors, analyzers, architecture tests, CodeQL workflow |
| Access control to secrets | `secret://` references; redaction helpers; doctor never prints secrets |
| Vulnerability management | NuGet audit, Dependabot, scheduled security workflow |
| Logging & monitoring | Structured progress/events; no secret logging requirement |
| Supplier / dependency control | CPM + lock files + DependencyResearch.md |
| Incident response readiness | SECURITY.md private reporting path |
