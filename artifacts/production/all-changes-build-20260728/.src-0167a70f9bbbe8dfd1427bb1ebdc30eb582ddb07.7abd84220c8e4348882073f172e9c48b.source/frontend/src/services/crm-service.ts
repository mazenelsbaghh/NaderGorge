import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';

const invalidateCrm = (keys: string[] = ['crm:queues', 'crm:calls', 'crm:reports']) => invalidateMany(keys);

export interface CrmStudentDto {
  studentId: string;
  studentName: string;
  studentPhone: string;
  parentTrackingCode?: string | null;
  crmStatus: string;
  assignedAgentId?: string;
  assignedAgentName?: string;
  priority: string;
  nextFollowUpDate?: string;
  lastCalledAt?: string;
  notes?: string;
}

export interface CrmStudentListResponse {
  items: CrmStudentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CrmCallLogDto {
  id: string;
  studentId: string;
  agentName: string;
  callDate: string;
  outcome: string;
  notes?: string;
  nextFollowUpDate?: string;
}

export interface AgentPerformanceDto {
  agentId: string;
  agentName: string;
  callsMade: number;
  completedCalls: number;
  noAnswerCalls: number;
}

export interface CrmPerformanceReportDto {
  totalCalls: number;
  outcomeBreakdown: Record<string, number>;
  agentPerformance: AgentPerformanceDto[];
}

export interface GetStudentsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  agentId?: string;
  priority?: string;
  onlyOverdue?: boolean;
}

export interface AssignStudentPayload {
  assignedAgentId?: string;
  priority: string;
  notes?: string;
}

export interface LogCallPayload {
  outcome: string;
  notes?: string;
  nextFollowUpDate?: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const crmService = {
  getStudents: (params: GetStudentsParams) => {
    const query = new URLSearchParams();
    if (params.page) query.append('page', params.page.toString());
    if (params.pageSize) query.append('pageSize', params.pageSize.toString());
    if (params.search) query.append('search', params.search);
    if (params.status) query.append('status', params.status);
    if (params.agentId) query.append('agentId', params.agentId);
    if (params.priority) query.append('priority', params.priority);
    if (params.onlyOverdue !== undefined) query.append('onlyOverdue', params.onlyOverdue.toString());

    return apiClient.get<ApiResponse<CrmStudentListResponse>>(`/crm/students?${query.toString()}`)
      .then(r => r.data.data);
  },

  assignStudent: async (studentId: string, payload: AssignStudentPayload) => {
    const response = await apiClient.post<ApiResponse<void>>(`/crm/students/${studentId}/assign`, payload);
    invalidateCrm();
    return response.data;
  },

  logCall: async (studentId: string, payload: LogCallPayload) => {
    const response = await apiClient.post<ApiResponse<void>>(`/crm/students/${studentId}/calls`, payload);
    invalidateCrm(['crm:queues', 'crm:calls', 'crm:reports']);
    return response.data;
  },

  getCallHistory: (studentId: string) =>
    apiClient.get<ApiResponse<CrmCallLogDto[]>>(`/crm/students/${studentId}/history`)
      .then(r => r.data.data),

  getPerformanceReport: () =>
    apiClient.get<ApiResponse<CrmPerformanceReportDto>>('/crm/reports/performance')
      .then(r => r.data.data),
};
