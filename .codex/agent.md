# Compatibility note

This file is a human-readable index only. **Codex does not automatically discover `.codex/agent.md` as project instructions.**

The automatically discovered project instructions are in:

- `../AGENTS.md`
- nested `AGENTS.md` files under the working directory hierarchy

Project settings live in `config.toml`, project-scoped subagents live in `agents/*.toml`, and repository skills live in `../.agents/skills/*/SKILL.md`.

For session/context recovery, use `state/CURRENT_TASK.md`, `state/DECISIONS.md`, and `state/VERIFICATION.md`.
