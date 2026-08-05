# README catcher — Designer ownership

**Status:** Design (v1)  
**Date:** 2026-08-05

This file owns the **catcher text** and **section outline** for the repository README. Full README rewrite may land in docs phase; do not invent screenshots.

## Exact catcher text (hero)

Use verbatim (or lightly copy-edited without changing meaning):

```text
ARTR Pien

PIEN — Proba. Inspice. Examina. Nuntia.
Test. Inspect. Examine. Report.

Long before software systems existed, Roman engineers understood that critical
infrastructure could not rely on hope alone. Aqueducts were surveyed, inspected,
maintained, and built with durability in mind. Parts of those systems still stand
today because reliability was treated as an engineering discipline rather than an
assumption.

Pien applies the same principle to modern websites and APIs: test what can fail,
inspect what was returned, examine the evidence, and report what requires attention.
```

Disclaimer (required near catcher):

```text
This is an engineering metaphor. It does not claim that ancient Rome practiced
modern cybersecurity. Surviving Roman structures symbolize inspection, maintenance,
durability, and disciplined engineering. The Latin phrase is the source of the
PIEN acronym and the conceptual pipeline (Test → Inspect → Examine → Report).
```

## README section outline (Designer-owned)

1. **Catcher** — brand + Latin/English + metaphor + disclaimer  
2. **What it is** — local-first CLI; authorized HTTP(S) websites/APIs; multi-format reports  
3. **What it is not** — no Docker; no exploit framework; no certification claims; no hosted control plane  
4. **Quick start** — build from source; `pien init`; authorize; `validate`; `scan`  
5. **Configuration** — link `pien.json` + `config/schemas/` + `config/examples/`  
6. **CLI surface** — link [CliUx.md](../cli/CliUx.md)  
7. **Checks** — link [CheckCatalog.md](../checks/CheckCatalog.md)  
8. **Reports** — console/json/sarif/junit/markdown/html; link [ReportFormats.md](../reporting/ReportFormats.md)  
9. **Security** — authorization gate; SSRF; secrets; link SECURITY.md + ThreatModel  
10. **Docs index** — HowToUse, AI_Onboard, Dev_Onboard, architecture/  
11. **License** — Apache-2.0  

## Current README

Root `README.md` already carries a thin catcher. Keep it until a docs pass expands sections 3–10; prefer linking into `docs/` rather than duplicating long tables.
