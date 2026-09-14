# Teacher Management API Contract

All routes are under `/api/admin` and require the existing Admin `users.manage` permission. Response shape follows the existing `ApiResponse<T>` envelope.

## Read teacher

`GET /teachers` and `GET /teachers/{teacherId}` return the existing teacher DTO plus:

```json
{
  "isVisibleToStudents": true,
  "isContentVisibleToStudents": true,
  "isActive": true
}
```

No password hash, password reset token, or secret credential is returned.

## Update teacher

`PUT /teachers/{teacherId}` accepts the existing profile fields plus linked account and visibility fields:

```json
{
  "fullName": "string",
  "phoneNumber": "string",
  "newPassword": "string|null",
  "bio": "string",
  "specialization": "string",
  "commissionRate": 0,
  "profileImageUrl": "string|null",
  "contactInfo": "string",
  "subjectIds": ["uuid"],
  "assistantPhoneNumbers": "string|null",
  "facebookUrl": "string|null",
  "youtubeUrl": "string|null",
  "telegramUrl": "string|null",
  "showOnLanding": true,
  "isVisibleToStudents": true,
  "isContentVisibleToStudents": true
}
```

`newPassword` is optional and write-only. Invalid or duplicate identity data returns a validation/business error and makes no partial update.

## Public behavior

- `GET /api/public/teachers`, `/landing`, `/{slugOrId}`, and teacher community routes exclude hidden teachers.
- Student/public content and direct protected access exclude/deny teacher-owned content when `isContentVisibleToStudents=false`.
- Hidden content returns the existing not-found/forbidden style used by the endpoint; it must not disclose that a hidden record exists.
