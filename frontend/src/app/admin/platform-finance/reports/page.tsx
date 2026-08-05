import { AdminPage } from '@/components/admin';
import FinancialReports from '@/components/admin/platform-finance/FinancialReports';
import AccountingPeriodManager from '@/components/admin/platform-finance/AccountingPeriodManager';

export default function PlatformFinanceReportsPage() { return <AdminPage activePath="/admin/platform-finance/reports" sectionLabel="المالية" pageTitle="التقارير والإقفال" subtitle="تقارير موحدة من القيود مع إغلاق وإعادة فتح الفترات بسبب مسجل."><div className="space-y-6"><FinancialReports /><AccountingPeriodManager /></div></AdminPage>; }
