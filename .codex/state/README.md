# Durable working state

These files supplement Codex Memories with deterministic repository-local state.

- `CURRENT_TASK.md`: current objective/progress/next action; overwrite stale content.
- `DECISIONS.md`: durable project decisions; keep concise and promote mature decisions to ADRs.
- `VERIFICATION.md`: latest build/test/security verification evidence.
- `logs/`: full command output if needed; ignored by Git.
- `history/`: archived task snapshots; ignored by Git.

Do not store secrets, tokens, passwords, private keys, production PII, or sensitive customer data in these files.
