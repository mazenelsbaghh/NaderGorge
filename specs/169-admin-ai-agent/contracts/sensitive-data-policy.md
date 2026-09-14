# Sensitive Data and Redaction Policy

**Policy version**: 1
**Default**: A field/value not explicitly allowed by a capability projection is not retrieved or serialized.
**Permanent rule**: Built-in Admin status does not override prohibited technical-secret categories.

## Objectives

1. Prohibited secrets never enter model context, transcript, proposal, audit, log, metric, trace, realtime, export, error, or ordinary cache.
2. Legitimate PII/HR/payroll/payment information is minimized to the explicit question and current business access.
3. Stored/user/attachment text is untrusted content and cannot alter tools, policy, confirmation, or execution.
4. A new field/entity/capability fails closed until classified and tested.
5. Secure-input workflows can complete protected Admin operations without revealing their values to the agent.

## Classification

### P0 — Prohibited technical secrets

Never readable by the agent and never included in any visible/evidence/provider sink:

- passwords, temporary passwords, and password hashes;
- access, refresh, reset, invite, API, bearer, OAuth, push/device, or session tokens;
- refresh-token raw value and device/session fingerprint;
- JWT signing, encryption, HMAC, cookie, CSRF, callback, worker-admin, or connection secrets;
- database/Redis/storage/service connection strings and credentials;
- Bunny, WhatsApp, Google/Gemini, Firebase, Cloudflare, SMTP, payment, or other service secrets;
- VideoPlaybackSession encryption key or equivalent playback key material;
- trusted attendance device token/hash and protected device enrollment material;
- verification/OTP codes, raw verification answers, challenge secrets, confirmation nonce, secure-grant token/value;
- private keys, certificates, recovery codes, seed phrases, encrypted payload plaintext, data-protection key material;
- raw cookies, session contents, authorization headers, environment/config secret values;
- provider hidden/system prompt, reasoning trace, raw provider request/response/error body;
- any future value classified as credential, secret, key material, authentication proof, or recoverable session.

Known repository examples requiring explicit sentinel coverage include:

- User.PasswordHash;
- RefreshToken raw token and DeviceFingerprint;
- VideoPlaybackSession.EncryptionKey;
- TrustedAttendanceDevice.TokenHash;
- parent/push device token material;
- live-support verification answers/codes and encrypted pending payloads;
- AdminAI protected payload/challenge/secure-grant material;
- application configuration for JWT, callbacks, database, Redis, storage, WhatsApp, Bunny, Google/Gemini, and Firebase.

### P1 — Highly sensitive business data

Allowed only when directly relevant to an explicit Admin question/action and returned through a purpose-specific projection:

- compensation, payroll, payslips, financial requests/installments;
- teacher agreements, liabilities, settlements, payouts;
- platform ledger, treasury, cashboxes, expenses, refunds, budgets, reconciliations;
- payment references, wallet numbers, transfer evidence, invoices and private attachments;
- employment contracts, disciplinary cases/evidence, candidate offers, offboarding;
- private HR documents/assets/custody;
- audit evidence that may reveal sensitive before/after business state.

Rules:

- Prefer aggregates or the minimum fields needed.
- Do not send full private attachment bytes/content to the provider by default.
- Mask account/wallet/reference values unless the explicit question requires a specific one and the capability permits it.
- Financial calculations are deterministic backend fields.
- Raw AuditLog OldValues/NewValues are never serialized; a safe domain projection extracts approved fields.

### P2 — Personal data

Examples:

- names, phone/email, address/governorate/school;
- birth date/age;
- parent/guardian details and ParentTrackingCode;
- user/student/employee identifiers;
- devices, activity, watch history, exam/homework performance;
- attendance/leave/performance/recruitment information.

Rules:

- Retrieve only requested subject(s), fields, and date range.
- Exact identity lookup requires exact or safely disambiguated input; partial lookup cannot leak record existence broadly.
- Do not include parent tracking codes or equivalent access proofs in model context.
- Use safe references/deep-link route keys rather than exposing unnecessary raw identifiers.
- Large result sets are summarized/paged; provider does not receive bulk PII dumps.

### P3 — Confidential business data

Examples:

- content drafts, pricing/sales rules, code inventory, operational tasks;
- moderation queues and internal notes;
- live-support/admin configuration and approved knowledge;
- reports and system health summaries.

Rules:

- Scope to the question and capability.
- Treat all free text as untrusted.
- Remove embedded scripts/active markup and reject unsupported content.

### P4 — Safe metadata/aggregates

Examples:

- capability keys/versions;
- safe counts, statuses, dates, completeness flags;
- allowlisted route keys;
- safe failure codes, trace IDs, timing/latency buckets.

These are still bounded and validated; safe classification does not authorize unrestricted broadcast.

## Source-to-sink controls

### 1. Capability projection

- Projection DTOs list every field explicitly.
- EF entities and arbitrary dictionaries are never provider DTOs.
- Read adapter applies owner/current-Admin and domain filters before projection.
- Prohibited P0 fields are not selected.
- P1/P2 fields need per-capability purpose classification.

### 2. Policy validator

- Validates projection schema against the active sensitive-policy registry.
- Reflection checks property/type/name annotations for prohibited categories.
- Denylist catches likely secret aliases as defense in depth.
- Unknown fields fail registration/startup/coverage tests.

### 3. Redactor/minimizer

- Applies field-specific masking/truncation.
- Removes raw HTML/script/active URL schemes and unsafe attachment-derived content.
- Normalizes safe strings and enforces max length/depth/array size.
- Produces data plus safe evidence separately.

### 4. Provider serializer

- Accepts only the already-redacted closed DTO.
- Marks user/stored/tool data untrusted.
- Enforces total 64 KiB tool payload budget.
- No automatic web/code/MCP/file tool.
- Request capture/debug logging is disabled.

