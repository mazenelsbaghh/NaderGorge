import apiClient from './api-client';
import type {
  CodeGroupFinancialTerms,
  PagedTeacherLedger,
  SettlementPreview,
  TeacherAgreement,
  TeacherFinanceSummary,
  TeacherSettlement,
} from '@/features/teacher-finance-center/types';

export interface ApiResponse<T = any> {
  data: T;
  success: boolean;
  message: string;
}

// DTOs matching backend
export interface PayrollAdjustmentDto {
  id: string;
  type: 'Addition' | 'Deduction';
  amount: number;
  reason: string;
  createdAt: string;
}

export interface PayrollRecordDto {
  id: string;
  employeeProfileId: string;
  employeeName: string;
  month: number;
  year: number;
  basicSalary: number;
  additions: number;
  deductions: number;
  netSalary: number;
  status: 'Draft' | 'Approved' | number;
  approvedByUserId?: string;
  approvedByName?: string;
  approvedAt?: string;
  createdAt: string;
  adjustments: PayrollAdjustmentDto[];
}

export interface AdminPayoutDto {
  id: string;
  teacherId: string;
  teacherName: string;
  amount: number;
  status: 'Pending' | 'Approved' | 'Paid' | 'Rejected' | number;
  rejectionReason?: string;
  approvedByUserId?: string;
  approvedByName?: string;
  approvedAt?: string;
  paidByUserId?: string;
  paidByName?: string;
  paidAt?: string;
  handledByUserId?: string;
  handledByName?: string;
  handledAt?: string;
  createdAt: string;
}

export interface TeacherAccountDto {
  teacherId: string;
  teacherName: string;
  todayEarnings: number;
  totalEarnings: number;
  currentBalance: number;
  reservedBalance: number;
  availableBalance: number;
  debtBalance: number;
  commissionRate: number;
}

export interface TeacherTransactionDto {
  id: string;
  occurredAt: string;
  sourceType: string;
  contentName: string;
  packageName?: string;
  studentName: string;
  studentPhone?: string;
  codeSerialNumber?: number;
  serialNumber?: number;
  grossAmount: number;
  discountAmount: number;
  paidAmount: number;
  teacherShareAmount: number;
  platformShareAmount: number;
  allocationMode: string;
  allocationValue: number;
  reviewStatus: string;
  payoutStatus: string;
  price?: number;
  commissionRate?: number;
  commissionEarned?: number;
  activatedAt?: string;
}

export interface TeacherPayoutDto {
  id: string;
  amount: number;
  status: 'Pending' | 'Approved' | 'Paid' | 'Rejected' | number;
  rejectionReason?: string;
  createdAt: string;
  approvedAt?: string;
  paidAt?: string;
  handledAt?: string;
}

export interface TeacherFinanceDayDto {
  date: string;
  grossAmount: number;
  teacherShareAmount: number;
  platformShareAmount: number;
  transactionCount: number;
  pendingReviewCount: number;
  transactions: TeacherFinanceDayTransactionDto[];
}

export interface TeacherFinanceDayTransactionDto {
  id: string;
  occurredAt: string;
  studentName: string;
  studentPhone?: string;
  contentName: string;
  codeSerialNumber?: number;
  paidAmount: number;
  teacherShareAmount: number;
  sourceType: string;
  reviewStatus: string;
  payoutStatus: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminCodeAccountingDto {
  id: string;
  packageName: string;
  teacherName: string;
  studentName: string;
  serialNumber: number;
  price: number;
  commissionRate: number;
  commissionEarned: number;
  activatedAt: string;
}

export interface AdminTeacherFinancialEventDto {
  allocationId: string;
  eventId: string;
  teacherId: string;
  teacherName: string;
  studentName?: string;
  studentPhone?: string;
  contentNameSnapshot: string;
  codeSerialNumber?: number;
  sourceType: string;
  targetType: string;
  grossAmount: number;
  discountAmount: number;
  paidAmount: number;
  promotionalAmount: number;
  teacherShareAmount: number;
  platformShareAmount: number;
  reviewStatus: string;
  payoutStatus: string;
  occurredAt: string;
}

export const financeService = {
  // --- Admin-only teacher finance center ---
  getTeacherAgreements: async (teacherId: string): Promise<TeacherAgreement[]> => {
    const res = await apiClient.get<ApiResponse<TeacherAgreement[]>>(
      `/admin/teacher-finance-center/teachers/${teacherId}/agreements`,
    );
    return res.data?.data ?? [];
  },

  createTeacherAgreement: async (teacherId: string, payload: Omit<TeacherAgreement, 'id' | 'teacherId' | 'isActive'>): Promise<ApiResponse<{ id: string }>> => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>(
      `/admin/teacher-finance-center/teachers/${teacherId}/agreements`, payload,
    );
    return res.data;
  },

