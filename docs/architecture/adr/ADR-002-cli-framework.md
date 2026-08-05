# ADR-002 — CLI framework

- **Status:** Accepted
- **Date:** 2026-08-05
- **Verified:** NuGet `System.CommandLine` **2.0.10** (latest stable 2.x as of 2026-08-05; 3.x is preview-only)

## Context

`pien` needs POSIX/Windows parsing, nested commands, `--help`, async handlers, and stable exit codes without a heavy UI toolkit.

## Decision

Use **System.CommandLine 2.0.10** (MIT). Reject Spectre.Console.Cli for v1 (extra styling dependency). Reject System.CommandLine 3.0 previews for stability.

## Alternatives

| Option | Why not (v1) |
|--------|----------------|
| Manual argv | Fragile help/completions |
| Spectre.Console.Cli | Extra surface; branding/UI not required |
| Cocona / CliFx | Less Microsoft stewardship alignment |

## Consequences

CLI code follows SCL 2.x `RootCommand` / `SetAction` patterns. Upgrade to 3.x requires a dedicated ADR after stable release + API review.
