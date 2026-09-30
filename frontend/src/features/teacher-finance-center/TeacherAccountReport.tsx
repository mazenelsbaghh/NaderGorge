'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { TeacherPayoutRequests } from './TeacherPayoutRequests';
import { TeacherFinanceCenterWorkspace } from './TeacherFinanceCenterWorkspace';
import { TeacherCollectionsPanel } from './TeacherCollectionsPanel';
import { TeacherStatementPanel } from './TeacherStatementPanel';

export function TeacherAccountReport({ teacherId }: { teacherId: string }) {
  const [teacher, setTeacher] = useState<TeacherDto | null>(null);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    let active = true;
    setError(false);
    void teacherService.getTeacherById(teacherId).then(response => {
      if (!active) return;
      if (!response.success || !response.data) { setError(true); return; }
      setTeacher(response.data);
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [teacherId, attempt]);
  return <div className="mx-auto max-w-6xl space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-bold">{teacher?.fullName || 'حساب المدرس'}</h2><Link className="inline-flex min-h-11 items-center underline" href="/admin/platform-profits">كل الحسابات</Link></div>
    <TeacherStatementPanel teacherId={teacherId} refreshVersion={version} />
    {error ? <p role="alert">تعذر تحميل بيانات المدرس. <button className="min-h-11 px-3 underline" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button></p> : teacher ? <TeacherFinanceCenterWorkspace teacher={teacher} onChanged={() => setVersion(value => value + 1)} /> : <p role="status">جارٍ تحميل بيانات المدرس...</p>}
    <details className="rounded-xl border border-[var(--admin-border)] p-4"><summary className="min-h-11 cursor-pointer font-bold">طلبات السحب</summary><TeacherPayoutRequests teacherId={teacherId} onChanged={() => setVersion(value => value + 1)} /></details>
    <details className="rounded-xl border border-[var(--admin-border)] p-4"><summary className="min-h-11 cursor-pointer font-bold">سجل تحويلات الطلاب المقبولة</summary><TeacherCollectionsPanel teacherId={teacherId} details /></details>
  </div>;
}
