'use client';

import { AssessmentAttemptReview } from './AssessmentAttemptReview';

export function ExamAttemptReviewButton({ examId, attemptId, onChanged }: {
  examId: string; attemptId: string; onChanged?: () => void | Promise<void>;
}) {
  return <AssessmentAttemptReview kind="exam" assessmentId={examId} attemptId={attemptId} onChanged={onChanged} />;
}
