# Student Mim welcome

The authenticated student shell displays the approved transparent Mim welcome with Charon audio, lip movement driven by the audio envelope, and one of three direction-specific images. Audio autoplay is attempted; browser policy may require a tap. Escape dismisses without recording completion; the skip control becomes available after 60 seconds. A stalled welcome closes after 120 seconds.

## Account-wide rules

- `FirstWelcomeCompletedAt == null` makes every existing or new student eligible for the first welcome. No creation-date cutoff and no backfill marking older students complete.
- Natural playback completion sends a receipt to the API. Closing, hiding the tab, navigating away, failing to load audio, and dismissing do not count as completion.
- After the first completion, returning greetings are due once per Cairo calendar day. The first greeting also consumes that day's greeting.
- PostgreSQL is authoritative; no localStorage completion flag is used. A three-minute claim prevents two tabs/devices from receiving simultaneous greetings. Interrupted claims are released when possible and otherwise expire.
- Already-open pages check for a new day when focused/visible again. A greeting does not interrupt a continuously active page merely because midnight passes.

## Endpoints

Student role required; the authenticated user ID is taken from claims, never the request body.

- `POST /api/student/welcome/claim`: returns null when not due or currently claimed; otherwise returns `{ token, kind, expiresAt }`.
- `POST /api/student/welcome/complete` with `{ token }`: idempotent receipt, bound to the student and current unexpired claim.
- `POST /api/student/welcome/release` with `{ token }`: abandons only the caller's current uncompleted claim.

`20260923225502_AddStudentWelcomeState` adds five nullable columns to `student_profiles`. Existing rows remain eligible. Publish/apply this migration before running the updated application. Production release remains a separate operation through the repository's release gates.

## Verification

- Eight SQLite relational integration cases in `StudentWelcomeTests` cover both account ages, completion persistence and retries, Cairo midnight, interrupted/expired claims, foreign/stale tokens, missing profiles, and administrative statistics (deleted accounts, empty populations and Cairo day boundaries). These are not PostgreSQL concurrency/load tests.
- `make ops-db-guard` validates migration/snapshot agreement against the PostgreSQL EF model.
- Frontend lint/typecheck/build validate the integration. Local visual preview remains `design/mim-welcome/platform.html`; its scenario controls do not write student state.

## Administrative statistics

Settings → «ترحيب البداية» reads `GET /api/admin/settings/student-welcome-stats`, protected by `settings.manage`. It shows total student profiles, first welcomes completed, first welcomes pending, first completions today (Cairo), and completion percentage. A refresh button reloads the figures. Deleted users are excluded; suspended users remain included. Pending means not completed, not necessarily never displayed. Returning welcomes do not increment first-welcome completion counts.
