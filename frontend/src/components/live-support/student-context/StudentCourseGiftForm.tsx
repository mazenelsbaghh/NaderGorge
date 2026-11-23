'use client';

import { useEffect, useId, useState } from 'react';
import { liveSupportService, type LiveSupportActionDefinition, type LiveSupportGiftCourse } from '@/services/live-support-service';
import { createClientId } from '@/lib/client-id';

type Props = { conversationId: string; onCompleted: (message: string) => void; onBusyChange: (busy: boolean) => void };
const inputClass = 'mt-1 h-11 w-full rounded-xl border border-slate-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-teal-600 disabled:bg-slate-100';

export function StudentCourseGiftForm({ conversationId, onCompleted, onBusyChange }: Props) {
  const formId = useId();
  const [courses, setCourses] = useState<LiveSupportGiftCourse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [loadFailed, setLoadFailed] = useState(false);
  const [search, setSearch] = useState('');
  const [packageId, setPackageId] = useState('');
  const [reason, setReason] = useState('');
  const [confirmation, setConfirmation] = useState<{ action: LiveSupportActionDefinition; course: LiveSupportGiftCourse; key: string }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    liveSupportService.getGiftCourses(conversationId, controller.signal).then(available => {
      if (controller.signal.aborted) return;
      setCourses(available); setLoadFailed(false);
    }).catch(() => {
      if (!controller.signal.aborted) setLoadFailed(true);
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [conversationId, loadAttempt]);

  async function review() {
    setBusy(true); onBusyChange(true); setError('');
    try {
      const [catalog, available] = await Promise.all([liveSupportService.getActionCatalog(conversationId), liveSupportService.getGiftCourses(conversationId)]);
      const action = catalog.find(item => item.key === 'student.course.gift');
      const course = available.find(item => item.id === packageId);
      if (!action || !course) { setCourses(available); setError('الكورس لم يعد متاحًا كهدية لهذا الطالب. اختر كورسًا آخر.'); return; }
      setConfirmation({ action, course, key: createClientId() });
    } catch {
      setError('تعذر مراجعة الهدية. أعد المحاولة.');
    } finally {
      setBusy(false); onBusyChange(false);
    }
  }

  async function issue() {
    if (!confirmation) return;
    setBusy(true); onBusyChange(true); setError('');
    try {
      const receipt = await liveSupportService.executeStudentAction<Record<string, unknown>, { message: string }>(conversationId, 'student.course.gift', confirmation.key,
        confirmation.action.confirmationVersion, { packageId: confirmation.course.id, reason });
      onCompleted(receipt.message);
    } catch (cause) {
      const response = (cause as { response?: { data?: { message?: string; errors?: string[] } } }).response?.data;
      if (response?.errors?.some(code => ['CONFIRMATION_STALE', 'COURSE_UNAVAILABLE'].includes(code))) setConfirmation(undefined);
      setError(response?.message ?? 'لم يصل تأكيد إضافة الهدية. أعد المحاولة بنفس الطلب للتحقق من نتيجته.');
    } finally {
      setBusy(false); onBusyChange(false);
    }
  }

  if (loading) return <p role="status" className="py-8 text-sm text-slate-700">جارٍ تحميل كورسات الهدايا…</p>;
  if (loadFailed) return <div role="alert" className="space-y-3 py-5 text-sm text-red-800"><p>تعذر تحميل كورسات الهدايا.</p><button type="button" onClick={() => { setLoading(true); setLoadAttempt(attempt => attempt + 1); }} className="min-h-11 font-bold underline">إعادة المحاولة</button></div>;
  if (!courses.length) return <p className="py-6 text-sm text-slate-700">لا توجد كورسات متاحة كهدية لهذا الطالب ضمن مرحلته وصفه.</p>;

  return <div className="space-y-4 py-5 text-slate-900">
    {!confirmation ? <form onSubmit={event => { event.preventDefault(); void review(); }} className="space-y-4">
      <label htmlFor={`${formId}-search`} className="block text-sm font-semibold">ابحث بالمدرس أو اسم الكورس
        <input id={`${formId}-search`} value={search} disabled={busy} onChange={event => setSearch(event.target.value)} className={inputClass} />
      </label>
      <label htmlFor={`${formId}-course`} className="block text-sm font-semibold">الكورس الهدية
        <select id={`${formId}-course`} required disabled={busy} value={packageId} onChange={event => setPackageId(event.target.value)} className={inputClass}>
          <option value="">اختر الكورس</option>
          {courses.filter(course => course.id === packageId || `${course.context ?? ''} ${course.name}`.includes(search.trim())).map(course => <option key={course.id} value={course.id}>{course.context ? `${course.context} · ` : ''}{course.name}</option>)}
        </select>
      </label>
      <label htmlFor={`${formId}-reason`} className="block text-sm font-semibold">سبب إضافة الهدية
        <input id={`${formId}-reason`} required maxLength={500} disabled={busy} value={reason} onChange={event => setReason(event.target.value)} className={inputClass} />
      </label>
      <button disabled={busy || !packageId || !reason.trim()} className="min-h-11 w-full rounded-xl bg-slate-900 px-4 font-bold text-white disabled:bg-slate-400">{busy ? 'جارٍ المراجعة…' : 'مراجعة الهدية'}</button>
    </form> : <>
      <h4 className="font-bold">{confirmation.course.name}</h4>
      {confirmation.course.context && <p className="text-sm text-slate-700">{confirmation.course.context}</p>}
      <p className="text-sm leading-6">سيُضاف الكورس للطالب هدية دون خصم أي رصيد، وستُسجل الهدية باسمك.</p>
      <div className="flex gap-3">
        <button type="button" disabled={busy} onClick={() => { setConfirmation(undefined); setError(''); }} className="min-h-11 flex-1 rounded-xl border border-slate-300 px-3 font-semibold">رجوع</button>
        <button type="button" disabled={busy} onClick={() => void issue()} className="min-h-11 flex-1 rounded-xl bg-teal-700 px-3 font-bold text-white disabled:bg-slate-400">{busy ? 'جارٍ إضافة الهدية…' : 'تأكيد إضافة الهدية'}</button>
      </div>
    </>}
    {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{error}</p>}
  </div>;
}
