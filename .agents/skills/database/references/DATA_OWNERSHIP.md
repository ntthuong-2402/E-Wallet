# Data Ownership Baseline

Identity owns users/roles/permissions/refresh tokens/login attempts/MFA/service clients.
CustomerAccount owns customer profile, account profile/relationship, limits, beneficiaries, and non-authoritative balance projection if used.
PaymentLedger owns payment orders/status history/idempotency/fees and authoritative ledger journals/lines/balance snapshots within its boundary.
Integration owns provider mappings/attempt metadata required by its workflow.
Notification owns webhook/notification delivery history.
Audit/security-event persistence may begin as a module/worker but must have explicit ownership and retention.

Cross-context IDs are references, not database foreign keys.
