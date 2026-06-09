# API Endpoints: Media Production and Social Planner

All endpoints are protected and require the user to have either an `Admin` or `Supervisor` role, or an assistant with the `media.manage` permission claim.

---

## 1. Media Production Pipeline

### GET `/api/admin/media/pipelines`
Lists media production assets with filters.

**Parameters**:
- `search` (string, optional): Search by title
- `stage` (int, optional): Filter by `MediaStage` enum
- `assigneeId` (Guid, optional): Filter by editor assignee
- `page` (int, default: 1)
- `pageSize` (int, default: 20)

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "e5b7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
        "title": "Lesson 1: Introduction to Mechanics",
        "description": "Introduction video for Grade 3 math package",
        "stage": "Editing",
        "assignedAgentId": "f7d7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
        "assignedAgentName": "Mazen Elsbagh",
        "assetFolderUrl": "https://drive.google.com/drive/folders/123",
        "editingErrorCount": 1,
        "publishedAt": null,
        "createdAt": "2026-06-09T08:00:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20
  }
}
```

### POST `/api/admin/media/pipelines`
Creates a new media production item.

**Request Body**:
```json
{
  "title": "Lesson 2: Newton's Laws",
  "description": "Second lesson in Grade 3 Physics",
  "assignedAgentId": "f7d7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
  "assetFolderUrl": "https://drive.google.com/drive/folders/456"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Media production item created successfully.",
  "data": "e5b7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c"
}
```

### PUT `/api/admin/media/pipelines/{id}`
Updates an existing pipeline item (metadata, assignee, error count, or stage transition).

**Request Body**:
```json
{
  "title": "Lesson 2: Newton's Laws (Updated)",
  "description": "Updated description",
  "assignedAgentId": "f7d7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9d",
  "assetFolderUrl": "https://drive.google.com/drive/folders/456",
  "editingErrorCount": 2,
  "stage": "Review",
  "supervisorId": "a1b2c3d4-e35b-4c5c-9c9c-9c9c9c9c9c9c" // Required only when stage transitions to "Review"
}
```

*Note: Transitioning to `Review` creates a task for the designated `supervisorId` in the operations module.*

**Response (200 OK)**:
```json
{
  "success": true,
  "message": "Media pipeline updated successfully."
}
```

---

## 2. Social Media Planner

### GET `/api/admin/media/social-plans`
Lists social calendar items.

**Parameters**:
- `platform` (int, optional): Filter by `SocialPlatform` enum
- `status` (int, optional): Filter by `SocialPlanStatus` enum
- `startDate` (DateTime, optional)
- `endDate` (DateTime, optional)

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "c3d4e5f6-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "title": "Exam Prep Tips video release",
      "description": "Promo snippet for Facebook/TikTok",
      "script": "Don't miss the revision session! ...",
      "platform": "TikTok",
      "status": "Scheduled",
      "scheduledDate": "2026-06-15T16:00:00Z",
      "mediaProductionPipelineId": "e5b7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
      "linkedMediaStage": "Editing"
    }
  ]
}
```

### POST `/api/admin/media/social-plans`
Creates a new social media schedule.

**Request Body**:
```json
{
  "title": "Exam Prep Tips video release",
  "description": "Promo snippet for Facebook/TikTok",
  "script": "Don't miss the revision session! ...",
  "platform": 3, // TikTok
  "scheduledDate": "2026-06-15T16:00:00Z",
  "mediaProductionPipelineId": "e5b7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c"
}
```

---

## 3. Analytics & reports

### GET `/api/admin/media/reports/kpis`
Retrieves production performance statistics.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "totalPublished": 42,
    "averageEditingDays": 3.4,
    "editorPerformance": [
      {
        "editorId": "f7d7b99c-e35b-4c5c-9c9c-9c9c9c9c9c9c",
        "editorName": "Mazen Elsbagh",
        "assetsProduced": 15,
        "totalErrors": 3,
        "averageStageDurationDays": 2.1
      }
    ]
  }
}
```
