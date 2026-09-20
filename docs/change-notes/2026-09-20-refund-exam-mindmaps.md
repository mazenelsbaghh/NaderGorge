# Production fixes — 2026-09-20

The owner selected build scope `all` after the release plan detected changes in the backend, frontend, worker, database-aware release inputs, and infrastructure context.

- Historical direct grants without a linked sale can appear in the refund workflow. The current content price is only a ceiling; the operator must enter the verified amount actually paid. Access-code and gift grants remain ineligible for cash refunds.
- A submitted exam awaiting essay review no longer blocks the next lesson or its video. The attempt remains pending and is not marked as passed.
- Essays without a teacher answer key move to teacher review instead of retrying AI grading indefinitely, and AI grading uses the configured text model.
- Published chapter mind maps remain visible to students even when optional video-learning tools are disabled or stale.
- Lesson MIM game jobs no longer enter an unsupported queued-alias path, and contract-invalid Gemini output receives one bounded correction attempt before failing safely. The owner's existing `all` build-scope selection applies to this follow-up production repair.

Planned verification: backend application tests, frontend lint and production build, worker tests and build, database migration guard, release preview, rolling three-node deployment, and final cluster health.
