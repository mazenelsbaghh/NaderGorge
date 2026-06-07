# API Contracts: Form ID Mismatch Fix

## 1. PUT /api/admin/forms/{id}
Updates details of an existing custom form.

### Request Body JSON
```json
{
  "id": "126af52c-5839-4f96-9b2f-fb25b88f6c74",
  "title": "Example Form Title",
  "description": "Example Description",
  "slug": "example-slug",
  "isActive": true,
  "coverImageUrl": "/uploads/form-covers/some-image.png",
  "startsAt": "2026-06-07T12:00:00.000Z",
  "expiresAt": "2026-06-14T12:00:00.000Z",
  "fieldsJson": "[]"
}
```

### Response
- **200 OK**: On success
- **400 Bad Request**: On mismatch between URL `id` and payload `id`, validation errors, or malformed JSON.

---

## 2. PUT /api/admin/forms/submissions/{submissionId}/status
Updates the workflow status and admin review notes of a form submission.

### Request Body JSON
```json
{
  "submissionId": "3b2e7c9f-d31e-42c2-87a2-f94d284a1ef1",
  "status": "Reviewed",
  "adminNotes": "Approved for interview"
}
```

### Response
- **200 OK**: On success
- **400 Bad Request**: On mismatch between URL `submissionId` and payload `submissionId` or validation errors.
