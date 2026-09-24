import { AdminPage } from '@/components/admin';
import ExpenseManager from '@/components/admin/platform-finance/ExpenseManager';

export default function PlatformFinanceExpensesPage() { return <AdminPage activePath="/admin/platform-finance/expenses" sectionLabel="الحسابات" pageTitle="مراجعة المصاريف" subtitle="راجع كل مبلغ اتصرف وسببه، وحدد تحويلات المحافظ اللي لسه محتاجة مراجعة."><ExpenseManager /></AdminPage>; }
