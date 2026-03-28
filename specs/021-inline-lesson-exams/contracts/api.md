# API Contracts: Inline Lesson Exams

## 1. POST /api/admin/exams/inline
Create a full exam with its questions in a single, comprehensive transaction.

### Request Body
```json
{
  "title": "Module 1 Assessment",
  "description": "Please complete this quiz after watching the related video.",
  "passingScore": 50,
  "target": {
    "type": "Lesson", // or "Video"
    "id": "123e4567-e89b-12d3-a456-426614174000" // Requires LessonId or LessonVideoId
  },
  "questions": [
    {
      "text": "What is the capital of France?",
      "type": "MCQ", // mapped backend to QuestionType.MCQ
      "points": 10,
      "order": 1,
      "options": [
        { "text": "Berlin", "isCorrect": false },
        { "text": "Paris", "isCorrect": true },
        { "text": "Madrid", "isCorrect": false }
      ]
    },
    {
      "text": "Explain your reasoning for the previous answer.",
      "type": "Essay", // mapped backend to QuestionType.Essay
      "points": 20,
      "order": 2,
      "options": [] // Must be empty or omitted
    }
  ]
}
```

### Response (201 Created)
```json
{
  "success": true,
  "data": {
    "examId": "987e6543-e21b-12d3-a456-426614174000"
  },
  "message": "Exam created and linked successfully."
}
```

## 2. GET /api/admin/exams/{id}
Retrieve a fully serialized exam object to populate the editor state if editing.

### Response (200 OK)
Returns the same structure as the POST payload body, alongside unique `Id` guarantees for individual components like Questions or Options.

```json
{
  "success": true,
  "data": {
    "id": "987e6543-e21b-12d3-a456-426614174000",
    "title": "Module 1 Assessment",
    "description": "Please complete this quiz...",
    "passingScore": 50,
    "totalScore": 30,
    "questions": [
      {
        "id": "...",
        "text": "...",
        "type": "MCQ",
        "points": 10,
        "order": 1,
        "options": [
          { "id": "...", "text": "Paris", "isCorrect": true }
        ]
      }
    ]
  }
}
```
