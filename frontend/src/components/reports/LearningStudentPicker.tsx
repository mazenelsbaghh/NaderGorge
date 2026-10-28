'use client';

import { useEffect, useState } from 'react';
import { lessonStudentReportService, reportError, type ReportStudent } from '@/services/lesson-student-report-service';
import type { ReportAudience } from '@/services/advanced-report-service';

export function LearningStudentPicker({ audience, teacherId, packageId, selected, onSelect }: {
  audience: ReportAudience; teacherId: string; packageId: string; selected: ReportStudent | null; onSelect: (student: ReportStudent | null) => void;
}) {
  const [search, setSearch] = useState('');
  const [students, setStudents] = useState<ReportStudent[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [hasMore, setHasMore] = useState(false);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    if (!packageId || search.trim().length < 2 || selected) return () => controller.abort();
    const timer = window.setTimeout(() => {
      setBusy(true); setError('');
      lessonStudentReportService.students(audience, { teacherId, packageId, search: search.trim() }, controller.signal)
        .then(choices => { if (!controller.signal.aborted) { setStudents(choices.students); setHasMore(choices.hasMore); } })
        .catch(reason => { if (!controller.signal.aborted) setError(reportError(reason)); })
        .finally(() => { if (!controller.signal.aborted) setBusy(false); });
    }, 300);
    return () => { controller.abort(); window.clearTimeout(timer); };
  }, [audience, teacherId, packageId, search, selected, version]);
  return <div className="space-y-3 sm:col-span-2">
    <label className="block text-sm font-bold text-[var(--admin-text)]">ابحث عن طالب
      <input aria-label="ابحث عن طالب" value={search} disabled={!packageId} maxLength={100} placeholder="اسم الطالب أو الموبايل أو الكود، حرفان على الأقل"
        onChange={event => { setSearch(event.target.value); setStudents([]); setHasMore(false); setError(''); setBusy(event.target.value.trim().length >= 2); onSelect(null); }}
        className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-normal text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60" />
    </label>
    {selected ? <p role="status" className="text-sm text-[var(--admin-text)]">الطالب المختار: <strong>{selected.name}</strong> · <bdi>{selected.phone}</bdi><button type="button" onClick={() => onSelect(null)} className="mr-3 min-h-11 font-bold underline">تغيير الطالب</button></p>
      : <div aria-live="polite" className="text-sm text-[var(--admin-muted)]">
        {busy ? <p role="status">جاري البحث عن الطالب…</p> : error ? <p role="alert">{error}<button type="button" onClick={() => setVersion(current => current + 1)} className="mr-3 min-h-11 underline">إعادة البحث</button></p>
          : search.trim().length >= 2 && students.length === 0 ? <p>لا يوجد طالب مطابق له صلاحية مشاهدة في هذا الكورس.</p> : null}
        {students.length > 0 && <ul className="max-h-60 divide-y divide-[var(--admin-border)] overflow-y-auto rounded-xl border border-[var(--admin-border)]">{students.map(student => <li key={student.id}><button type="button" onClick={() => onSelect(student)} className="flex min-h-12 w-full flex-wrap items-center justify-between gap-2 px-3 py-2 text-right text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]"><span className="font-bold">{student.name}</span><bdi>{student.phone}</bdi><span className="sr-only">اختيار الطالب</span></button></li>)}</ul>}
        {hasMore && <p className="mt-2">توجد نتائج أخرى. اكتب اسمًا أكمل أو رقم الموبايل لتحديد الطالب.</p>}
      </div>}
  </div>;
}
