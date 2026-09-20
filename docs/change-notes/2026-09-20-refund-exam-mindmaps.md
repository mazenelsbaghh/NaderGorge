# Production fixes — 2026-09-20

The owner selected build scope `all` after the release plan detected changes in the backend, frontend, worker, database-aware release inputs, and infrastructure context.

- Historical direct grants without a linked sale can appear in the refund workflow. The current content price is only a ceiling; the operator must enter the verified amount actually paid. Access-code and gift grants remain ineligible for cash refunds.
- A submitted exam awaiting essay review no longer blocks the next lesson or its video. The attempt remains pending and is not marked as passed.
- Essays without a teacher answer key move to teacher review instead of retrying AI grading indefinitely, and AI grading uses the configured text model.
- Published chapter mind maps remain visible to students even when optional video-learning tools are disabled or stale.
- Lesson MIM game jobs no longer enter an unsupported queued-alias path, and contract-invalid Gemini output receives one bounded correction attempt before failing safely. The owner's existing `all` build-scope selection applies to this follow-up production repair.
- The release migration gate allows enough time for the observed strict-SSH handshake before probing Patroni, while retaining the five-second remote health deadline and exact-one-primary requirement.
- MIM generation now restricts AI source identifiers to analyzed chapters and rewrites the chosen chapter reference to the server-owned video identity and timestamps before validation.
- MIM generation requires four non-empty choices and non-empty required copy in the provider schema, keeping every generated answer index valid; rejected responses emit only their allowlisted contract code for diagnosis.
- The worker validates lesson source copy as bounded untrusted input instead of applying rendered-output URL/markup rules to it, and accepts all non-empty .NET GUID forms before model generation.
- The backend serializes the nested MIM lesson source with the worker's camel-case field contract, so valid analyzed chapters reach generation instead of being rejected before the AI request.

Planned verification: backend application tests, frontend lint and production build, worker tests and build, database migration guard, release preview, rolling three-node deployment, and final cluster health.
