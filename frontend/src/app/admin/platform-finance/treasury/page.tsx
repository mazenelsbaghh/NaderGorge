import { AdminPage } from '@/components/admin';
import TreasuryManager from '@/components/admin/platform-finance/TreasuryManager';
import HistoricalMigrationManager from '@/components/admin/platform-finance/HistoricalMigrationManager';

export default function PlatformFinanceTreasuryPage() { return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="الخزائن وإعادة البناء" subtitle="مطابقة المحافظ والكاش وإعادة بناء الحركات التاريخية مع استثناءات صريحة."><div className="space-y-6"><TreasuryManager /><HistoricalMigrationManager /></div></AdminPage>; }
