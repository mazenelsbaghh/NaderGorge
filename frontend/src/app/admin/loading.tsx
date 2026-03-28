'use client';

import { AdminPageSkeleton, AdminShellChrome } from '@/components/admin';

export default function AdminLoading() {
  return (
    <AdminShellChrome
      activePath="/admin"
      sectionLabel="لوحة الإدارة"
      pageTitle="جاري تحميل الصفحة"
      subtitle="يتم تجهيز بيانات القسم الآن."
    >
      <AdminPageSkeleton />
    </AdminShellChrome>
  );
}