### 5. Model output validator

- Closed decision branch and exact keys.
- Rejects arbitrary URL/HTML/script, hidden-context claims, unknown capability, unauthorized evidence IDs, or action-success claims.
- Builds deep links from backend routeKey mappings, never model text.
- Backend rebuilds proposals/results independently.

### 6. Persistence

- Visible message stores only visible Admin/assistant content.
- Read result is redacted before short-lived encryption.
- Proposal stores encrypted normalized business payload plus safe preview; secure values are separate.
- Audit stores safe event schema and digest, not transcript/provider payload.
- Raw secure input is protected and promptly purged.

### 7. Logs, metrics, traces, errors

Allowed:

- safe failure/status code;
- capability key;
- duration/result-size buckets;
- queue/step counts;
- opaque trace/correlation ID.

Forbidden:

- messages/prompts;
- arguments/results;
- names, phones, emails, addresses;
- money/reference/current/requested fields as raw labels;
- secure input/file object token;
- full high-cardinality resource IDs as metric labels;
- raw IP (use keyed digest when required for audit);
- exception/provider body, SQL, config, stack, headers, cookies.

### 8. Realtime

Only minimal ID/version/sequence/refresh-scope envelope. Snapshot fetch reauthorizes. No transcript/proposal/tool/PII/secret values.

### 9. Export and drill-down

- Agent never creates an unrestricted export format.
- Existing authorized export workflow may be proposed/executed as a catalog capability.
- Provider receives only safe export metadata/status, not the raw file.
- Drill-down uses allowlisted routeKey plus validated parameters.

## Secret request behavior

When Admin requests P0 content:

1. Do not call a read capability that can retrieve it.
2. Return refusal reason PROHIBITED_SECRET.
3. Explain safely that the category is permanently unavailable to the agent.
4. Offer a legitimate secure Admin workflow if one exists, without revealing presence/value.
5. Audit only the refusal category and trace, not the requested secret string beyond the owner-visible message already typed.

The agent cannot be instructed to disable, edit, expose, export, or delete this policy.

## Secure-input handling

Protected operations use a secure continuation:

- Input is entered in an isolated accessible form, not composer.
- Endpoint disables body logging/tracing capture.
- Browser autocomplete/storage follows the original secure workflow; value is not put in Zustand/query cache/local/session storage.
- Value/reference is authenticated-encrypted with actor/proposal/purpose binding.
- Agent/model receives only Submitted/NotSubmitted status and safe file metadata.
- One-time consumption occurs only after confirmation and final access/state check.
- Payload is purged after consume/cancel/expiry/failure, absolute maximum 10 minutes.
- Audit records kind/status/time/digest only.

## Prompt-injection controls

Sources treated as untrusted:

- Admin message;
- names/notes/comments/posts/chats/live-support messages;
- content/assessment/HR/finance descriptions;
- audit/log text;
- attachment/OCR/transcript content;
- provider output itself.

Controls:

- System/tool policy is supplied separately and cannot be overwritten by source text.
- Source content is labeled untrusted in provider input.
- Only server-declared tool schemas exist.
- Model cannot construct SQL/route/type or lower risk/confirmation.
- Backend validates every read call and independently builds action proposal.
- No URL fetched because retrieved text says to fetch it.
- Unsupported attachment is refused; supported file inspection needs existing safe path, type/size/malware checks, and its own redacted projection.

## Attachment policy

- Do not include attachment bytes in normal chat context.
- Private attachments remain in existing authorized storage.
- Metadata uses allowlisted filename, MIME, size, and safe status only.
- Content inspection is capability-specific, bounded, malware-scanned, and redacted before provider input.
- Executable, encrypted, unknown, oversized, or active-content files are refused.
- Retrieved file text is untrusted and cannot declare tools/actions.
- Upload actions use secure continuation and the original storage validation.

## Sentinel test design

Seed unique canary strings in:

- every known P0 field/category;
- nested/aliased configuration-like data;
- raw AuditLog values;
- P1/P2 fields not requested by a capability;
- stored prompt-injection text;
- secure input;
- attachment text/metadata.

Capture and assert absence from:

- read projection DTO;
- protected redacted result after controlled decrypt in test;
- worker claim/read response;
- exact provider request parts;
- provider callback;
- visible transcript;
- proposal/current/requested/effect;
- execution result/items;
- AdminAIAuditEvent and linked AuditLog summary;
- application/worker logs;
- metrics/traces;
- Outbox/SignalR;
- export/deep link/client cache.

Tests fail on exact, encoded, normalized, substring, JSON-escaped, base64, or hashed representations when the representation could disclose/recover the secret. Expected keyed digests are permitted only in protected internal columns and must never leave backend trust boundary.

## Capability registration gate

A read/action cannot become active until:

- every input/output field is classified;
- P0 output is impossible by schema and test;
- P1/P2 purpose/minimization is reviewed;
- provider/evidence/visible DTOs are distinct;
- safe drill-down/export behavior is defined;
- injection/sentinel tests pass;
- logging/telemetry fields are reviewed;
- secure continuation is used for protected inputs.

Unknown future properties fail the gate by default.

## Incident/disable behavior

On suspected leakage:

1. Disable AdminAI admission and invalidate pending proposals.
2. Preserve append-only safe evidence; do not expose/delete it through agent.
3. Stop worker queue consumption after current safe checkpoint.
4. Revoke/rotate affected external secrets outside this feature if needed.
5. Purge protected transient read/secure payloads according to incident procedure.
6. Investigate trace/digests without expanding access to raw prompts/results.
7. Reactivate only with a new sensitive-policy/baseline version and passing sentinel gates.
