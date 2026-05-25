# Contributing to Nightwalk

This is a solo hobby project. Contributions are welcome but not actively solicited — please open an issue to discuss before sending a PR for anything larger than a typo or small bug fix.

## Before you start

1. **Read [CLAUDE.md](CLAUDE.md)** — the entry point for collaborators, points at the docs that matter.
2. **Read the conventions**:
   - [docs/conventions/naming.md](docs/conventions/naming.md) — file/folder layout, namespaces, type suffixes, member casing.
   - [docs/conventions/patterns.md](docs/conventions/patterns.md) — assembly split, manual DI, `EventBus` lifecycle, config pipeline, state-machine flags.
3. **Skim [docs/adr/](docs/adr/)** — architecture decisions are load-bearing, not aspirational. Don't propose changes that contradict an ADR without first proposing an update to the ADR.

## Issue-first

For anything other than a small fix:

1. [Open an issue](https://github.com/jazzonaut/nightwalk/issues/new/choose) describing the problem or proposed change.
2. Wait for triage — labels follow the canonical scheme in [docs/agents/triage-labels.md](docs/agents/triage-labels.md). When the label moves to `ready-for-human` or `ready-for-agent`, the issue is well-specified and safe to start.
3. Then send the PR.

This avoids wasted work on changes that conflict with in-flight plans.

## PR expectations

- Keep changes focused. One concern per PR.
- Follow the existing conventions (file layout, naming, `EventBus` lifecycle, manual DI).
- If you change behavior, update the relevant doc in `docs/systems/` in the same PR.
- If you introduce a new ADR-level decision, add an ADR in `docs/adr/`.
- Build cleanly — `dotnet build Nightwalk.csproj` should produce no new warnings.

## Licensing of contributions

By submitting a contribution, you agree that your contribution is licensed under the same GPL-3.0 license that covers the project source (see [LICENSE](LICENSE)).

## Code of conduct

By participating, you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).
