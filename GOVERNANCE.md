# Governance

ARTR Pien is maintained by ARTR.

## Decision making

- Architecture decisions are recorded as ADRs under `docs/architecture/adr/`.
- Breaking CLI or report schema changes require an ADR and a CHANGELOG entry.
- Security-sensitive changes (network policy, secret handling, redaction) require explicit review.

## Maintainers

Maintainers are responsible for releases, dependency upgrades, and enforcing project constraints (no containers/databases; Apache-2.0 cleanliness).

## Releases

Releases are tagged from `main` after CI verification. See `docs/` and `.github/workflows/` for the release process.
