# Phase 4: Auth, Session, Secrets, and Server-Side Input Hardening

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
ينفذ بعد: Phase 1, Phase 2, Phase 3  
الأولوية: عالية جدا

## الهدف

إغلاق المخاطر المتبقية في identity/session/secrets/XSS التي لم تكن مناسبة للتنفيذ السريع في أول 3 phases لأنها تحتاج تعديل auth model وDB/tests.

## البنود المغطاة

- P0-1: default seeding المتبقي: one-time admin setup + tests + rotation checklist.
- P0-4: server-side sanitization عند حفظ الأسئلة والإجابات.
- P1-2: إزالة/تدوير tracked secrets وإضافة secret scanning.
- P1-3: نقل refresh token من browser storage إلى HttpOnly cookie.
- P1-4: reset password one-time server-side token.
- P1-5: عدم تسجيل E2E controller إلا في E2E profile آمن.

## Task 1: First Admin Setup بدل default credentials

### المشكلة

Phase 1 قيد seeding الافتراضي، لكنه لم يبن آلية production آمنة لإنشاء أول admin.

### التنفيذ

1. في backend، أضف إعداد:
   - `FirstAdminSetup:Enabled`
   - `FirstAdminSetup:SetupTokenHash`
   - `FirstAdminSetup:ExpiresAtUtc`
2. أنشئ endpoint داخلي أو command آمن لإنشاء أول admin فقط إذا:
   - لا يوجد admin حالي.
   - setup token صحيح.
   - token غير منتهي.
   - البيئة Production/Docker لا تسمح بأي default password.
3. لا تخزن setup token plaintext.
4. أضف audit log عند إنشاء أول admin.
5. أضف tests:
   - يرفض إنشاء admin إذا يوجد admin.
   - يرفض token خاطئ.
   - يرفض token منتهي.
   - لا ينشئ default admin خارج Development/E2e.

### قبول

- production database جديدة لا تنشئ admin معروف تلقائيا.
- أول admin يتم إنشاؤه فقط بتوكن one-time.

## Task 2: Secret Rotation and Secret Scanning

### المشكلة

Phase 1/2 شددت startup validation، لكن أي أسرار كانت tracked يجب تدويرها خارج الكود.

### التنفيذ

1. أنشئ `appsettings.Development.example.json` بدون أسرار.
2. انقل secrets الحقيقية إلى user secrets أو env فقط.
3. راجع:
   - `backend/src/NaderGorge.API/appsettings.Development.json`
   - `backend/src/NaderGorge.API/appsettings.E2e.json`
   - `.env.example`
   - `worker/.env.example`
   - `docker-compose.yml`
4. أضف GitHub Actions secret scanning step مثل `gitleaks` أو بديل موثوق.
5. وثق rotation checklist:
   - JWT secret
   - Evolution API key
   - callback secrets
   - parent report signing secret
   - worker admin token
6. لا تضع أي secret حقيقي في docs.

### قبول

- secret scanner ينجح في CI.
- ملفات example تحتوي placeholders فقط.

## Task 3: HttpOnly Refresh Token Migration

### المشكلة

الـ refresh token ما زال في `localStorage/sessionStorage`. Phase 1 قلل أثر XSS، لكنه لم يغير session model.

### التنفيذ

1. Backend:
   - عند login/refresh، أرسل refresh token في cookie:
     - `HttpOnly`
     - `Secure` في production
     - `SameSite=Lax` أو `Strict` حسب cross-site needs
     - path مناسب مثل `/api/auth`
   - لا ترجع refresh token في JSON للfrontend.
   - أضف endpoint logout يمسح cookie ويعمل revoke.
2. Frontend:
   - عدل `frontend/src/lib/auth-storage.ts` ليحفظ access token فقط أو user فقط حسب التصميم.
   - عدل `frontend/src/services/api-client.ts` ليستخدم `withCredentials`.
   - عند refresh، يعتمد على cookie لا browser storage.
3. Security:
   - أضف CSP مبدئية في frontend/backend headers تقلل XSS blast radius.
4. Tests:
   - login sets cookie.
   - refresh works without refresh token in localStorage.
   - logout clears cookie.
   - reset password revokes old refresh cookies.

### قبول

- `localStorage` لا يحتوي refresh token.
- refresh flow ما زال يعمل.

## Task 4: One-Time Password Reset Token

### المشكلة

reset password ما زال يعتمد على JWT stateless للreset token. Phase 1 أضاف revoke للrefresh tokens بعد reset، لكن token نفسه ليس one-time server-side.

### التنفيذ

1. أضف entity مثل `PasswordResetToken`:
   - `Id`
   - `UserId`
   - `TokenHash`
   - `ExpiresAtUtc`
   - `ConsumedAtUtc`
   - `AttemptCount`
   - `CreatedIp`
2. `VerifyResetFieldsCommand` ينشئ token عشوائي قوي ويخزن hash فقط.
3. `ResetPasswordCommand`:
   - يبحث عن hash.
   - يرفض expired/consumed.
   - يستهلك token داخل transaction.
   - يرفض المحاولات الزائدة.
   - يغير password ويرفض refresh tokens.
4. أضف rate limit per phone/account.
5. لاحقا يمكن إرسال reset عبر WhatsApp/SMS بدلا من إرجاعه مباشرة.

### قبول

- نفس reset token لا يعمل مرتين.
- token غير موجود plaintext في DB.

## Task 5: Server-Side Rich Text Sanitization

### المشكلة

Phase 1 أضاف client sanitizer، لكن الإدخال الخطر يجب أن ينظف أو يرفض عند الحفظ أيضا.

### التنفيذ

1. حدد الحقول التي تقبل rich HTML:
   - question text
   - options text
   - answer/correction rich text
   - mistake pages inputs لو موجودة
2. أضف service في backend مثل `IRichTextSanitizer`.
3. استخدم allowlist:
   - `p`, `br`, `strong`, `b`, `em`, `i`, `u`, `ul`, `ol`, `li`, `span`
   - attributes محدودة جدا، بدون `on*`, بدون `javascript:`, بدون `style` إلا لو allowlist مدروسة.
4. طبقه داخل commands التي تحفظ الأسئلة/الواجبات/الامتحانات.
5. أضف tests payloads:
   - `<img onerror=alert(1)>`
   - `<a href="javascript:alert(1)">`
   - `<script>alert(1)</script>`
   - encoded variants.

### قبول

- أي HTML خطر ينظف أو يرفض قبل DB.
- client sanitizer يبقى دفاع إضافي فقط.

## Task 6: E2E Controller Registration Guard

### المشكلة

Phase 1 أضاف token، لكن controller المدمر لا يجب أن يسجل أصلا في غير E2E.

### التنفيذ

1. اجعل E2E endpoints تسجل فقط إذا:
   - `ASPNETCORE_ENVIRONMENT == E2e`
   - و `E2E_TEST_TOKEN` موجود وقوي.
2. افصل controller في assembly/route mapping conditional لو أمكن.
3. `EnsureDeleted` لا يعمل إلا إذا connection string يحتوي test DB marker مثل `_e2e` أو env `E2E_ALLOW_DB_RESET=true`.
4. أضف integration/startup tests.

### قبول

- production/staging لا تحتوي `/api/e2e/*`.
- E2E يعمل في pipeline فقط مع token.
