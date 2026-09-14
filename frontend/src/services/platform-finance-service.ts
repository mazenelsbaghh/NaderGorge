import apiClient from '@/services/api-client';

export type FinanceAccountBalance = {
  accountId: string;
  code: string;
  name: string;
  type: number;
  debit: number;
  credit: number;
  balance: number;
};

export type PlatformFinanceDashboard = {
  from: string;
  to: string;
  cash: number;
  generalStudentLiability: number;
  teacherStudentLiability: number;
  teacherPayable: number;
  supplierPayable: number;
  revenue: number;
  refunds: number;
  expenses: number;
  netProfit: number;
  accounts: FinanceAccountBalance[];
};

export type FinanceJournalLine = {
  id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  studentId?: string | null;
  teacherId?: string | null;
  treasuryAccountId?: string | null;
  memo?: string | null;
};

export type FinanceJournal = {
  id: string;
  sequenceNumber: number;
  occurredAt: string;
  postedAt: string;
  sourceType: string;
  sourceId?: string | null;
  postingKind: string;
  description: string;
  lines: FinanceJournalLine[];
};

export type FinanceBootstrap = {
  accounts: Array<{ id: string; code: string; name: string; type: number }>;
  treasuryAccounts: Array<{ id: string; name: string; type: number; maskedIdentifier?: string | null }>;
  categories: Array<{ id: string; name: string; accountCode: string }>;
  costCenters: Array<{ id: string; name: string }>;
  vendors: Array<{ id: string; name: string }>;
};

export type FinanceTeacherSummary = {
  teacherId: string;
  teacherName: string;
  grossSales: number;
  platformShare: number;
  teacherShare: number;
  refunds: number;
  paid: number;
  outstanding: number;
};

export type PlatformExpenseRow = { id: string; documentNumber: string; amount: number; occurredAt: string; status: number; description: string; paid: number };
export type WalletTransferReview = { id: string; destinationPhoneNumber: string; amount: number; serviceFee: number; transferReference?: string | null; occurredAt: string; sourceWallet: string; sourceWalletNumber?: string | null; sourceTreasuryAccountId?: string | null };
export type PlatformRefundRow = { id: string; originalSourceId: string; originalSourceType: string; studentId: string; studentName: string; studentPhoneNumber: string; teacherId?: string | null; platformAmount: number; teacherAmount: number; totalAmount: number; method: number; status: number; reason: string; journalEntryId?: string | null; createdAt: string; isHistorical: boolean };
export type PlatformFinancialReport = { kind: string; from: string; to: string; totalDebit: number; totalCredit: number; rows: Array<{ code: string; name: string; type: number; debit: number; credit: number; balance: number }> };
export type WalletFinanceReport = { wallets: Array<{ id: string; label: string; phoneNumber: string; currentBalance: number; incoming: number; outgoing: number; expenses: number; internalTransfers: number; transactions: number }>; teacherRechargeCards: Array<{ walletId: string; teacherName: string; amount: number; count: number }>; transactions: Array<{ id: string; walletId: string; receivedAt: string; amount: number; type: 'incoming' | 'outgoing'; phone?: string | null; body: string }> };

