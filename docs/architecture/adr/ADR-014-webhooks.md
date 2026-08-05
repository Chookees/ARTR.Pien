> **Superseded:** Duplicate ADR ID from concurrent writes. ID reserved for **ADR-014-json-config.md**. Webhook controls: TrustBoundaries B7. See [README.md](README.md).

# ADR-014 — Optional webhook notifications

- **Status:** Superseded (see TrustBoundaries B7)
- **Date:** 2026-08-05

## Context

Operators want ScanCompleted/Failed/Threshold/Critical/BaselineChanged alerts without embedding arbitrary scripts.

## Decision

Optional HTTPS webhooks configured in `pien.json` with:

- Bounded timeout/payload
- Optional HMAC-SHA-256 via secret reference
- Max 2 retries, exponential backoff upper bound
- Idempotency key
- No secrets in payload
- Notification failure must not erase scan results; may yield exit 9 when configured as required

## Alternatives

| Option | Why not |
|--------|---------|
| SMTP/Slack SDKs | Extra deps; scripts forbidden |
| Fire-and-forget without visibility | Hides failures |

## Consequences

Implement `INotificationSender` in Hosting/Web; Hosting wires after report persist. Unit-test signing and retry policy without external network.
