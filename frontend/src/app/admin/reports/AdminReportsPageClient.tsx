'use client';

import { RefreshCw } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { ReportsWorkspace } from '@/components/reports/ReportsWorkspace';

export default function AdminReportsPageClient() {
  return (
    <AdminPage
      activePath="/admin/reports"
      sectionLabel="مركز القرارات"
      pageTitle="مركز التقارير"
      subtitle="راجع حضور حصة محددة أو مشاهدة طالب، وابنِ تقاريرك المتقدمة من مكان واحد."
      headerAccessory={<span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary)]"><RefreshCw className="h-3.5 w-3.5" /> توقيت القاهرة</span>}
    >
      <ReportsWorkspace audience="admin" />
    </AdminPage>
  );
}
