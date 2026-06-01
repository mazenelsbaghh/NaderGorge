import apiClient from './api-client';

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
  baseText?: string;
  options: QuestionOptionDto[];
}

export interface ActiveExamAttemptDto {
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

export interface AnswerSubmissionDto {
  examQuestionId: string;
  selectedOptionId?: string;
  answerText?: string;
  selectedText?: string;
  audioUrl?: string;
}

export interface ExamResultDto {
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

export type ExamResultState = 'Pending' | 'PartiallyGraded' | 'Completed';
export type EssaySubmissionStatus = 'WaitAI' | 'AIScored' | 'WaitTeacher' | 'TeacherGraded';

export interface ExamQuestionReviewDto {
  examQuestionId: string;
  order: number;
  questionText: string;
  selectedOptionText?: string;
  isAnswered: boolean;
  isCorrect: boolean;
  pointsAwarded: number;
  correctOptionText?: string;
  audioUrl?: string;
  writtenCorrection?: string;
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

export const examService = {
  useFiftyFifty: (examId: string, attemptId: string, questionId: string) =>
    apiClient.get<{ data: string[] }>(`/exams/${examId}/attempts/${attemptId}/questions/${questionId}/fifty-fifty`),
  swapQuestion: (examId: string, attemptId: string, questionId: string) =>
    apiClient.post<{ data: ExamQuestionDto; message?: string }>(`/exams/${examId}/attempts/${attemptId}/questions/${questionId}/swap`),
  startExam: (examId: string) => apiClient.post<{ data: ActiveExamAttemptDto }>(`/exams/${examId}/start`),
  getLatestPassedResult: (examId: string) =>
    apiClient.get<{ data: ExamResultDto }>(`/exams/${examId}/latest-passed-result`),
  getGradingStatus: (attemptId: string) =>
    apiClient.get<{ data: ExamAttemptGradingStatusDto }>(`/exams/attempts/${attemptId}/grading-status`),
  submitExam: (examId: string, attemptId: string, answers: AnswerSubmissionDto[]) => 
    apiClient.post<{ data: ExamResultDto }>(`/exams/${examId}/submit/${attemptId}`, answers),
};
