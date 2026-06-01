# Interfaces & Contracts

### **1. Lesson Detail Response Update**
The `GetLessonDetailQueryHandler` response payload is extended with two optional properties indicating exact blockages.

```json
{
  "id": "guid",
  "title": "Lesson Title",
  // ...
  "isLocked": true,
  "lockedReason": "يجب اجتياز امتحان 'مراجعة الميكانيكا' التابع للحصة السابقة بنجاح.",
  "blockingExamId": "guid",                // NEW: Points directly to the blocking exam
  "blockingHomeworkLessonId": "guid"       // NEW: Points directly to the lesson holding the blocking homework
}
```

### **2. Submit Exam Endpoint (Harden Contract)**
`POST /api/exams/{id}/submit/{attemptId}`
The contract remains visually the same to the frontend, but the internal handling must ensure proper `.Nullable` or error handling when mapping `StudentExamAttempt` answers so that it guarantees a 200 OK or 400 Bad Request, never a 500 Internal Server error.
