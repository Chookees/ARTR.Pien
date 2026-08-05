# ADR-022 — Work on main

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Early Pien development favors simplicity over long-lived feature branches for a single-product repo.

## Decision

- Primary integration branch is **`main`**.
- Architecture and docs land on `main`.
- External contributors use short-lived branches / PRs targeting `main`.
- Avoid permanent `develop` dual-branch complexity in v1.

## Consequences

- CI always reflects `main`.
- Breaking work should be incremental or feature-flagged via config, not long divergent branches.
- Release tags cut from `main`.
