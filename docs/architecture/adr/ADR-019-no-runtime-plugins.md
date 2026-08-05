# ADR-019 — No runtime plugins

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Runtime plugin loaders are a common RCE/SSRF bypass vector and break determinism.

## Decision

**Prohibit** for v1:

- MEF / `AssemblyLoadContext` plugin folders
- Downloading and executing extension packages at runtime
- Evaluating scripts as checks
- Reflection-based “discover all ICheck in directory”

## Consequences

- Architecture and security reviews reject plugin PRs.
- Third parties extend via compiled references + DI, not drop-in DLLs into Pien’s process without rebuild.
