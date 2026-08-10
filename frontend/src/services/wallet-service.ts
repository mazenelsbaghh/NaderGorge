import apiClient from '@/services/api-client';

export interface WalletDto {
  id: string;
  phoneNumber: string;
  label: string;
  dailyLimit: number;
  monthlyLimit: number;
  currentBalance: number;
  pairingToken: string;
  deviceStatus: string;
  lastSeenAt?: string;
  isActive: boolean;
  smsSenderFilters: string[];
  dailyReceived: number;
  monthlyReceived: number;
  totalReceived: number;
  createdAt: string;
}

export interface CreateWalletDto {
  phoneNumber: string;
  label: string;
  dailyLimit: number;
  monthlyLimit: number;
  smsSenderFilters: string[];
}

export interface UpdateWalletLimitsDto {
  label: string;
  dailyLimit: number;
  monthlyLimit: number;
  smsSenderFilters: string[];
}

export interface AdminRechargeRequestDto {
  id: string;
  userId: string;
  studentName: string;
  studentPhoneNumber: string;
  studentBalance: number;
  teacherBalance: number;
  hasPreviousRequest: boolean;
  previousRequestStatus?: number | string;
  previousRequestCreatedAt?: string;
  walletId: string;
  walletLabel: string;
  walletPhoneNumber: string;
  amount: number;
  teacherId?: string;
  teacherName?: string;
  senderPhoneNumber: string;
  originalSenderPhoneNumber?: string;
  requiresSenderPhoneConfirmation: boolean;
  screenshotUrl?: string;
  status: number | string; // RechargeRequestStatus
  createdAt: string;
  matchDiagnosis?: AdminRechargeMatchDiagnosisDto | null;
  resolvedAt?: string;
  resolvedByUserId?: string;
  resolvedByUserName?: string;
  rejectionReason?: string;
  matchedSmsLogId?: string;
  reservationExpiresAt?: string;
}

export type RechargeMatchDiagnosisCode =
  | 'AwaitingEvidence'
  | 'MissingTeacherScope'
  | 'EligibleWaiting'
  | 'MultipleExactSms'
  | 'CompetingPendingRequests'
  | 'SmsClaimedByAnotherRequest'
  | 'OutsideWindow'
  | 'AmountMismatch'
  | 'PhoneMismatch'
  | 'NoCandidate';

export interface AdminRechargeMatchDiagnosisDto {
  code: RechargeMatchDiagnosisCode;
  exactSmsCount: number;
  competingRequestCount: number;
  candidate?: AdminRechargeMatchCandidateDto | null;
}

export interface AdminRechargeMatchCandidateDto {
  smsLogId: string;
  walletId: string;
  walletLabel: string;
  amount?: number | null;
  senderPhoneNumber: string;
  receivedAt: string;
  timeOffsetMinutes: number;
  outsideWindowByMinutes: number;
  matchingDigits: number;
  hasSingleDigitMismatchPattern: boolean;
  matchingDigitsBeforeMismatch: number;
  matchingDigitsAfterMismatch: number;
  amountMatches: boolean;
  phoneMatches: boolean;
  withinWindow: boolean;
  sameWallet: boolean;
  isMatched: boolean;
  matchedRechargeRequestId?: string;
}

export interface AdminIncomingSmsLogDto {
  id: string;
  walletId: string;
  walletLabel: string;
  walletPhoneNumber: string;
  sender: string;
  body: string;
  receivedAt: string;
  parsedAmount?: number;
  parsedSenderPhone?: string;
  transferReference?: string;
  isMatched: boolean;
  matchedRechargeRequestId?: string;
  matchedStudentName?: string;
  matchedStudentPhoneNumber?: string;
  deduplicationHash: string;
}

export interface RechargeShiftReviewItemDto {
  rechargeRequestId: string;
  studentId: string;
  studentName: string;
  studentPhoneNumber: string;
  amount: number;
  balanceScope: string;
  teacherName?: string;
  balanceBefore?: number;
  balanceAfter?: number;
  currentBalance: number;
  acceptanceMethod: 'يدوي' | 'آلي';
  resolvedAt: string;
  resolvedByUserId?: string;
  resolvedByUserName: string;
  walletId: string;
  walletLabel: string;
  walletPhoneNumber: string;
  senderPhoneNumber: string;
  matchedSmsLogId?: string;
  suspectedDuplicate: boolean;
  duplicateReason?: string;
  isReversed: boolean;
  canReverse: boolean;
  reverseBlockedReason?: string;
}

