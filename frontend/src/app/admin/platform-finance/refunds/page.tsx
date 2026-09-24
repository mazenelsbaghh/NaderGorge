import { AdminPage } from '@/components/admin';
import RefundManager from '@/components/admin/platform-finance/RefundManager';

export default function PlatformFinanceRefundsPage() { return <AdminPage activePath="/admin/platform-finance/refunds" sectionLabel="الحسابات" pageTitle="إرجاع فلوس للطلاب" subtitle="ابحث برقم الطالب، واختار الباقة اللي هترجّع فلوسها."><RefundManager /></AdminPage>; }
