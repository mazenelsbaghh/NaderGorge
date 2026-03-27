import apiClient from './api-client';

export interface HomeworkQuestionDto {
    id: string;
    text: string;
    points: number;
    questionType: number; // 0 for MCQ, 1 for Essay
    options?: { id: string, text: string }[];
}

export interface HomeworkDto {
    id: string;
    title: string;
    description: string;
    questionsCount: number;
    questions?: HomeworkQuestionDto[]; // Optional full questions, if fetched by Details endpoint.
}

export interface AnswerSubmissionDto {
    questionId: string;
    providedAnswer: string;
}

export const homeworkService = {
    getPending: async () => {
        return apiClient.get<{ data: HomeworkDto[] }>('/api/v1/students/homework/pending');
    },

    submitHomework: async (homeworkId: string, answers: AnswerSubmissionDto[]) => {
        return apiClient.post(`/api/v1/students/homework/${homeworkId}/submit`, answers);
    }
};
