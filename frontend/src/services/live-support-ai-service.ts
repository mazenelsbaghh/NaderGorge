import apiClient from './api-client';
import type { LiveSupportAdminConversation } from './live-support-service';

export interface AICatalogItem { key: string; label: string; description: string; requiresVerification: boolean }
export interface AICatalogs { readableData: AICatalogItem[]; actions: AICatalogItem[]; lookupKeys: AICatalogItem[]; verificationQuestions: AICatalogItem[] }
export interface AIPolicy {
  id: string; versionNumber: number; status: 'Draft' | 'Published' | 'Superseded'; isEnabled: boolean;
  systemInstructions: string; readableDataKeys: string[]; actionKeys: string[]; lookupKeys: string[];
  verificationQuestionKeys: string[]; verificationRequiredCorrect: number; verificationMaxAttempts: number;
  pendingActionExpirySeconds: number; inactivityMinutes: number; inactivityWarningGraceSeconds: number;
  version: number; publishedAt?: string;
}
export interface AIStats {
  activeConversations: number;
  resolvedIssues: number;
  handoffs: number;
  totalMessagesSent: number;
  successfulActions: number;
}
export interface AIConfig { draft?: AIPolicy; published?: AIPolicy; catalogs: AICatalogs }
export type SaveAIDraft = Omit<AIPolicy, 'id' | 'versionNumber' | 'status' | 'isEnabled' | 'version' | 'publishedAt'> & { expectedVersion?: number };
export type AIStatsPeriod = 'last-24h' | 'last-7d' | 'last-30d' | 'all';
export interface AIKnowledgeRevision {
  entryId: string; revisionId: string; title: string; revisionNumber: number; content: string; sourceLabel?: string;
  isPublished: boolean; validFrom?: string; validUntil?: string; publishedAt?: string;
}
export interface SaveAIKnowledgeRevision {
  entryId?: string; title: string; content: string; sourceLabel?: string; publish: boolean; validFrom?: string; validUntil?: string;
}
export interface AIPreviewDecision {
  schemaVersion: '1'; type: 'reply' | 'propose_action' | 'request_verification' | 'propose_account_creation' | 'request_resolution' | 'handoff';
  messageAr?: string; action?: Record<string, unknown>; verification?: Record<string, unknown>;
  accountCreation?: Record<string, unknown>; resolution?: Record<string, unknown>; handoff?: Record<string, unknown>;
}
export interface AIPreviewResult {
  policyVersionId: string; dryRun: true; knowledgeDocuments: number; allowedDecisionTypes: string[]; safeOutcome: string;
  decision: AIPreviewDecision; decisionHash: string; provider: string; model: string; latencyMs: number;
}
export interface AIEvidenceItem { turnId: string; conversationId: string; at: string; status: string; decisionType?: string; failureCode?: string; provider?: string; model?: string; callbackAttempts: number }
export interface AIEvidencePage { items: AIEvidenceItem[]; nextCursor?: string }
export interface AIReadiness { status: 'healthy' | 'unhealthy'; callbackAuthentication: string; redis: string; worker: string; policy: string }
interface ApiResponse<T> { data: T }

export function getLiveSupportAIError(error: unknown, fallback: string) {
  const message = (error as { response?: { data?: { message?: unknown } } } | null)?.response?.data?.message;
  return typeof message === 'string' ? message : fallback;
}

export const liveSupportAIService = {
  getConfig: () => apiClient.get<ApiResponse<AIConfig>>('/live-support/admin/ai/config').then(response => response.data.data),
  saveDraft: (payload: SaveAIDraft) => apiClient.put<ApiResponse<AIPolicy>>('/live-support/admin/ai/config', payload).then(response => response.data.data),
  publish: (expectedVersion: number) => apiClient.post<ApiResponse<AIPolicy>>('/live-support/admin/ai/publish', { expectedVersion }).then(response => response.data.data),
  disable: (expectedVersion: number) => apiClient.post('/live-support/admin/ai/disable', { expectedVersion }),
  enable: (expectedVersion: number) => apiClient.post<ApiResponse<AIPolicy>>('/live-support/admin/ai/enable', { expectedVersion }).then(response => response.data.data),
  getStats: (period: AIStatsPeriod) => apiClient.get<ApiResponse<AIStats>>('/live-support/admin/ai/stats', { params: { period } }).then(response => response.data.data),
  getActiveConversations: () => apiClient.get<ApiResponse<LiveSupportAdminConversation[]>>('/live-support/admin/ai/active-conversations').then(response => response.data.data),
  getKnowledge: () => apiClient.get<ApiResponse<AIKnowledgeRevision[]>>('/live-support/admin/ai/knowledge').then(response => response.data.data),
  saveKnowledgeRevision: (payload: SaveAIKnowledgeRevision) => apiClient.post<ApiResponse<AIKnowledgeRevision>>('/live-support/admin/ai/knowledge/revisions', payload).then(response => response.data.data),
  linkKnowledge: (policyVersionId: string, revisionIds: string[]) => apiClient.put('/live-support/admin/ai/knowledge/links', { policyVersionId, revisionIds }),
  preview: (message: string, policyVersionId?: string) => apiClient.post<ApiResponse<AIPreviewResult>>('/live-support/admin/ai/preview', { message, policyVersionId }).then(response => response.data.data),
  getEvidence: (period: AIStatsPeriod, cursor?: string, pageSize = 50) => apiClient.get<ApiResponse<AIEvidencePage>>('/live-support/admin/ai/evidence', { params: { period, cursor, pageSize } }).then(response => response.data.data),
  getReadiness: () => apiClient.get<AIReadiness>('/health/ready/ai-live-support').then(response => response.data),
};
