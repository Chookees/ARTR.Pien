# ADR-020 — Apache-2.0 licensing

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

ARTR projects standardize on Apache-2.0 for patent grant clarity and redistribution.

## Decision

- License the Pien codebase under **Apache License 2.0** (`LICENSE`, `NOTICE`).
- Only accept dependencies with licenses compatible with Apache-2.0 redistribution (MIT, Apache-2.0, BSD-2/3, etc.).
- Reject packages with field-of-use restrictions or commercial binary EULAs incompatible with project goals (see ADR-003).

## Consequences

- CONTRIBUTING requires Apache-2.0 for contributions.
- DependencyResearch records license per package.
- NOTICE updated when transitive attribution is required.
