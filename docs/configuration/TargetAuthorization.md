# Target authorization

**Status:** Design (v1)

Pien probes only **authorized** targets. This is an ethical and safety gate, not optional polish.

## Rules

1. Every target requires `authorization.confirmed: true` before any network probe.
2. `confirmed: false` or missing → exit **4** (`TargetRejected`). No connect.
3. `notes` may record owner/ticket/environment — **never** credentials.
4. Private/link-local/metadata addresses require both:
   - `network.allowPrivateNetworks: true`
   - host present in `network.allowedHosts`
5. Public third-party hosts must not be scanned without explicit permission. Examples use `example.com` with `confirmed: false` as placeholders.
6. CLI `--target` does not bypass authorization or SSRF destination validation.
7. Redirects revalidate destinations (ADR-012). Cross-host redirect to disallowed host fails closed.

## Init templates

`pien init` may set `confirmed: true` only for loopback templates with notes like `local development only`.

## Operator checklist

- [ ] I own or have written authorization for every target URL.
- [ ] `confirmed` is true only for those targets.
- [ ] Allowlists are minimal.
- [ ] Secrets use `secret://` references, not inline values.

See [TrustBoundaries.md](../architecture/TrustBoundaries.md) and [ThreatModel.md](../security/ThreatModel.md) (T-AUTH-001).
