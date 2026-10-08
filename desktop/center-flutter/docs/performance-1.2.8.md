# Reception performance in 1.2.8+11

This release keeps attendance and money separate. Acceptance still follows a committed, validated SQLite transaction. The secondary still obtains authoritative state from the host and creates no operational database.

## Changes

SQLite storage version 2 saves changed records instead of rewriting the complete logical JSON document. On the first host opening, the migration validates the version 1 snapshot, flushes a separate `pre-storage-v2` logical backup, migrates in a transaction and checks that the reconstructed data matches. A failed migration rolls back. Older binaries cannot open storage version 2; retain the pre-migration backup for recovery using supported backup tools.

Immutable lists share their unchanged records until a mutation. Encoding reuses unchanged sections. Financial validation and academic import use indexed relevant records. Financial summaries and report inputs are reused until committed state changes. Report CSV writes buffered UTF-8 in a worker, retaining the original filtering, formula protection and authorization checks.

Automatic backups capture immutable records briefly in the reception queue, then serialize, flush and prune outside it. Store shutdown waits for a captured backup. Manual and pre-update backups remain preserved. Support capture uses a consistent read transaction, including command receipts, and serializes outside Internet transfer.

The secondary waits for host changes outside the reception queue, and foreground operations cancel its background wait. Old hosts retain periodic refresh. Full large snapshots use session-bound immutable chunks with a SHA-256 check before application; failed transfer does not imply a successful command or create an offline authority. Sixteen recent state versions share bounded encoded history, reducing full snapshot fallbacks.

## Slow-operation logs

Sanitized `performance` events include app version, role, operation, duration in microseconds, budget in milliseconds, completion/failure, fixed phase timings and numeric counts. They contain no student identifiers, contact details, raw messages, credentials, SQL or arbitrary paths. Normal reception commands use a 150 ms threshold and LAN transfers 300 ms; report/search/import checks 32 ms, frames 50 ms, event-loop delays 150 ms, backups/exports 250 ms and Internet support/update operations 1000 ms. A normal 10-second LAN state wait uses a 15-second budget.

Events of the same operation are rate limited to one per five seconds; later events include the suppressed repetition count. The existing five-file diagnostic rotation stays bounded. Diagnostic export and private support upload retain timings; the cloud service sanitizes metrics again on retrieval. Support diagnostics remain optional and separate from the complete private host backup.

## Evidence limits

Development measurements use temporary SQLite and a local TLS gateway on macOS. They measure code lookup through committed attendance/application, not physical keyboard entry or a real Windows/router pair. Release artifacts require separate Windows compilation, role/asset/checksum verification, private publication and complete download verification. No physical installation or two-device smoke is implied by those checks.
