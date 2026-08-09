# Source traceability map

This bundle is derived from the supplied Vietnamese architecture prompt “Banking / Payment API Gateway” version 1.0, reviewed 03/08/2026.

| Original topic | Codex artifact |
|---|---|
| Goals, scope, principles | `AGENTS.md` sections 1–3 |
| Service decomposition | `AGENTS.md` section 2; `architecture-baseline.md` |
| Payment flow, state, idempotency | `AGENTS.md` sections 3 and 6; `domain-decisions.md` |
| Data ownership and tables | `architecture-baseline.md`; `domain-decisions.md` |
| Security | `AGENTS.md` section 5; `security_reviewer` agent |
| RabbitMQ/Valkey | `AGENTS.md` sections 3 and 6 |
| Three phases | `AGENTS.md` section 8 |
| Testing/performance | `coding-and-verification.md`; `test_verifier` agent |
| Docker/CI/CD/observability | `AGENTS.md` sections 2, 10, and 12 |
| Clean code and DoD | `AGENTS.md` sections 7, 9, 11, and 12 |
| Verification and unclear items | `.codex/VERIFICATION_REPORT.md` |

The original document is intentionally not copied wholesale into `AGENTS.md`, because Codex project-instruction discovery has a default combined size limit. Detailed guidance is progressively loaded through the repository skill.
