import apiClient from './api-client';

export interface QuestionOptionDto {
  id: string;
  text: string;
}

export interface ExamQuestionDto {
  id: string;
  text: string;
  points: number;
  options: QuestionOptionDto[];
}

export interface ExamDto {
  id: string;
  title: string;
  description: string;
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
}

export const examService = {
  startExam: (examId: string) => apiClient.get(`/exams/${examId}/start`),
  submitExam: (examId: string, answers: AnswerSubmissionDto[]) => 
    apiClient.post(`/exams/${examId}/submit`, answers),
};
