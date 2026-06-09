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

export interface ApiResponse<T = any> {
  data: T;
  success: boolean;
  message: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogDetailDto {
  id: string;
  action: string;
  entityType: string;
  entityId?: string;
  performedByUserId?: string;
  performedByUserName?: string;
  performedByUserPhone?: string;
  oldValues?: string;
  newValues?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface AttendanceKpiDto {
  totalLogs: number;
  presentCount: number;
  lateCount: number;
  absentCount: number;
  presentRate: number;
  lateRate: number;
  absentRate: number;
}

export interface TaskKpiDto {
  totalTasks: number;
  completedCount: number;
  pendingCount: number;
  overdueCount: number;
  completionRate: number;
}

export interface CrmOutcomeKpiDto {
  outcome: string;
  count: number;
}

export interface MediaKpiDto {
  totalItems: number;
  publishedCount: number;
  averageProductionDays: number;
}

export interface PaymentKpiDto {
  totalTransactions: number;
  autoMatchedCount: number;
  couponActivatedCount: number;
  autoMatchRate: number;
}

export interface PayrollStatusKpiDto {
  status: string;
  count: number;
}

export interface KpiDashboardDto {
  attendance: AttendanceKpiDto;
  tasks: TaskKpiDto;
  crmOutcomes: CrmOutcomeKpiDto[];
  media: MediaKpiDto;
  payments: PaymentKpiDto;
  payrollStatus: PayrollStatusKpiDto[];
}

export const reportService = {
  getAuditLogs: async (params: {
    startDate?: string;
    endDate?: string;
    performedByUserId?: string;
    entityType?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<AuditLogDetailDto>> => {
    const res = await apiClient.get<ApiResponse<PagedResult<AuditLogDetailDto>>>(
      '/admin/reports/audit',
      { params }
    );
    return res.data?.data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
  },

  getKpiDashboard: async (params: {
    startDate?: string;
    endDate?: string;
    roleName?: string;
    employeeId?: string;
  }): Promise<KpiDashboardDto> => {
    const res = await apiClient.get<ApiResponse<KpiDashboardDto>>(
      '/admin/reports/kpi',
      { params }
    );
    return res.data?.data ?? {
      attendance: { totalLogs: 0, presentCount: 0, lateCount: 0, absentCount: 0, presentRate: 0, lateRate: 0, absentRate: 0 },
      tasks: { totalTasks: 0, completedCount: 0, pendingCount: 0, overdueCount: 0, completionRate: 0 },
      crmOutcomes: [],
      media: { totalItems: 0, publishedCount: 0, averageProductionDays: 0 },
      payments: { totalTransactions: 0, autoMatchedCount: 0, couponActivatedCount: 0, autoMatchRate: 0 },
      payrollStatus: []
    };
  },

  getParentSummary: async (studentId: string, token: string) => {
    return apiClient.get<{ data: ParentReportDto }>(`/parent/reports/${studentId}/summary`, {
      params: { token },
    });
  },

  createParentReportLink: async (studentId: string) => {
    return apiClient.post<{ data: { token: string; expiresInDays: number } }>(`/parent/reports/${studentId}/links`);
  }
};
