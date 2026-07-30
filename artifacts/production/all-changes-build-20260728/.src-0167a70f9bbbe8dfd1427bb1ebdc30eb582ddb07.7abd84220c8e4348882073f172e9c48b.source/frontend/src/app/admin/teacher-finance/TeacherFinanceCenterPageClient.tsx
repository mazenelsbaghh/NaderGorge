'use client';

import { useEffect, useState } from 'react';
import { AdminShellChrome } from '@/components/admin';
import { TeacherFinanceCenterWorkspace } from '@/features/teacher-finance-center/TeacherFinanceCenterWorkspace';
import { teacherService, type TeacherDto } from '@/services/teacher-service';

export default function TeacherFinanceCenterPageClient() {
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    void teacherService.getTeachers()
      .then((response) => {
        if (isMounted && response.success) setTeachers(response.data ?? []);
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <AdminShellChrome
      activePath="/admin/teacher-finance"
      sectionLabel="مالية المدرسين"
      pageTitle="مركز مالية المدرسين"
      subtitle="اضبط الاتفاق الافتراضي لكل محتوى المدرس، أو استثنِ كورسًا أو درسًا أو فيديو أو دفعة أكواد باتفاق مستقل."
    >
      {isLoading ? (
        <div className="border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-12 text-center text-sm font-bold text-[var(--admin-muted)]">
          جارٍ تحميل حسابات المدرسين...
        </div>
      ) : (
        <TeacherFinanceCenterWorkspace teachers={teachers} />
      )}
    </AdminShellChrome>
  );
}
