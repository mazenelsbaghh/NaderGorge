'use client';

import { useEffect, useState } from 'react';
import { searchDesktopStudents, type DesktopReceipt, type DesktopStudentSearch } from '@/services/center-desktop-service';
import { timestamp } from './display';

const attendanceLabel = (value: string) => ({ present: 'حاضر', absent: 'غائب', makeup: 'معوّض' }[value] ?? value);
const homeworkLabel = (value: string) => ({ complete: 'كامل', incomplete: 'ناقص', missing: 'لم يعمل', notReviewed: 'لم يُراجع' }[value] ?? value);

export default function DesktopStudentsPanel({ receipt }: { receipt: DesktopReceipt }) {
  const [query, setQuery] = useState('');
  const [studentId, setStudentId] = useState<string | null>(null);
  const [result, setResult] = useState<DesktopStudentSearch | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setLoading(true); setError(''); setResult(null);
      void searchDesktopStudents(receipt.uploadId, query, studentId, controller.signal)
        .then(value => { if (!controller.signal.aborted) setResult(value); })
        .catch(() => { if (!controller.signal.aborted) setError('تعذر قراءة الطلاب من النسخة. جرّب مرة أخرى أو اختَر نسخة أحدث.'); })
        .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    }, 350);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [receipt.uploadId, query, studentId]);
  const student = studentId ? result?.students[0] : null;
  const profile = result?.profile;
  return <section className="admin-panel space-y-5 p-5" aria-label="طلاب النسخة المرفوعة">
    <h2 className="text-xl font-bold">بحث الطلاب وسجل الحضور</h2>
    <p className="text-sm text-[var(--admin-muted)]">عرض فقط من نسخة {receipt.centerId} المرفوعة {timestamp(receipt.receivedAt)} · الإصدار <bdi>{receipt.app.version}</bdi>. التسجيلات الأحدث على الجهاز تظهر بعد رفعها.</p>
    <label className="block space-y-2"><span>الاسم أو الكود أو الباركود أو رقم الهاتف</span>
      <input className="admin-input w-full" type="search" value={query} onChange={e => { setStudentId(null); setResult(null); setQuery(e.target.value); }} placeholder="ابحث داخل بيانات هذه النسخة" />
    </label>
    {studentId && <button className="admin-btn-ghost" onClick={() => { setStudentId(null); setResult(null); }}>رجوع لنتائج البحث</button>}
    {loading && <p role="status">جاري قراءة النسخة…</p>}
    {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
    {!studentId && result && <>
      <p>{result.total} طالب مطابق · المعروض أول ٥٠ نتيجة</p>
      <div className="overflow-x-auto"><table className="w-full text-start text-sm"><thead className="bg-[var(--admin-card-soft)]"><tr>{['الكود', 'الاسم', 'المجموعات', 'الهاتف', 'السجل'].map(v => <th className="p-3 text-start" key={v}>{v}</th>)}</tr></thead><tbody>
        {result.students.map(s => <tr className="border-b border-[var(--admin-border)]" key={s.id}><td className="p-3"><bdi>{s.code}</bdi></td><td>{s.name}</td><td>{s.groups.join('، ')}</td><td><bdi>{s.phone}</bdi></td><td><button className="admin-btn-ghost" onClick={() => { setResult(null); setStudentId(s.id); }}>عرض البروفايل</button></td></tr>)}
      </tbody></table></div>
    </>}
    {student && profile && <>
      <h3 className="text-lg font-bold">{student.name} · <bdi>{student.code}</bdi>{student.suspended ? ' · موقوف' : ''}</h3>
      <p>الطالب: <bdi>{student.phone || '—'}</bdi> · ولي الأمر: <bdi>{student.guardianPhone || '—'}</bdi> · الخصم الثابت: {student.discountPercent ?? 0}٪</p>
      <p>{student.groups.join('، ')}</p><p className="whitespace-pre-wrap">{student.notes || 'لا توجد ملاحظات'}</p>
      <p>حضور ومعوّض: {profile.present} · غياب مسجل: {profile.absent}</p>
      <div className="overflow-x-auto"><table className="w-full text-sm"><caption className="mb-2 text-start font-bold">الحضور والغياب · {profile.attendanceTotal} سجل (آخر ٥٠٠)</caption><thead className="bg-[var(--admin-card-soft)]"><tr>{['المجموعة', 'الشهر / الحصة', 'التاريخ', 'الحالة'].map(v => <th key={v} className="p-3 text-start">{v}</th>)}</tr></thead><tbody>{profile.attendances.map(a => <tr key={a.id} className="border-b border-[var(--admin-border)]"><td className="p-3">{a.lesson.group}</td><td>{a.lesson.month ?? '—'} / {a.lesson.number ?? '—'}</td><td>{a.lesson.date ? timestamp(a.lesson.date) : 'غير متوفر'}</td><td className={a.status === 'absent' ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-success)]'}>{attendanceLabel(a.status)}</td></tr>)}</tbody></table></div>
      <div className="overflow-x-auto"><table className="w-full text-sm"><caption className="mb-2 text-start font-bold">الدرجات والواجبات · {profile.examTotal} سجل (آخر ٥٠٠)</caption><thead className="bg-[var(--admin-card-soft)]"><tr>{['المجموعة', 'الشهر / الحصة', 'الدرجة', 'الواجب'].map(v => <th key={v} className="p-3 text-start">{v}</th>)}</tr></thead><tbody>{profile.exams.map(a => <tr key={a.id} className="border-b border-[var(--admin-border)]"><td className="p-3">{a.lesson.group}</td><td>{a.lesson.month ?? '—'} / {a.lesson.number ?? '—'}</td><td>{a.absent ? 'غائب' : a.score == null ? 'لم يُرصد' : `${a.score} / ${a.maxScore ?? 'غير محدد'}`}</td><td>{homeworkLabel(a.homework)}</td></tr>)}</tbody></table></div>
    </>}
  </section>;
}
