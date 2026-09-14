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

interface SubmitRechargeOptions {
  confirmSenderPhone?: boolean;
  onUploadProgress?: (percent: number) => void;
}

export const rechargeService = {
  initiate: async (amount: number, teacherId: string) => {
    const { data } = await apiClient.post<{ success: boolean; data: InitiateRechargeResponse; message: string }>('/student/recharge/initiate', { amount, teacherId });
    return data;
  },

  submit: async (
    rechargeRequestId: string,
    senderPhoneNumber: string,
    screenshot?: File | null,
    options: SubmitRechargeOptions = {},
  ) => {
    const formData = new FormData();
    formData.append('rechargeRequestId', rechargeRequestId);
    formData.append('senderPhoneNumber', senderPhoneNumber);
    formData.append('confirmSenderPhone', String(options.confirmSenderPhone ?? false));
    if (screenshot) formData.append('screenshot', screenshot);

    const { data } = await apiClient.post<{ success: boolean; data: SubmitRechargeResponse; message: string }>(
      '/student/recharge/submit',
      formData,
      {
        // Compressed proof images can still need longer than the global timeout
        // on a slow mobile connection.
        timeout: 120_000,
        onUploadProgress: (progressEvent) => {
          if (!options.onUploadProgress) return;

          const uploadRatio = progressEvent.total
            ? progressEvent.loaded / progressEvent.total
            : progressEvent.progress;
          if (uploadRatio === undefined) return;

          options.onUploadProgress(Math.max(0, Math.min(100, Math.round(uploadRatio * 100))));
        },
      },
    );
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
