import apiClient from './api-client';

export type PayComponentClass = 'Earning' | 'Deduction' | 'EmployerContribution' | 'Informational';
export interface PayComponentDto { id: string; code: string; name: string; classification: PayComponentClass; isTaxable: boolean; isInsurable: boolean; isActive: boolean; }
export interface PayrollRuleDto { id: string; payComponentId: string; component: string; name: string; expression: string; rate: number; effectiveFrom: string; effectiveTo?: string | null; priority: number; version: number; isActive: boolean; }
export interface PayrollRunDto { id: string; runNumber: string; periodStart: string; periodEnd: string; status: string; totalGross: number; totalDeductions: number; totalNet: number; employees: number; reconciliationHash: string; version: number; }
export interface PayrollLineDto { id: string; component: string; classification: PayComponentClass; amount: number; explanation: string; sourceType: string; sourceId: string; }
export interface EmployeePayrollDto { id: string; employeeId: string; employeeNumberSnapshot: string; employeeNameSnapshot: string; baseSalarySnapshot: number; currency: string; gross: number; deductions: number; net: number; status: string; lines: PayrollLineDto[]; }
export interface PayslipDto { id: string; runNumber: string; periodStart: string; periodEnd: string; baseSalarySnapshot: number; currency: string; gross: number; deductions: number; net: number; lines: Array<{ component: string; classification: PayComponentClass; amount: number; explanation: string }>; payslip?: { id: string; version: number; assetReference: string; contentHash: string } | null; }
export type FinancialRequestType = 'Advance' | 'Loan' | 'Expense' | 'Commission';
export interface FinancialRequestDto { id: string; type: FinancialRequestType; state: string; amount: number; outstandingBalance: number; requestedInstallments: number; reason: string; attachmentReference: string; version: number; installments: Array<{ id: string; sequence: number; dueDate: string; amount: number; state: string; appliedAt?: string | null }>; }

export const hrPayrollService = {
  config: async (): Promise<{ components: PayComponentDto[]; rules: PayrollRuleDto[] }> => (await apiClient.get('/hr/payroll/config')).data,
  createComponent: async (payload: { code: string; name: string; classification: PayComponentClass; isTaxable: boolean; isInsurable: boolean }): Promise<{ id: string }> => (await apiClient.post('/hr/payroll/components', payload)).data,
  createRule: async (payload: { payComponentId: string; name: string; expression: string; rate: number; effectiveFrom: string; effectiveTo?: string | null; priority: number }) => (await apiClient.post('/hr/payroll/rules', payload)).data,
  runs: async (): Promise<PayrollRunDto[]> => (await apiClient.get('/hr/payroll/runs')).data ?? [],
  run: async (id: string): Promise<EmployeePayrollDto[]> => (await apiClient.get(`/hr/payroll/runs/${id}`)).data ?? [],
  prepare: async (payload: { periodStart: string; periodEnd: string; cutoffAt: string }) => (await apiClient.post('/hr/payroll/runs/prepare', payload)).data,
  transition: async (id: string, action: 'finance-review' | 'finance-approve' | 'gm-approve' | 'pay' | 'close' | 'return', expectedVersion: number) => (await apiClient.post(`/hr/payroll/runs/${id}/${action}`, { expectedVersion })).data,
  myPayslips: async (): Promise<PayslipDto[]> => (await apiClient.get('/hr/payroll/self/payslips')).data ?? [],
  myFinancialRequests: async (): Promise<FinancialRequestDto[]> => (await apiClient.get('/hr/payroll/self/financial-requests')).data ?? [],
  submitFinancialRequest: async (payload: { type: FinancialRequestType; amount: number; installments: number; reason: string; attachmentReference: string }) => (await apiClient.post('/hr/payroll/self/financial-requests', payload)).data,
};
