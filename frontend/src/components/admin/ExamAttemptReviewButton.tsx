'use client';

import { AssessmentAttemptReview } from './AssessmentAttemptReview';

export function ExamAttemptReviewButton({ examId, attemptId, studentName, onChanged }: {
  examId: string; attemptId: string; studentName: string; onChanged?: () => void | Promise<void>;
}) {
  return <AssessmentAttemptReview kind="exam" assessmentId={examId} attemptId={attemptId} studentName={studentName} onChanged={onChanged} />;
}
