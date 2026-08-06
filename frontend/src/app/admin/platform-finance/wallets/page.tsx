import { AdminPage } from '@/components/admin';
import WalletFinanceReports from '@/components/admin/platform-finance/WalletFinanceReports';

export default function WalletFinanceReportsPage() {
  return <AdminPage activePath="/admin/platform-finance/wallets" sectionLabel="المالية" pageTitle="تقارير المحافظ" subtitle="الوارد والصادر والمصروفات والتحويلات الداخلية وشحن رصيد المدرسين من كل محفظة."><WalletFinanceReports /></AdminPage>;
}
