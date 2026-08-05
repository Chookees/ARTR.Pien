# CLI UX — ARTR Pien (`pien`)

**Status:** Design (v1)  
**Date:** 2026-08-05  
**Audience:** Operators, CI authors, Developer implementers

CLI-only product. No web UI. English stage names only in progress: **Testing → Inspecting → Examining → Reporting**.

Related: [ProfilesAndPolicies.md](../configuration/ProfilesAndPolicies.md), [ReportFormats.md](../reporting/ReportFormats.md), [HowToUse.md](../HowToUse.md).

---

## Principles

1. **Stdout** = product data / reports. **Stderr** = progress, diagnostics, errors.
2. Deterministic, log-safe text (no animated spinners).
3. Fail closed on safety (`authorization.confirmed`, SSRF); fail open on empty findings.
4. No interactive prompts in v1 — use `--force` / flags.
5. Respect `NO_COLOR` and non-TTY (plain text, no ANSI).

---

## Command matrix

| Command | Purpose | Primary exit codes |
|---------|---------|--------------------|
| `pien init` | Create starter `pien.json` | 0, 2, 8, 10 |
| `pien scan` | Full PIEN pipeline | 0–10 |
| `pien watch` | Interval scans, no overlap | last-cycle or 10 on Ctrl+C |
| `pien validate` | Schema + semantic config check (no probe unless `--network`) | 0, 3, 2 |
| `pien list-checks` | Catalog of built-in checks | 0 |
| `pien explain <check-id>` | Explain one check | 0, 2 |
| `pien baseline create` | Snapshot fingerprints from a run | 0, 7, 8 |
| `pien baseline show [id]` | Show baseline metadata | 0, 7 |
| `pien baseline compare` | Diff baseline vs run | 0, 7 |
| `pien baseline remove <id>` | Delete baseline | 0, 7 |
| `pien history list` | List recent runs | 0, 8 |
| `pien history show <run-id>` | Show one run | 0, 2, 8 |
| `pien history clean` | Apply retention | 0, 8 |
| `pien report` | Re-export stored run | 0, 6, 8 |
| `pien doctor` | Local diagnosis (never prints secrets) | 0, 3, 8 |
| `pien version` | SemVer line | 0 |

Global: `pien --help` / `pien -h`. Config discovery: `--config <path>` else `./pien.json`.

---

## Exit codes (0–10)

| Code | Name | Meaning |
|------|------|---------|
| 0 | Success | Done; policy passed (or non-scan success) |
| 1 | PolicyFailed | Scan finished; threshold breached (reports still written) |
| 2 | InvalidArguments | Bad CLI usage / unknown check-id / missing run id as user error |
| 3 | InvalidConfiguration | Config load/schema/validation failed |
| 4 | TargetRejected | Auth/safety blocked probe |
| 5 | ScanFailed | Hard execution failure |
| 6 | ReportFailed | Export failed / unknown format / run missing for `report` |
| 7 | BaselineFailed | Baseline op failed |
| 8 | StorageFailed | `.pien` / disk IO failure |
| 9 | NotificationFailed | `notifications.required` and send failed |
| 10 | Cancelled | Ctrl+C / cancel |

CI: `0` green; `1` expected test failure; `2–9` job error; `10` cancelled.

---

## Global flags & environment

| Flag / env | Applies | Behavior |
|------------|---------|----------|
| `--config <path>` | config-aware cmds | Override `./pien.json` |
| `--quiet` / `-q` | scan, watch | Suppress progress on stderr |
| `--verbose` / `-v` | scan, watch, validate | Extra diagnostics on stderr (still no secrets) |
| `--no-color` | all | Disable ANSI even on TTY |
| `NO_COLOR` (any value) | all | Same as `--no-color` |
| `ARTR_PIEN_*` | load | Env overlay after `pien.json` (see ProfilesAndPolicies) |

---

## Progress (stderr)

```
[Testing]    Probing local (1/1)                          20%
[Inspecting] Inspecting local                             40%
[Examining]  Examined PIEN-HEADERS-001 (3/12)             65%
[Reporting]  Writing reports                              95%
[Reporting]  Done                                         100%
```

Fixed-width stage column (12). Watch cycles: `── watch cycle N @ {utc} ──` then progress.

---

## Command details

### `init`

Flags: `--website` | `--api` (default website; both set → exit 2), `--ci` (profile `ci`), `--force`.

| State | Exit |
|-------|------|
| Wrote file | 0 |
| Exists without `--force` | 2 |
| Write failure | 8 |

Stdout success: path + three next-step lines (edit auth → validate → scan). Template may set `authorization.confirmed: true` only for loopback with notes.

### `scan`

Flags: `--config`, `--target`, `--profile`, `--format` (comma-list; `md` alias → `markdown`), `--output`, `--baseline`, `--fail-on`, `--max-pages`, `--max-depth`, `--timeout`, `--no-color`, `--quiet`, `--verbose`.

CLI target URL still requires a matching authorized target or constructs a target that must pass the same safety gates (prefer documenting: `--target` filters by target id; absolute URL override must still have confirmed auth — Developer: align with validator).

Default formats if empty: include `console`. Console summary on stdout; files under `--output` / `output.directory`.

### `watch`

Flags: same scan overrides **plus** `--interval <seconds>` (default 300, min 1, max 86400).

No cron parser. OS schedulers (Task Scheduler / cron / systemd) documented in HowToUse.

Ctrl+C → exit **10**. No overlapping runs.

### `validate`

Flags: `--config`, optional `--network` (only then may probe reachability — default offline).

Success: `Configuration is valid.` + counts. Failure: path-scoped issues, exit **3**.

### `list-checks`

Flags: `--json`. Human table: ID, CATEGORY, SEVERITY, NAME. Sorted by ID.

### `explain <check-id>`

Case-insensitive match; display canonical ID. Unknown → **2**.

### `baseline`

| Subcommand | Key flags | Notes |
|------------|-----------|-------|
| `create` | `--run-id`, `--name`/`--id`, `--target` | Default latest completed run |
| `show` | `[id]`, `--json` | Default latest |
| `compare` | `--baseline`, `--run-id` | Exit 0 on success even if diffs exist |
| `remove` | `<id>` | Irreversible; no prompt |

### `history`

| Subcommand | Flags | Notes |
|------------|-------|-------|
| `list` | `--limit` (20), `--json` | Empty → hint to scan, exit 0 |
| `show` | `<run-id>` | Not found → 2 |
| `clean` | `--keep` | Default `storage.retainRuns` |

### `report`

Flags: `--run-id` (required), `--format` (default `json`), `--output` (file or dir).

Supported: `console`, `json`, `sarif`, `junit`, `markdown` (`md`), `html`.

### `doctor`

Never prints secrets. Checks runtime, writability, `.pien`, config presence/validity, exporters. Exit **3** if config present but invalid; **8** if cwd not writable.

### `version`

Stdout: `ARTR Pien {semver}`. Optional `Commit: {sha}`.

---

## Message shape

```
error: Target rejected — authorization.confirmed is false for 'prod'.
  hint: Confirm ownership in pien.json, or remove the target.
```

---

## Non-goals (CLI v1)

- Interactive prompts / TUI
- Cron expression parser
- Web dashboard
- Docker-based runner
- Printing resolved secrets
- Claiming certification in console copy

---

## Implementation notes for Developer

1. Align `watch` with full scan flag parity (scaffold may only pass `--quiet`).
2. Normalize `md` → `markdown` at CLI boundary.
3. Baseline stubs must not return 0 on failure — use exit **7**.
4. Exit **9** only when `notifications.required` is true.
