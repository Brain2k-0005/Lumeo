# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 3.x     | ✅ Active — security fixes land here |
| 2.x     | ⚠️ Critical fixes only |
| < 2.0   | ❌ Unsupported — please upgrade |

All Lumeo packages (`Lumeo`, `Lumeo.Charts`, `Lumeo.DataGrid`, `Lumeo.Editor`,
`Lumeo.Scheduler`, `Lumeo.Gantt`, `Lumeo.Motion`, `Lumeo.Cli`,
`Lumeo.Templates`, `@lumeo-ui/mcp-server`) ship lockstep and are covered by
this policy.

## Reporting a vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Report privately via one of:

- **GitHub Security Advisories** — [open a private report](https://github.com/Brain2k-0005/Lumeo/security/advisories/new) (preferred)
- **Email** — `security@nativ.sh` with subject `Lumeo security: <short summary>`

Please include:

- Affected package(s) and version(s)
- A description of the vulnerability and its impact
- Reproduction steps or a proof-of-concept
- Any suggested remediation, if you have one

## What to expect

| Stage | Target |
|-------|--------|
| Acknowledgement of your report | within 3 working days |
| Initial assessment + severity triage | within 7 working days |
| Fix + coordinated release | severity-dependent — critical issues are prioritised |
| Public disclosure | after a fix ships, coordinated with the reporter |

We credit reporters in the release notes and the advisory unless you ask to
remain anonymous.

## Scope

In scope:

- The Lumeo .NET packages and their JavaScript interop bundles
- The `@lumeo-ui/mcp-server` npm package
- The `Lumeo.Cli` global tool and `Lumeo.Templates`

Out of scope:

- The documentation site (`lumeo.nativ.sh`) infrastructure itself —
  report site/hosting issues to `security@nativ.sh` but they are not
  covered by the package release cadence
- Vulnerabilities in transitive dependencies that are not reachable from
  shipped code paths (we still want to know — we scope our CI vulnerability
  gate to reachable code, see `.github/workflows/ci.yml`)

## Dependency hygiene

- The CI pipeline fails the build if any **shipped** package (core +
  satellites) has a known-vulnerable NuGet dependency.
- `@lumeo-ui/mcp-server` and the docs site are kept at
  `npm audit --audit-level=high` clean.
- All packages are deterministic builds with SourceLink + embedded
  untracked sources for verifiable provenance.
