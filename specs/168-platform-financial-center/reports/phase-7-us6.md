# Phase 7 — treasury and historical reconstruction

- Historical batches persist source checksum, candidate/posted/already-posted/failed counts, item status, and explicit exceptions.
- Replay is safe through the journal idempotency key and the source item uniqueness constraint.
- Treasury reconciliation is read-only against the posted journal and stores counted balance plus evidence note.
- Transfer and reconciliation APIs are protected by separate permissions.
