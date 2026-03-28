import apiClient from './api-client';

// ── Code Types ───────────────────────────────────────────────────────────────
export type CodeType = 'Package' | 'Term' | 'Month' | 'Lesson' | 'Video' | 'Exam' | 'Balance';

export interface CreateCodeGroupData {
  groupName: string;
  codeType: CodeType;
  count: number;
  codeLength: number;
  packageId?: string;
  termId?: string;
  contentSectionId?: string;
  lessonId?: string;
  examId?: string;
  videoTargetIds?: string[];
  balanceAmount?: number;
  discountPercentage?: number;
  expiresAt?: string;
}

export interface CodeGroupResponse {
  codeGroupId: string;
  codesGenerated: number;
  codes: string[];
}

export interface ActivateCodeResponse {
  grantId: string;
  message: string;
  grantType: CodeType;
  redirectUrl?: string;
}

// ── Generic API Response ─────────────────────────────────────────────────────
export interface ApiResponse<T = any> {
  success: boolean;
  message: string;
  data?: T;
  errors?: string[];
}

// ── Code Service ─────────────────────────────────────────────────────────────
export const codeService = {
  /** Create a new code group with the specified type and targets */
  createCodeGroup: (data: CreateCodeGroupData) =>
    apiClient.post<ApiResponse<CodeGroupResponse>>('/admin/codes/bulk-generate', data),

  /** List all code groups */
  listCodeGroups: () => apiClient.get<ApiResponse>('/admin/codes/groups'),

  /** Get codes in a code group */
  getCodeGroupDetails: (groupId: string) =>
    apiClient.get<ApiResponse>(`/admin/codes/groups/${groupId}/details`),

  /** Redeem a code (manual entry) */
  redeemCode: (code: string) =>
    apiClient.post<ApiResponse<ActivateCodeResponse>>('/codes/activate', { code }),
};
