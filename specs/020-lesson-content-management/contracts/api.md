# API Contracts: Lesson Content Management

## 1. Lesson Cockpit Data (GET)
**Endpoint**: `GET /api/content/lessons/{id}/cockpit`

**Response (`LessonCockpitDto`)**:
```json
{
  "success": true,
  "data": {
    "lessonId": "uuid",
    "title": "Lesson title",
    "summary": "Lesson summary",
    "videos": [
      {
        "id": "uuid",
        "title": "Video title",
        "provider": "YouTube",
        "url": "https://...",
        "order": 1,
        "maxWatchCount": 3
      }
    ],
    "resources": [
      {
        "id": "uuid",
        "title": "Resource Title",
        "fileUrl": "https://...",
        "resourceType": "PDF"
      }
    ],
    "homework": [
      {
        "id": "uuid",
        "title": "Week 1 Assignment",
        "isMandatory": true,
        "passingScoreThreshold": 50
      }
    ],
    "examId": "uuid-of-exam-attached" // or null if none
  }
}
```

## 2. Attach File/Document (POST)
**Endpoint**: `POST /api/admin/lessons/resources`

**Command (`CreateLessonResourceCommand`)**:
```json
{
  "lessonId": "uuid",
  "title": "File/Document Title",
  "fileUrl": "https://link/to/upload",
  "resourceType": "PDF" // Option: PDF, Document, Image, Other
}
```

## 3. Create Homework (POST)
**Endpoint**: `POST /api/homework` (assuming HomeworkController handles assignments)

**Command (`CreateHomeworkCommand`)**:
```json
{
  "lessonId": "uuid",
  "title": "Homework Title",
  "description": "Optional instructions",
  "isMandatory": true,
  "passingScoreThreshold": null
}
```

## 4. Link Exam to Lesson (PUT)
**Endpoint**: `PUT /api/admin/lessons/{id}/exam`

**Command (`LinkLessonExamCommand`)**:
```json
{
  "lessonId": "uuid",
  "examId": "uuid // null to clear"
}
```
