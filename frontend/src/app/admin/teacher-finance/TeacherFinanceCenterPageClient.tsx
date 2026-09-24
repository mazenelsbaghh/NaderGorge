'use client';

import { useEffect, useState } from 'react';
import { AdminPage } from '@/components/admin';
import Link from 'next/link';
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
      subtitle="اختر المدرّس لعرض أرباحه، طريقة حسابها، المدفوع والمتاح للسحب في مكان واحد."
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
        <section className="admin-panel rounded-2xl p-6">
          <h2 className="mb-4 text-lg font-bold">حسابات المدرسين</h2>
          {teachers.length ? <ul className="divide-y divide-[var(--admin-border)]">{teachers.map(teacher => <li key={teacher.id}><Link href={`/admin/teachers/${teacher.id}/account`} className="flex min-h-16 flex-wrap items-center justify-between gap-3 py-4"><span className="font-bold">{teacher.fullName}{!teacher.isActive && <span className="ms-2 text-sm text-[var(--admin-muted)]">غير نشط</span>}</span><span className="text-sm text-[var(--admin-primary)]">فتح الحساب ←</span></Link></li>)}</ul> : <p>لا يوجد مدرسون بعد.</p>}
        </section>
      )}
    </AdminPage>
  );
}
