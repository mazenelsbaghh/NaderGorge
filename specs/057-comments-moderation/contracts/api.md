# API Contracts

## Student Lesson Comment APIs

### `GET /api/content/lessons/{lessonId}/comments`
Returns the approved comments that can be shown beneath the lesson video area.

- **Authorization**: Authenticated user with access to the lesson
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "4e3dd65a-07a6-4f09-a1d8-a35b93b0f118",
      "lessonId": "0f93e5e7-95a2-4eea-82db-0ee038771c84",
      "authorName": "Ahmed Ali",
      "body": "هل الجزء ده مهم في الامتحان؟",
      "status": "Approved",
      "createdAt": "2026-04-08T16:30:00Z",
      "isOwnComment": false
    }
  ]
}
```

### `POST /api/content/lessons/{lessonId}/comments`
Creates a new lesson comment in `Pending` status.

- **Authorization**: Authenticated student with access to the lesson
- **Request**:

```json
{
  "body": "ممكن شرح النقطة الخاصة بالتمرين الأخير؟"
}
```

- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "252263a8-22ce-4cb5-8a4e-4f87d8a93984",
    "status": "Pending",
    "createdAt": "2026-04-08T16:32:00Z",
    "message": "تم إرسال التعليق وهو الآن في انتظار المراجعة."
  }
}
```

### `GET /api/content/lessons/{lessonId}/comments/mine`
Returns the current student's submitted comments for the lesson with their moderation status.

- **Authorization**: Authenticated student with access to the lesson
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "252263a8-22ce-4cb5-8a4e-4f87d8a93984",
      "lessonId": "0f93e5e7-95a2-4eea-82db-0ee038771c84",
      "authorName": "Ahmed Ali",
      "body": "ممكن شرح النقطة الخاصة بالتمرين الأخير؟",
      "status": "Pending",
      "createdAt": "2026-04-08T16:32:00Z",
      "isOwnComment": true
    }
  ]
}
```

## Teacher/Admin Moderation APIs

### `GET /api/admin/lessons/{lessonId}/comments`
Lists lesson comments for the moderation surface in the lesson cockpit, optionally filtered by status.

- **Authorization**: `Admin` or `Teacher`
- **Query Parameters**:
  - `status` (optional): `Pending`, `Approved`, or `Rejected`
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "252263a8-22ce-4cb5-8a4e-4f87d8a93984",
      "lessonId": "0f93e5e7-95a2-4eea-82db-0ee038771c84",
      "lessonTitle": "الحركة الدائرية",
      "studentId": "8fdd5d88-c7a7-44b1-9314-98f3f15fdf87",
      "studentName": "Ahmed Ali",
      "body": "ممكن شرح النقطة الخاصة بالتمرين الأخير؟",
      "status": "Pending",
      "createdAt": "2026-04-08T16:32:00Z",
      "reviewedAt": null,
      "reviewedByName": null
    }
  ]
}
```

### `POST /api/admin/comments/{commentId}/approve`
Approves a pending lesson comment and makes it publicly visible.

- **Authorization**: `Admin` or `Teacher`
- **Request**: `{}`
- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "252263a8-22ce-4cb5-8a4e-4f87d8a93984",
    "status": "Approved",
    "reviewedAt": "2026-04-08T16:40:00Z",
    "reviewedByUserId": "f7acc366-f5ad-4f36-b58f-41f74e7fd377"
  }
}
```

### `POST /api/admin/comments/{commentId}/reject`
Rejects a pending lesson comment and keeps it hidden from the public lesson page.

- **Authorization**: `Admin` or `Teacher`
- **Request**: `{}`
- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "252263a8-22ce-4cb5-8a4e-4f87d8a93984",
    "status": "Rejected",
    "reviewedAt": "2026-04-08T16:41:00Z",
    "reviewedByUserId": "f7acc366-f5ad-4f36-b58f-41f74e7fd377"
  }
}
```
