# Phase 5: Worker, Public Endpoints, Parent Report Audit, and Service-to-Service Hardening

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
ينفذ بعد: Phase 4 أو بالتوازي معه لو الفريق منفصل  
الأولوية: عالية

## الهدف

تحويل حماية worker/callbacks/public endpoints من shared tokens بسيطة إلى trust boundary أقوى: HMAC، private network، admin authorization، auditability، spam controls، وredacted logging شامل.

## البنود المغطاة

- P0-2: HMAC callback signing، worker/Bull Board protection، private network، CORS allowlist.
- P0-3: parent report audit/revoke/rotate.
- P2-5: WhatsApp check anti-enumeration أعمق.
- P2-6: Public forms schema validation/captcha.
- P2-7: access code plaintext storage policy.
- P2-8: subtitle storage health check.
- P2-9: worker/birthday logging redaction.

## Task 1: HMAC Signed Internal Callback Protocol

### المشكلة

Phase 1 أزال default secrets، لكن shared static token وحده يظل أقل من المطلوب.

### التنفيذ

1. Backend internal callback endpoints يجب أن تقبل headers:
   - `X-Service-Id`
   - `X-Timestamp`
   - `X-Nonce`
   - `X-Signature`
2. Signature:
   - `HMACSHA256(secret, method + path + timestamp + nonce + bodyHash)`
3. Backend:
   - يرفض timestamp أقدم من 5 دقائق.
   - يخزن nonce مؤقتا في Redis لمنع replay.
   - يستخدم fixed-time compare.
4. Worker:
   - يوقع callbacks بنفس البروتوكول.
   - لا يرسل shared secret plaintext كـ header.
5. Tests:
   - valid signature accepted.
   - wrong signature rejected.
   - old timestamp rejected.
   - repeated nonce rejected.

### قبول

- callbacks لا تقبل replay أو static token فقط.

## Task 2: Worker API and Bull Board Production Isolation

### المشكلة

Phase 1/2 حسنت proxy/token، لكن worker service يجب ألا يكون public في production.

### التنفيذ

1. `docker-compose.yml`:
   - لا ينشر worker port في production.
   - dev-only port في `docker-compose.override.yml`.
2. Worker Express:
   - CORS allowlist فقط.
   - `/ui` خلف admin JWT أو disabled unless `WORKER_UI_ENABLED=true`.
   - status/cancel/retry endpoints require service/admin token.
3. Frontend proxy:
   - GET status مسموح فقط بعد admin/session check server-side.
   - DELETE/POST cancel/retry admin-only.
   - لا يسمح بأي path خارج allowlist.
4. Add tests for proxy allowlist and auth.

### قبول

- لا يوجد worker host port production.
- Bull Board لا يعمل إلا explicit/dev/admin.

## Task 3: Parent Report Audit, Revoke, and Rotate

### المشكلة

Phase 1 أضاف signed expiring token، لكن لا يوجد audit/revoke/rotate.

### التنفيذ

1. أضف entity:
   - `ParentReportLink`
   - `StudentId`
   - `TokenVersion` أو `TokenId`
   - `ExpiresAtUtc`
   - `RevokedAtUtc`
   - `CreatedByUserId`
2. token payload يحتوي `jti` أو `version`.
3. GET summary:
   - يتحقق signature.
   - يتحقق link غير revoked.
   - يسجل `ParentReportAccessLog`:
     - studentId
     - tokenId/version
     - IP
     - user agent
     - accessedAt
4. Admin endpoint:
   - create link
   - revoke link
   - rotate link
   - list active links
5. Frontend:
   - زر إنشاء/نسخ/إلغاء رابط ولي الأمر.

### قبول

- أي فتح تقرير له audit row.
- link يمكن إلغاؤه فوريا.

## Task 4: Public WhatsApp Anti-Enumeration

### المشكلة

Phase 2 أضاف rate limit وماسك للرقم، لكن enumeration يمكن أن يستمر ببطء.

### التنفيذ

1. rate limit per IP + per phone hash.
2. لا تعرض response يوضح هل الرقم مستخدم داخل المنصة، فقط WhatsApp reachability المطلوب للتسجيل.
3. أضف captcha/proof lightweight بعد عدد محاولات.
4. سجل abuse metrics بدون أرقام plaintext.
5. Tests:
   - attempts كثيرة لنفس phone hash ترفض.
   - response لا يكشف بيانات طالب.

### قبول

- endpoint لا يصلح لتعداد أرقام الطلاب.

## Task 5: Public Forms Schema Validation and Abuse Controls

### المشكلة

Phase 2 أضاف rate/size guards، لكن submit يجب أن يطابق schema الفعلي.

### التنفيذ

1. عند submit:
   - load form schema.
   - reject fields unknown.
   - enforce required fields.
   - enforce type: email/phone/select/text.
   - enforce max length per field.
2. أضف captcha/proof بعد threshold.
3. أضف body size limit للroute.
4. Add tests for:
   - unknown field rejected.
   - required missing rejected.
   - oversized value rejected.
   - invalid phone/email rejected.

### قبول

- submissions لا تتجاوز schema المخزن.

## Task 6: Access Code Plaintext Storage Policy

### المشكلة

Phase 2 جعل `CodeHash` hash حقيقي، لكن `CodePlaintext` ما زال موجودا للحفظ/التصدير.

### التنفيذ

1. قرر السياسة:
   - one-time export فقط ثم لا تخزن plaintext.
   - أو encrypted-at-rest باستخدام key خارج DB.
2. لو one-time:
   - worker يرجع codes plaintext للطلب مرة واحدة.
   - DB يخزن hash فقط.
3. لو encrypted:
   - أضف encryption service.
   - key من env/secret manager.
   - لا تحفظ key في DB.
4. Tests:
   - DB لا يحتوي الكود plaintext في hash.
   - redeem يستخدم hash compare.

### قبول

- database leak لا يعطي أكواد قابلة للاستخدام مباشرة.

## Task 7: Subtitle Storage Health Check

### المشكلة

Phase 2 أضاف shared volume/config، لكن لا يوجد health check يثبت backend يرى ملفات worker.

### التنفيذ

1. Backend health endpoint أو check:
   - يقرأ `SubtitleStorage:PublicBaseUrl`
   - يثبت wwwroot/subtitles أو mounted directory موجود.
2. Worker startup:
   - يتأكد `SUBTITLE_STORAGE_PATH` writable.
3. Docker:
   - health check يفشل لو volume غير mounted.
4. E2E/smoke:
   - generate dummy subtitle file.
   - backend can serve it.

### قبول

- misconfigured subtitle volume يظهر قبل production use.

## Task 8: Worker Logging Redaction Completion

### المشكلة

Phase 2 نظف active logs الأساسية، لكن birthday script وغيره قد يطبع بيانات سياقية.

### التنفيذ

1. استخدم `worker/src/logging.ts` في كل jobs/scripts.
2. راجع:
   - `worker/src/scripts/birthday-congratulator.ts`
   - notification jobs
   - essay/mindmap/video jobs
3. لا تطبع:
   - phone numbers
   - full student IDs
   - message content
   - file API URIs
   - raw payloads
4. أضف grep verification command.

### قبول

- logs تحتوي ids masked وcorrelation فقط.
