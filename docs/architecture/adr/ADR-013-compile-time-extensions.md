> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-018-compile-time-extensions.md** instead. See [README.md](README.md).

# ADR-013 — Compile-time extension model

- **Status:** Superseded (see ADR-018)
- **Date:** 2026-08-05

## Context

Future commercial products need extension points without loading untrusted assemblies at runtime.

## Decision

Extensions register via explicit DI (`AddPien` / `AddPienChecks`). No plugin directory scanning, no runtime compilation, no `Assembly.Load` of operator-supplied DLLs in v1.

## Alternatives

| Option | Why not |
|--------|---------|
| MEF / directory plugins | Supply-chain and sandbox risk |
| Roslyn scripting | Arbitrary code execution |

## Consequences

Third parties extend by referencing libraries and registering services in their host. Open-source built-ins remain source-of-truth in-repo.
