# البنود المتبقية: الموظفون والتحديث والدعم الفني

**آخر مراجعة:** 2026-07-13
**الحالة:** مفتوحة جزئيًا — التنفيذ الأساسي مكتمل، وبنود القبول التالية تحتاج إغلاقًا فعليًا.

## 1. اختبارات E2E والصلاحيات

- [ ] إصلاح وتشغيل سيناريو Admin A/B الحقيقي.
  - إنشاء جلستين مستقلتين.
  - تنفيذ create/update/disable للموظف.
  - تغيير role/permissions وانعكاسها على جلسة الموظف الأخرى.
  - التعامل مع conflict مع الحفاظ على draft.
  - إغلاق المسار عند سحب الصلاحية.
  - معالجة فشل `ERR_ABORTED` عند فتح `admin.lvh.me:3000/admin/users`.

- [ ] تشغيل سيناريوهات Chromium كاملة بدون `skipped`.
- [ ] تشغيل نفس السيناريوهات على WebKit.
- [x] إثبات عدم استخدام `window.location.reload` في مسارات الموظفين والصلاحيات عبر reload guard؛ الاستثناء الوحيد SecureVideoPlayer.

## 2. واجهة Participant للدعم الفني

- [ ] إغلاق `401` أثناء session bootstrap runtime؛ الكود وboundary contract موجودان، لكن تشغيل Participant الحقيقي ما زال يفشل بـ`401`.
- [ ] إثبات بقاء history عند توقف availability في المتصفح الحقيقي؛ contract موجود، لكن runtime gate لم ينجح.
- [ ] إثبات عزل draft بين محادثتين في المتصفح الحقيقي؛ contract موجود، لكن runtime gate لم ينجح.
- [ ] إثبات ترتيب responses العكسي وعدم عرض رسائل محادثة داخل أخرى في runtime حقيقي.
- [ ] إثبات retry بعد فشل إرسال الرسالة مع الاحتفاظ بالمسودة في runtime حقيقي.
- [ ] إثبات rating لمحادثة مغلقة في runtime حقيقي.
- [ ] إثبات duplicate close وduplicate transfer وفقد الملكية في runtime حقيقي.

## 3. الطابور والتوزيع

- [x] capacity وFIFO وclose-and-admit مع PostgreSQL وRedis حقيقيين.
- [x] transfer إلى موظف غير متاح.
- [x] StudentContext ownership وlinked/unlinked.
- [x] presence متعدد الاتصالات وانتهاء مهلة disconnect.
- [ ] إثبات least-load والتدوير عند تعادل الحمل؛ اختبار acceptance موجود لكن تشغيله لم ينجح بعد.
- [ ] إثبات عدم تجاوز capacity تحت طلبات متزامنة؛ اختبار acceptance موجود لكن تشغيله لم ينجح بعد.
- [ ] إثبات إعادة التوزيع بعد تجاوز مهلة disconnect؛ اختبار acceptance موجود لكن تشغيله لم ينجح بعد.
- [ ] تشغيل اختبارين staff A/B في جلستين مستقلتين عبر المتصفح.

## 4. Realtime وSignalR

- [x] event envelope وevent ID وdedupe الأساسي.
- [x] malformed/missing/out-of-order contract assertions.
- [x] session reconciliation API على Chromium: `4/4 passed`.
- [ ] اختبار malformed JSON عبر اتصال SignalR فعلي؛ contract test موجود فقط.
- [ ] اختبار missing event ID حسب legacy policy عبر hub فعلي؛ contract test موجود فقط.
- [ ] اختبار sequence gap/reconnect وعدم تكرار الرسائل في اتصال فعلي؛ contract test موجود فقط.
- [ ] إخراج counters الخاصة بـreconnect duration وreconciliation failures إلى monitoring خارجي؛ counters المحلية موجودة.

## 5. StudentContext وعمليات المحادثة

- [x] linked student context — مثبت في integration test.
- [x] unlinked student context — مثبت في integration test.
- [ ] link replacement في runtime حقيقي.
- [ ] action success/failure في runtime حقيقي.
- [ ] tablet drill-in في runtime حقيقي.
- [ ] ownership loss أثناء فتح StudentContext في runtime حقيقي.
- [ ] إغلاق/تحويل conversation مع 403 و409 مع الحفاظ على selection والدraft في runtime حقيقي.

## 6. AI provider — T119

- [ ] تشغيل provider حقيقي configured.
- [ ] تسجيل provider/model/correlation ID/latency بدون أسرار أو PII.
- [ ] إثبات callback حقيقي من worker إلى backend.
- [ ] اختبار reconnect أثناء دورة AI.
- [ ] حفظ screenshot آمنة للنتيجة.
- [ ] عدم إغلاق البند عند غياب credentials أو quota أو network أو callback path.

## 7. Release readiness — T122

