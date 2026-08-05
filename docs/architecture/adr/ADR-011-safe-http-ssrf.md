# ADR-011 — Safe HTTP and SSRF resistance

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

A verification tool that fetches URLs is a classic SSRF client. Targets, redirects, and webhooks can point at cloud metadata or internal networks.

## Decision

All scan (and webhook) HTTP uses `ISafeHttpTransport` / `IDestinationValidator`:

1. Allow only `http`/`https` absolute URIs.
2. Resolve DNS; classify addresses (`IpAddressClassifier`); block loopback/private/link-local/metadata/multicast unless explicitly allowlisted with `allowPrivateNetworks`.
3. Pin TCP connect via `SocketsHttpHandler.ConnectCallback` to validated IPs.
4. Cap redirects, timeouts, headers, and body size (`HardLimits`).
5. Fail closed on validation errors (`TargetSafetyException`).

## Consequences

- Checks never own raw `HttpClient` for probing.
- Private network scanning requires conscious config (local/dev/CI loopback).
- Residual risk: imperfect IP classification edge cases — tracked in ThreatModel.
