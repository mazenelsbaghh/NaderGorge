import apiClient from '@/services/api-client';

export interface InitiateRechargeResponse {
  rechargeRequestId: string;
  reviewCode: string;
  walletPhoneNumber: string;
  walletLabel: string;
  expirationTime: string;
}

export interface SubmitRechargeResponse {
  isMatched: boolean;
  requiresSenderPhoneConfirmation: boolean;
  originalSenderPhoneNumber: string;
  message: string;
  reviewCode: string;
}

export interface StudentRechargeRequestDto {
  id: string;
  reviewCode: string;
  amount: number;
  teacherId?: string;
  teacherName?: string;
  senderPhoneNumber: string;
  originalSenderPhoneNumber?: string;
  requiresSenderPhoneConfirmation: boolean;
  walletLabel: string;
  walletPhoneNumber: string;
  status: number | string;
  screenshotUrl?: string;
  rejectionReason?: string;
  createdAt: string;
  resolvedAt?: string;
  reservationExpiresAt?: string;
}

export const rechargeService = {
  initiate: async (amount: number, teacherId?: string) => {
    const { data } = await apiClient.post<{ success: boolean; data: InitiateRechargeResponse; message: string }>('/student/recharge/initiate', { amount, teacherId });
    return data;
  },

  submit: async (
    rechargeRequestId: string,
    senderPhoneNumber: string,
    screenshot?: File | null,
    confirmSenderPhone = false,
  ) => {
    const formData = new FormData();
    formData.append('rechargeRequestId', rechargeRequestId);
    formData.append('senderPhoneNumber', senderPhoneNumber);
    formData.append('confirmSenderPhone', String(confirmSenderPhone));
    if (screenshot) formData.append('screenshot', screenshot);

    const { data } = await apiClient.post<{ success: boolean; data: SubmitRechargeResponse; message: string }>('/student/recharge/submit', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return data;
  },

  getMyRequests: async () => {
    const { data } = await apiClient.get<{ success: boolean; data: StudentRechargeRequestDto[] }>('/student/recharge/requests');
    return data.data || [];
  },

  cancel: async (rechargeRequestId: string, reason: string) => {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(
      `/student/recharge/requests/${rechargeRequestId}/cancel`,
      { reason },
    );
    return data;
  },
};