- [ ] إغلاق جميع بنود P0.
- [ ] إغلاق بنود P1 المطلوبة للإصدار.
- [ ] نجاح T119.
- [x] تشغيل backend/worker/frontend build النهائي: `make verify` نجح، مع PostgreSQL integration skip عند غياب connection string.
- [ ] تشغيل اختبارات PostgreSQL وRedis والتزامن.
- [ ] تشغيل Chromium وWebKit.
- [ ] تشغيل Docker health/readiness/restart checks.
- [x] توثيق rollback وسلامة البيانات في تقرير الجاهزية أدناه.
- [x] كتابة release-readiness report بنتائج فعلية؛ البوابات المحجوبة مذكورة صراحة.

## أوامر التحقق

```bash
make verify
make verify-e2e
cd frontend && npm run lint
cd frontend && npm run typecheck
cd frontend && npm run build
cd frontend && npm run check:live-support-contracts
cd frontend && npm run check:platform-events
cd frontend && node scripts/check-query-contracts.mjs
cd frontend && node scripts/check-no-unallowlisted-reloads.mjs
```

## الوضع الحالي

تم التحقق من backend build، migrations، PostgreSQL/Redis integration، realtime API، frontend build، والـstatic contracts. لا تُعتبر الخطة مكتملة قبل إغلاق البنود المفتوحة أعلاه وتسجيل نتائج تشغيل فعلية في:

- `specs/160-employee-realtime-refresh/achievements.md`
- `docs/employee-and-realtime-refresh-remediation-plan.md`

## Evidence Update — 2026-07-13

- `npm run check:live-support-contracts`, `npm run check:platform-events`, query-contract checker، وreload guard نجحت.
- `npm run typecheck`, `npm run lint`، و`npm run build` نجحت.
- StudentContext tests نجحت: 5/5 في آخر تشغيل مركّز.
- أضيفت اختبارات acceptance حقيقية لـleast-load/tie rotation، concurrent capacity، وdisconnect redistribution؛ التشغيل محجوب حاليًا بغياب PostgreSQL وRedis.
- اختبارات Admin A/B أصبحت تستخدم جلسات مستقلة، بصمات أجهزة فريدة، retry محدود لـ`ERR_ABORTED`، وskips واضحة عند غياب `REAL_ADMIN_AB_E2E` أو seed.
- أضيفت تغطية linked/unlinked/link replacement/action failure/tablet/ownership loss و401/history/draft/rating/duplicate actions؛ اختبارات mock لا تُعد قبولًا حقيقيًا للنظام.
- لا يمكن إغلاق Chromium/WebKit وSignalR hub الفعلي وT119 وT122 من البيئة الحالية بدون Docker/API seed/browser binaries/AI credentials.

## Release Readiness Report — 2026-07-13

### Passed

- Backend build: 0 warnings / 0 errors.
- Application tests: 375 passed, 1 Redis-dependent test skipped by `make verify`.
- Frontend lint, typecheck, and production build passed.
- Live-support client contracts passed for malformed/missing-id/duplicate/out-of-order/gap events.
- Platform-event, query-contract, and reload-allowlist checks passed.
- StudentContext focused tests passed 5/5.
- Rollback: keep the typed invalidation adapter and compatible API contracts; disable realtime consumers and return to the documented polling interval without database rollback. Durable PostgreSQL state, outbox rows, drafts, and audit records remain authoritative.

### Blocked and not claimed as passed

- Real Admin A/B browser workflow: requires the E2E backend seed and browser runtime; current runs still encounter `ERR_ABORTED`/session bootstrap failures.
- Chromium/WebKit full runs: browser binaries and a stable E2E app/API are unavailable.
- PostgreSQL/Redis routing acceptance: tests exist for least-load, tie rotation, concurrent capacity, and disconnect redistribution; current execution is blocked by missing services.
- Real SignalR malformed/missing-id/reconnect monitoring: client contracts are green, but a live hub run is not available.
- T119 real AI provider: provider credentials/quota/network/worker callback and screenshot are unavailable.

This report intentionally separates repository verification from environment-dependent acceptance and does not mark skipped or mocked runs as real-system success.

## Latest verification override — 2026-07-13

- PostgreSQL/Redis runtime is available locally; backend integration suite passed `22/22` after the `PlatformSettings` migration fix.
- Worker Docker readiness is healthy; the real-AI harness exits blocked before provider invocation because `AI_CALLBACK_SECRET`, `WORKER_URL`, and `GOOGLE_CLOUD_PROJECT` are not configured.
- Latest focused Chromium live-support run: `13 passed / 4 failed`. The passing routing test confirms real queue close admission; the remaining failures are participant message-proxy interception and shared live-support feature/session setup, so the related runtime checkboxes remain open.
- Admin A/B and realtime browser coverage exists and must be reported only from a complete combined Chromium/WebKit run; no full green run is claimed here.
