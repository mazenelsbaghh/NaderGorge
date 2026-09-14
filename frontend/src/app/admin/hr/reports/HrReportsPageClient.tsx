'use client';

import { AdminPage } from '@/components/admin';
import { WorkforceReports } from '@/features/hr/governance';

export default function HrReportsPageClient() {
  return <AdminPage
    activePath="/admin/hr/reports"
    sectionLabel="الموارد البشرية"
    pageTitle="أداء فريق الدعم"
    subtitle="تابع الحضور والورديات وسرعة الرد والإغلاق وتقييمات الطلاب من مكان واحد."
  >
    <WorkforceReports />
  </AdminPage>;
}
