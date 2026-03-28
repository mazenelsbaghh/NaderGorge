# API Contracts: Assessment Grading

## 1. Exam Creation Context (Existing Endpoint Modification)

**POST /api/admin/exams/inline**

```json
{
  "title": "Final Assessment",
  "description": "",
  "passingScore": 50,
  "totalScore": 100, // <--- NEW REQUIREMENT
  "target": { "type": "Lesson", "id": "..." },
  "questions": [ ... ]
}
```

## 2. Homework Creation Context (Existing Endpoint Modification)

**POST /api/admin/content/lessons/{lessonId}/homework**

```json
{
  "title": "Math Homework 1",
  "instructions": "Solve all equations",
  "isMandatory": true,
  "requiredPointsToPass": 50,
  "totalScore": 100, // <--- NEW REQUIREMENT
  "questions": [ ... ]
}
```

## 3. Student Assessment Result View (Read Context)

When fetching a student's profile or progress reporting `GET /api/admin/users/students/{userId}/profile` or similar views:

```json
{
  ...
  "examResults": [
    {
       "examId": "...",
       "title": "Final Assessment",
       "earnedScore": 86.5,
       "totalScore": 100,
       "passingScore": 50,
       "evaluation": "ممتاز" // <--- AUTO COMPUTED BY BACKEND
    }
  ]
}
```
