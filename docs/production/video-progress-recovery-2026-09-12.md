# Video progress and support incident — 2026-09-12

Build scope: `all`, continuing the owner's explicit all-components Git and Production deployment authorization. Reviewed base: `f71581e626290ae79461778278c47fde1abc07b3`.

## Confirmed defects and changes

- Delayed single progress requests previously used the interval since the preceding HTTP save. Back-to-back retries could acknowledge a complete sequence while accepting only a fraction of its watched time. Singles now use the same cumulative session wall-time budget as batches; sequence replay remains idempotent, and segment, playback-rate and watch-quota limits remain enforced.
- Progress now has its own 120-request/minute/user policy, separate from the 30-request/minute session policy. The browser retains failed segments, respects Retry-After (also exposed through CORS), and retries transient failures even after playback stops. Automatic advancement waits for acknowledgement. A sub-10ms clock remainder cannot keep the drain pending.
- Bunny HLS already had reactive expired-source recovery. This change also renews a playing source within 30 seconds of expiry when progress has extended the underlying session by more than 60 seconds. It uses the existing recovery path to retain position and playback rate. A genuine unexpired 403 still fails closed.
- Authenticated holders of `content.manage` may use preview mode, alongside Admin and Teacher. Teacher ownership checks remain in the controller. Preview does not consume student watch quotas. Admin playback diagnostics are no longer discarded.
- New support events receive database-allocated sequence values before saving, and matching outbox cursors are rewritten in the same save. This addresses the observed concurrent duplicate conversation/sequence constraint failure. The assignment transaction finishes its commit independently of a browser disconnect after assignment work succeeds.

## Database compatibility

`20260912204214_AllocateLiveSupportEventSequences` adds a PostgreSQL sequence and allocation function. Existing tables and rows remain intact. Allocated values retain the legacy tick-scale cursor range. SQLite test contexts do not declare this PostgreSQL sequence. The previous application can run against the expanded schema; the release must still pass the isolated restore and N-1 application gate before migration. Rollback is application-only, retaining the compatible forward schema.

## Verification and limits

Regression coverage includes delayed full-duration singles and duplicate replay on isolated PostgreSQL, eight concurrent support-event writers with matching outbox cursors, authorization policy decisions, real Redis rate-limit rejection, transient progress retries, source renewal boundaries, and browser playback continuity. The browser regression exercises a finished video receiving 429 before its retry succeeds.

The reported student's identity has not been supplied. Existing 71%/64% records are not rewritten: the old handler may have acknowledged time it never persisted, and completion cannot be reconstructed from a screenshot alone. No claim is made that this change recovers historical lost time or guarantees delivery after a browser process is discarded.

The bounded log audit also contained rejected Android wallet requests, access/validation responses, stale/static asset requests and slow lesson reads. These have not been reclassified as successful requests or bypassed. The admin screenshot's exact failed request was not present in the retained logs; its 403 must not be equated with the observed student Bunny expiry events. Detailed read-only findings remain in the ignored incident artifacts under `artifacts/production/nader-video-errors/`.
