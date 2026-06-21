import apiClient from './api-client';

export interface AICatalogItem { key: string; label: string; description: string; requiresVerification: boolean }
export interface AICatalogs { readableData: AICatalogItem[]; actions: AICatalogItem[]; lookupKeys: AICatalogItem[]; verificationQuestions: AICatalogItem[] }
export interface AIPolicy {
  id: string; versionNumber: number; status: 'Draft' | 'Published' | 'Superseded'; isEnabled: boolean;
  systemInstructions: string; readableDataKeys: string[]; actionKeys: string[]; lookupKeys: string[];
  verificationQuestionKeys: string[]; verificationRequiredCorrect: number; verificationMaxAttempts: number;
  pendingActionExpirySeconds: number; inactivityMinutes: number; inactivityWarningGraceSeconds: number;
  version: number; publishedAt?: string;
}
export interface AIConfig { draft?: AIPolicy; published?: AIPolicy; catalogs: AICatalogs }
export type SaveAIDraft = Omit<AIPolicy, 'id' | 'versionNumber' | 'status' | 'isEnabled' | 'version' | 'publishedAt'> & { expectedVersion?: number };
interface ApiResponse<T> { data: T }

export const liveSupportAIService = {
  getConfig: () => apiClient.get<ApiResponse<AIConfig>>('/live-support/admin/ai/config').then(response => response.data.data),
  saveDraft: (payload: SaveAIDraft) => apiClient.put<ApiResponse<AIPolicy>>('/live-support/admin/ai/config', payload).then(response => response.data.data),
  publish: (expectedVersion: number) => apiClient.post<ApiResponse<AIPolicy>>('/live-support/admin/ai/publish', { expectedVersion }).then(response => response.data.data),
  disable: () => apiClient.post('/live-support/admin/ai/disable'),
};
