import apiClient from '@/services/api-client';
import type { FinanceTeacherSummary, PlatformFinanceDashboard } from '@/services/platform-finance-service';

export type ProfitTeacherRow = {
  period: FinanceTeacherSummary;
  currentCalculatedBalance: number;
  currentAccountBalance: number;
  currentLedgerBalance: number;
  reconciliationDifference: number;
};
export type PlatformProfitReport = {
  generatedAt: string;
  earliestDate: string;
  platform: PlatformFinanceDashboard;
  teachers: ProfitTeacherRow[];
};

export async function getPlatformProfitReport(from: string, to: string, signal?: AbortSignal) {
  return (await apiClient.get<PlatformProfitReport>('/admin/platform-finance/profits', { params: { from, to }, signal })).data;
}

export function profitReportCsv(report: PlatformProfitReport, rows: ProfitTeacherRow[], from: string, to: string) {
  const cell = (value: string | number) => {
    const text = String(value);
    const safe = typeof value === 'string' && /^[\s]*[=+@-]/.test(text) ? `'${text}` : text;
    return `"${safe.replaceAll('"', '""')}"`;
  };
  const data: (string | number)[][] = [
    ['أرباح المنصة', 'من', from, 'إلى', to],
    ['إيرادات المنصة', report.platform.revenue], ['مرتجعات المنصة', report.platform.refunds],
    ['مصروفات المنصة', report.platform.expenses], ['صافي الربح المسجل', report.platform.netProfit],
    [], ['المدرس', 'المبيعات قبل المرتجعات', 'حصة المدرس', 'حصة المنصة', 'المرتجعات', 'المصروف للمدرس',
      'المستحق المحسوب بنهاية الفترة', 'رصيد حساب المدرس الآن', 'رصيد الدفتر الآن', 'فرق الحساب المحسوب عن المسجل', 'المستحق المحسوب الآن'],
    ...rows.map(({ period: row, ...current }) => [row.teacherName, row.grossSales, row.teacherShare,
      row.platformShare, row.refunds, row.paid, row.outstanding, current.currentAccountBalance,
      current.currentLedgerBalance, current.reconciliationDifference, current.currentCalculatedBalance]),
  ];
  return '\uFEFF' + data.map(row => row.map(cell).join(',')).join('\r\n');
}
