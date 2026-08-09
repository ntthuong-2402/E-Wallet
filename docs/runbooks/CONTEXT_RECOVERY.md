# Codex Context Recovery Runbook

When continuing after compaction or a new chat:

1. Run `git status` and inspect `git diff --stat`.
2. Read root + nearest nested `AGENTS.md`.
3. Read `.codex/state/CURRENT_TASK.md`.
4. Read only relevant entries in `.codex/state/DECISIONS.md`.
5. If tests/debugging are involved, read `.codex/state/VERIFICATION.md`.
6. Search for listed files/symbols and inspect current source, because source can supersede stale summaries.
7. Continue from the exact next action or explicitly revise the checkpoint if evidence changed.

Before `/compact` or session handoff, ask `context_keeper` to update the state files.
