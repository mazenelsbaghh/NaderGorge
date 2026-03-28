# API Contracts

```typescript
// GET /api/admin/exams/{examId}/dashboard
interface GetExamDashboardResponse {
  data: {
    examId: string;
    title: string;
    targetType: 'Lesson' | 'Video';
    targetId: string;
    questionCount: number;
    totalScore: number;
    passingScore: number;
    durationMinutes: number | null;
    attempts: {
      studentId: string;
      studentName: string;
      studentCode: string;
      submittedAt: string;
      scoreAchieved: number;
      evaluation: string;
      isPassed: boolean;
      isTimeExpired: boolean;
    }[];
  };
  isSuccess: boolean;
  message: string;
}

// POST /api/exams/{examId}/attempts
// Initiates the server-side timer
interface StartExamAttemptResponse {
  data: {
    attemptId: string;
    startedAt: string; // ISO DB timestamp
    durationMinutes: number | null; 
    questions: {
      id: string;
      text: string;
      type: string;
      points: number;
      durationSeconds: number | null;
      options: { id: string; text: string }[];
    }[];
  };
  isSuccess: boolean;
  message: string;
}
```
