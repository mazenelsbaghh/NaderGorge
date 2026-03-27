# API Contracts: Phase 2 — Structured Learning and Academic Operations

## Overview
This document specifies the JSON DTOs and API endpoints connecting the Next.js frontend to the .NET Backend API for the new Phase 2 features.

---

### Homework Endpoints

**`GET /api/v1/students/homework/pending`**
Returns a list of homework assignments the student currently needs to complete.
*Response:*
```json
[
  {
    "id": "guid",
    "lessonId": "guid",
    "title": "Module 3 - Essay Practice",
    "dueDate": "2026-04-01T23:59:59Z",
    "isMandatory": true,
    "status": "Pending"
  }
]
```

**`POST /api/v1/students/homework/{homeworkId}/submit`**
Submits a student's answers (MCQ or Essay text).
*Request:*
```json
{
  "answers": [
    {
      "questionId": "guid",
      "providedAnswer": "To be or not to be..."
    }
  ]
}
```
*Response: 200 OK*

---

### Gamification Endpoints

**`GET /api/v1/students/gamification/status`**
Returns user's total points, rank, and badges.
*Response:*
```json
{
  "totalPoints": 5200,
  "levelName": "Pharaoh Initiate",
  "currentStreak": 14,
  "badges": [
    { "id": "guid", "name": "Early Bird", "iconUrl": "/images/badges/early-bird.png" }
  ],
  "globalRank": 45
}
```

---

### Assistant Ops Endpoints

**`GET /api/v1/assistant/tasks/queue`**
Lists work items for the logged-in assistant (based on RBAC: e.g. "EssayGrading" or "FollowUp").
*Response:*
```json
[
  {
    "taskId": "guid",
    "taskType": "GradeEssay",
    "studentId": "guid",
    "studentName": "Ali Hassan",
    "referenceEntityId": "guid",
    "createdAt": "2026-03-26T00:00:00Z"
  }
]
```

**`POST /api/v1/assistant/tasks/{taskId}/resolve`**
Completes a task (grades the essay, clears the warning).
*Request:*
```json
{
  "resolutionNotes": "Student improved reasoning on Question 3.",
  "scoreAwarded": 18,
  "status": "Done"
}
```
*Response: 200 OK*

---

### Parent Endpoints

**`GET /api/v1/parent/reports/{studentId}/summary`**
Produces a read-only metric summary.
*Response:*
```json
{
  "studentName": "Ali Hassan",
  "status": "Committed",
  "lessonsCompleted": 15,
  "missedHomeworks": 0,
  "examAveragePercent": 88.5,
  "activeWarnings": []
}
```
