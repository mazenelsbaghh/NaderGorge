import apiClient from '@/services/api-client';

export interface InitiateRechargeResponse {
  rechargeRequestId: string;
  walletPhoneNumber: string;
  walletLabel: string;
  expirationTime: string;
}

export interface SubmitRechargeResponse {
  isMatched: boolean;
  message: string;
}

export const rechargeService = {
  initiate: async (amount: number) => {
    const { data } = await apiClient.post<{ success: boolean; data: InitiateRechargeResponse; message: string }>('/student/recharge/initiate', { amount });
    return data;
  },

  submit: async (rechargeRequestId: string, senderPhoneNumber: string, screenshot: File) => {
    const formData = new FormData();
    formData.append('rechargeRequestId', rechargeRequestId);
    formData.append('senderPhoneNumber', senderPhoneNumber);
    formData.append('screenshot', screenshot);

    const { data } = await apiClient.post<{ success: boolean; data: SubmitRechargeResponse; message: string }>('/student/recharge/submit', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return data;
  },
};
