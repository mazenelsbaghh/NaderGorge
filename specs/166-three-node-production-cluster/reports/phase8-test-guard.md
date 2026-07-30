# Test Guard

## Findings and corrections

- Migration-from-zero now supplies system roles but no users. Integration tests
  that relied on an old fixed user or inserted a duplicate Student role were
  corrected to create their own real user and reuse the migrated role.
- Lease behavior runs against real PostgreSQL and proves one claimant, expired
  takeover with a higher fencing generation, and stale-owner renewal refusal.
- Admin bootstrap runs against real PostgreSQL and proves BCrypt storage,
  atomic creation, duplicate refusal and rollback when the Admin role is absent.
- The database audit executes its real dynamic orphan/duplicate-key queries
  instead of mocking PostgreSQL metadata.
- Storage tests use real temporary files and validate traversal/symlink refusal,
  checksums, atomic publication, reads, deletes and failure cleanup.
- Operations tests keep live distribution, external scans and origin/domain
  checks explicitly skipped unless their bounded environment gates are set;
  none is reported as a passing live drill.
- Playwright discovery caught an invalid per-describe trace override before
  rehearsal. The secret-bearing domain suite now disables trace/video at file
  scope and lists ten valid HTTP/API/cookie/WebSocket/upload tests.

The reviewed tests assert observable state and boundary behavior. No project
entity or database behavior under test is replaced with a mock.

Result: passed.
