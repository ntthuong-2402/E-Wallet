---
name: messaging-reliability
description: Use for RabbitMQ topology, events/commands, transactional outbox, inbox deduplication, retries, DLQ/replay, Saga/process managers, webhooks, and resilience under dependency outages.
---


# Messaging and Reliability Workflow

Assume at-least-once delivery.

For each message flow define:
- message id, type/version, correlation/causation/trace metadata;
- producer transaction and outbox boundary;
- queue/exchange/routing key;
- consumer idempotency key/inbox rule;
- ack point;
- transient vs permanent failures;
- bounded retry with backoff/jitter;
- DLQ and replay procedure;
- ordering assumptions;
- observability/queue-depth alerts.

Never retry every error. Never let notification failure roll back an already successful payment.

Read `references/RELIABILITY_PATTERNS.md` for the baseline.

