# Tasks: 026 — تحقق حقيقي من واتساب + بريفيو + تقويم

**Input**: `/specs/026-whatsapp-dob-ux/`
**Branch**: `026-whatsapp-dob-ux`
**Stack**: Backend (.NET 8) + Frontend (Next.js 15, TypeScript)

> **تحقق حقيقي** عبر Evolution API — backend proxy + frontend auto-check

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Backend Configuration)

**Purpose**: إضافة Evolution API configuration للـ backend

- [x] T001 إضافة `EvolutionApi` section في `backend/src/NaderGorge.API/appsettings.Development.json` بالقيم: `BaseUrl: "https://evo.n8n-mazen.online"`, `ApiKey: "Mazen.12"`, `InstanceName: "Nader"`
- [x] T002 [P] إضافة environment variables في `backend/src/NaderGorge.API/.env`: `EvolutionApi__BaseUrl`, `EvolutionApi__ApiKey`, `EvolutionApi__InstanceName`

---

## Phase 2: Foundational (Backend WhatsApp Service + Controller)

**Purpose**: إنشاء WhatsApp verification service و controller — يجب أن يكتملا قبل أي عمل frontend

**⚠️ CRITICAL**: الـ frontend يعتمد على هذا الـ endpoint

- [x] T003 إنشاء `backend/src/NaderGorge.Application/Services/WhatsAppVerificationService.cs` — class يستقبل `HttpClient` و `IConfiguration`، يقدم method واحدة `CheckWhatsAppAsync(string phoneNumber)` تُرسل POST لـ `{BaseUrl}/chat/whatsappNumbers/{InstanceName}` مع header `apikey` و body `{"numbers": ["20{phone}"}`، تُرجع `WhatsAppCheckResult` (record: `bool? Exists, string Number`)
- [x] T004 إنشاء `backend/src/NaderGorge.API/Controllers/WhatsAppController.cs` — controller واحد بـ endpoint `POST /api/whatsapp/check` يستقبل `{ phoneNumber: string }`، يتحقق من تنسيق الرقم (11 رقم يبدأ بـ 01)، يستدعي `WhatsAppVerificationService`، يُرجع `{ exists, number }` أو `503` عند فشل الاتصال
- [x] T005 تسجيل `WhatsAppVerificationService` + `HttpClient` في DI container في `backend/src/NaderGorge.API/Program.cs` — إضافة `builder.Services.AddHttpClient<WhatsAppVerificationService>()`
- [x] T006 اختبار يدوي: `dotnet build` ثم `curl -X POST http://localhost:5000/api/whatsapp/check -H "Content-Type: application/json" -d '{"phoneNumber":"01012345678"}'`

**Checkpoint**: ✅ Backend endpoint شغال — يُرجع `exists: true/false` لأي رقم

---

## Phase 3: User Story 1 — مؤشر واتساب أوتوماتيك حقيقي (Priority: P1) 🎯 MVP

**Goal**: عند إدخال رقم مصري مكتمل، يتحقق النظام تلقائيًا من واتساب عبر الـ backend ويعرض النتيجة

**Independent Test**: اكتب رقم واتساب حقيقي → "✓ الرقم على واتساب" (أخضر). اكتب رقم مش على واتساب → "✗ ليس على واتساب" (أحمر).

### Implementation for User Story 1

- [x] T007 [P] [US1] إنشاء `frontend/src/services/whatsapp-service.ts` — function `checkWhatsApp(phoneNumber: string): Promise<{exists: boolean | null, number: string}>` تستدعي `POST /api/whatsapp/check` عبر fetch
- [x] T008 [US1] إعادة كتابة `frontend/src/utils/whatsapp-utils.ts` — حذف `openWhatsAppVerification()` القديمة، إضافة: (1) `getPhoneFormatStatus(phone)` يتحقق من التنسيق محليًا، (2) `type WhatsAppStatus = 'idle' | 'checking' | 'verified' | 'not_found' | 'error'`، (3) hook `useWhatsAppCheck(phone: string)` يستخدم debounce 1s + يستدعي `checkWhatsApp` من الـ service
- [x] T009 [US1] إضافة مؤشر واتساب أسفل حقل `phoneNumber` في `frontend/src/components/forms/RegistrationForm.tsx` — يستخدم `useWhatsAppCheck(formData.phoneNumber)` ويعرض: spinner أثناء الفحص، badge أخضر "✓ الرقم على واتساب"، badge أحمر "✗ الرقم ليس على واتساب"، badge رمادي "تعذر التحقق" عند خطأ
- [x] T010 [US1] حذف زرار WhatsApp القديم (`reg-whatsapp-verify` + `openWhatsAppVerification`) من Step 3 (Security) في `frontend/src/components/forms/RegistrationForm.tsx` — لم يعد مطلوبًا بعد التحقق الأوتوماتيك
- [x] T011 [US1] حذف import `MessageCircle` من lucide-react في `frontend/src/components/forms/RegistrationForm.tsx` إذا لم يعد مستخدمًا في مكان آخر

**Checkpoint**: ✅ US1 مكتملة — تحقق واتساب حقيقي أوتوماتيك يعمل

---

## Phase 4: User Story 2 — عمر وعيد الميلاد في البريفيو (Priority: P2)

