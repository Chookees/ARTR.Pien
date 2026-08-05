> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-006-dotnet10.md** instead. See [README.md](README.md).

# ADR-017 — .NET 10 platform

- **Status:** Superseded (see ADR-006)
- **Date:** 2026-08-05
- **Verified:** SDK pin `global.json` → **10.0.302**; `Microsoft.Extensions.*` **10.0.10** latest stable on NuGet for net10 line

## Context

Greenfield product should use current LTS/current .NET with modern HTTP/TLS/JSON APIs.

## Decision

Target `net10.0` exclusively for v1. Use BCL `HttpClient`/`SocketsHttpHandler`/`SslStream`/`System.Text.Json`/`TimeProvider`/`Channels`.

## Alternatives

| Option | Why not |
|--------|---------|
| Multi-TFM net8+net10 | Extra CI matrix for v1 |
| Native AOT mandatory | Analyzer/reflection constraints; defer |

## Consequences

Contributors need SDK 10.0.302+. AOT publish may be explored later without blocking v1.
