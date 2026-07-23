# Contract: Teacher Public Profile and Community API

## Public/Student Teacher Profile

### GET `/api/public/teachers`

Query parameters: `subjectId`, `gradeLevel`, `search`, `page`, `pageSize`.

Returns public teacher cards.

### GET `/api/public/teachers/{slugOrId}`

Returns public profile detail.

Response data:

```json
{
  "teacherId": "guid",
  "slug": "teacher-slug",
  "displayName": "Teacher Name",
  "bio": "Public bio",
  "introVideoUrl": "https://...",
  "subjects": [
    {
      "id": "guid",
      "name": "Math"
    }
  ],
  "packages": [
    {
      "id": "guid",
      "name": "Single teacher package",
      "price": 300.0,
      "hasAccess": false
    }
  ],
  "sharedPackages": [
    {
      "id": "guid",
      "name": "Shared package",
      "price": 1000.0,
      "hasAccess": false
    }
  ],
  "lessons": [
    {
      "id": "guid",
      "title": "Lesson title",
      "hasAccess": false
    }
  ],
  "ratingAverage": 4.8,
  "ratingCount": 120
}
```

Access rules:
- Public metadata is visible without purchase.
- Paid content details expose only browse-safe metadata until purchase/grant.
- Student-specific `hasAccess` uses existing grants.

## Teacher-Scoped Community

### GET `/api/public/teachers/{teacherId}/community-posts`

Returns approved teacher-scoped posts. Authenticated students may receive additional access-aware metadata if existing community rules support it.

### POST `/api/student/teachers/{teacherId}/community-posts`

Creates a teacher-scoped community post using existing moderation defaults.

Request:

```json
{
  "content": "Question or discussion",
  "attachments": []
}
```

### POST `/api/student/teachers/{teacherId}/community-posts/{postId}/comments`

Creates a comment using existing comment moderation behavior.

## Admin Moderation

Existing moderation endpoints remain authoritative. They must expose/filter `teacherId` where present so admins can moderate teacher profile community without a separate moderation system.