**Goal**: إزالة معلومات السن من جانب الحقل، وعرضها في البريفيو اليمنى فقط

**Independent Test**: اختر تاريخ ميلاد → لا شيء جنب الحقل → البريفيو يعرض "سنك دلوقتي: X سنة" + "باقي X يوم 🎂"

### Implementation for User Story 2

- [x] T012 [US2] حذف بلوك `{formData.dateOfBirth && (() => {...})()}` (حوالي 15 سطر يبدأ بـ `{formData.dateOfBirth && (() =>`) من منطقة حقل dateOfBirth في `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T013 [US2] تحديث `case 0` في `renderPreviewPanel()` في `frontend/src/components/forms/RegistrationForm.tsx` — استبدال بطاقة "تاريخ الميلاد" البسيطة ببطاقتين شرطيتين: (1) بطاقة السن: `computeBirthdayInfo(formData.dateOfBirth).ageYears` سنة (2) بطاقة الأيام: باقي `daysToNextBirthday` يوم 🎂 — إذا لم يُختر تاريخ تظهر placeholder

**Checkpoint**: ✅ US2 مكتملة — السن والأيام في البريفيو فقط

---

## Phase 5: User Story 3 — تقييد التقويم قبل 2020 (Priority: P3)

**Goal**: منع اختيار أي تاريخ >= 2020 في حقل تاريخ الميلاد

**Independent Test**: افتح التقويم → 2020+ معطل. اكتب "2020-01-01" يدويًا → خطأ.

### Implementation for User Story 3

- [x] T014 [US3] إضافة `max="2019-12-31"` على `<input type="date" name="dateOfBirth">` في `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T015 [US3] تحديث Zod schema في `frontend/src/components/forms/RegistrationForm.tsx` — إضافة `.refine(d => !d || new Date(d) <= new Date('2019-12-31'), 'يجب أن تكون مولودًا قبل عام 2020')` بعد `.min(1, '...')` على حقل `dateOfBirth`

**Checkpoint**: ✅ US3 مكتملة — التقويم مقيد على ما قبل 2020

---

## Phase 6: Polish & Cross-Cutting

- [x] T016 [P] التأكد من أن `dotnet build` ناجح بدون أخطاء في `backend/`
- [x] T017 [P] التأكد من أن `npx tsc --noEmit` بدون أخطاء جديدة في `frontend/`
- [x] T018 اختبار كامل لتدفق التسجيل حسب quickstart.md — التحقق من الـ 4 حالات (واتساب ✓ / ✗ / loading / error) + بريفيو (مع وبدون تاريخ) + تقييد التقويم

---

## Dependencies & Execution Order

### Phase Dependencies

- **T001–T002** (Setup): لا تبعيات — يبدأ فورًا
- **T003–T006** (Foundational Backend): يتبع T001 — **BLOCKS كل عمل الـ frontend**
- **T007–T011** (US1): بعد T006 (يحتاج الـ backend endpoint)
- **T012–T013** (US2): بعد T001 فقط — مستقل عن US1
- **T014–T015** (US3): بعد T001 فقط — مستقل عن US1 & US2
- **T016–T018** (Polish): بعد اكتمال كل الـ US

### User Story Dependencies

- **US1 (P1)**: T001 → T003–T006 → T007 → T008 → T009 → T010 → T011
- **US2 (P2)**: T012 → T013 — **مستقلة** (يمكن تنفيذها بالتوازي مع US1)
- **US3 (P3)**: T014 → T015 — **مستقلة** (يمكن تنفيذها بالتوازي مع US1)

### Parallel Opportunities

```bash
# بعد T006 (Backend ready)، يمكن تشغيل:
Task "T007 [US1] whatsapp-service.ts"
Task "T012 [US2] حذف birthday من الحقل"    # مختلف الملف
Task "T014 [US3] max=2019-12-31"           # نفس الملف لكن مكان مختلف

# T001 و T002 بالتوازي (ملفات مختلفة)
```

---

## Implementation Strategy

### MVP First (Backend + US1 فقط)

1. T001–T002 — Config
2. T003–T006 — Backend endpoint + test
3. T007–T011 — Frontend WhatsApp مؤشر
4. **STOP & VALIDATE**: اختبر التحقق الحقيقي

### Incremental Delivery

1. Backend + US1 → تحقق واتساب حقيقي ✅
2. US2 → نقل birthday للبريفيو ✅
3. US3 → تقييد التقويم ✅
4. Polish T016–T018

### الوقت الإجمالي المقدَّر

| المرحلة | الوقت |
|---|---|
| Setup (T001–T002) | ~5 دقائق |
| Backend (T003–T006) | ~30 دقيقة |
| US1 (T007–T011) | ~30 دقيقة |
| US2 (T012–T013) | ~15 دقيقة |
| US3 (T014–T015) | ~10 دقائق |
| Polish (T016–T018) | ~10 دقائق |
| **الإجمالي** | **~100 دقيقة** |

---

## Notes

- **API Key**: `Mazen.12` — مُختبر ويعمل ✅
- **Instance**: `Nader` على `evo.n8n-mazen.online`
- **الرقم يُرسل بصيغة دولية**: `01012345678` → `201012345678` (بدون +)
- **Backend يحمي الـ API key**: لا يُكشف في الـ frontend أبدًا
