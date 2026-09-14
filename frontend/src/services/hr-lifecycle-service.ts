import apiClient from './api-client';
export interface EmployeeDocumentDto { id: string; category: string; name: string; issuedOn?: string | null; expiresOn?: string | null; latestVersion?: number | null; latestHash?: string | null; }
export interface EmployeeAssetDto { id: string; assetId: string; asset: string; code: string; serialNumber?: string | null; state: string; assignedAt: string; assignedCondition: string; returnedAt?: string | null; returnCondition?: string | null; }
export const hrLifecycleService = {
  myDocuments: async (): Promise<EmployeeDocumentDto[]> => (await apiClient.get('/hr/self/documents')).data ?? [],
  myAssets: async (): Promise<EmployeeAssetDto[]> => (await apiClient.get('/hr/self/assets')).data ?? [],
  downloadDocument: async (id: string): Promise<string> => { const response = await apiClient.get(`/hr/self/documents/${id}/download`); return response.data?.data; },
};
