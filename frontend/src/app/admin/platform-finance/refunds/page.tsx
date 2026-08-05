import { AdminPage } from '@/components/admin';
import RefundManager from '@/components/admin/platform-finance/RefundManager';

export default function PlatformFinanceRefundsPage() { return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="استردادات الطلاب" subtitle="رصيد الطالب أو كاش مع تاريخ قابل للمراجعة."><RefundManager /></AdminPage>; }
