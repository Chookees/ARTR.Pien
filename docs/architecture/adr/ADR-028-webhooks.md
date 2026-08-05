# ADR-028 — Optional webhook notifications

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Operators want ScanCompleted / ScanFailed / ThresholdExceeded / NewCriticalFinding / BaselineChanged alerts without embedding arbitrary scripts or cloud bus SDKs.

## Decision

Optional HTTPS webhooks in `pien.json` with:

- Bounded timeout and payload size
- Optional HMAC-SHA-256 via secret reference
- Maximum two retries, exponential backoff with upper bound
- Idempotency identifier
- No secrets/cookies/auth headers in payload
- Delivery through the same SSRF-safe transport rules as probes (ADR-011)
- Notification failure must not erase scan results; may yield exit code 9 when configured as required

Do not implement arbitrary notification scripts or Slack/SMTP SDKs in v1.

## Consequences

Implement `INotificationSender` in Hosting composition; invoke after run persistence. Unit-test signing and retry without external network.
