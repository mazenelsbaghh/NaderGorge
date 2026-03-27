# API Contract: Code System

## POST /api/admin/code-groups

Create a new code group with a specific type.

### Request Body

```json
{
  "name": "Term 1 Access Codes - Batch 3",
  "codeType": "Term",
  "totalCodes": 50,
  "targetId": "uuid-of-term",
  "discountPercentage": null,
  "balanceAmount": null,
  "expiresAt": "2026-06-30T23:59:59Z",
  "videoTargetIds": null
}
```

### Field Rules by CodeType

| CodeType | targetId points to | videoTargetIds | balanceAmount |
|----------|-------------------|----------------|---------------|
| Package | Package.Id | null | null |
| Term | Term.Id | null | null |
| Month | ContentSection.Id | null | null |
| Lesson | Lesson.Id | null | null |
| Video | null | [LessonVideo.Id, ...] | null |
| Exam | Exam.Id | null | null |
| Balance | null | null | decimal > 0 |

### Success Response (201)

```json
{
  "id": "uuid",
  "name": "Term 1 Access Codes - Batch 3",
  "codeType": "Term",
  "totalCodes": 50,
  "codesGenerated": 50,
  "qrGenerated": false,
  "expiresAt": "2026-06-30T23:59:59Z"
}
```

---

## POST /api/admin/code-groups/{id}/generate-qr

Generate QR codes for all codes in a group.

### Success Response (200)

```json
{
  "codeGroupId": "uuid",
  "totalQrGenerated": 50,
  "downloadUrl": "/api/admin/code-groups/{id}/qr-download"
}
```

---

## GET /api/admin/code-groups/{id}/qr-download

Download ZIP of all QR code images for a code group.

### Response

Binary ZIP file containing PNG images named `{code-plaintext}.png`.

---

## PUT /api/admin/codes/{id}

Modify an existing code.

### Request Body

```json
{
  "expiresAt": "2026-12-31T23:59:59Z",
  "isRevoked": false
}
```

---

## POST /api/codes/redeem

Redeem a code (manual entry).

### Request Body

```json
{
  "code": "ABC123XYZ"
}
```

### Success Response (200)

```json
{
  "success": true,
  "grantType": "Term",
  "grantedContent": {
    "type": "Term",
    "id": "uuid",
    "name": "الترم الأول",
    "redirectUrl": "/student/packages/{packageId}/terms/{termId}"
  },
  "balanceAdded": null
}
```

For balance codes:

```json
{
  "success": true,
  "grantType": "Balance",
  "grantedContent": null,
  "balanceAdded": 150.00,
  "newBalance": 300.00
}
```

### Error Responses

| Code | Condition |
|------|-----------|
| 400 | Code invalid or not found |
| 400 | Code already consumed |
| 400 | Code expired |
| 400 | Already have access to this content |

---

## GET /api/qr/{codeHash}

QR auto-redeem endpoint. Called when student scans a QR code URL.

### Behavior

1. Validates the code hash
2. If user is authenticated → auto-redeem and redirect to content
3. If user is not authenticated → redirect to login with `returnUrl` containing the QR redemption

### Success (302 Redirect)

Redirects to the unlocked content page.

### Error (302 Redirect)

Redirects to error page with message (code expired, already used, etc.).
