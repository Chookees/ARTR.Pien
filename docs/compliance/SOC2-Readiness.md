# SOC 2 readiness mapping (not a certification)

This document maps ARTR Pien practices to common SOC 2 Trust Services Criteria themes for **readiness** conversations.

**This is not a SOC 2 report, examination, or certification.**

| Criteria theme | Pien evidence |
|----------------|----------------|
| Security | SSRF-resistant transport, redirect revalidation, target authorization gate |
| Availability | Local-first CLI; no mandatory cloud dependency |
| Confidentiality | Secret references; report redaction; cookie values not logged |
| Processing integrity | Deterministic checks; bounded inputs; exit codes for CI gating |
| Privacy | No PII collection service; operator-controlled local state under `.pien/` |
