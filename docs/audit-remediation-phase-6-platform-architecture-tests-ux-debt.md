# Phase 6: Platform Architecture Debt, Tests, Time Handling, and Deep UX Follow-Up

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
ينفذ بعد: Phase 4 و Phase 5  
الأولوية: متوسطة، لكنها مهمة لجاهزية production طويلة المدى

## الهدف

تجميع المتبقي الذي يحتاج عمل معماري أوسع: package centralization، timestamp migration، deployment rollback، CI warning enforcement، bundle analysis، full student shell redesign، واختبارات تكامل للـ critical workflows.

## البنود المغطاة

- P1-7: deploy rollback/backup وMakefile deploy hardening.
- P1-8/P1-9/P1-10: concurrency/role/balance integration tests والتعاملات المتبقية.
- P2-1: central package management.
- P2-4: CSP/security headers الأعمق.
- P2-13: CI يفشل على frontend warnings.
- P2-14: redesign أعمق للstudent shell وتقليل border-driven UI.
- P2-17: UTC DateTime migration.
- P3-2: bundle analyzer/lazy loading/dead UI components.
- P3-3: glossary centralization طويل المدى.

## Task 1: Central Package Management for .NET

### المشكلة

Phase 2 tests صارت clean في التشغيل الحالي، لكن dependency versions ما زالت موزعة على csproj files.

### التنفيذ

1. أضف `Directory.Packages.props`.
2. انقل EF Core, Npgsql, MediatR, FluentValidation, Microsoft.Extensions packages إلى central versions.
3. اجعل كل backend projects تستخدم `PackageReference Include` بدون `Version`.
4. شغل:
   - `dotnet restore backend/NaderGorge.sln`
   - `dotnet test backend/NaderGorge.sln --no-restore`
5. أضف CI step يفشل على restore warnings المهمة.

### قبول

- لا توجد package conflict warnings.
- versions موحدة ومقروءة من ملف واحد.

## Task 2: UTC Timestamp Migration

### المشكلة

`Npgsql.EnableLegacyTimestampBehavior` ما زال موجودا ويؤجل timezone correctness.

### التنفيذ

1. inventory لكل DateTime fields في domain/entities.
2. حدد الحقول:
   - UTC instant
   - local date فقط
   - local display فقط
3. Migration:
   - حول timestamps إلى UTC where needed.
   - استخدم `DateTimeOffset` أو UTC `DateTime` حسب convention واحد.
4. أزل:
   - `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`
5. Tests:
   - save/read UTC.
   - report dates display correctly.
   - exams/submissions deadlines unaffected.

### قبول

- legacy timestamp behavior غير موجود.
- tests تمر في timezone مختلفة.

## Task 3: Deployment Rollback and Makefile Safety

### المشكلة

Phase 2 حسنت deploy workflow، لكن rollback/backup وMakefile deploy ما زالا يحتاجان hardening.

### التنفيذ

1. في GitHub deploy:
   - backup DB قبل migration.
   - migration dry-run أو validation.
   - restart service.
   - health check.
   - rollback لو health فشل.
2. Makefile:
   - أزل `git add .` من deploy.
   - لا يعمل merge main تلقائي.
   - اجعل deploy dry-run by default.
   - يتطلب branch صريح وconfirmation env مثل `CONFIRM_DEPLOY=true`.
3. Tests/verification:
   - simulate failed health check.
   - verify workflow stops.

### قبول

- deploy لا يدفع ملفات غير مقصودة ولا يكمل بعد health failure.

## Task 4: Critical Integration and E2E Tests

### المشكلة

كثير من fixes تمت دون integration coverage كاف.

### التنفيذ

أضف tests للآتي:

1. Auth:
   - login
   - refresh
   - logout
   - reset password revokes sessions
2. Roles:
   - reject empty roles
   - reject unknown roles
   - reject removing last admin
   - reject self-demotion without another admin
