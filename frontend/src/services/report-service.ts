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
    getParentSummary: async (studentId: string) => {
        // Doesn't require auth since it's anonymous for MVP
        return apiClient.get<{ data: ParentReportDto }>(`/parent/reports/${studentId}/summary`);
    }
};
