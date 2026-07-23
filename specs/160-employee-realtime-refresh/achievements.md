# Spec 160 Evidence Ledger / سجل الأدلة الفعلية

آخر تحديث: 2026-07-13

هذا الملف يوثق نتائج يمكن إسنادها إلى أمر تشغيل أو ملف موجود. لا يعتبر وجود اختبار Playwright أو نجاح عملية build دليلًا على نجاح E2E حقيقي. البنود المحجوبة أو التي انتهت بـ`skipped` تبقى مفتوحة في [tasks.md](./tasks.md).

## Artifacts reviewed

- الخطة الحالية: [docs/employee-and-realtime-refresh-remediation-plan.md](../../docs/employee-and-realtime-refresh-remediation-plan.md).
- المواصفة: [spec.md](./spec.md).
- خطة التنفيذ: [plan.md](./plan.md).
- عقود الجلسة والأحداث وinvalidation: [contracts/](./contracts/).
- قائمة المهام: [tasks.md](./tasks.md).

## Verified implementation evidence

- [x] `backend/src/NaderGorge.Domain/Events/DataChangedEvent.cs` موجود ويعرّف envelope typed للأحداث؛ راجعه مع [contracts/staff-data-changed.md](./contracts/staff-data-changed.md).
- [x] `frontend/src/lib/cache-invalidation.ts` يدعم registrations متعددة مع cleanup مستقل وتجميع invalidation؛ اختبار contract موجود في [frontend/src/lib/query-contracts.test.ts](../../frontend/src/lib/query-contracts.test.ts).
- [x] عقود الموظفين/session والـhooks موجودة في [frontend/src/features/employee/](../../frontend/src/features/employee/)، [frontend/src/hooks/useCurrentSession.ts](../../frontend/src/hooks/useCurrentSession.ts)، و[frontend/src/services/auth-service.ts](../../frontend/src/services/auth-service.ts).
- [x] فحص `git diff --check` نجح.
- [x] `dotnet build backend/NaderGorge.sln --no-restore` نجح: 0 warnings و0 errors.
- [x] `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-build --logger 'console;verbosity=minimal'` نجح: 372 passed، 1 skipped، 0 failed. الـskip هو `RedisRateLimitingMiddlewareTests.Policy_ExceedingPermitLimit_Returns429`.
- [x] من داخل `frontend/`: `npm run typecheck` نجح.
- [x] من داخل `frontend/`: `npm run lint` نجح.
- [x] `node frontend/scripts/check-query-contracts.mjs` نجح: 27 service files و217 `apiClient` mutations.
- [x] `cd frontend && npm run check:platform-events` نجح: 61 producers و60 listeners.
- [x] `node frontend/scripts/check-no-unallowlisted-reloads.mjs` نجح؛ الاستثناء المسموح هو `frontend/src/components/video/SecureVideoPlayer.tsx`.
- [x] `docker compose config -q` نجح.
- [x] `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/160-employee-realtime-refresh/tasks.md` نجح.
- [x] `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/160-employee-realtime-refresh` نجح بعد تصحيح حالة المهام.

## Runtime results that remain open

## New runtime verification — 2026-07-13

- [x] تم تشغيل PostgreSQL وRedis عبر Docker، وتطبيق migrations من قاعدة اختبار نظيفة.
- [x] تم اكتشاف وإصلاح غياب جدول `PlatformSettings` بإضافة migration `20260712224208_EnsurePlatformSettingsTable`; بعدها أصبح `POST /api/e2e/seed` يرجع `200`.
- [x] اختبارات routing/queue/StudentContext/presence الحقيقية نجحت: `LiveSupportRoutingAcceptanceIntegrationTests` = `4/4 passed` باستخدام PostgreSQL وRedis حقيقيين.
- [x] تشغيل Chromium لملفات employee/realtime: `6 passed, 2 skipped`; اختبارات API/session الأساسية نجحت.
- [x] تشغيل `realtime-reconciliation.spec.ts` منفردًا على Chromium مع backend حقيقي: `4/4 passed`.
- [ ] تشغيل RF-R02 بجلسة admin A/B كاملة فشل في فتح `admin.lvh.me:3000/admin/users` بـ`ERR_ABORTED`؛ لم يُعتبر نجاحًا.
- [ ] تشغيل participant live-support UI كشف فشلًا حقيقيًا في history/draft/rating مع `401` أثناء session bootstrap؛ لا تُغلق LS-R01/LS-R02/LS-R03 runtime gate بهذا التشغيل.
- [ ] T119 ما زال مفتوحًا: لا توجد credentials/quota/provider callback حقيقي لتشغيل AI configured.

