# ADR-023 — Commit policy

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Consistent history aids review, changelog generation, and automation.

## Decision

- Use **Conventional Commits**: `feat:`, `fix:`, `docs:`, `test:`, `ci:`, `chore:`, `refactor:`, etc.
- Do not commit secrets, `bin/`/`obj/`, or `inDev/` orchestration scratch unless explicitly maintained product prompts.
- Docs-only architecture commits are allowed on `main` as `docs:`.
- Do not use `--no-verify` to bypass hooks unless explicitly required for an emergency and documented.

## Consequences

- CHANGELOG can be derived from commit types.
- Architecture agents may create 1–2 docs commits when authorized by the orchestrator/user.
