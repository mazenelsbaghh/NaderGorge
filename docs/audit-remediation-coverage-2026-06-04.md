# Audit Remediation Coverage Matrix

تاريخ التحديث: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
المراحل المنفذة: Phase 1, Phase 2, Phase 3

## الخلاصة

تم تنفيذ أكبر أجزاء الخطر الفوري: default secret fallbacks، parent report token، XSS client/embed hardening، RequireStudent policy، dependency audits، CI/Docker/Redis/rate-limit improvements، AI lock atomic updates، mock analytics removal، وfrontend lint cleanup.

لكن بعض البنود لم تنته بالكامل لأنها تحتاج تغييرات أعمق في auth/session model، worker service-to-service signing، DB concurrency، secret rotation خارج الكود، أو اختبارات تكامل أكبر. لذلك تم إنشاء phases جديدة لما تبقى:

- `docs/audit-remediation-phase-4-auth-session-secrets-hardening.md`
- `docs/audit-remediation-phase-5-worker-public-endpoints-parent-audit.md`
- `docs/audit-remediation-phase-6-platform-architecture-tests-ux-debt.md`

## Status Legend

- `Done`: تم تنفيذه والتحقق منه.
- `Partial`: تم تقليل الخطر أو تنفيذ جزء أساسي، لكن يوجد جزء معماري/تشغيلي متبقي.
- `Remaining`: لم ينفذ بعد أو يحتاج phase مستقلة.

## Coverage By Finding

| Finding | Status | What Was Done | What Remains |
|---|---:|---|---|
| P0-1 Default seeding | Partial | seeding الافتراضي اتقيد بـ Development/E2e وconfig flag. | one-time first-admin setup، تدوير أي حسابات حقيقية، test يمنع seeding خارج dev/e2e. |
| P0-2 Worker/internal callbacks | Partial | أزيلت secret fallbacks، أضيف startup validation، worker token، proxy allowlist/auth، وحذف raw logs نشطة. | HMAC timestamp/nonce، حماية Bull Board/JWT admin، إغلاق worker host port production، CORS allowlist كامل، منع DELETE/POST proxy بدون admin server session. |
| P0-3 Parent report | Partial | endpoint يتطلب signed expiring token، frontend/admin link generation أضيفت. | audit log لكل فتح، revoke/rotate links، ربط token بولي الأمر أو purpose/version. |
| P0-4 XSS from rich HTML | Partial | client sanitizer أضيف، embed watermark injection اتصلح. | server-side sanitization عند الحفظ، validation tests لمدخلات XSS، CSP أشمل. |
| P0-5 RequireStudent policy | Done | policy أضيفت في backend auth config. | integration test endpoint. |
| P1-1 npm vulnerabilities | Done | frontend وworker audits أصبحت 0 production vulnerabilities، builds passed. | مراقبة دورية في CI. |
| P1-2 tracked/default secrets | Partial | startup validation للأسرار الحرجة، compose fallbacks الحساسة اتشددت. | إزالة/تدوير أي أسرار حقيقية من الملفات، example configs، secret scanning CI. |
| P1-3 localStorage/sessionStorage tokens | Remaining | لم يتم تغيير session architecture. | refresh token إلى HttpOnly Secure SameSite cookie، access token short-lived/in-memory، CSP. |
| P1-4 password reset | Partial | reset password الآن يلغي refresh tokens القديمة. | one-time server-side hashed reset token، attempts/rate limit per account، قناة تحقق مملوكة. |
| P1-5 E2E destructive controller | Partial | E2E token guard أضيف. | عدم تسجيل controller إلا في E2E، منع EnsureDeleted خارج test container. |
| P1-6 CI mismatch | Done | .NET 9، frontend port 8738، worker/frontend gates، env secrets. | E2E smoke أوسع. |
| P1-7 Docker/deploy exposure | Partial | DB/Redis ports انتقلت للoverride، deploy migration/health check، destructive prune اتشال. | worker host port/Bull Board production isolation، rollback/backup strategy، Makefile deploy hardening. |
| P1-8 AI locking | Done | atomic `ExecuteUpdateAsync` locks لـ AI/mindmaps مع release on enqueue failure. | concurrency tests. |
| P1-9 role updates | Partial | منع empty/unknown roles وآخر admin. | self-demotion guard، transaction explicit. |
| P1-10 balance race/transaction | Partial | validation للamount/reason/negative balance وتحسين save flow. | DB atomic update أو concurrency token/transaction كاملة. |
| P2-1 .NET package conflicts | Remaining | tests الآن clean في التشغيل الحالي، لكن central versioning لم ينفذ. | Central Package Management وتوحيد EF/Microsoft.Extensions versions. |
| P2-2 Redis config | Done | Redis required خارج Development وconfig موحد. | dev docs. |
| P2-3 forwarded headers/rate limit | Done | forwarded headers قبل pipeline، public policies. | known proxy config في production. |
| P2-4 security headers | Partial | security headers middleware وHTTPS/HSTS gated production. | CSP عامة، secure cookies بعد auth migration. |
| P2-5 WhatsApp enumeration | Partial | rate limit وماسك للرقم. | per-phone hash limit، captcha/proof في registration. |
| P2-6 public forms spam | Partial | rate limit، null/size guards. | schema validation حسب form، captcha/body size limit. |
| P2-7 access code hash | Partial | `CodeHash` لم يعد plaintext، أصبح hash. | عدم تخزين `CodePlaintext` إلا encrypted/export one-time. |
| P2-8 subtitle storage | Partial | config paths وshared volume. | health check يثبت backend يرى الملفات، S3/local bucket policy. |
| P2-9 worker logs | Partial | raw active payload/PII logs اتشالت من paths الأساسية. | birthday script redaction، structured logging شامل. |
| P2-10 mock analytics | Done | mock/random admin analytics removed, empty states. | real analytics API لاحقا. |
| P2-11 API/domain drift | Done | API fallback 5245، old domain fallbacks removed. | env docs. |
| P2-12 middleware deprecation | Done | `proxy.ts` بدل `middleware.ts`. | host routing tests. |
| P2-13 frontend lint warnings | Done | `npm run lint` أصبح 0 warnings. | enforce max warnings 0 في CI. |
| P2-14 student/admin UX alignment | Partial | lint/copy/analytics/video cleanup، smoke no overflow. | redesign أعمق للstudent shell وglobal border/token pass. |
| P2-15 SecureVideoPlayer unfinished state | Done | quality dead state removed، `onEnded` wired، embed injection fixed earlier. | CSP/global headers. |
| P2-16 Guid.Empty fallback | Done | shared claim helper واستبدال fallbacks. | tests. |
| P2-17 legacy timestamp behavior | Remaining | لم ينفذ. | UTC migration وإزالة `Npgsql.EnableLegacyTimestampBehavior`. |
| P3-1 build artifacts | Done | git tracked artifact check رجع 0. | استمرار gitignore. |
| P3-2 heavy UI components | Partial | لم تدخل student first screens، reviewed. | bundle analyzer/lazy loading/dead component removal. |
| P3-3 UX copy/brand drift | Partial | old brand strings removed، visible worker copy changed. | glossary centralization لكل النصوص طويلة المدى. |

## Required Next Phases

### Phase 4

يركز على auth/session/secrets/XSS/server-side sanitization لأن دي المتبقي الأعلى خطورة بعد Phase 1.

### Phase 5

يركز على worker/service-to-service/public endpoints/parent report audit لأن دي سطح هجوم تشغيلي مستقل.

### Phase 6

يركز على architecture debt والاختبارات وUTC/package centralization وbundle/UX deep pass.
