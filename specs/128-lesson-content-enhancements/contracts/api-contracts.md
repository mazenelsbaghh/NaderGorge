# API Contracts: 128 — Lesson Content Enhancements

## New Endpoints

### PATCH `/api/admin/videos/{id:guid}/toggle-active`
**Auth**: Admin role required  
**Request**: No body  
**Response**:
```json
{ "success": true, "data": { "videoId": "guid", "isActive": true } }
```

---

### POST `/api/admin/resources/upload`
**Auth**: Admin role required  
**Request**: `multipart/form-data`
- `file` (IFormFile, required, max 10MB)
- Allowed MIME types: `application/pdf`, `image/*`, `application/msword`, `application/vnd.openxmlformats-officedocument.*`

**Response**:
```json
{ "success": true, "data": { "url": "/uploads/resources/{guid}_{filename}" } }
```

---

## Modified Endpoints

### GET `/api/admin/exams/{examId}/dashboard`
**Changes**: `ExamQuestionSummaryDto` now includes:
```json
{
  "examQuestionId": "guid",
  "text": "question text",
  "type": "MCQ",
  "points": 2.0,
  "totalAttempts": 45,
  "correctCount": 30,
  "wrongCount": 15,
  "correctPercentage": 66.67
}
```

### PUT `/api/admin/videos/{id:guid}`
**Changes**: Response DTO includes `isActive` field.

---

## Frontend Service Methods

```typescript
// New methods in admin-service.ts
toggleVideoActive(videoId: string): Promise<any>
uploadResourceFile(file: File): Promise<{ url: string }>
```
