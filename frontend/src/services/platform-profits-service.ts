import type { TeacherFinanceSummary } from '@/features/teacher-finance-center/types';
import apiClient from '@/services/api-client';
import type { FinanceTeacherSummary, PlatformFinanceDashboard } from '@/services/platform-finance-service';

export type ProfitTeacherRow = {
  purchasingStudents: number;
  purchaseOperations: number;
  account: TeacherFinanceSummary | null;
  period: FinanceTeacherSummary;
  historicalPeriod: FinanceTeacherSummary;
  currentCalculatedBalance: number;
  currentAccountBalance: number;
  currentLedgerBalance: number;
  reconciliationDifference: number;
};
export type PlatformProfitReport = {
  generatedAt: string;
  purchasingStudents: number;
  historicalPlatformNetRevenue: number;
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
    [], ['المدرس', 'طلاب اشتروا', 'عمليات الشراء', 'المبيعات بعد المرتجعات', 'نصيب المدرس', 'نصيب المنصة', 'استلم فعليًا من بداية الحساب', 'باقي له الآن', 'الباقي عليه من الأكواد'],
    ...rows.map(({ period: row, account, purchasingStudents, purchaseOperations }) => [row.teacherName, purchasingStudents, purchaseOperations,
      row.grossSales - row.refunds, row.teacherShare, row.platformShare, account?.paid ?? 'غير متاح',
      account?.netPayable ?? 'غير متاح', account?.codeAmountDue ?? 0]),
  ];
  return '\uFEFF' + data.map(row => row.map(cell).join(',')).join('\r\n');
}
