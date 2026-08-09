# Context Management Playbook

## Goal

Prevent degradation from an oversized active context while retaining the minimum durable state needed to resume work safely.

## What belongs where

| Information | Location | Lifecycle |
|---|---|---|
| Universal project invariants | `AGENTS.md` | Long-lived |
| Service-specific rules | nearest `AGENTS.md` | Long-lived |
| Reusable specialist workflow | `.agents/skills/*/SKILL.md` | Long-lived, loaded on demand |
| Durable architecture/business decision | `.codex/state/DECISIONS.md` or `docs/adr/` | Long-lived |
| Current objective/progress | `.codex/state/CURRENT_TASK.md` | Replace/update frequently |
| Latest test/build evidence | `.codex/state/VERIFICATION.md` | Replace/update frequently |
| Full logs | `.codex/state/logs/` | Temporary, do not inject wholesale |
| Historical checkpoints | `.codex/state/history/` | Temporary/archive |

## Checkpoint trigger

Write/update a checkpoint when any of these occurs:

- a coherent implementation milestone is complete;
- important source files have changed and tests have been run;
- a major design decision is settled;
- context is becoming large or `/compact` will be used;
- switching to another service/workstream;
- ending a long session.

## Checkpoint quality

A good checkpoint lets a fresh agent answer these questions without reading the old conversation:

1. What are we trying to accomplish?
2. What is in and out of scope?
3. What has already changed?
4. Which files/symbols matter?
5. Which decisions must not be reopened accidentally?
6. What verification has actually passed or failed?
7. What is blocked or unknown?
8. What exactly should happen next?

## Hard limits

- `CURRENT_TASK.md`: target <= 120 lines and <= 12 KiB.
- `VERIFICATION.md`: target <= 100 lines and <= 12 KiB.
- `DECISIONS.md`: target <= 20 concise active decisions. Promote mature items to ADRs.
- Never paste full multi-thousand-line logs into any state file.

The verification script warns when these thresholds are exceeded.
