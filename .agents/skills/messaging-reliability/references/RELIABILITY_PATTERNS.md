# Reliability Patterns

RabbitMQ baseline:
- durable queues and persistent messages where required;
- exchange/routing-key naming by domain intent;
- bounded retry with exponential backoff + jitter;
- DLQ with inspect/replay runbook;
- queue depth, age, DLQ count, consumer latency metrics;
- consumer prefetch/concurrency tuned from measured workload.

Consistency:
- outbox row is committed with local business state;
- publisher marks publication after broker confirmation as designed;
- inbox/dedup prevents duplicate side effects;
- ack only after local processing transaction succeeds;
- permanent business validation is not retried indefinitely;
- webhook delivery is separately idempotent and does not undo payment success.
