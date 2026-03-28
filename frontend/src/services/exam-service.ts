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
  durationSeconds?: number;
  options: QuestionOptionDto[];
}

export interface ActiveExamAttemptDto {
  attemptId: string;
  title: string;
  description: string;
  startedAt: string;
  durationMinutes?: number;
  totalScore: number;
  questions: ExamQuestionDto[];
}

export interface AnswerSubmissionDto {
  examQuestionId: string;
  selectedOptionId: string;
}

export interface ExamResultDto {
  attemptId: string;
  scoreAchieved: number;
  totalScore: number;
  isPassed: boolean;
  blocksNextLesson: boolean;
  evaluation: string;
  isTimeExpired: boolean;
}

export const examService = {
  startExam: (examId: string) => apiClient.post<{ data: ActiveExamAttemptDto }>(`/exams/${examId}/start`),
  submitExam: (examId: string, attemptId: string, answers: AnswerSubmissionDto[]) => 
    apiClient.post<{ data: ExamResultDto }>(`/exams/${examId}/submit/${attemptId}`, answers),
};
