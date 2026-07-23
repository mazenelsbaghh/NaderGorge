'use client';

import { RefreshCw } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import { AdvancedReportsCenter } from '@/components/reports/AdvancedReportsCenter';

export default function AdminReportsPageClient() {
  return (
    <AdminShellChrome
      activePath="/admin/reports"
      sectionLabel="مركز القرارات"
      pageTitle="مركز التقارير"
      subtitle="ابنِ أي تقرير من بيانات المنصة، ادمج فلاتر و/أو، ثم راجع الرسم والجدول أو صدّره كاملًا."
      headerAccessory={<span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary)]"><RefreshCw className="h-3.5 w-3.5" /> توقيت القاهرة</span>}
    >
      <AdvancedReportsCenter audience="admin" />
    </AdminShellChrome>
  );
}
