# API Contract: Content Hierarchy

## POST /api/admin/packages/{packageId}/terms

Create a new term within a package.

### Request Body

```json
{
  "title": "الترم الأول",
  "order": 1
}
```

### Success Response (201)

```json
{
  "id": "uuid",
  "title": "الترم الأول",
  "order": 1,
  "packageId": "uuid",
  "sectionsCount": 0
}
```

---

## GET /api/packages/{packageId}/terms

List all terms in a package.

### Success Response (200)

```json
{
  "terms": [
    {
      "id": "uuid",
      "title": "الترم الأول",
      "order": 1,
      "sectionsCount": 5
    },
    {
      "id": "uuid",
      "title": "الترم التاني",
      "order": 2,
      "sectionsCount": 4
    }
  ]
}
```

---

## PUT /api/admin/terms/{termId}

Update term title or order.

### Request Body

```json
{
  "title": "الترم الأول - مُحدث",
  "order": 1
}
```

---

## DELETE /api/admin/terms/{termId}

Delete a term (only if it has no sections).

---

## POST /api/admin/terms/{termId}/sections

Create a content section within a term (replaces direct package-section relationship).

### Request Body

```json
{
  "title": "شهر أكتوبر",
  "order": 1
}
```

---

## GET /api/student/dashboard/quick-access

Returns direct-access shortcuts for the student's purchased content.

### Success Response (200)

```json
{
  "quickAccess": [
    {
      "type": "Lesson",
      "id": "uuid",
      "title": "الحصة الرابعة - المعادلات",
      "parentPath": "الباكدج > الترم الأول > شهر أكتوبر",
      "url": "/student/packages/{id}/terms/{id}/sections/{id}/lessons/{id}",
      "thumbnailUrl": "..."
    },
    {
      "type": "Month",
      "id": "uuid",
      "title": "شهر نوفمبر",
      "parentPath": "الباكدج > الترم الأول",
      "url": "/student/packages/{id}/terms/{id}/sections/{id}",
      "lessonsCount": 8,
      "completedCount": 3
    }
  ]
}
```

---

## GET /api/student/balance

Get student's current balance.

### Success Response (200)

```json
{
  "currentBalance": 150.00,
  "recentTransactions": [
    {
      "id": "uuid",
      "amount": 100.00,
      "balanceAfter": 150.00,
      "type": "CodeRedemption",
      "description": "Code redeemed: ABC123",
      "createdAt": "2026-03-27T10:00:00Z"
    }
  ]
}
```

---

## POST /api/student/purchase

Purchase content using balance.

### Request Body

```json
{
  "contentType": "Lesson",
  "contentId": "uuid"
}
```

### Success Response (200)

```json
{
  "success": true,
  "newBalance": 50.00,
  "grantedContent": {
    "type": "Lesson",
    "id": "uuid",
    "name": "الحصة الرابعة",
    "redirectUrl": "/student/packages/{id}/terms/{id}/sections/{id}/lessons/{id}"
  }
}
```

### Error Responses

| Code | Condition |
|------|-----------|
| 400 | Insufficient balance |
| 400 | Already have access |
| 404 | Content not found |
