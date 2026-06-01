# API Contracts: Package Code Page Profile

## Overview

These contracts cover package-specific code page profile management for admins and package-specific code page reads for student-facing rendering.

## 1. Get Admin Package Code Profile

**Endpoint**: `GET /api/admin/packages/{packageId}/code-profile`

**Role**: Admin

**Purpose**: Load the package-scoped code page profile editor state, including whether the package currently uses fallback content.

**Response 200**

```json
{
  "data": {
    "packageId": "6dce4d7d-5989-4e84-a7c1-0f9c5f0fe0e5",
    "packageName": "باقة الفيزياء المكثفة",
    "status": "Draft",
    "isUsingFallback": true,
    "heroEyebrow": "ابدأ من هنا",
    "heroTitle": "فعّل باقة الفيزياء المكثفة",
    "heroDescription": "ابدأ رحلتك داخل الباقة من نفس الصفحة وبهوية واضحة.",
    "offerTitle": "ماذا ستحصل بعد التفعيل؟",
    "offerDescription": "الوصول المباشر إلى المحتوى الخاص بهذه الباقة.",
    "activationTitle": "أدخل الكود الخاص بالباقة",
    "activationDescription": "اكتب الكود كما وصلك لتفعيل الوصول فورًا.",
    "supportTitle": "تحتاج مساعدة؟",
    "supportDescription": "تواصل مع الإدارة إذا واجهت مشكلة في التفعيل.",
    "themeAccentKey": "physics-gold",
    "publishedAt": null,
    "lastUpdatedAt": "2026-04-08T12:00:00Z"
  },
  "isSuccess": true,
  "message": ""
}
```

## 2. Save Admin Package Code Profile

**Endpoint**: `PUT /api/admin/packages/{packageId}/code-profile`

**Role**: Admin

**Purpose**: Create or update the package profile draft/published content.

**Request**

```json
{
  "status": "Published",
  "heroEyebrow": "ابدأ من هنا",
  "heroTitle": "فعّل باقة الفيزياء المكثفة",
  "heroDescription": "كل ما تحتاجه لبدء الدراسة موجود في هذه الصفحة.",
  "offerTitle": "بعد التفعيل",
  "offerDescription": "تظهر لك الباقة مباشرة داخل حسابك.",
  "activationTitle": "أدخل كود الباقة",
  "activationDescription": "يتم التحقق من الكود فورًا وربط الوصول بحسابك.",
  "supportTitle": "مساعدة سريعة",
  "supportDescription": "في حال وجود مشكلة، راجع الإدارة.",
  "themeAccentKey": "physics-gold"
}
```

**Response 200**

```json
{
  "data": {
    "packageId": "6dce4d7d-5989-4e84-a7c1-0f9c5f0fe0e5",
    "status": "Published",
    "publishedAt": "2026-04-08T12:05:00Z"
  },
  "isSuccess": true,
  "message": "تم حفظ بروفايل صفحة الكود بنجاح"
}
```

**Validation failures**

`400 Bad Request`

```json
{
  "isSuccess": false,
  "message": "لا يمكن نشر بروفايل غير مكتمل",
  "errors": {
    "heroTitle": ["العنوان الرئيسي مطلوب عند النشر"],
    "activationDescription": ["وصف التفعيل مطلوب عند النشر"]
  }
}
```

## 3. Reset Package Code Profile

**Endpoint**: `DELETE /api/admin/packages/{packageId}/code-profile`

**Role**: Admin

**Purpose**: Clear the active custom profile and return the package to default code page behavior.

**Response 200**

```json
{
  "isSuccess": true,
  "message": "تمت إعادة الصفحة إلى الوضع الافتراضي"
}
```

## 4. Get Student Package Code Page

**Endpoint**: `GET /api/content/packages/{packageId}/code-page`

**Role**: Authenticated student

**Purpose**: Return the resolved student-facing code page model for a package, using published custom content when present and the fallback profile otherwise.

**Response 200**

```json
{
  "data": {
    "packageId": "6dce4d7d-5989-4e84-a7c1-0f9c5f0fe0e5",
    "packageName": "باقة الفيزياء المكثفة",
    "packageDescription": "شرح ومراجعات وتدريبات",
    "packagePrice": 500,
    "isPackageActive": true,
    "isUsingCustomProfile": true,
    "hero": {
      "eyebrow": "ابدأ من هنا",
      "title": "فعّل باقة الفيزياء المكثفة",
      "description": "كل ما تحتاجه لبدء الدراسة موجود في هذه الصفحة."
    },
    "offerPanel": {
      "title": "بعد التفعيل",
      "description": "تظهر لك الباقة مباشرة داخل حسابك."
    },
    "activationPanel": {
      "title": "أدخل كود الباقة",
      "description": "يتم التحقق من الكود فورًا وربط الوصول بحسابك."
    },
    "supportPanel": {
      "title": "مساعدة سريعة",
      "description": "في حال وجود مشكلة، راجع الإدارة."
    },
    "themeAccentKey": "physics-gold"
  },
  "isSuccess": true,
  "message": ""
}
```

## Frontend Route Contract

The student UI should expose a package-specific route:

`/student/code-redemption/packages/{packageId}`

Expected behavior:
- fetch `GET /api/content/packages/{packageId}/code-page`
- render package-specific branding if `isUsingCustomProfile = true`
- otherwise render the generic fallback composition with package context
- submit activation through the existing code activation endpoint
