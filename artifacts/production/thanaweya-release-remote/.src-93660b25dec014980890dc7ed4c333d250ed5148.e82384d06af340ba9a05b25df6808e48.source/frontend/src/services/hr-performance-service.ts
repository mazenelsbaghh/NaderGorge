import apiClient from './api-client';
export interface PerformanceCycleDto { id: string; name: string; startsOn: string; endsOn: string; state: string; goals: Array<{ id: string; name: string; weight: number }>; }
export interface EmployeeCaseDto { id: string; caseNumber: string; employeeId: string; employee: string; title: string; description: string; isConfidential: boolean; state: string; version: number; actions: Array<{ id: string; type: string; financialAmount?: number | null; reason: string; payrollLineItemId?: string | null }>; }
export const hrPerformanceService = {
  cycles: async (): Promise<PerformanceCycleDto[]> => (await apiClient.get('/hr/admin/performance/cycles')).data ?? [],
  createCycle: async (payload: { name: string; startsOn: string; endsOn: string; goals: Array<{ name: string; weight: number }> }) => (await apiClient.post('/hr/admin/performance/cycles', payload)).data,
  activateCycle: async (id: string) => (await apiClient.post(`/hr/admin/performance/cycles/${id}/activate`, {})).data,
  publishReview: async (payload: { cycleId: string; employeeId: string; scores: Record<string, number> }) => (await apiClient.post('/hr/admin/performance/reviews', payload)).data,
  cases: async (): Promise<EmployeeCaseDto[]> => (await apiClient.get('/hr/admin/cases')).data ?? [],
  openCase: async (payload: { employeeId: string; title: string; description: string; isConfidential: boolean }) => (await apiClient.post('/hr/admin/cases', payload)).data,
  decideCase: async (id: string, payload: { type: string; financialAmount?: number | null; reason: string; expectedVersion: number }) => (await apiClient.post(`/hr/admin/cases/${id}/decision`, payload)).data,
};