- التشغيل السابق بدون backend انتهى بـ7 حالات `skipped`، لكنه لم يعد وصفًا للحالة الحالية بعد تشغيل Docker؛ النتائج الأحدث موثقة أعلاه.
- اختبارات الجلسات الحقيقية A/B، صلاحيات الموظفين، conflict draft، reconnect، duplicate events، وsequence reconciliation لم تُثبت بتشغيل Chromium مع backend/PostgreSQL/Redis حقيقيين.
- اختبارات الرسائل بترتيب responses عكسي وعزل drafts لم تُشغّل بنتيجة runtime مثبتة.
- اختبارات queue/distribution متعددة الموظفين مع PostgreSQL وRedis حقيقيين لم تُشغّل.
- اختبارات malformed JSON وmissing event ID وout-of-order/sequence gap وreconnect الفعلي لم تُغلق.
- اختبارات close/transfer conflict وفقد ownership لم تُغلق.
- اختبارات StudentContext وlinked/unlinked وreplacement وtablet drill-in لم تُغلق.
- T119: قبول مزود AI الحقيقي غير منفذ؛ لا توجد credentials/quota/callback runtime evidence في هذه المراجعة.
- T122: release readiness غير منفذة؛ لا توجد Docker health/restart، Chromium/WebKit، أو multi-session runtime evidence كاملة.

## Task status corrections

أُعيدت البنود التالية إلى unchecked لأنها تعتمد على تشغيل runtime غير مثبت: T028، T043، T044، T060، T065، T071، T072، T082، T083، T084. لم يتم تغيير product code أو أي artifact خارج هذا المجلد.

## Not claimed

لا يثبت هذا السجل إغلاق بنود P0/P1/P2 في خطة remediation، ولا يضع checkbox على blocker أو على E2E انتهى بـ`skipped`.

## Final guard and verification pass — 2026-07-13

- [x] `clean-code-guard`: تمت مراجعة production diff؛ لا توجد findings blocking جديدة، وتم إصلاح detector/migration قبل إعادة الاختبارات.
- [x] `test-guard`: تمت مراجعة اختبارات xUnit وPlaywright وcontract؛ الاختبارات الجديدة تختبر حدودًا قابلة للملاحظة، وintegration queue يستخدم PostgreSQL/Redis حقيقيين.
- [x] `git diff --check` نجح.
- [x] `make verify` نجح: backend build، Application tests `373 passed / 1 skipped`، frontend lint/build، worker build، وDocker compose config.
- [x] backend integration السابق مع PostgreSQL/Redis مسجل كـ`4/4 passed` في بيئة سابقة؛ إعادة التشغيل الحالية حُجبت بغياب PostgreSQL وRedis.
- [x] `realtime-reconciliation.spec.ts` السابق على Chromium وbackend حقيقي مسجل كـ`4/4 passed`؛ لا يُعاد اعتماده كتشغيل حالي لأن Chromium/backend غير متاحين الآن.
- [x] employee/session Playwright runtime: `2 passed / 2 skipped`; الـskips تخص gated browser workflow أو fixture غير مكتمل، وليست نجاحًا للـA/B workflow.
- [ ] admin A/B browser workflow: فشل `admin.lvh.me:3000/admin/users` بـ`ERR_ABORTED`.
- [ ] participant live-support UI: فشل runtime في history/draft/rating بسبب `401` أثناء session bootstrap.
- [x] Open-items follow-up: live-support client contracts passed; frontend lint/typecheck/build passed; StudentContext focused tests passed 5/5; real routing acceptance tests were added for least-load/tie rotation, concurrent capacity, and disconnect redistribution.
- [x] Open-items follow-up: Admin A/B E2E now uses independent sessions, unique device fingerprints, bounded `ERR_ABORTED` retry, conflict/draft assertions, and explicit environment skips.
- [x] Open-items follow-up: linked/unlinked context, link replacement, action failure/success, tablet drill-in, ownership loss, 401 bootstrap, history/draft/rating/duplicate-action coverage added.
- [x] Open-items blockers recorded: PostgreSQL/Redis, real SignalR hub, Chromium/WebKit, E2E seed, and real AI provider credentials are unavailable in the current environment; no blocked runtime test is counted as passed.

## Latest verification override — 2026-07-13

- PostgreSQL/Redis are running locally and the integration suite was re-run successfully: `22/22 passed`.
- `PlatformSettings` was made migration-backed by `20260712224208_EnsurePlatformSettingsTable` so E2E seed now returns 200.
- Worker container readiness is healthy. T119 remains open because the real-provider harness safely stops on missing `AI_CALLBACK_SECRET`, `WORKER_URL`, and `GOOGLE_CLOUD_PROJECT`.
- Latest live-support Chromium focused run is `13 passed / 4 failed`; therefore participant retry/history runtime and release readiness are not marked complete.
- The previous claims in this file that PostgreSQL/Redis or Docker were unavailable are superseded by this section; external-gated SignalR/WebKit/T119 claims remain unverified.
