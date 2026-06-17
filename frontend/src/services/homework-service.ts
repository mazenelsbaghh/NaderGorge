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

export interface StartHomeworkAttemptDto {
    homeworkId: string;
    submissionId: string;
    title: string;
    instructions?: string;
    totalScore: number;
    passingScore?: number;
    alreadyCompleted: boolean;
    score?: number;
    evaluation?: string;
    startedAt: string;
    durationMinutes?: number;
    remainingSeconds?: number;
    questions: StartHomeworkQuestionDto[];
}

export interface StartHomeworkQuestionDto {
    id: string;
    order: number;
    questionType: number; // 0=MCQ, 1=Essay, 2=FindTheMistake
    text: string;
    maxPoints: number;
    possibleAnswers?: string[];
    audioUrl?: string;
    imageUrl?: string;
    hintText?: string;
    baseText?: string;
    mistakeStartIndex?: number;
    mistakeEndIndex?: number;
}

export interface HomeworkResultDto {
    homeworkId: string;
    submissionId: string;
    title: string;
    score: number;
    totalScore: number;
    passingScore?: number;
    isPassed: boolean;
    evaluation?: string;
    submittedAt?: string;
    gradedAt?: string;
    status: string;
    totalQuestions: number;
    correctAnswers: number;
    wrongAnswers: number;
    ungradedAnswers: number;
    questionReviews: HomeworkQuestionReviewDto[];
}

export interface HomeworkQuestionReviewDto {
    questionId: string;
    order: number;
    questionType: number;
    text: string;
    providedAnswer?: string;
    correctAnswer?: string;
    maxPoints: number;
    scoreReceived?: number;
    isCorrect?: boolean;
    writtenCorrection?: string;
    audioUrl?: string;
    imageUrl?: string;
    possibleAnswers?: string[];
}

export const homeworkService = {
    getPending: async () => {
        return apiClient.get<{ data: HomeworkDto[] }>('/homework/pending');
    },

    submitHomework: async (homeworkId: string, answers: AnswerSubmissionDto[]) => {
        return apiClient.post(`/homework/${homeworkId}/submit`, answers);
    },

    startHomework: (homeworkId: string) =>
        apiClient.get<{ data: StartHomeworkAttemptDto }>(`/homework/${homeworkId}/start`),

    getHomeworkResult: (homeworkId: string) =>
        apiClient.get<{ data: HomeworkResultDto }>(`/homework/${homeworkId}/result`),
};
