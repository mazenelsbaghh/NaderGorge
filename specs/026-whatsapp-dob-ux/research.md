# Research Notes: 026-whatsapp-dob-ux

## RQ-1: تحقق واتساب — الحل التقني

### القرار
**الحل المختار**: Evolution API عبر Backend Proxy

### التفاصيل
المستخدم لديه Evolution API instance جاهز ومتصل:
- **URL**: `https://evo.n8n-mazen.online`
- **Instance**: `Nader`
- **API Key**: `Mazen.12`
- **Endpoint**: `POST /chat/whatsappNumbers/{instance}`

### اختبار التحقق (2026-03-31)
```bash
curl -s -X POST "https://evo.n8n-mazen.online/chat/whatsappNumbers/Nader" \
  -H "Content-Type: application/json" \
  -H "apikey: Mazen.12" \
  -d '{"numbers": ["201012345678"]}'

# Response: [{"jid":"201012345678@s.whatsapp.net","exists":false,"number":"201012345678"}]
```
✅ **الـ API شغال 100%**

### المسار المعماري
```
Frontend (debounce 1s)
  → POST /api/whatsapp/check { phoneNumber: "01012345678" }
  → .NET Controller (validates format)
  → WhatsAppVerificationService (builds request)
  → POST evo.n8n-mazen.online/chat/whatsappNumbers/Nader
  → Response: { exists: true/false }
  → Frontend badge: ✓ / ✗ / تعذر
```

### البدائل المدروسة

| البديل | لماذا رُفض |
|---|---|
| Format check فقط | لا يتحقق فعليًا — المستخدم يريد تحقق حقيقي |
| Frontend مباشرة لـ Evolution | يكشف الـ API key في browser — أمنيًا مرفوض |
| wa.me redirect | CORS يمنعه + غير دقيق |

---

## RQ-2: تقييد تاريخ الميلاد

### القرار: `max="2019-12-31"` + Zod refine
لم يتغير عن الخطة الأصلية.

---

## RQ-3: موقع عرض السن

### القرار: البريفيو فقط
لم يتغير عن الخطة الأصلية.
