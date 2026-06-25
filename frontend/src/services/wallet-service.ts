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
  walletId: string;
  walletLabel: string;
  walletPhoneNumber: string;
  amount: number;
  senderPhoneNumber: string;
  screenshotUrl?: string;
  status: number; // RechargeRequestStatus
  createdAt: string;
  resolvedAt?: string;
  resolvedByUserId?: string;
  resolvedByUserName?: string;
  rejectionReason?: string;
  matchedSmsLogId?: string;
  reservationExpiresAt?: string;
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
  isMatched: boolean;
  matchedRechargeRequestId?: string;
  deduplicationHash: string;
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

  resolveRechargeRequest: async (
    id: string, 
    approve: boolean, 
    rejectionReason?: string, 
    smsLogId?: string
  ) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(
      `/admin/wallets/recharge-requests/${id}/resolve`, 
      { approve, rejectionReason, smsLogId }
    );
    return data;
  },
};
