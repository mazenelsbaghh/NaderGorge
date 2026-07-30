'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { TeacherShellChrome } from '@/components/teacher/TeacherShellChrome';

export default function TeacherLoading() {
  return (
    <TeacherShellChrome
      activePath="/teacher"
      sectionLabel="لوحة المعلم"
      pageTitle="جاري تحميل الصفحة"
      subtitle="يتم تجهيز بيانات القسم الآن."
    >
      <AdminPageSkeleton />
    </TeacherShellChrome>
  );
}
