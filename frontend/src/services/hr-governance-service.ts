import apiClient from './api-client';
export interface MigrationRowInput { sourceType: string; sourceId: string; targetId: string; amount: number; sourceHash: string; }
export interface MigrationBatchDto { id: string; module: string; sourceSystem: string; state: string; sourceCount: number; targetCount: number; sourceTotal: number; targetTotal: number; sourceHash: string; targetHash?: string | null; reportJson: string; createdAt: string; }
export interface RolloutDto { id: string; module: string; state: string; readTarget: string; writeTarget: string; reconciliationBatchId?: string | null; reason?: string | null; }
export interface WorkforceRowDto {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  status: string;
  hireDate: string;
  organizationUnit?: string | null;
  shiftName?: string | null;
  attendanceDays: number;
  completedAttendanceDays: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  workedMinutes: number;
  approvedLeaveDays: number;
  lastNetPayroll?: number | null;
  supportConversations: number;
  closedSupportConversations: number;
  respondedSupportConversations: number;
  averageFirstResponseMinutes?: number | null;
  ratingCount: number;
  averageStudentRating?: number | null;
}
export const hrGovernanceService = {
  status: async (): Promise<{ rollouts: RolloutDto[]; batches: MigrationBatchDto[]; conflicts: unknown[] }> => (await apiClient.get('/hr/governance/migration')).data,
  dryRun: async (module: string, rows: MigrationRowInput[]) => (await apiClient.post('/hr/governance/migration/dry-run', { module, sourceSystem: 'legacy', rows })).data,
  reconcile: async (batchId: string, module: string, rows: MigrationRowInput[]) => (await apiClient.post(`/hr/governance/migration/${batchId}/reconcile`, { module, sourceSystem: 'legacy', rows })).data,
  activate: async (batchId: string, module: string, reason: string) => (await apiClient.post(`/hr/governance/migration/${batchId}/activate`, { module, reason })).data,
  rollback: async (module: string, reason: string) => (await apiClient.post('/hr/governance/migration/rollback', { module, reason })).data,
  workforce: async (params: { from?: string; to?: string; organizationUnitId?: string; search?: string; page?: number; pageSize?: number }): Promise<{ items: WorkforceRowDto[]; total: number; page: number; pageSize: number }> => (await apiClient.get('/hr/governance/reports/workforce', { params })).data,
  exportWorkforce: async (params: { from?: string; to?: string; search?: string; reason: string }) => (await apiClient.get('/hr/governance/reports/workforce/export', { params, responseType: 'blob' })).data as Blob,
};
