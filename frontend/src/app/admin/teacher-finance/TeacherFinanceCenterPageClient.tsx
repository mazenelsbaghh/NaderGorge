'use client';

import { useEffect, useState } from 'react';
import { AdminPage } from '@/components/admin';
import { TeacherFinanceCenterWorkspace } from '@/features/teacher-finance-center/TeacherFinanceCenterWorkspace';
import { teacherService, type TeacherDto } from '@/services/teacher-service';

export default function TeacherFinanceCenterPageClient() {
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let isMounted = true;
    setIsLoading(true);
    setHasError(false);

    void teacherService.getTeachers()
      .then((response) => {
        if (!isMounted) return;
        if (response.success && response.data) setTeachers(response.data);
        else setHasError(true);
      })
      .catch(() => { if (isMounted) setHasError(true); })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [attempt]);

  return (
    <AdminPage
      activePath="/admin/teacher-finance"
      sectionLabel="مالية المدرسين"
      pageTitle="مركز مالية المدرسين"
      subtitle="اضبط الاتفاق الافتراضي لكل محتوى المدرس، أو استثنِ كورسًا أو درسًا أو فيديو أو دفعة أكواد باتفاق مستقل."
    >
      {isLoading ? (
        <div className="border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-12 text-center text-sm font-bold text-[var(--admin-muted)]">
          جارٍ تحميل حسابات المدرسين...
        </div>
      ) : hasError ? (
        <div role="alert" className="rounded-xl border border-[var(--admin-border)] p-6">
          تعذر تحميل حسابات المدرسين.
          <button type="button" className="min-h-11 px-4 underline" onClick={() => setAttempt((value) => value + 1)}>إعادة المحاولة</button>
        </div>
      ) : (
        <TeacherFinanceCenterWorkspace teachers={teachers} />
      )}
    </AdminPage>
  );
}
