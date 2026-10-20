import axios from 'axios';
import type { PartialAssessmentResultDto } from './assessment-result';
import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';

export type ExamEssayGradingMode = 'Automatic' | 'AutomaticFullCredit' | 'AutomaticFullOrHalfCredit' | 'AutomaticFullOrHalfCreditFrom70Percent' | 'Manual' | 'ModelAnswerOnly';

export interface QuestionOptionDto {
  id: string;
  text: string;
}

export interface ExamQuestionDto {
  id: string;
  text: string;
  type?: string;
  points: number;
  hintText?: string;
  imageUrl?: string;
  baseText?: string;
  options: QuestionOptionDto[];
}

export interface ActiveExamAttemptDto {
  revisionId?: string | null;
  attemptId: string;
  title: string;
  description: string;
  startedAt: string;
  durationMinutes?: number;
  remainingSeconds?: number;
  totalScore: number;
  lessonId?: string;
  packageId?: string;
  questions: ExamQuestionDto[];
}

export interface SwapQuestionResultDto {
  question: ExamQuestionDto;
  totalScore: number;
  passingScore: number;
}

export interface AnswerSubmissionDto {
  examQuestionId: string;
  selectedOptionId?: string;
  answerText?: string;
  selectedText?: string;
}

export interface ExamDraftDto {
  version: number;
  lastMutationId?: string | null;
  savedAt?: string | null;
  answers: Record<string, string>;
  isClosed: boolean;
}

export interface SaveExamDraftRequest {
  expectedVersion: number;
  mutationId: string;
  answers: Record<string, string>;
  revisionId?: string | null;
}

export type PartialExamResultDto = PartialAssessmentResultDto;

export interface ExamResultDto {
  essayScoreStatus?: 'NotApplicable' | 'Pending' | 'PartiallyIncluded' | 'Included' | 'Excluded';
  answersVisible?: boolean;
  examId?: string;
  allowRetake?: boolean;
  partialResult?: PartialExamResultDto | null;
  attemptId: string;
  scoreAchieved: number;
  totalScore: number;
  isPassed: boolean;
  blocksNextLesson: boolean;
  evaluation: string;
  isTimeExpired: boolean;
  resultState: string;
  lessonId?: string;
  packageId?: string;
  questions: ExamQuestionReviewDto[];
}

export type ExamResultState = 'Pending' | 'PartiallyGraded' | 'Completed' | 'RequiresCompletion';
export type EssaySubmissionStatus = 'WaitAI' | 'AIScored' | 'WaitTeacher' | 'TeacherGraded';

export interface ExamQuestionReviewDto {
  examQuestionId: string;
  order: number;
  questionText: string;
  selectedOptionText?: string;
  isAnswered: boolean;
  isCorrect: boolean;
  pointsAwarded: number;
  maximumPoints?: number;
  isExcludedFromScore?: boolean;
  isPendingGrading?: boolean;
  gradingFeedback?: string;
  correctOptionText?: string;
  audioUrl?: string;
  imageUrl?: string;
  writtenCorrection?: string;
  studentAudioUrl?: string;
}

export interface EssayGradingStatusItemDto {
  essaySubmissionId: string;
  questionId: string;
  status: EssaySubmissionStatus;
  aiInitialScore?: number;
  teacherFinalScore?: number;
}

export interface ExamAttemptGradingStatusDto {
  attemptId: string;
  resultState: ExamResultState;
  essays: EssayGradingStatusItemDto[];
}

function mayHaveSubmitted(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return false;
  const status = error.response?.status;
  return !error.response || status === 408 || status === 409 || (status !== undefined && status >= 500)
    || (status === 400 && error.response?.data?.message === 'This attempt has already been submitted.');
}

async function recoverSubmittedAttempt(attemptId: string, submissionError: unknown) {
  if (!mayHaveSubmitted(submissionError)) throw submissionError;
  try {
    const saved = await apiClient.get<{ data: ExamResultDto }>(`/exams/attempts/${attemptId}/result`, { suppressErrorToast: true });
    if (saved.data.data?.attemptId === attemptId) return saved;
  } catch (recoveryError) {
    if (!axios.isAxiosError(recoveryError)) throw recoveryError;
    // A failed read cannot prove submission; retain the original error and draft.
  }
  throw submissionError;
}

export const examService = {
  getDraft: (examId: string, attemptId: string, revisionId?: string | null) =>
    apiClient.get<{ data: ExamDraftDto }>(`/exams/${examId}/attempts/${attemptId}/draft`, {
      params: { revisionId }, suppressErrorToast: true, timeout: 15000,
    }),
  saveDraft: (examId: string, attemptId: string, draft: SaveExamDraftRequest) =>
    apiClient.put<{ data: ExamDraftDto }>(`/exams/${examId}/attempts/${attemptId}/draft`, draft, {
      suppressErrorToast: true, timeout: 15000,
    }),
  useFiftyFifty: (examId: string, attemptId: string, questionId: string) =>
    apiClient.get<{ data: string[] }>(`/exams/${examId}/attempts/${attemptId}/questions/${questionId}/fifty-fifty`),
  swapQuestion: (examId: string, attemptId: string, questionId: string) =>
    apiClient.post<{ data: SwapQuestionResultDto; message?: string }>(`/exams/${examId}/attempts/${attemptId}/questions/${questionId}/swap`),
  startExam: async (examId: string) => {
    const response = await apiClient.post<{ data: ActiveExamAttemptDto }>(`/exams/${examId}/start`);
    invalidateMany(['student:exams', 'assessments']);
    return response;
  },
  getLatestPassedResult: (examId: string) =>
    apiClient.get<{ data: ExamResultDto }>(`/exams/${examId}/latest-passed-result`),
  getLatestResult: (examId: string) =>
    apiClient.get<{ data: ExamResultDto }>(`/exams/${examId}/latest-result`, { suppressErrorToast: true }),
  getGradingStatus: (attemptId: string) =>
    apiClient.get<{ data: ExamAttemptGradingStatusDto }>(`/exams/attempts/${attemptId}/grading-status`),
  getAttemptResult: (attemptId: string) =>
    apiClient.get<{ data: ExamResultDto }>(`/exams/attempts/${attemptId}/result`),
  submitExam: async (examId: string, attemptId: string, answers: AnswerSubmissionDto[], revisionId?: string | null, draftVersion?: number) => {
    const response = await apiClient.post<{ data: ExamResultDto }>(`/exams/${examId}/submit/${attemptId}`, answers, {
      params: { revisionId, draftVersion }, suppressErrorToast: true,
    }).catch(error => recoverSubmittedAttempt(attemptId, error));
    if (response.data.data?.attemptId !== attemptId)
      throw new Error('تعذر تأكيد تسليم الامتحان. إجاباتك ما زالت موجودة؛ حاول مرة أخرى.');
    invalidateMany(['student:exams', 'assessments']);
    return response;
  },
};
