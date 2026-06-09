import apiClient from './api-client';

export interface BackendResponse<T> {
    success: boolean;
    data: T;
    message: string;
    errors?: string[];
}

export interface AssistantTaskDto {
    id: string;
    taskType: number; // 0=GradeEssay, 1=FollowUpAtRisk, 2=ResolvePaymentIssue
    studentId: string | null;
    studentName: string;
    referenceEntityId: string | null;
    status: string; // "Open", "InReview", "Done"
    createdAt: string;
}

// Operations Task Management Interfaces
export interface TaskItemDto {
    id: string;
    title: string;
    description: string;
    assigneeId: string;
    assigneeName: string;
    createdById: string;
    createdByName: string;
    status: number | string; // 1=New, 2=InProgress, 3=Review, 4=Completed, 5=Paused, 6=Overdue
    priority: number | string; // 1=Low, 2=Medium, 3=High, 4=Critical
    dueDate: string | null;
    completedAt: string | null;
    approvedById: string | null;
    approvedByName: string | null;
    createdAt: string;
    updatedAt: string | null;
}

export interface CommentDto {
    id: string;
    userId: string;
    userName: string;
    content: string;
    attachmentUrl: string | null;
    createdAt: string;
}

export interface TaskDetailsDto {
    task: TaskItemDto;
    comments: CommentDto[];
}

export const assistantService = {
    getPendingTasks: async (typeFilter?: number) => {
        const queryParams = typeFilter !== undefined ? `?typeFilter=${typeFilter}` : '';
        return apiClient.get<BackendResponse<AssistantTaskDto[]>>(`/v1/assistant/tasks/pending${queryParams}`);
    },

    resolveTask: async (taskId: string, resolutionNotes: string) => {
        return apiClient.post<BackendResponse<Guid>>(`/v1/assistant/tasks/${taskId}/resolve`, { resolutionNotes });
    },

    // Operations Task Management APIs for Assistants
    getMyOperationsTasks: async () => {
        return apiClient.get<BackendResponse<TaskItemDto[]>>('/v1/assistant/tasks/my');
    },

    getOperationsTaskDetails: async (taskId: string) => {
        return apiClient.get<BackendResponse<TaskDetailsDto>>(`/v1/assistant/tasks/my/${taskId}`);
    },

    updateOperationsTaskStatus: async (taskId: string, status: number) => {
        return apiClient.post<BackendResponse<boolean>>(`/v1/assistant/tasks/my/${taskId}/status`, { status });
    },

    addOperationsTaskComment: async (taskId: string, content: string, attachmentUrl?: string) => {
        return apiClient.post<BackendResponse<string>>(`/v1/assistant/tasks/my/${taskId}/comments`, { content, attachmentUrl });
    },

    // Operations Task Management APIs for Admins/Supervisors
    getAdminOperationsTasks: async (filters?: {
        search?: string;
        assigneeId?: string;
        status?: number;
        priority?: number;
    }) => {
        const params = new URLSearchParams();
        if (filters?.search) params.append('search', filters.search);
        if (filters?.assigneeId) params.append('assigneeId', filters.assigneeId);
        if (filters?.status) params.append('status', filters.status.toString());
        if (filters?.priority) params.append('priority', filters.priority.toString());

        const query = params.toString() ? `?${params.toString()}` : '';
        return apiClient.get<BackendResponse<TaskItemDto[]>>(`/admin/operations/tasks${query}`);
    },

    createAdminOperationsTask: async (taskData: {
        title: string;
        description?: string;
        assigneeId: string;
        priority: number;
        dueDate?: string;
    }) => {
        return apiClient.post<BackendResponse<string>>('/admin/operations/tasks', taskData);
    },

    resolveAdminOperationsTaskApproval: async (taskId: string, approve: boolean, rejectionReason?: string) => {
        return apiClient.post<BackendResponse<boolean>>(`/admin/operations/tasks/${taskId}/resolve`, { approve, rejectionReason });
    }
};

// Dummy type alias to prevent TS errors on resolveTask
type Guid = string;
