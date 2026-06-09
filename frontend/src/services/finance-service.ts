import apiClient from './api-client';

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
  status: 'Pending' | 'Paid' | 'Rejected' | number;
  rejectionReason?: string;
  handledByUserId?: string;
  handledByName?: string;
  handledAt?: string;
  createdAt: string;
}

export interface TeacherAccountDto {
  teacherId: string;
  teacherName: string;
  totalEarnings: number;
  currentBalance: number;
  commissionRate: number;
}

export interface TeacherTransactionDto {
  id: string;
  packageName: string;
  studentName: string;
  serialNumber: number;
  price: number;
  commissionRate: number;
  commissionEarned: number;
  activatedAt: string;
}

export interface TeacherPayoutDto {
  id: string;
  amount: number;
  status: 'Pending' | 'Paid' | 'Rejected' | number;
  rejectionReason?: string;
  createdAt: string;
  handledAt?: string;
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

export const financeService = {
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

  // --- Teacher Self-Service Finance (Teacher role) ---
  getTeacherAccountSummary: async (): Promise<TeacherAccountDto | null> => {
    const res = await apiClient.get<ApiResponse<TeacherAccountDto>>(
      '/teacher/finance/account'
    );
    return res.data?.data ?? null;
  },

  getTeacherTransactions: async (
    page: number = 1,
    pageSize: number = 20
  ): Promise<PagedResult<TeacherTransactionDto>> => {
    const res = await apiClient.get<ApiResponse<PagedResult<TeacherTransactionDto>>>(
      '/teacher/finance/transactions',
      { params: { page, pageSize } }
    );
    return res.data?.data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
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