const platformFinanceService = {
  async getDashboard(from?: string, to?: string) {
    const response = await apiClient.get<PlatformFinanceDashboard>('/admin/platform-finance/dashboard', { params: { from, to } });
    return response.data;
  },
  async getLedger(from?: string, to?: string, page = 1, pageSize = 50) {
    const response = await apiClient.get<FinanceJournal[]>('/admin/platform-finance/ledger', { params: { from, to, page, pageSize } });
    return response.data;
  },
  async getTeacherSummary(from?: string, to?: string) {
    const response = await apiClient.get<FinanceTeacherSummary[]>('/admin/platform-finance/teachers/summary', { params: { from, to } });
    return response.data;
  },
  async getTeacherDetail(teacherId: string, from?: string, to?: string) {
    const response = await apiClient.get<FinanceTeacherSummary>(`/admin/platform-finance/teachers/${teacherId}/summary`, { params: { from, to } });
    return response.data;
  },
  async bootstrap() {
    const response = await apiClient.get<FinanceBootstrap>('/admin/platform-finance/bootstrap');
    return response.data;
  },
  async createExpense(payload: { amount: number; occurredAt: string; categoryId: string; description: string; documentNumber?: string }) {
    return (await apiClient.post('/admin/platform-finance/expenses', payload)).data;
  },
  async postExpense(expenseId: string, payload: { treasuryAccountId?: string; idempotencyKey: string }) {
    return (await apiClient.post(`/admin/platform-finance/expenses/${expenseId}/post`, payload)).data;
  },
  async getExpenses(from?: string, to?: string) {
    return (await apiClient.get<PlatformExpenseRow[]>('/admin/platform-finance/expenses', { params: { from, to } })).data;
  },
  async getWalletTransferReviews() {
    return (await apiClient.get<WalletTransferReview[]>('/admin/platform-finance/wallet-transfers/reviews')).data;
  },
  async backfillWalletTransferReviews() {
    return (await apiClient.post<{ added: number }>('/admin/platform-finance/wallet-transfers/reviews/backfill')).data;
  },
  async getWalletReport(from?: string, to?: string) {
    return (await apiClient.get<WalletFinanceReport>('/admin/platform-finance/wallets/report', { params: { from, to } })).data;
  },
  async recordWalletTransferExpense(reviewId: string, payload: { categoryId: string; costCenterId?: string; beneficiaryName: string; reason: string }) {
    return (await apiClient.post(`/admin/platform-finance/wallet-transfers/reviews/${reviewId}/expense`, payload)).data;
  },
  async recordWalletInternalTransfer(reviewId: string, destinationTreasuryAccountId: string) {
    return (await apiClient.post(`/admin/platform-finance/wallet-transfers/reviews/${reviewId}/internal-transfer`, { destinationTreasuryAccountId })).data;
  },
  async reverseExpense(expenseId: string, reason: string) {
    return (await apiClient.post(`/admin/platform-finance/expenses/${expenseId}/reverse`, { reason })).data;
  },
  async createRefund(payload: { originalSourceId: string; originalSourceType: string; studentId: string; teacherId?: string; platformAmount: number; teacherAmount: number; method: number; treasuryAccountId?: string; reason: string; paymentReference?: string }) {
    return (await apiClient.post('/admin/platform-finance/refunds', payload)).data;
  },
  async createExternalPackageRefund(payload: { accessGrantId: string; purchaseOperationId: string; studentId: string; teacherId?: string; platformAmount: number; teacherAmount: number; treasuryAccountId: string; reason: string; paymentReference?: string }) {
    return (await apiClient.post('/admin/platform-finance/refunds/external-package', payload)).data;
  },
  async postRefund(refundId: string, idempotencyKey: string) {
    return (await apiClient.post(`/admin/platform-finance/refunds/${refundId}/post`, { idempotencyKey })).data;
  },
  async getRefunds(from?: string, to?: string) {
    return (await apiClient.get<PlatformRefundRow[]>('/admin/platform-finance/refunds', { params: { from, to } })).data;
  },
  async reverseRefund(refundId: string, reason: string) {
    return (await apiClient.post(`/admin/platform-finance/refunds/${refundId}/reverse`, { reason })).data;
  },
  async createBudget(payload: { name: string; periodKind: number; startDate: string; endDate: string; lines: Array<{ financialAccountId: string; plannedAmount: number }> }) {
    return (await apiClient.post('/admin/platform-finance/budgets', payload)).data;
  },
  async getBudgetActuals(from: string, to: string) {
    return (await apiClient.get('/admin/platform-finance/budgets/actuals', { params: { from, to } })).data as Array<{ financialAccountId: string; code: string; name: string; actual: number }>;
  },
  async transfer(payload: { sourceTreasuryAccountId: string; destinationTreasuryAccountId: string; amount: number; reference: string; idempotencyKey: string }) {
    return (await apiClient.post('/admin/platform-finance/treasury/transfers', payload)).data;
  },
  async reconcile(payload: { treasuryAccountId: string; asOfDate: string; countedOrStatementBalance: number; evidenceNote: string }) {
    return (await apiClient.post('/admin/platform-finance/treasury/reconciliations', payload)).data;
  },
  async getReport(kind: string, from: string, to: string) {
    return (await apiClient.get<PlatformFinancialReport>(`/admin/platform-finance/reports/${kind}`, { params: { from, to } })).data;
  },
  async getReconciliation(from: string, to: string) {
    return (await apiClient.get('/admin/platform-finance/reconciliation', { params: { from, to } })).data as { from: string; to: string; totalDebit: number; totalCredit: number; rows: Array<{ sourceType: string; month: string; journalCount: number; debit: number; credit: number; variance: number }>; exceptions: string[] };
  },
  async migrationPreview(from: string, to: string) {
    return (await apiClient.get('/admin/platform-finance/migration/preview', { params: { from, to } })).data as { from: string; to: string; rechargeCandidates: number; rechargeAmount: number; saleCandidates: number; saleAmount: number; balanceAdjustmentCandidates: number; balanceAdjustmentAmount: number; teacherPayoutCandidates: number; teacherPayoutAmount: number; payrollCandidates: number; payrollAmount: number; ambiguousCandidates: number; ambiguities: string[] };
  },
  async postMigration(from: string, to: string) {
    return (await apiClient.post('/admin/platform-finance/migration/post', null, { params: { from, to } })).data as { batchId: string; posted: number; alreadyPosted: number; failed: number; errors: string[] };
  },
};

export default platformFinanceService;
