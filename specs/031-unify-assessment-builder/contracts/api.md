# API Contracts: Unified Assessment Builder

The following endpoints govern the creation and management of unified assessments.

## POST /api/Admin/exams

Creates a new exam attached to a specific `Lesson` or `LessonVideo`.

**Request Body Schema:**
```json
{
  "lessonId": "string (uuid)",
  "title": "string",
  "description": "string",
  "durationMinutes": "number | null",
  "timePerQuestionSeconds": "number | null",
  "passingScore": "number",
  "totalScore": "number",
  "isMandatory": "boolean",
  "isRandomized": "boolean",
  "targetVideoId": "string (uuid) | null",    // Specifies if it's a Pop Quiz
  "questions": [
     // Question structure (differs between Homework/Exam currently, abstracting here)
     {
         "bodyText": "string",
         "points": "number",
         "options": [
            { "text": "string", "isCorrect": "boolean" }
         ]
     }
  ]
}
```

## POST /api/Admin/homework

Creates a new homework attached to a specific `Lesson`.

**Request Body Schema:**
```json
{
  "lessonId": "string (uuid)",
  "title": "string",
  "description": "string",
  "isMandatory": "boolean",
  "isRandomized": "boolean",
  "passingScoreThreshold": "number",
  "totalScore": "number",
  "questions": [
     {
         "bodyText": "string",
         "order": "number",
         "pointsActive": "number",
         "questionType": "string",
         "possibleAnswers": ["string"],
         "correctAnswerKey": "string"
     }
  ]
}
```
