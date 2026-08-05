# Secrets

**Status:** Design (v1)

## Reference format

```text
secret://env/VARIABLE_NAME
secret://file/relative-or-absolute-path
```

Parsed by `SecretReference`. Invalid syntax → configuration error (exit **3**).

## Rules

1. Prefer references in config; never commit raw tokens.
2. Resolved values are never written to reports, baselines, history JSON, SARIF, HTML, or webhooks.
3. `pien doctor` never prints secret values (only whether resolvers are configured).
4. File secrets must resolve under allowed roots (working directory / explicit allow) — no path traversal.
5. Example configs use names like `PIEN_SAMPLE_API_TOKEN` — placeholders, not real credentials.
6. Webhook HMAC key: `notifications.webhookSecretReference`.

## Redaction

Use shared redaction helpers before any sink (logs, findings evidence, exporters, notifications). Markers replace sensitive material.

Related: [Authentication.md](Authentication.md), [ExtensionModel.md](../architecture/ExtensionModel.md) (`ISecretResolver`).
