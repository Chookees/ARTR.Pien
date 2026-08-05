# Extension model

**Status:** Accepted  
**Date:** 2026-08-05

## Principle

ARTR Pien extends **only at compile time** through explicit registration in the composition root. There is **no** runtime plugin system (no MEF, no `Assembly.Load` of operator DLLs, no directory scanning for checks).

See ADR-018 and ADR-019.

## Why compile-time only

| Concern | Runtime plugins | Compile-time registration |
|---------|-----------------|---------------------------|
| Supply chain | Operator can load untrusted DLLs | Extensions ship in reviewed source / packages |
| Determinism | Version skew at runtime | Same binary → same check set |
| SSRF / trust | Plugin could bypass transport | Plugins cannot exist; checks use injected `ISafeHttpTransport` |
| Power-of-Ten | Dynamic dispatch sprawl | Closed set of types |

## Adding a check

1. Choose a **stable** ID (`PIEN-{AREA}-{nnn}`) and add it to `CheckIds` if built-in.
2. Implement `ICheck` in `ARTR.Pien.Checks` (or an embedder assembly that references Core/Web as appropriate).
3. Register in `PienServiceCollectionExtensions.AddPienChecks`:

```csharp
services.AddSingleton<ICheck, MyNewCheck>();
```

4. Document the ID in user docs; never renumber IDs.
5. Add unit tests with synthetic `InspectionEvidence` (no network).
6. Architecture tests must still pass (dependency direction preserved).

Checks **must not**:

- Create their own `HttpClient` for scanning
- Perform destructive HTTP methods unless a future, explicit opt-in API is designed and ADR’d
- Log or emit secret material

## Adding a reporter

1. Implement `IReportExporter` with a unique `Format` string (see `ReportFormats`).
2. Register in `AddPien`:

```csharp
services.AddSingleton<IReportExporter, MyExporter>();
```

3. Apply redaction and HTML encoding as required by format.
4. Wire CLI `--format` enumeration to include the new format.

## Adding a secret provider

1. Implement `ISecretResolver` (or decorate `DefaultSecretResolver`).
2. Register **instead of** or **before** the default in embedder/hosting setup.
3. Resolve only at probe time; never persist resolved values to `.pien/` or reports.
4. Supported reference shapes should remain documented and stable.

## Adding notifications

1. Implement `INotificationSender`.
2. Register in DI.
3. Enforce SSRF validation on webhook URIs and redacted payloads.

## Forbidden extension paths

- Dropping DLLs into a `plugins/` folder
- Reflection over all types in an assembly looking for `ICheck`
- Downloading check packs over the network at runtime
- Scripting engines evaluating operator code inside the scan process

## Embedders

External products may ship additional checks in **their** compiled assemblies and call `AddPien` + extra `services.AddSingleton<ICheck, …>()`. That remains compile-time composition for the embedder’s build — not a Pien runtime plugin API.
