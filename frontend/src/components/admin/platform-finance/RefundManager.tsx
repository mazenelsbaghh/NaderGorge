'use client';

import Link from 'next/link';
import axios from 'axios';
import { useHasPermission } from '@/hooks/useHasPermission';
import { FormEvent, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import platformFinanceService, { type FinanceBootstrap, type PlatformRefundRow, type RefundStudent, type RefundUsagePreview } from '@/services/platform-finance-service';
import { isExternallyRefundableGrant, refundablePurchaseOperationId, refundSourceKey } from '@/lib/refund-source';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function RefundManager({ staff = false }: { staff?: boolean }) {
  const { hasPermission } = useHasPermission();
  const canCreate = hasPermission('finance.refunds.create');
  const canReverse = hasPermission('finance.refunds.post');
  const [rows, setRows] = useState<PlatformRefundRow[]>([]);
  const [bootstrap, setBootstrap] = useState<Pick<FinanceBootstrap, 'treasuryAccounts'> | null>(null);
  const [error, setError] = useState('');
  const [phone, setPhone] = useState('');
  const [students, setStudents] = useState<Array<{ id: string; fullName: string; phoneNumber: string }>>([]);
  const [student, setStudent] = useState<RefundStudent | null>(null);
  const [grantId, setGrantId] = useState('');
  const [treasuryId, setTreasuryId] = useState('');
  const [refundAmount, setRefundAmount] = useState('');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [preview, setPreview] = useState<RefundUsagePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState('');
  const [previewKey, setPreviewKey] = useState('');
  const studentRequest = useRef(0);

  const load = async () => {
    try {
      const [refunds, financeBootstrap] = await Promise.all([
        platformFinanceService.getRefunds(),
        platformFinanceService.refundBootstrap(),
      ]);
      setRows(refunds);
      setBootstrap(financeBootstrap);
    } catch {
      setError('تعذر تحميل الاستردادات');
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    const selected = student?.packages.find(item => item.accessGrantId === grantId && item.isActive);
    if (!student || !selected) {
      setPreview(null);
      setPreviewKey('');
      setPreviewError('');
      setPreviewLoading(false);
      return;
    }
    let current = true;
    setPreview(null);
    setPreviewKey('');
    setPreviewError('');
    setPreviewLoading(true);
    const sourceKey = refundSourceKey(selected);
    void platformFinanceService.getRefundUsagePreview(student.id, selected.accessGrantId, refundablePurchaseOperationId(selected))
      .then(result => {
        if (!current) return;
        setPreview(result);
        setPreviewKey(`${student.id}:${selected.accessGrantId}:${sourceKey}`);
        setRefundAmount('');
      })
      .catch((caught: unknown) => {
        if (!current) return;
        setPreviewError(axios.isAxiosError(caught) && caught.response?.status === 404
          ? 'هذه المنحة هدية أو كود، ولا يوجد مبلغ مدفوع من الطالب يمكن استرداده.'
          : 'تعذر تحميل استخدام هذا المحتوى. لا تنفذ الاسترداد قبل التحقق.');
      })
      .finally(() => { if (current) setPreviewLoading(false); });
    return () => { current = false; };
  }, [student, grantId]);

  async function searchStudent() {
    try {
      const matches = await platformFinanceService.findRefundStudents(phone.trim());
      setStudents(matches);
      if (matches.length === 0) toast.error('لا يوجد طالب بهذا الرقم');
    } catch { toast.error('تعذر البحث؛ اكتب رقم الهاتف كاملًا وحاول مجددًا'); }
  }

  async function selectStudent(userId: string) {
    const request = ++studentRequest.current;
    const selectedStudent = await platformFinanceService.getRefundStudent(userId);
    if (request !== studentRequest.current) return;
    setStudent(selectedStudent);
    setGrantId('');
    setStudents([]);
  }

  async function createExternalRefund(event: FormEvent) {
    event.preventDefault();
    const selectedPackage = student?.packages.find(item => item.accessGrantId === grantId && item.isActive);
    const amount = Number(refundAmount);
    const sourceKey = selectedPackage ? refundSourceKey(selectedPackage) : '';
    const selectionKey = student && selectedPackage ? `${student.id}:${selectedPackage.accessGrantId}:${sourceKey}` : '';
    if (!canCreate || !student || !selectedPackage || !preview || previewKey !== selectionKey || !treasuryId || !reason.trim() || !Number.isFinite(amount) || amount <= 0 || amount > preview.remainingRefundableAmount) return;
    const teacherRatio = !preview.isHistoricalSource && selectedPackage.paidAmount > 0 ? selectedPackage.teacherShareAmount / selectedPackage.paidAmount : 0;
    const teacherAmount = Math.min(amount, Math.max(0, Number((amount * teacherRatio).toFixed(2))));
    setSubmitting(true);
    try {
      await platformFinanceService.createExternalPackageRefund({
        accessGrantId: selectedPackage.accessGrantId,
        purchaseOperationId: refundablePurchaseOperationId(selectedPackage),
        studentId: student.id,
        teacherId: selectedPackage.teacherId || undefined,
        platformAmount: amount - teacherAmount,
        teacherAmount,
        treasuryAccountId: treasuryId,
        reason: reason.trim(),
      });
      toast.success('تم إلغاء الباقة وتسجيل الاسترداد الخارجي في المركز المالي');
      setStudent(await platformFinanceService.getRefundStudent(student.id));
      setGrantId('');
      setRefundAmount('');
      setReason('');
      await load();
    } catch (caught: any) {
      toast.error(caught?.response?.data?.message || 'تعذر تنفيذ الاسترداد');
    } finally {
      setSubmitting(false);
    }
  }

  async function reverse(id: string) {
    const reversalReason = window.prompt('سبب عكس الاسترداد؟');
    if (!reversalReason) return;
    try { await platformFinanceService.reverseRefund(id, reversalReason); await load(); }
    catch { setError('تعذر عكس الاسترداد'); }
  }

  const activePackages = student?.packages.filter(isExternallyRefundableGrant) || [];
  const selectedRefundPackage = activePackages.find(item => item.accessGrantId === grantId);
  const isZeroCashExternalReview = Boolean(selectedRefundPackage?.purchaseOperationId && selectedRefundPackage.paidAmount <= 0);

  return <div className="space-y-6" dir="rtl">
    <section className="admin-panel rounded-2xl p-6">
      <div className="mb-5">
        <h2 className="text-lg font-black">استرداد خارجي لطالب</h2>
        <p className="mt-1 text-xs text-[var(--admin-muted)]">يلغي الباقة بدون إضافة رصيد للطالب، ويسجل المبلغ الخارج من الخزنة كاسترداد في المركز المالي.</p>
      </div>
      {canCreate ? <form onSubmit={createExternalRefund} className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <label className="mb-1 block text-xs font-bold">رقم هاتف الطالب</label>
          <div className="flex gap-2">
            <input className="admin-input" value={phone} onChange={event => setPhone(event.target.value)} placeholder="01xxxxxxxxx" />
            <button className="admin-btn-ghost shrink-0" type="button" onClick={() => void searchStudent()}>بحث</button>
          </div>
          {students.length > 0 ? <div className="mt-2 divide-y rounded-xl border border-[var(--admin-border)]">{students.map(item => <button key={item.id} type="button" onClick={() => void selectStudent(item.id)} className="flex w-full justify-between p-3 text-right hover:bg-[var(--admin-card-strong)]"><b>{item.fullName}</b><bdi className="font-mono">{item.phoneNumber}</bdi></button>)}</div> : null}
          {student ? <p className="mt-2 rounded-xl bg-emerald-500/10 p-3 text-sm font-bold text-emerald-700">تم اختيار: {student.fullName} — <bdi>{student.phone}</bdi></p> : null}
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">الباقة التي سيتم إلغاؤها</label>
          <select className="admin-input" required value={grantId} onChange={event => { setGrantId(event.target.value); setPreview(null); setPreviewKey(''); setRefundAmount(''); }} disabled={!student}>
            <option value="">اختر باقة نشطة</option>
            {activePackages.map(item => <option key={item.accessGrantId} value={item.accessGrantId}>{item.name} — {refundablePurchaseOperationId(item) ? `المدفوع ${money(item.paidAmount)}` : `مراجعة يدوية (الحد ${money(item.price)})`}</option>)}
          </select>
          {student && activePackages.length === 0 ? <p className="mt-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm font-bold text-amber-800">لا توجد لهذا الطالب باقة مدفوعة قابلة للاسترداد. باقات الهدايا والأكواد لا تحتوي مبلغًا مدفوعًا من الطالب.</p> : null}
        </div>
        <div className="md:col-span-2" aria-live="polite">
          {previewLoading ? <p className="rounded-xl border border-[var(--admin-border)] p-4 text-sm text-[var(--admin-muted)]">جارٍ تحميل المشاهدة ومحاولات الامتحانات…</p> : null}
          {previewError ? <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm font-bold text-rose-700">{previewError}</p> : null}
          {preview ? <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div><p className="text-xs font-bold text-[var(--admin-muted)]">نطاق الاستخدام</p><p className="font-black">{preview.scopeLabel}</p></div>
              <div className="text-left"><p className="text-xs font-bold text-[var(--admin-muted)]">المتاح للاسترداد</p><p className="font-black text-[var(--admin-primary)]">{money(preview.remainingRefundableAmount)}</p></div>
            </div>
            <div className="grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
              <p><b>الفيديوهات المشاهدة:</b> {preview.videosAvailable ? `${preview.watchedVideos} من ${preview.totalVideos}` : 'غير متاح لهذا النطاق'}</p>
              <p><b>الفيديوهات المكتملة:</b> {preview.videosAvailable ? `${preview.completedVideos} من ${preview.totalVideos}${preview.unknownDurationVideos > 0 ? `، ومدة ${preview.unknownDurationVideos} غير معروفة` : ''}` : 'غير متاح لهذا النطاق'}</p>
              <p><b>الامتحانات المُجرّبة:</b> {preview.examsAvailable ? `${preview.attemptedExams} من ${preview.totalExams}` : 'غير متاح لهذا النطاق'}</p>
              <p><b>المحاولات:</b> {preview.examsAvailable ? `${preview.totalAttempts} (${preview.submittedAttempts} مُسلّمة)` : 'غير متاح لهذا النطاق'}</p>
            </div>
            {!preview.usageAvailable ? <p className="text-sm font-bold text-amber-700">{preview.unavailableReason}</p> : null}
            {preview.isHistoricalSource ? <p className="rounded-xl bg-amber-500/10 p-3 text-sm font-bold text-amber-800">{isZeroCashExternalReview ? 'عملية الشراء المسجلة مدفوعة بالكامل من رصيد ترويجي، والمدفوع من الطالب فيها صفر. أدخل فقط مبلغًا خارجيًا تأكدت أنه رُد فعليًا؛ السعر الظاهر حد أقصى وليس إثبات دفع.' : 'لا يوجد مبلغ مدفوع موثّق يمكن الاعتماد عليه لهذه المنحة. أدخل فقط المبلغ الذي تأكدت أنه دُفع فعليًا؛ السعر الظاهر حد أقصى وليس إثبات دفع.'}</p> : null}
            <p className="text-xs text-[var(--admin-muted)]">{preview.isHistoricalSource ? 'الحد الأقصى' : 'المدفوع'} {money(preview.paidAmount)}، والاستردادات السابقة {money(preview.previouslyRefundedAmount)}. {!preview.isHistoricalSource ? preview.historicalUsageNote : null}</p>
          </div> : null}
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">الخزنة أو المحفظة التي خرج منها المبلغ</label>
          <select className="admin-input" required value={treasuryId} onChange={event => setTreasuryId(event.target.value)}>
            <option value="">اختر الخزنة</option>
            {bootstrap?.treasuryAccounts.map(item => <option key={item.id} value={item.id}>{item.name}{item.maskedIdentifier ? ` — ${item.maskedIdentifier}` : ''}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">المبلغ المرتجع فعلياً (تحدده يدويًا)</label>
          <input className="admin-input" type="number" min="0.01" max={preview?.remainingRefundableAmount} step="0.01" required value={refundAmount} onChange={event => setRefundAmount(event.target.value)} placeholder="المبلغ بالجنيه" disabled={!preview || preview.remainingRefundableAmount <= 0} />
          <p className="mt-1 text-xs text-[var(--admin-muted)]">تنفيذ الاسترداد يلغي المنحة دائمًا؛ أرقام الاستخدام للمساعدة في الحساب فقط ولا تقترح مبلغًا.</p>
        </div>
        <div className="md:col-span-2">
          <label className="mb-1 block text-xs font-bold">سبب الاسترداد</label>
          <input className="admin-input" required value={reason} onChange={event => setReason(event.target.value)} placeholder="اكتب سبب إلغاء الباقة ورد المبلغ" />
        </div>
        <div className="md:col-span-2 flex justify-end">
          <button className="admin-btn-primary" type="submit" disabled={submitting || previewLoading || !preview || !previewKey || preview.remainingRefundableAmount <= 0 || !student || !grantId || !treasuryId}>{submitting ? 'جارٍ التنفيذ…' : 'إلغاء الباقة وتسجيل الاسترداد'}</button>
        </div>
      </form> : <p className="text-sm">تحتاج إلى صلاحية إنشاء الاستردادات لتنفيذ استرداد.</p>}
    </section>

    <section className="admin-panel rounded-2xl p-6">
      <div className="mb-4 flex items-center justify-between"><div><h2 className="text-lg font-black">سجل الاستردادات</h2><p className="mt-1 text-xs text-[var(--admin-muted)]">يشمل الاستردادات المسجلة بالمركز المالي واستردادات الرصيد القديمة تلقائياً.</p></div><button className="admin-btn-ghost" type="button" onClick={() => void load()}>تحديث</button></div>
      {error ? <p role="alert" className="mb-3 text-rose-600">{error}</p> : null}
      <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>الطالب</th><th>التاريخ</th><th>الطريقة</th><th>السبب</th><th>حصة المنصة</th><th>حصة المدرس</th><th>الإجمالي</th><th>الحالة</th><th /></tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t border-[var(--admin-border)]"><td><Link href={staff ? `/assistant/students/${row.studentId}` : `/admin/users/${row.studentId}`} className="font-bold text-[var(--admin-primary)] hover:underline">{row.studentName}</Link><bdi className="block font-mono text-xs text-[var(--admin-muted)]">{row.studentPhoneNumber}</bdi></td><td>{new Date(row.createdAt).toLocaleString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' })}</td><td>{row.method === 2 ? 'كاش' : 'رصيد طالب'}</td><td>{row.reason || '—'}</td><td>{money(row.platformAmount)}</td><td>{money(row.teacherAmount)}</td><td className="font-bold">{money(row.totalAmount)}</td><td>{row.isHistorical ? 'استرداد قديم' : row.status === 3 ? 'معكوس' : row.status === 2 ? 'مقيد' : 'مسودة'}</td><td>{canReverse && !row.isHistorical && row.status === 2 ? <button className="text-rose-600" type="button" onClick={() => void reverse(row.id)}>عكس</button> : null}</td></tr>)}</tbody></table>{rows.length === 0 ? <p className="py-8 text-center text-[var(--admin-muted)]">لا توجد استردادات.</p> : null}</div>
    </section>
  </div>;
}
