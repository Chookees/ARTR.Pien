# Operations runbooks

## Local scan

1. `pien init --website` (or `--api`)
2. Confirm `authorization.confirmed` and network allowlists for private targets
3. `pien validate`
4. `pien scan --format console,json,sarif,junit,markdown,html`

## Watch

`pien watch --interval 300` runs non-overlapping cycles. Cancel with Ctrl+C (exit 10).

## Doctor

`pien doctor` checks writability and optional config validation. `pien doctor --network` resolves loopback DNS only.

## Baselines

```text
pien baseline create --id prod --run-id <id>
pien baseline show --id prod
pien baseline compare --id prod --run-id <id>
pien baseline remove --id prod
```

## History retention

`pien history clean` applies retention (default keep 50). Scan engine also applies `storage.retainRuns` after each run.

## Webhooks

Configure `notifications.webhookUrl` (https) and optional `webhookSecretReference`. Failures are visible; optional webhooks do not erase scan results. `required: true` maps to exit 9.

## Incident: target rejected (exit 4)

Confirm ownership (`authorization.confirmed` or `--confirm-authorization` with `--target`), allowlist hosts, and ensure private networks are only enabled intentionally.
