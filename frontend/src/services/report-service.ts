import apiClient from './api-client';

export interface WarningDto {
    severity: string;
    reason: string;
    generatedAt: string;
}

export interface ParentReportDto {
    studentId: string;
    studentName: string;
    overallStatus: string;
    completedLessonsCount: number;
    passedExamsCount: number;
    failedExamsCount: number;
    recentWarnings: WarningDto[];
}

export const reportService = {
    getParentSummary: async (studentId: string, token: string) => {
        return apiClient.get<{ data: ParentReportDto }>(`/parent/reports/${studentId}/summary`, {
            params: { token },
        });
    },
    createParentReportLink: async (studentId: string) => {
        return apiClient.post<{ data: { token: string; expiresInDays: number } }>(`/parent/reports/${studentId}/links`);
    },
};
