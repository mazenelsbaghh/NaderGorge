'use client';

import { AdminPage } from '@/components/admin';
import { AttendanceAdjustmentRules } from '@/features/hr/payroll';

export default function AttendanceAdjustmentsPageClient() {
  return <AdminPage activePath="/admin/hr/attendance-adjustments" sectionLabel="الموارد البشرية" pageTitle="بدلات وخصومات الحضور" subtitle="تفعيل الزيادة والخصم تلقائياً من مواعيد الشفت وسجل الحضور عند إعداد الرواتب."><AttendanceAdjustmentRules /></AdminPage>;
}