export interface RechargeShiftReviewDto {
  items: RechargeShiftReviewItemDto[];
  acceptedCount: number;
  manualCount: number;
  automaticCount: number;
  suspectedDuplicateCount: number;
  totalAmount: number;
}

export interface RechargeSmsSuggestionDto {
  smsLogId: string;
  walletId: string;
  walletLabel: string;
  walletPhoneNumber: string;
  amount?: number;
  senderPhoneNumber: string;
  transferReference?: string;
  receivedAt: string;
  isMatched: boolean;
  matchedRechargeRequestId?: string;
  matchedStudentName?: string;
  matchedStudentPhoneNumber?: string;
  matchScore: number;
  matchReasons: string[];
}

export interface RechargeMessageConflictDto {
  rechargeRequestId: string;
  studentName: string;
  studentPhoneNumber: string;
  amount: number;
  senderPhoneNumber: string;
  walletLabel: string;
  createdAt: string;
  conflictType: 'ClaimedByAnotherStudent' | 'ReceivedOnDifferentWallet';
  conflictDescription: string;
  candidates: RechargeSmsSuggestionDto[];
}

export const walletService = {
  getWallets: async () => {
    const { data } = await apiClient.get<{ success: boolean; data: WalletDto[] }>('/admin/wallets');
    return data.data;
  },

  createWallet: async (dto: CreateWalletDto) => {
    const { data } = await apiClient.post<{ success: boolean; data: WalletDto }>('/admin/wallets', dto);
    return data;
  },

  toggleWallet: async (id: string, isActive: boolean) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(`/admin/wallets/${id}/toggle`, { isActive });
    return data;
  },

  regenerateToken: async (id: string) => {
    const { data } = await apiClient.post<{ success: boolean; data: string; message: string }>(`/admin/wallets/${id}/regenerate-token`);
    return data;
  },

  updateLimits: async (id: string, dto: UpdateWalletLimitsDto) => {
    const { data } = await apiClient.put<{ success: boolean; message: string }>(`/admin/wallets/${id}/limits`, dto);
    return data;
  },

  getRechargeRequests: async (status?: number) => {
    const url = status !== undefined
      ? `/admin/wallets/recharge-requests?status=${status}`
      : '/admin/wallets/recharge-requests';
    const { data } = await apiClient.get<{ success: boolean; data: AdminRechargeRequestDto[] }>(url);
    return data.data;
  },

  getUnmatchedSms: async () => {
    const { data } = await apiClient.get<{ success: boolean; data: AdminIncomingSmsLogDto[] }>('/admin/wallets/unmatched-sms');
    return data.data;
  },

  getSmsLogs: async (params: { search?: string; isMatched?: boolean; walletId?: string; page?: number; pageSize?: number }) => {
    const { data } = await apiClient.get<{ items: AdminIncomingSmsLogDto[]; totalCount: number; page: number; pageSize: number }>('/admin/wallets/sms-logs', { params });
    return data;
  },

  getRechargeSmsSuggestions: async (id: string, search?: string) => {
    const { data } = await apiClient.get<{ success: boolean; data: RechargeSmsSuggestionDto[] }>(
      `/admin/wallets/recharge-requests/${id}/sms-suggestions`, { params: { search } }
    );
    return data.data;
  },

  getRechargeMessageConflicts: async () => {
    const { data } = await apiClient.get<{ success: boolean; data: RechargeMessageConflictDto[] }>(
      '/admin/wallets/recharge-message-conflicts'
    );
    return data.data;
  },

  reassignRechargeSms: async (id: string, smsLogId: string, reason: string) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(
      `/admin/wallets/recharge-requests/${id}/reassign-sms`, { smsLogId, reason }
    );
    return data;
  },

  resolveRechargeRequest: async (
    id: string,
    approve: boolean,
    rejectionReason?: string,
    smsLogId?: string,
    walletId?: string
  ) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(
      `/admin/wallets/recharge-requests/${id}/resolve`,
      { approve, rejectionReason, smsLogId, walletId }
    );
    return data;
  },

  getRechargeShiftReview: async (params: { from: string; to: string; walletId?: string; resolvedByUserId?: string }) => {
    const { data } = await apiClient.get<{ success: boolean; data: RechargeShiftReviewDto }>('/admin/wallets/recharge-shift-review', { params });
    return data.data;
  },

  reverseRechargeCredit: async (id: string, reason: string) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(`/admin/wallets/recharge-requests/${id}/reverse-credit`, { reason });
    return data;
  },
};
