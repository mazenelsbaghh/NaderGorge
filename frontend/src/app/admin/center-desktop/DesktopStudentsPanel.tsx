'use client';

import { useEffect, useState } from 'react';
import { ArrowRight, Clock3, RefreshCw, Search } from 'lucide-react';
import { searchDesktopStudents, type DesktopGroup, type DesktopReceipt, type DesktopStudentSearch } from '@/services/center-desktop-service';
import { timestamp } from './display';
import DesktopLessonHistory from './DesktopLessonHistory';
import DesktopStudentPdfButton from './DesktopStudentPdfButton';

function ageLabel(value: string, now: number) {
  const minutes = Math.floor((now - Date.parse(value)) / 60_000);
  if (!Number.isFinite(minutes)) return '';
  if (minutes < 1) return 'منذ أقل من دقيقة';
  if (minutes < 60) return `منذ ${minutes} دقيقة`;
  if (minutes < 1440) return `منذ ${Math.floor(minutes / 60)} ساعة`;
  return `منذ ${Math.floor(minutes / 1440)} يوم`;
}

export default function DesktopStudentsPanel({ receipt, onRefresh, refreshing = false }: {
  receipt: DesktopReceipt; onRefresh?: () => void; refreshing?: boolean;
}) {
  const [query, setQuery] = useState('');
  const [studentId, setStudentId] = useState<string | null>(null);
  const [centerId, setCenterId] = useState('');
  const [gradeId, setGradeId] = useState('');
  const [groupId, setGroupId] = useState('');
  const [groups, setGroups] = useState<DesktopGroup[]>([]);
  const [response, setResponse] = useState<{ key: string; data: DesktopStudentSearch } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  const requestKey = JSON.stringify([receipt.uploadId, query, studentId, centerId, gradeId, groupId, retry]);
  const result = response?.key === requestKey ? response.data : null;
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setLoading(true); setError('');
      void searchDesktopStudents(receipt.uploadId, { q: query, studentId, centerId, gradeId, groupId }, controller.signal)
        .then(data => {
          if (!controller.signal.aborted) {
            setResponse({ key: requestKey, data }); setGroups(data.groups ?? []);
          }
        })
        .catch(() => { if (!controller.signal.aborted) setError('تعذر قراءة الطلاب من هذه النسخة. حاول مرة أخرى أو حدّث بيانات الطلاب.'); })
        .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    }, 250);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [receipt.uploadId, query, studentId, centerId, gradeId, groupId, requestKey]);

  const student = studentId ? result?.students[0] : null;
  const profile = result?.profile;
  const centers = [...new Map(groups.filter(g => g.centerId).map(g => [g.centerId, g.center || g.centerId])).entries()];
  const grades = [...new Map(groups.filter(g => g.gradeId && (!centerId || g.centerId === centerId)).map(g => [g.gradeId, g.grade || g.gradeId])).entries()];
  const availableGroups = groups.filter(g => (!centerId || g.centerId === centerId) && (!gradeId || g.gradeId === gradeId));
  const resetSelection = () => { setStudentId(null); setResponse(null); setLoading(true); setError(''); };
  const busy = loading || (!result && !error);

  return <section className="admin-panel space-y-6 p-4 sm:p-6" aria-label="بحث طلاب السنتر">
    <div className="flex flex-wrap items-end gap-4">
      <label className="min-w-0 flex-1 basis-72 space-y-2"><span className="block font-semibold">البحث عن طالب</span>
        <div className="relative"><Search aria-hidden className="pointer-events-none absolute start-3 top-3.5 h-4 w-4 text-[var(--admin-muted)]" /><input className="admin-input w-full ps-10" type="search" maxLength={100} value={query} onChange={e => { resetSelection(); setQuery(e.target.value); }} placeholder="اسم الطالب، الكود، الباركود، موبايل الطالب أو ولي الأمر" /></div>
      </label>
      <div className="flex min-w-0 flex-wrap items-start gap-3 text-sm" aria-label="آخر تحديث لبيانات الطلاب">
        <Clock3 aria-hidden className="mt-1 h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
        <div><p className="font-medium">آخر تحديث للبيانات</p><time dateTime={receipt.createdAt}>{timestamp(receipt.createdAt)}</time><p className="mt-1 text-xs text-[var(--admin-muted)]">{ageLabel(receipt.createdAt, now)}</p>
          <details className="mt-1 text-xs text-[var(--admin-muted)]"><summary className="cursor-pointer">تفاصيل النسخة</summary><div className="mt-2 space-y-1"><p>وصلت للمنصة: {timestamp(receipt.receivedAt)}</p><p>إصدار البرنامج: <bdi>{receipt.app.version}</bdi></p><p className="max-w-64 break-all">رقم النسخة: <bdi>{receipt.uploadId}</bdi></p></div></details>
        </div>
        {onRefresh && <button type="button" className="admin-btn-ghost" aria-label="تحديث أحدث نسخة للطلاب" disabled={refreshing} onClick={onRefresh}><RefreshCw aria-hidden className={`h-4 w-4 ${refreshing ? 'motion-safe:animate-spin' : ''}`} /></button>}
      </div>
    </div>
    {!studentId && <div className="flex flex-wrap gap-3">
      <label className="min-w-40 flex-1 space-y-1 text-sm"><span className="block">السنتر</span><select className="admin-input w-full" value={centerId} onChange={e => { resetSelection(); setCenterId(e.target.value); setGradeId(''); setGroupId(''); }}><option value="">كل السناتر</option>{centers.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      <label className="min-w-40 flex-1 space-y-1 text-sm"><span className="block">الصف الدراسي</span><select className="admin-input w-full" value={gradeId} onChange={e => { resetSelection(); setGradeId(e.target.value); setGroupId(''); }}><option value="">كل الصفوف</option>{grades.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      <label className="min-w-40 flex-1 space-y-1 text-sm"><span className="block">المجموعة</span><select className="admin-input w-full" value={groupId} onChange={e => { resetSelection(); setGroupId(e.target.value); }}><option value="">كل المجموعات</option>{availableGroups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}</select></label>
    </div>}
    {studentId && <button type="button" className="admin-btn-ghost inline-flex items-center gap-2" onClick={resetSelection}><ArrowRight aria-hidden className="h-4 w-4" />رجوع لنتائج البحث</button>}
    {error && <div role="alert" className="flex flex-wrap items-center gap-3 text-[var(--admin-danger)]"><p>{error}</p><button type="button" className="admin-btn-ghost" onClick={() => { setError(''); setLoading(true); setRetry(v => v + 1); }}>إعادة المحاولة</button></div>}
    {busy && <div role="status" className="space-y-3 py-4"><p className="text-sm text-[var(--admin-muted)]">جاري تحميل بيانات الطالب…</p><div aria-hidden className="h-12 rounded-lg bg-[var(--admin-card-soft)] motion-safe:animate-pulse" /><div aria-hidden className="h-12 rounded-lg bg-[var(--admin-card-soft)] motion-safe:animate-pulse" /></div>}
    {!studentId && result && <>
      <p className="text-sm text-[var(--admin-muted)]">{result.total} طالب مطابق{result.total > result.students.length ? ` · المعروض أول ${result.students.length}، ضيّق البحث لعرض الطالب المطلوب` : ''}</p>
      {!result.students.length ? <p className="py-8 text-center">لا يوجد طالب مطابق. جرّب الكود أو رقم الطالب أو ولي الأمر.</p> : <div className="overflow-x-auto"><table className="w-full min-w-[740px] text-start text-sm"><thead className="bg-[var(--admin-card-soft)]"><tr>{['الكود', 'الطالب والمجموعة', 'السنتر', 'موبايل الطالب', 'ولي الأمر', 'السجل'].map(v => <th scope="col" className="px-3 py-3 text-start" key={v}>{v}</th>)}</tr></thead><tbody className="divide-y divide-[var(--admin-border)]">
        {result.students.map(s => <tr className="hover:bg-[var(--admin-card-soft)]" key={s.id}><td className="p-3"><bdi>{s.code}</bdi></td><th scope="row" className="px-3 py-4 text-start"><p className="font-semibold">{s.name}</p><p className="mt-1 font-normal text-[var(--admin-muted)]">{s.groups.join('، ')}</p></th><td className="p-3">{s.centers?.join('، ') || 'غير متوفر'}</td><td className="p-3"><bdi>{s.phone || 'غير مسجل'}</bdi></td><td className="p-3"><bdi>{s.guardianPhone || 'غير مسجل'}</bdi></td><td className="p-3"><button type="button" className="admin-btn-ghost whitespace-nowrap" aria-label={`عرض سجل ${s.name}`} onClick={() => { setResponse(null); setLoading(true); setStudentId(s.id); }}>عرض السجل</button></td></tr>)}
      </tbody></table></div>}
    </>}
    {studentId && result && !student && <p role="status">الطالب غير موجود في هذه النسخة. ارجع للبحث أو حدّث النسخة.</p>}
    {student && profile && <div className="space-y-6">
      <div className="space-y-2 border-b border-[var(--admin-border)] pb-5"><div className="flex flex-wrap items-center justify-between gap-3"><div className="space-y-1"><h3 className="text-xl font-bold">{student.name}</h3><p className="font-semibold">الكود <bdi>{student.code}</bdi>{student.suspended ? ' · موقوف' : ''}</p></div><DesktopStudentPdfButton student={student} profile={profile} receipt={receipt} /></div>
        <p className="text-sm">موبايل الطالب: <bdi>{student.phone || 'غير مسجل'}</bdi> · ولي الأمر: <bdi>{student.guardianPhone || 'غير مسجل'}</bdi></p>
        <p className="text-sm">السنتر: {student.centers?.join('، ') || 'غير متوفر'}</p><p className="text-sm text-[var(--admin-muted)]">{student.groups.join('، ')}</p>
        {student.notes && <p className="whitespace-pre-wrap text-sm">{student.notes}</p>}
        <p className="text-sm">حضور وتعويض: <strong>{profile.present}</strong> · غياب مسجل: <strong>{profile.absent}</strong> · الخصم الثابت: {student.discountPercent ?? 0}٪</p>
      </div>
      <DesktopLessonHistory key={student.id} lessons={profile.lessons ?? []} />
    </div>}
    <p className="text-xs text-[var(--admin-muted)]">عرض من النسخة المرفوعة. الحضور والدفع يتسجلوا على الهوست، ويظهروا هنا بعد نجاح المزامنة.</p>
  </section>;
}
