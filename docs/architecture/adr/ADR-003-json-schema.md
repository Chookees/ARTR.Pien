# ADR-003 — JSON Schema engine

- **Status:** Accepted
- **Date:** 2026-08-05
- **Verified:** NuGet `NJsonSchema` **11.6.1** (latest stable); JsonSchema.Net binary EULA/OSMF fee for revenue-generating NuGet use (effective 2026-02-01)

## Context

API checks need JSON Schema validation with local `$ref` only (no remote schema fetch).

## Decision

Use **NJsonSchema 11.6.1** (MIT) in `ARTR.Pien.Checks`. Disable network resolvers; bound validation work.

## Alternatives

| Option | Outcome |
|--------|---------|
| JsonSchema.Net | Rejected — NuGet binary EULA / Open Source Maintenance Fee for revenue-generating users |
| Hand-rolled draft validator | Incomplete; high defect risk |
| Newtonsoft.Json.Schema | Legacy; license/commercial concerns |

## Consequences

Transitive `Newtonsoft.Json` via NJsonSchema is accepted in Checks only; keep it out of Core/Reporting hot paths. Document in NOTICE.
