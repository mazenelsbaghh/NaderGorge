'use client';

import { useEffect, useId, useState } from 'react';
import { liveSupportService, type LiveSupportPurchaseCourse, type LiveSupportPurchasePreview } from '@/services/live-support-service';
import { createClientId } from '@/lib/client-id';

type Props = { conversationId: string; onCompleted: (message: string) => void; onBusyChange: (busy: boolean) => void };
const money = (amount: number) => `${amount.toLocaleString('ar-EG', { maximumFractionDigits: 2 })} ج.م`;
const inputClass = 'mt-1 h-11 w-full rounded-xl border border-slate-300 px-3 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-teal-600 disabled:bg-slate-100';

export function StudentCoursePurchaseForm({ conversationId, onCompleted, onBusyChange }: Props) {
  const formId = useId();
  const [courses, setCourses] = useState<LiveSupportPurchaseCourse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [loadFailed, setLoadFailed] = useState(false);
  const [search, setSearch] = useState('');
  const [packageId, setPackageId] = useState('');
  const [reason, setReason] = useState('');
  const [preview, setPreview] = useState<LiveSupportPurchasePreview>();
  const [idempotencyKey, setIdempotencyKey] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    liveSupportService.getPurchaseCourses(conversationId, controller.signal).then(available => {
      if (controller.signal.aborted) return;
      setCourses(available);
      setLoadFailed(false);
    }).catch(() => {
      if (!controller.signal.aborted) setLoadFailed(true);
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [conversationId, loadAttempt]);

  const matches = courses.filter(course => `${course.name} ${course.teacherName} ${course.subjectName}`.includes(search.trim()));

  async function review() {
    setBusy(true); onBusyChange(true); setError('');
    try {
      setPreview(await liveSupportService.previewCoursePurchase(conversationId, packageId));
      setIdempotencyKey(createClientId());
    } catch (cause) {
      setError((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر حساب تكلفة الشراء. أعد المحاولة.');
    } finally {
      setBusy(false); onBusyChange(false);
    }
  }

  async function purchase() {
    if (!preview || !preview.funding.isSufficient) return;
    setBusy(true); onBusyChange(true); setError('');
    try {
      const receipt = await liveSupportService.executeStudentAction<Record<string, unknown>, { message: string }>(conversationId, 'student.course.purchase', idempotencyKey,
        preview.confirmationVersion, { packageId: preview.course.packageId, quoteVersion: preview.quoteVersion, reason });
      onCompleted(receipt.message);
    } catch (cause) {
      const response = (cause as { response?: { data?: { message?: string; errors?: string[] } } }).response?.data;
      if (response?.errors?.some(code => ['CONFIRMATION_STALE', 'PURCHASE_QUOTE_STALE', 'COURSE_UNAVAILABLE', 'INSUFFICIENT_BALANCE'].includes(code))) {
        setPreview(undefined);
        setIdempotencyKey('');
      }
      setError(response?.message ?? 'لم يصل تأكيد الشراء. أعد المحاولة بنفس الطلب للتحقق من نتيجته.');
    } finally {
      setBusy(false); onBusyChange(false);
    }
  }

  if (loading) return <p role="status" className="py-8 text-sm text-slate-700">جارٍ تحميل الكورسات المتاحة للطالب…</p>;
  if (loadFailed) return <div role="alert" className="space-y-3 py-5 text-sm text-red-800"><p>تعذر تحميل الكورسات.</p><button type="button" onClick={() => { setLoading(true); setLoadAttempt(attempt => attempt + 1); }} className="min-h-11 font-bold underline">إعادة المحاولة</button></div>;
  if (!courses.length) return <p className="py-6 text-sm text-slate-700">لا توجد كورسات متاحة للشراء لهذا الطالب. الكورسات المشترك فيها أو المغلقة للبيع لا تظهر هنا.</p>;

  return <div className="space-y-4 py-5 text-slate-900">
    {!preview ? <form onSubmit={event => { event.preventDefault(); void review(); }} className="space-y-4">
      <label htmlFor={`${formId}-search`} className="block text-sm font-semibold">ابحث بالمدرس أو المادة أو اسم الكورس
        <input id={`${formId}-search`} value={search} disabled={busy} onChange={event => setSearch(event.target.value)} className={inputClass} />
      </label>
      <label htmlFor={`${formId}-course`} className="block text-sm font-semibold">الكورس
        <select id={`${formId}-course`} required value={packageId} disabled={busy} onChange={event => setPackageId(event.target.value)} className={inputClass}>
          <option value="">اختر الكورس</option>
          {courses.filter(course => matches.includes(course) || course.packageId === packageId).map(course => <option key={course.packageId} value={course.packageId}>{course.teacherName} · {course.subjectName} · {course.name} ({money(course.price)})</option>)}
        </select>
      </label>
      {!matches.length && <p className="text-sm text-slate-700">لا توجد نتائج مطابقة للبحث.</p>}
      <label htmlFor={`${formId}-reason`} className="block text-sm font-semibold">سبب الشراء للطالب
        <input id={`${formId}-reason`} required maxLength={500} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} className={inputClass} />
      </label>
      <button disabled={busy || !packageId || !reason.trim()} className="min-h-11 w-full rounded-xl bg-slate-900 px-4 font-bold text-white disabled:bg-slate-400">{busy ? 'جارٍ حساب التكلفة…' : 'مراجعة السعر والرصيد'}</button>
    </form> : <>
      <div className="border-b border-slate-200 pb-3"><h4 className="font-bold">{preview.course.name}</h4><p className="mt-1 text-sm text-slate-700">{preview.course.teacherName} · {preview.course.subjectName}</p></div>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
        <dt>إجمالي سعر الشراء</dt><dd className="text-left font-bold">{money(preview.funding.discountedPrice)}</dd>
        <dt>رصيد الطالب العام</dt><dd className="text-left">{money(preview.funding.currentPaidBalance)}</dd>
        <dt>المستخدم من الرصيد الإضافي المتاح للكورس</dt><dd className="text-left">{money(preview.funding.promotionalAmountToUse)}</dd>
        <dt>المطلوب من الرصيد العام</dt><dd className="text-left font-bold">{money(preview.funding.paidAmountToUse)}</dd>
      </dl>
      {!preview.funding.isSufficient && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-800">رصيد الطالب لا يكفي. المتبقي المطلوب {money(preview.funding.paidAmountToUse - preview.funding.currentPaidBalance)}.</p>}
      <div className="flex gap-3">
        <button type="button" disabled={busy} onClick={() => { setPreview(undefined); setError(''); }} className="min-h-11 flex-1 rounded-xl border border-slate-300 px-3 font-semibold">رجوع</button>
        <button type="button" disabled={busy || !preview.funding.isSufficient} onClick={() => void purchase()} className="min-h-11 flex-1 rounded-xl bg-teal-700 px-3 font-bold text-white disabled:bg-slate-400">{busy ? 'جارٍ تنفيذ الشراء…' : 'تأكيد الشراء من رصيد الطالب'}</button>
      </div>
    </>}
    {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{error}</p>}
  </div>;
}