3. Parent report:
   - no token rejected
   - valid token accepted
   - expired token rejected
   - revoked token rejected after Phase 5
4. AI jobs:
   - concurrent analyze requests create one DB lock.
   - cancel/retry requires admin/service auth.
5. Balance:
   - negative balance prevented.
   - concurrent adjustments preserve final amount.

### قبول

- CI fails if these critical workflows regress.

## Task 5: CSP and Global Security Headers Completion

### المشكلة

Phase 2 أضاف security headers أساسية، لكن CSP العامة لم تكتمل بسبب تعقيد providers/video.

### التنفيذ

1. Inventory external sources:
   - video providers
   - images
   - fonts
   - APIs
   - worker proxy
2. Add environment-specific CSP:
   - report-only first
   - enforce after testing
3. Add route-specific relaxations only where needed for embed route.
4. Add tests:
   - headers exist in production env.
   - embed still works.
   - admin/student pages still load.

### قبول

- CSP موجودة كدفاع عام وليس فقط في embed route.

## Task 6: Frontend Lint Warning Enforcement in CI

### المشكلة

Phase 3 جعل lint clean، لكن CI يجب أن يحافظ على ذلك.

### التنفيذ

1. عدل frontend lint CI command إلى:
   - `npm run lint -- --max-warnings=0`
2. أو أضف script:
   - `"lint:ci": "eslint --max-warnings=0"`
3. حدث workflows لاستخدام lint:ci.
4. وثق أن warnings الجديدة تفشل CI.

### قبول

- أي warning جديد يفشل CI.

## Task 7: Bundle Analyzer and Heavy UI Components

### المشكلة

Phase 3 راجع imports، لكنه لم يقس bundle فعليا.

### التنفيذ

1. أضف bundle analyzer للـ Next app.
2. قس routes:
   - `/student`
   - `/student/packages`
   - `/student/lessons/[id]`
   - `/login`
   - landing page
3. راجع مكونات:
   - `circular-gallery`
   - `feature-carousel`
   - `ripple-grid`
   - `resizable-navbar`
4. lazy load decorative components في public/landing فقط.
5. لا تدخل هذه المكونات إلى student first screens.

### قبول

- bundle report مرفق.
- student first route لا يحمل زخرفة ثقيلة غير مستخدمة.

## Task 8: Full Student Shell Redesign Follow-Up

### المشكلة

Phase 3 نظف lint/mock/video/copy وتحقق overflow، لكنه لم ينفذ redesign كامل للstudent shell.

### التنفيذ

1. افصل shell semantics:
   - student = next task, progress path, locked/unlocked.
   - admin = management, scanability, tables/actions.
2. صفحات student:
   - `/student`
   - `/student/packages`
   - `/student/lessons/[lessonId]`
   - `/student/exams/[examId]`
   - `/student/mistakes`
3. لكل صفحة:
   - first mobile viewport يحتوي action واضح.
   - لا nested cards.
   - لا borders كفاصل أساسي في كل مكان.
   - touch targets 44px+.
4. استخدم Playwright screenshots بعد backend seed أو mock API.

### قبول

- الطالب يرى "المهمة التالية" بدون scroll طويل.
- لا يوجد overlap/overflow في viewports الأساسية.

## Task 9: UX Glossary Centralization

### المشكلة

Phase 3 أزال old brand وبدل visible worker copy، لكن glossary لا يزال غير مركزي.

### التنفيذ

1. أنشئ file مثل `frontend/src/lib/ux-glossary.ts`.
2. ضع mappings:
   - queued = في الانتظار
   - processing = قيد المعالجة
   - completed = اكتمل
   - failed = فشل
   - AI analysis = تحليل الفيديو بالذكاء الاصطناعي
   - mindmaps = خرائط ذهنية
3. استخدمه في:
   - AI monitor
   - video processing components
   - admin job cards
4. لا تغير enum/API values، فقط display labels.

### قبول

- copy التقني متوحد في مكان واحد.
