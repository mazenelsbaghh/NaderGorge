import apiClient from './api-client';
export interface RecruitmentCandidateDto { id: string; fullName: string; phoneNumber: string; email?: string | null; stage: string; cvAssetReference?: string | null; employeeProfileId?: string | null; version: number; offers: Array<{ id: string; offerNumber: string; baseSalary: number; currency: string; proposedStartDate: string; state: string; version: number }>; }
export interface RequisitionDto { id: string; requisitionNumber: string; title: string; openings: number; state: string; requirements: string; candidates: RecruitmentCandidateDto[]; }
export interface LifecycleTaskDto { id: string; employeeId: string; employee: string; phase: string; title: string; dueAt: string; state: string; overdue: boolean; }
export const hrRecruitmentService = {
  board: async (): Promise<RequisitionDto[]> => (await apiClient.get('/hr/admin/recruitment/board')).data ?? [],
  createRequisition: async (payload: { title: string; organizationUnitId?: string | null; openings: number; requirements: string }) => (await apiClient.post('/hr/admin/recruitment/requisitions', payload)).data,
  addCandidate: async (id: string, payload: { fullName: string; phoneNumber: string; email?: string | null; cvAssetReference?: string | null }) => (await apiClient.post(`/hr/admin/recruitment/requisitions/${id}/candidates`, payload)).data,
  createOffer: async (candidateId: string, payload: { baseSalary: number; currency: string; proposedStartDate: string }) => (await apiClient.post(`/hr/admin/recruitment/candidates/${candidateId}/offers`, payload)).data,
  acceptOffer: async (id: string, expectedVersion: number) => (await apiClient.post(`/hr/admin/recruitment/offers/${id}/accept`, { expectedVersion })).data,
  hire: async (candidateId: string, offerId: string, temporaryPassword: string) => (await apiClient.post(`/hr/admin/recruitment/candidates/${candidateId}/hire`, { offerId, temporaryPassword })).data,
  tasks: async (): Promise<LifecycleTaskDto[]> => (await apiClient.get('/hr/admin/lifecycle/tasks')).data ?? [],
  startOffboarding: async (payload: { employeeId: string; lastWorkingDate: string; reason: string }) => (await apiClient.post('/hr/admin/lifecycle/offboarding', payload)).data,
};
