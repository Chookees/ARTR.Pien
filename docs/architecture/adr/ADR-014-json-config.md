# ADR-014 — JSON configuration

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Operators need a single portable config format for local and CI use, mergeable with environment variables and CLI flags.

## Decision

- Primary file: **`pien.json`** with `schemaVersion: 1`.
- JSON Schema document under `config/` for editor validation.
- Load via `Microsoft.Extensions.Configuration` (JSON + env `ARTR_PIEN_*` + command line).
- Validate with `PienConfigurationValidator` before scan.

Reject YAML-as-primary-config for v1 (OpenAPI YAML is data, not Pien config).

## Consequences

- Simple onboarding (`pien init`).
- Schema bumps require ADR + migration notes.
- Invalid config fails fast with `PienExitCode.InvalidArguments` / validation errors.
