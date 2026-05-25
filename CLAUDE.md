# Nightwalk

A Godot game project.

## Agent skills

### Issue tracker

Issues and PRDs live as GitHub Issues at `jazzonaut/nightwalk` (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical 5-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`) — labels match their role names. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Code conventions

Before writing new code or refactoring, consult:

- `docs/conventions/naming.md` — file/folder layout, namespaces, type suffixes (`*Event`, `*Id`, `*Tracker`, `*Setup`/`*Result`, …), member casing, capability-flag naming.
- `docs/conventions/patterns.md` — recurring code shapes: assembly split (`Core` vs `Godot`), manual DI in `GameRoot`, `*Setup`/`*Result` helpers, `EventBus` subscribe/unsubscribe lifecycle, state-machine capability flags, TOML config pipeline + hot reload fan-out, session reset, the `null!` field pattern.

Both files are inferred from the current codebase. If a new requirement doesn't fit, update the convention doc in the same change rather than diverging silently.
