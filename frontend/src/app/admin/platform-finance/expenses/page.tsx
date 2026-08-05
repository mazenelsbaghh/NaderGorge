import { AdminPage } from '@/components/admin';
import ExpenseManager from '@/components/admin/platform-finance/ExpenseManager';

export default function PlatformFinanceExpensesPage() { return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="مصروفات المنصة" subtitle="دورة المصروف من المسودة حتى الدفع والعكس."><ExpenseManager /></AdminPage>; }
