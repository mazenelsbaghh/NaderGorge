import type { TeacherFinanceSummary } from '@/features/teacher-finance-center/types';
import apiClient from '@/services/api-client';
import type { FinanceTeacherSummary, PlatformFinanceDashboard } from '@/services/platform-finance-service';

export type ProfitTeacherRow = {
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
    [], ['المدرس', 'المبيعات قبل المرتجعات', 'حصة المدرس', 'حصة المنصة', 'المرتجعات', 'المسوّى للمدرس: صرف أو نصيب محتفظ به',
      'رصيد الدفتر بنهاية الفترة', 'رصيد حساب المدرس بعد المديونية الآن', 'رصيد الدفتر الآن', 'فرق الدفتر عن الحساب التشغيلي', 'إعادة الحساب التاريخي للمقارنة', 'تسويات غير المبيعات', 'أرباح المدرس من حسابه الآن', 'استلم فعليًا من بداية الحساب', 'محجوز الآن', 'مديونية مفتوحة الآن', 'متاح لسحب جديد الآن', 'نصيب محتفظ به من الأكواد', 'المحصل من المدرّس عن الأكواد', 'الباقي عليه عن الأكواد'],
    ...rows.map(({ period: row, ...current }) => [row.teacherName, row.grossSales, row.teacherShare,
      row.platformShare, row.refunds, row.paid, row.outstanding, current.currentAccountBalance,
      current.currentLedgerBalance, current.reconciliationDifference, current.currentCalculatedBalance, row.adjustments, current.account?.totalEarned ?? 'غير متاح', current.account?.paid ?? 'غير متاح', current.account?.reserved ?? 'غير متاح', current.account?.debt ?? 'غير متاح', current.account?.netPayable ?? 'غير متاح', current.account?.retained ?? 0, current.account?.codeAmountCollected ?? 0, current.account?.codeAmountDue ?? 0]),
  ];
  return '\uFEFF' + data.map(row => row.map(cell).join(',')).join('\r\n');
}
