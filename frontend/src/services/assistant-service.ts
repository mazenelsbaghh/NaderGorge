import apiClient from './api-client';

export interface AssistantTaskDto {
    id: string;
    taskType: number; // 0=GradeEssay, 1=FollowUpAtRisk, 2=ResolvePaymentIssue
    studentId: string | null;
    studentName: string;
    referenceEntityId: string | null;
    status: string; // "Open", "InReview", "Done"
    createdAt: string;
}

export const assistantService = {
    getPendingTasks: async (typeFilter?: number) => {
        const queryParams = typeFilter !== undefined ? `?typeFilter=${typeFilter}` : '';
        return apiClient.get<{ data: AssistantTaskDto[] }>(`/api/v1/assistant/tasks/pending${queryParams}`);
    },

    resolveTask: async (taskId: string, resolutionNotes: string) => {
        return apiClient.post(`/api/v1/assistant/tasks/${taskId}/resolve`, { resolutionNotes });
    }
};
