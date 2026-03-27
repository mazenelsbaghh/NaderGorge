import apiClient from './api-client';

export type CodeType = 'Package' | 'Term' | 'Month' | 'Lesson' | 'Video' | 'Gamification' | 'Exam' | 'Balance';

export interface BalanceTransactionDto {
  id: string;
  amount: number;
  balanceAfter: number;
  transactionType: string;
  description: string;
  createdAt: string;
}

export interface StudentBalanceDto {
  currentBalance: number;
  recentTransactions: BalanceTransactionDto[];
}

class BalanceService {
  async getBalance(): Promise<StudentBalanceDto> {
    try {
      const response = await apiClient.get('/student/balance');
      return response.data?.data;
    } catch (error: any) {
      throw new Error(error.response?.data?.message || 'فشل في استرجاع الرصيد');
    }
  }

  async purchaseContent(contentType: CodeType, contentId: string): Promise<boolean> {
    try {
      const response = await apiClient.post('/student/balance/purchase', { contentType, contentId });
      return response.data?.success;
    } catch (error: any) {
      throw new Error(error.response?.data?.message || 'فشل في عملية الشراء');
    }
  }
}

export const balanceService = new BalanceService();
