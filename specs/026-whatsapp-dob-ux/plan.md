# Implementation Plan: 026-whatsapp-dob-ux
**تحقق حقيقي من واتساب عبر Evolution API + عمر في البريفيو + تقييد التقويم**

**Branch**: `026-whatsapp-dob-ux`
**Spec**: [spec.md](./spec.md)
**Research**: [research.md](./research.md)

---

## Constitution Check

| المبدأ | الحكم | الملاحظة |
|---|---|---|
| I. Clean Architecture | ✅ | Backend: Controller → Service → External API (مفصول بالكامل) |
| II. Provider Abstraction | ✅ | WhatsApp service خلف interface — يمكن تبديل Evolution API لاحقًا |
| III. Security by Default | ✅ | API key في env variables، proxy عبر backend فقط |
| VI. Single-Flow Registration | ✅ | يُحسِّن الـ UX بدون كسر الـ flow |
| VII. Observability | ✅ | Structured logging على الـ endpoint الجديد |
| VIII. Premium Design System | ✅ | مؤشرات بصرية تتبع نظام الألوان |

---

## Technical Context

### Evolution API Integration

```
Endpoint: POST https://evo.n8n-mazen.online/chat/whatsappNumbers/Nader
Headers:  Content-Type: application/json
          apikey: Mazen.12
Body:     { "numbers": ["201012345678"] }
Response: [{ "exists": true, "jid": "...@s.whatsapp.net", "number": "..." }]
```

**تم التحقق من الـ API**: ✅ شغال — اختبار `curl` ناجح بتاريخ 2026-03-31.

### الملفات المتأثرة

```
backend/src/NaderGorge.API/
├── appsettings.Development.json          ← إضافة EvolutionApi section
├── .env                                  ← إضافة Evolution env vars
├── Controllers/
│   └── WhatsAppController.cs             ← 🆕 Controller جديد
├── Configuration/
│   └── (DI registration)                 ← تسجيل الـ Service
└── Program.cs                            ← تسجيل HttpClient

backend/src/NaderGorge.Application/
└── Services/
    └── WhatsAppVerificationService.cs    ← 🆕 Service جديد

frontend/src/
├── utils/
│   └── whatsapp-utils.ts                ← إعادة كتابة — API call بدل wa.me
├── services/
│   └── whatsapp-service.ts              ← 🆕 API client
└── components/forms/
    └── RegistrationForm.tsx       ← 3 مناطق تعديل:
        ├── [1] phoneNumber field  → إضافة مؤشر حالة واتساب
        ├── [2] dateOfBirth field  → حذف birthday info
        └── [3] renderPreviewPanel → إضافة birthday cards
```

---

## Phase 0: Research ✅

### القرار: Evolution API عبر Backend Proxy

| الجانب | القرار |
|---|---|
| **التحقق** | حقيقي 100% عبر Evolution API |
| **الأمان** | API key في backend فقط (لا يُكشف في browser) |
| **المسار** | Frontend → `.NET Controller` → Evolution API → Response |
| **Rate Limiting** | لا حاجة حاليًا (debounce 1sec كافي) |
| **Fallback** | لو Evolution API fail → مؤشر "تعذر التحقق" + يتابع الطالب |

---

## Phase 1: Design & Contracts

### Backend Endpoint Contract

```
POST /api/whatsapp/check
Body:    { "phoneNumber": "01012345678" }
Headers: (no auth required - public endpoint for registration)

Response 200:
{
  "exists": true,
  "number": "201012345678"
}

Response 200 (not found):
{
  "exists": false,
  "number": "201012345678"
}

Response 503 (Evolution API down):
{
  "exists": null,
  "error": "WhatsApp verification service unavailable"
}
```

### Frontend Flow

```
User types phone → debounce 1s → format check (11 digits, 01X) →
  → POST /api/whatsapp/check → show result badge
  → on error → show neutral badge "تعذر التحقق"
```

### Configuration Model

```json
// appsettings.Development.json
{
  "EvolutionApi": {
    "BaseUrl": "https://evo.n8n-mazen.online",
    "ApiKey": "Mazen.12",
    "InstanceName": "Nader"
  }
}
```

---

## Phase 2: Implementation Tasks

### ملخص المهام

| # | المهمة | الطبقة | الملف |
|---|---|---|---|
| T1 | Config | Backend | `appsettings.Development.json` + `.env` |
| T2 | WhatsApp Service | Backend | `WhatsAppVerificationService.cs` |
| T3 | WhatsApp Controller | Backend | `WhatsAppController.cs` |
| T4 | DI + HttpClient | Backend | `Program.cs` |
| T5 | Frontend API client | Frontend | `whatsapp-service.ts` |
| T6 | Rewrite whatsapp-utils | Frontend | `whatsapp-utils.ts` |
| T7 | مؤشر واتساب في الفورم | Frontend | `RegistrationForm.tsx` |
| T8 | حذف birthday من الحقل | Frontend | `RegistrationForm.tsx` |
| T9 | إضافة birthday في البريفيو | Frontend | `RegistrationForm.tsx` |
| T10 | تقييد التقويم + Zod | Frontend | `RegistrationForm.tsx` |

---

## Quickstart — التحقق بعد التنفيذ

```bash
# 1. شغّل الـ backend
cd backend && dotnet run --project src/NaderGorge.API

# 2. اختبر الـ endpoint مباشرة
curl -X POST http://localhost:5000/api/whatsapp/check \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"01012345678"}'
# المتوقع: {"exists": true/false, "number": "201012345678"}

# 3. شغّل الـ frontend
cd frontend && npm run dev

# 4. افتح http://localhost:3000/register
# - اكتب رقم واتساب حقيقي → ✓ أخضر
# - اكتب رقم مش على واتساب → ✗ أحمر
# - اختر تاريخ ميلاد → السن في البريفيو مش جنب الحقل
# - حاول اختيار 2020+ → ممنوع
```
