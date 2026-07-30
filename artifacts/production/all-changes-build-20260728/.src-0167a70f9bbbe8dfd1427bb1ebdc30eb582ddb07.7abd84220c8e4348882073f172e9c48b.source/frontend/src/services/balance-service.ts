import apiClient from './api-client';

export type CodeType = 'Package' | 'Term' | 'Month' | 'Lesson' | 'Video' | 'Gamification' | 'Exam' | 'Balance';

export interface BalanceTransactionDto {
  id: string;
  amount: number;
  balanceAfter: number;
  transactionType: string;
  description: string;
  createdAt: string;
  affectsBalance: boolean;
}

export interface StudentBalanceDto {
  currentBalance: number;
  recentTransactions: BalanceTransactionDto[];
  promotionalBalance: number;
  promotionalAllocations: PromotionalBalanceDto[];
}

export interface PromotionalBalanceDto {
  id: string;
  originalAmount: number;
  availableAmount: number;
  consumedAmount: number;
  expiredAmount: number;
  revokedAmount: number;
  teacherId?: string | null;
  teacherName?: string | null;
  teacherProfileImageUrl?: string | null;
  expiresAt?: string | null;
  purchaseCount: number;
  maxPurchaseCount?: number | null;
  status: string;
}

export interface PurchaseFundingPreviewDto {
  price: number;
  couponDiscountAmount: number;
  printableCodeDiscountAmount: number;
  discountedPrice: number;
  eligiblePromotionalAmount: number;
  promotionalAmountToUse: number;
  paidAmountToUse: number;
  currentPaidBalance: number;
  isSufficient: boolean;
}

export interface PurchaseDiscountOptions {
  couponCodes?: string[];
  printableCodes?: string[];
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

  async purchaseContent(contentType: CodeType, contentId: string, discounts: PurchaseDiscountOptions = {}): Promise<boolean> {
    try {
      const response = await apiClient.post('/student/balance/purchase', {
        contentType,
        contentId,
        couponCodes: discounts.couponCodes,
        printableCodes: discounts.printableCodes,
      });
      return response.data?.success;
    } catch (error: any) {
      throw new Error(error.response?.data?.message || 'فشل في عملية الشراء');
    }
  }

  async getPurchasePreview(contentType: CodeType, contentId: string, discounts: PurchaseDiscountOptions = {}): Promise<PurchaseFundingPreviewDto> {
    try {
      const params = new URLSearchParams();
      params.set('contentType', contentType);
      params.set('contentId', contentId);
      discounts.couponCodes?.forEach((code) => params.append('couponCodes', code));
      discounts.printableCodes?.forEach((code) => params.append('printableCodes', code));

      const response = await apiClient.get('/student/balance/purchase-preview', {
        params,
      });
      return response.data?.data;
    } catch (error: any) {
      throw new Error(error.response?.data?.message || 'فشل في حساب طريقة الدفع');
    }
  }
}

export const balanceService = new BalanceService();