  replaceTeacherAgreement: async (agreementId: string, payload: Omit<TeacherAgreement, 'id' | 'teacherId' | 'isActive'>): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.put<ApiResponse<boolean>>(
      `/admin/teacher-finance-center/agreements/${agreementId}`, payload,
    );
    return res.data;
  },

  getTeacherFinanceSummary: async (teacherId: string): Promise<TeacherFinanceSummary | null> => {
    const res = await apiClient.get<ApiResponse<TeacherFinanceSummary>>(
      `/admin/teacher-finance-center/teachers/${teacherId}/summary`,
    );
    return res.data?.data ?? null;
  },

  getTeacherLedger: async (teacherId: string, params?: { from?: string; to?: string; status?: string; page?: number; pageSize?: number }): Promise<PagedTeacherLedger> => {
    const res = await apiClient.get<ApiResponse<PagedTeacherLedger>>(
      `/admin/teacher-finance-center/teachers/${teacherId}/ledger`, { params },
    );
    return res.data?.data ?? { items: [], total: 0, page: 1, pageSize: 50 };
  },

  previewTeacherSettlement: async (payload: { teacherId: string; periodFrom: string; periodTo: string; note?: string; allocationIds?: string[] }): Promise<SettlementPreview> => {
    const res = await apiClient.post<ApiResponse<SettlementPreview>>('/admin/teacher-finance-center/settlements/preview', payload);
    if (!res.data?.success) throw new Error(res.data?.message || 'تعذر معاينة التسوية');
    return res.data.data;
  },

  createTeacherSettlement: async (payload: { teacherId: string; periodFrom: string; periodTo: string; note?: string; allocationIds?: string[] }): Promise<ApiResponse<{ id: string }>> => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/teacher-finance-center/settlements', payload);
    return res.data;
  },

  getTeacherSettlement: async (settlementId: string): Promise<TeacherSettlement | null> => {
    const res = await apiClient.get<ApiResponse<TeacherSettlement>>(`/admin/teacher-finance-center/settlements/${settlementId}`);
    return res.data?.data ?? null;
  },

  reviewTeacherSettlement: async (settlementId: string): Promise<ApiResponse<boolean>> => (await apiClient.post<ApiResponse<boolean>>(`/admin/teacher-finance-center/settlements/${settlementId}/review`)).data,
  approveTeacherSettlement: async (settlementId: string): Promise<ApiResponse<boolean>> => (await apiClient.post<ApiResponse<boolean>>(`/admin/teacher-finance-center/settlements/${settlementId}/approve`)).data,
  cancelTeacherSettlement: async (settlementId: string): Promise<ApiResponse<boolean>> => (await apiClient.post<ApiResponse<boolean>>(`/admin/teacher-finance-center/settlements/${settlementId}/cancel`)).data,
  payTeacherSettlement: async (settlementId: string, payload: { paymentMethod: string; transferReference: string; attachmentUrl?: string; amount?: number }): Promise<ApiResponse<boolean>> => (await apiClient.post<ApiResponse<boolean>>(`/admin/teacher-finance-center/settlements/${settlementId}/pay`, payload)).data,

  reverseTeacherAllocations: async (payload: { lines: Array<{ allocationId: string; amount: number }>; reason: string; disposition: 'TeacherDebt' | 'NextSettlementDeduction'; idempotencyKey: string }): Promise<ApiResponse<{ id: string }>> => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>('/admin/teacher-finance-center/reversals', payload);
    return res.data;
  },

  setCodeGroupFinancialTerms: async (codeGroupId: string, payload: CodeGroupFinancialTerms): Promise<ApiResponse<boolean>> => (
    await apiClient.put<ApiResponse<boolean>>(`/admin/teacher-finance-center/code-groups/${codeGroupId}/financial-terms`, payload)
  ).data,

  confirmCodeGroupDelivery: async (codeGroupId: string, payload: { recipient: string; attachmentUrl?: string; deliveredAt?: string }): Promise<ApiResponse<{ id: string; confirmedAt: string }>> => (
    await apiClient.post<ApiResponse<{ id: string; confirmedAt: string }>>(`/admin/teacher-finance-center/code-groups/${codeGroupId}/confirm-delivery`, payload)
  ).data,

  // --- Administrative Payroll Management ---
  getPayroll: async (month: number, year: number): Promise<PayrollRecordDto[]> => {
    const res = await apiClient.get<ApiResponse<PayrollRecordDto[]>>(
      '/admin/finance/payroll',
      { params: { month, year } }
    );
    return res.data?.data ?? [];
  },

  generatePayroll: async (month: number, year: number): Promise<ApiResponse<number>> => {
    const res = await apiClient.post<ApiResponse<number>>(
      '/admin/finance/payroll/generate',
      { month, year }
    );
    return res.data;
  },

  addPayrollAdjustment: async (
    payrollId: string,
    payload: { type: number; amount: number; reason: string }
  ): Promise<ApiResponse<PayrollAdjustmentDto>> => {
    const res = await apiClient.post<ApiResponse<PayrollAdjustmentDto>>(
      `/admin/finance/payroll/${payrollId}/adjustments`,
      payload
    );
    return res.data;
  },

  deletePayrollAdjustment: async (
    payrollId: string,
    adjustmentId: string
  ): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.delete<ApiResponse<boolean>>(
      `/admin/finance/payroll/${payrollId}/adjustments/${adjustmentId}`
    );
    return res.data;
  },

  approvePayroll: async (payrollId: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/admin/finance/payroll/${payrollId}/approve`
    );
    return res.data;
  },

  // --- Teacher Payout Reviews (Admin/Supervisor) ---
  getPayouts: async (status?: number): Promise<AdminPayoutDto[]> => {
    const res = await apiClient.get<ApiResponse<AdminPayoutDto[]>>(
      '/admin/finance/payouts',
      { params: status !== undefined ? { status } : {} }
    );
    return res.data?.data ?? [];
  },

  resolvePayout: async (
    payoutId: string,
    payload: { status: number; rejectionReason?: string }
  ): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/admin/finance/payouts/${payoutId}/resolve`,
      payload
    );
    return res.data;
  },

  // --- Reconciliations & Code Accounting (Admin/Supervisor) ---
  getCodeAccounting: async (params: {
    teacherId?: string;
    packageId?: string;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<AdminCodeAccountingDto>> => {
    const res = await apiClient.get<ApiResponse<PagedResult<AdminCodeAccountingDto>>>(
      '/admin/finance/code-accounting',
      { params }
    );
    return res.data?.data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
  },

  getTeacherFinancialEvents: async (params: {
    status?: string;
    teacherId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<AdminTeacherFinancialEventDto>> => {
    const res = await apiClient.get<ApiResponse<{ items: AdminTeacherFinancialEventDto[]; total: number; page: number; pageSize: number }>>(
      '/admin/finance/teacher-events',
      { params }
    );
    const data = res.data?.data;
    return data
      ? { items: data.items, totalCount: data.total, page: data.page, pageSize: data.pageSize }
      : { items: [], totalCount: 0, page: 1, pageSize: 50 };
  },

  reviewTeacherFinancialEvent: async (
    allocationId: string,
    payload: { status: 'Approved' | 'Rejected'; note?: string }
  ): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/admin/finance/teacher-events/${allocationId}/review`,
      payload
    );
    return res.data;
  },

  createManualTeacherCompensation: async (payload: {
    teacherId: string;
    amount: number;
    reason?: string;
  }): Promise<ApiResponse<{ id: string }>> => {
    const res = await apiClient.post<ApiResponse<{ id: string }>>(
      '/admin/finance/teacher-events/manual-compensation',
      payload
    );
    return res.data;
  },

  // --- Teacher Self-Service Finance (Teacher role) ---
  getTeacherAccountSummary: async (): Promise<TeacherAccountDto | null> => {
    const res = await apiClient.get<ApiResponse<TeacherAccountDto>>(
      '/teacher/finance/account'
    );
    return res.data?.data ?? null;
  },

  getTeacherTransactions: async (
    page: number = 1,
    pageSize: number = 20,
    params?: { date?: string; from?: string; to?: string; status?: string }
  ): Promise<PagedResult<TeacherTransactionDto>> => {
    const res = await apiClient.get<ApiResponse<PagedResult<TeacherTransactionDto>>>(
      '/teacher/finance/transactions',
      { params: { page, pageSize, ...(params ?? {}) } }
    );
    return res.data?.data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
  },

  getTeacherFinanceCalendar: async (
    from: string,
    to: string
  ): Promise<TeacherFinanceDayDto[]> => {
    const res = await apiClient.get<ApiResponse<TeacherFinanceDayDto[]>>(
      '/teacher/finance/calendar',
      { params: { from, to } }
    );
    return res.data?.data ?? [];
  },

  exportTeacherFinanceDay: async (date: string): Promise<Blob> => {
    const res = await apiClient.get('/teacher/finance/calendar/export', {
      params: { date },
      responseType: 'blob',
    });
    return res.data;
  },

  getTeacherPayouts: async (): Promise<TeacherPayoutDto[]> => {
    const res = await apiClient.get<ApiResponse<TeacherPayoutDto[]>>(
      '/teacher/finance/payouts'
    );
    return res.data?.data ?? [];
  },

  requestTeacherPayout: async (amount: number): Promise<ApiResponse<any>> => {
    const res = await apiClient.post<ApiResponse<any>>(
      '/teacher/finance/payouts',
      { amount }
    );
    return res.data;
  },
};
