# API Contracts

## Student Community APIs

### `GET /api/community/posts`
Returns the public student community feed of approved posts.

- **Authorization**: Authenticated student with access to the community
- **Query Parameters**:
  - `cursor` (optional): pagination cursor for older posts
  - `limit` (optional): page size within platform defaults
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "7f47aa78-7350-4bc9-a7d4-913d04767b43",
      "authorName": "Ahmed Ali",
      "body": "حد عنده طريقة سهلة لمراجعة الفصل ده؟",
      "createdAt": "2026-04-08T17:30:00Z",
      "likeCount": 12,
      "commentCount": 3,
      "isLikedByCurrentUser": false
    }
  ]
}
```

### `POST /api/community/posts`
Creates a new community post in `Pending` status.

- **Authorization**: Authenticated student with access to the community
- **Request**:

```json
{
  "body": "جمعت أهم أسئلة المراجعة النهائية في ورقة واحدة."
}
```

- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "d2d3ae48-3ea8-4457-b85e-c50f38be7df3",
    "status": "Pending",
    "createdAt": "2026-04-08T17:31:00Z",
    "message": "تم إرسال البوست وهو الآن في انتظار المراجعة."
  }
}
```

### `GET /api/community/posts/mine`
Returns the current student's own submitted posts with moderation status.

- **Authorization**: Authenticated student with access to the community
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "d2d3ae48-3ea8-4457-b85e-c50f38be7df3",
      "body": "جمعت أهم أسئلة المراجعة النهائية في ورقة واحدة.",
      "status": "Pending",
      "createdAt": "2026-04-08T17:31:00Z"
    }
  ]
}
```

### `GET /api/community/posts/{postId}/comments`
Returns comments for a single approved post.

- **Authorization**: Authenticated student with access to the community
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "2af5ddc9-b9db-4461-8a43-0115ab2a9eb2",
      "postId": "7f47aa78-7350-4bc9-a7d4-913d04767b43",
      "authorName": "Sara Hassan",
      "body": "آه، جربي تقسيمه إلى أفكار قصيرة.",
      "createdAt": "2026-04-08T17:35:00Z",
      "isOwnComment": false
    }
  ]
}
```

### `POST /api/community/posts/{postId}/comments`
Creates a new comment on an approved post.

- **Authorization**: Authenticated student with access to the community
- **Request**:

```json
{
  "body": "ممكن تنزلها في شكل نقاط؟"
}
```

- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "3b9fa8e1-ea95-4c42-a401-2831b2d89ed3",
    "postId": "7f47aa78-7350-4bc9-a7d4-913d04767b43",
    "createdAt": "2026-04-08T17:36:00Z"
  }
}
```

### `POST /api/community/posts/{postId}/likes/toggle`
Adds or removes the current student's like on an approved post.

- **Authorization**: Authenticated student with access to the community
- **Request**: `{}`
- **Response**:

```json
{
  "success": true,
  "data": {
    "postId": "7f47aa78-7350-4bc9-a7d4-913d04767b43",
    "isLikedByCurrentUser": true,
    "likeCount": 13
  }
}
```

## Admin Moderation APIs

### `GET /api/admin/community/posts`
Lists community posts for moderation, optionally filtered by status.

- **Authorization**: `Admin`
- **Query Parameters**:
  - `status` (optional): `Pending`, `Approved`, or `Rejected`
- **Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "d2d3ae48-3ea8-4457-b85e-c50f38be7df3",
      "studentId": "8fdd5d88-c7a7-44b1-9314-98f3f15fdf87",
      "studentName": "Ahmed Ali",
      "body": "جمعت أهم أسئلة المراجعة النهائية في ورقة واحدة.",
      "status": "Pending",
      "createdAt": "2026-04-08T17:31:00Z",
      "reviewedAt": null,
      "reviewedByName": null,
      "commentCount": 0,
      "likeCount": 0
    }
  ]
}
```

### `POST /api/admin/community/posts/{postId}/approve`
Approves a pending community post and makes it visible in the public feed.

- **Authorization**: `Admin`
- **Request**: `{}`
- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "d2d3ae48-3ea8-4457-b85e-c50f38be7df3",
    "status": "Approved",
    "reviewedAt": "2026-04-08T17:40:00Z",
    "reviewedByUserId": "f7acc366-f5ad-4f36-b58f-41f74e7fd377"
  }
}
```

### `POST /api/admin/community/posts/{postId}/reject`
Rejects a pending community post and keeps it hidden from the public feed.

- **Authorization**: `Admin`
- **Request**: `{}`
- **Response**:

```json
{
  "success": true,
  "data": {
    "id": "d2d3ae48-3ea8-4457-b85e-c50f38be7df3",
    "status": "Rejected",
    "reviewedAt": "2026-04-08T17:41:00Z",
    "reviewedByUserId": "f7acc366-f5ad-4f36-b58f-41f74e7fd377"
  }
}
```
