import apiClient from './api-client';
import type { AcademicScopeSummary } from '@/lib/academic-labels';

export type GiftTargetType = 'Package' | 'Lesson' | 'Video' | 'Exam' | 'GeneralBalance' | 'TeacherBalance';
export type GiftIssuanceStatus = 'Active' | 'PartiallySuccessful' | 'Completed' | 'Expired' | 'Revoked';

export interface GiftLookupDto {
  id: string;
  name: string;
  context?: string | null;
  academicScopes?: AcademicScopeSummary[] | null;
}

export interface GiftRecipientResultDto {
  studentId: string;
  studentName: string;
  status: string;
  outcomeCode: string;
  outcomeMessage?: string | null;
  usesConsumed: number;
  maxUses?: number | null;
}

export interface GiftListItemDto {
  id: string;
  targetType: GiftTargetType;
  targetName: string;
  status: GiftIssuanceStatus;
  issuerName: string;
  recipientCount: number;
  successfulCount: number;
  originalValue?: number | null;
  availableValue?: number | null;
  expiresAt?: string | null;
  issuedAt: string;
}

export interface GiftPageDto {
  items: GiftListItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface GiftDetailsDto {
  id: string;
  requestId: string;
  targetType: GiftTargetType;
  targetName: string;
  status: GiftIssuanceStatus;
  issuerName: string;
  reason: string;
  amount?: number | null;
  originalValue?: number | null;
  availableValue?: number | null;
  availableAmount: number;
  consumedAmount: number;
  expiredAmount: number;
  revokedAmount: number;
  expiresAt?: string | null;
  maxUses?: number | null;
  issuedAt: string;
  academicScopes?: AcademicScopeSummary[] | null;
  recipients: GiftRecipientResultDto[];
}

export interface IssueGiftResultDto {
  id: string;
  requestId: string;
  targetType: GiftTargetType;
  status: GiftIssuanceStatus;
  targetName: string;
  isReplay: boolean;
  academicScopes?: AcademicScopeSummary[] | null;
  recipients: GiftRecipientResultDto[];
}

export interface IssueGiftPayload {
  requestId: string;
  targetType: GiftTargetType;
  targetId?: string | null;
  teacherId?: string | null;
  amount?: number | null;
  expiresAt?: string | null;
  maxUses?: number | null;
  studentIds: string[];
  reason: string;
}

function unwrap<T>(response: { data?: { data?: T } }): T {
  const data = response.data?.data;
  if (data === undefined) throw new Error('استجابة الخادم غير مكتملة.');
  return data;
}

export const giftTargetLabels: Record<GiftTargetType, string> = {
  Package: 'باكدج',
  Lesson: 'حصة',
  Video: 'فيديو',
  Exam: 'امتحان',
  GeneralBalance: 'رصيد عام من المنصة',
  TeacherBalance: 'رصيد مخصص لمدرس',
};

export const adminGiftsService = {
  async list(params: { search?: string; targetType?: GiftTargetType | ''; status?: GiftIssuanceStatus | ''; page?: number } = {}) {
    const query = {
      search: params.search || undefined,
      targetType: params.targetType || undefined,
      status: params.status || undefined,
      page: params.page,
    };
    return unwrap<GiftPageDto>(await apiClient.get('/admin/gifts', { params: query }));
  },
  async details(id: string) {
    return unwrap<GiftDetailsDto>(await apiClient.get(`/admin/gifts/${id}`));
  },
  async issue(payload: IssueGiftPayload) {
    return unwrap<IssueGiftResultDto>(await apiClient.post('/admin/gifts', payload));
  },
  async revoke(id: string, reason: string) {
    return unwrap<{ id: string; changed: boolean; status: GiftIssuanceStatus; revokedAmount: number }>(
      await apiClient.post(`/admin/gifts/${id}/revoke`, { reason }),
    );
  },
  async students(search = '') {
    return unwrap<GiftLookupDto[]>(await apiClient.get('/admin/gifts/lookups/students', { params: { search } }));
  },
  async teachers(search = '') {
    return unwrap<GiftLookupDto[]>(await apiClient.get('/admin/gifts/lookups/teachers', { params: { search } }));
  },
  async targets(targetType: GiftTargetType, teacherId?: string, search = '') {
    return unwrap<GiftLookupDto[]>(await apiClient.get('/admin/gifts/lookups/targets', { params: { targetType, teacherId, search } }));
  },
};
