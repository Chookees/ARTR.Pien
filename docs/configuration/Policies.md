# Policies

**Status:** Design (v1)

Short reference for policy presets. Full merge rules: [ProfilesAndPolicies.md](ProfilesAndPolicies.md).

## Presets (`policies.name`)

| Name | failOn | Notes |
|------|--------|-------|
| `balanced` | high | Default |
| `security-focused` | medium | TLS/headers/cookies emphasis |
| `quality-focused` | medium | HTML/a11y/SEO/link/perf |
| `api-contract` | high | API/OpenAPI/JSON |
| `ci-strict` | high | CI; Errors count as fail |

## Fields

| Field | Meaning |
|-------|---------|
| `failOn` | Minimum **Fail** severity that fails the scan (exit 1) |
| `severityOverrides` | Map checkId → severity |
| `suppressions` | checkId (+ optional fingerprint), reason, optional `expiresAt` |

## Evaluation

- Only `FindingStatus.Fail` (and Error under `ci-strict`) participate in fail-on.
- Advisory category scores never alone fail CI.
- Baseline `failOnNew` is separate (see `baselines` in schema).

Severity model: [SeverityModel.md](../checks/SeverityModel.md).
