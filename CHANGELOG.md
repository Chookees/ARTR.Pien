# Changelog

All notable changes to ARTR Pien are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository foundation for .NET 10 (`net10.0`), Central Package Management, and Apache-2.0 governance files.
- Power-of-Ten Roslyn analyzers (PIEN0001–0009).
- Core domain contracts, configuration validation, SSRF-resistant HTTP transport, TLS probe, crawler.
- Website checks, PIEN engine, `.pien` storage, report exporters, and `pien` CLI commands.
- Samples, architecture tests, build scripts, GitHub Actions workflows, and compliance readiness docs.

### Security

- Brotli response decompression is re-enabled on the patched .NET runtime 10.0.14 or later (CVE-2026-111497, GHSA-p3jp-9v83-76w9; upstream GHSA-9v8h-qqgm-4m35). On older runtimes `br` is not advertised, `br` bodies are returned undecoded, and a one-time warning is logged. Decompressed bytes count against `MaxResponseBodyBytes`.
- SDK pin raised to 10.0.403 (runtime 10.0.14) for CVE-2026-111497.
