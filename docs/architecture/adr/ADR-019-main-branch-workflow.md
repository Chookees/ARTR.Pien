> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-022-work-on-main.md** instead. See [README.md](README.md).

# ADR-019 — Initial implementation on `main`

- **Status:** Superseded (see ADR-022)
- **Date:** 2026-08-05

## Context

Autonomous initial build prefers a single linear history; future external contributors use PRs.

## Decision

Perform initial implementation commits directly on `main` with Conventional Commits and medium-sized coherent changes. Document in CONTRIBUTING that future contributors should open PRs.

## Alternatives

| Option | Trade-off |
|--------|-----------|
| Feature-branch-only from day one | Heavier for single-operator bootstrap |

## Consequences

CI protects `main`; no force-push policy for collaborators. This ADR does not waive review for post-bootstrap contributions.
