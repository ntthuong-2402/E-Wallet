# Codex setup — Banking Payment Gateway (context-safe edition)

This bundle is designed to reduce context overflow while preserving enough state to continue work across long sessions, compaction, and new chats.

## Context strategy

It uses two complementary layers:

1. **Codex Memories** via `.codex/config.toml` for eligible cross-session memory.
2. **Explicit repository checkpoints** under `.codex/state/` for deterministic recovery even when a thread was compacted, memory generation has not run yet, or a thread used external context that is excluded from memory generation.

Never use memory as the only record of a financial architecture decision. Stable decisions belong in `DECISIONS.md` or an ADR.

## Install into a repository

Copy the contents of this folder into the Git repository root so that `AGENTS.md`, `.codex/`, and `.agents/` are at the root.

If your actual service directory layout differs from the included `src/...` example, move the nested `AGENTS.md` files into the matching real service directories. Codex discovers instructions from the project root down to the current working directory.

Project config is only applied in a trusted repository. Check Codex status/settings after copying the files.

## First run

From the repository root, ask Codex to:

> Summarize the project instructions you loaded, then read `.codex/state/CURRENT_TASK.md` and tell me the next action without changing code.

For service-specific work, launch Codex from that service directory or explicitly ask it to work there so the nested instructions are relevant.

## Before a long session ends

Ask:

> Save a concise context checkpoint to `.codex/state/CURRENT_TASK.md` and update `.codex/state/VERIFICATION.md`. Do not store transcript text or large logs.

Or delegate this to the `context_keeper` subagent.

## When starting a new chat

Ask:

> Continue the current task. Recover state from Git status/diff and `.codex/state/CURRENT_TASK.md`, then read only the relevant decisions and source files.

## Git policy for context state

- `DECISIONS.md` is intended to be committed because it records durable project decisions.
- `CURRENT_TASK.md` and `VERIFICATION.md` are local working-state files and are ignored by the included `.codex/state/.gitignore` by default.
- Large logs and archived task snapshots are ignored.
- If your team wants shared task checkpoints, remove those ignore entries intentionally.

## Memory privacy choice

`disable_on_external_context = true` is enabled because this is a banking-oriented project. A thread using web/MCP/tool-search context is therefore excluded from automatic memory generation. If you explicitly want those threads considered for memories, change it to `false` after reviewing your data-handling requirements.

## Context budget practices

- Keep root `AGENTS.md` concise and invariant-focused.
- Put service-only rules in nested `AGENTS.md`.
- Use one focused skill at a time.
- Search before reading.
- Save raw logs to files; inspect small relevant slices.
- Use subagents for bounded exploration/review/test work.
- Rewrite checkpoints rather than endlessly appending.
- Start a new coherent chat/workstream after a major milestone instead of keeping the entire project in one thread.

## Verify bundle

Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/scripts/verify.ps1
```

Linux/macOS/WSL:

```bash
bash .codex/scripts/verify.sh
```
