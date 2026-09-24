'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CheckCircle2, FileText, HandCoins, History, ReceiptText, RefreshCw, RotateCcw, WalletCards } from 'lucide-react';
import toast from 'react-hot-toast';
import { createClientId } from '@/lib/client-id';
import { incomeSourceLabels } from './TeacherAccountOverview';
import { AdminModal } from '@/components/admin';
import { financeService } from '@/services/finance-service';
import type { SettlementPreview, TeacherLedgerLine, TeacherSettlement } from './types';
import { cairoCurrentDate, cairoCurrentMonthPeriod } from '@/lib/cairo-time';

const money = (value: number) => `${value.toLocaleString('ar-EG-u-nu-latn', { maximumFractionDigits: 2 })} ج.م`;
const statusLabel: Record<string, string> = { Unpaid: 'متاح', Reserved: 'محجوز', Paid: 'مدفوع', Reversed: 'معكوس', Debt: 'مديونية', Draft: 'مسودة', Reviewed: 'تمت المراجعة', Approved: 'معتمدة', Cancelled: 'ملغاة' };
const statusTone: Record<string, string> = { Unpaid: 'bg-emerald-50 text-emerald-800', Reserved: 'bg-amber-50 text-amber-900', Paid: 'bg-sky-50 text-sky-900', Reversed: 'bg-slate-100 text-slate-700', Debt: 'bg-rose-50 text-rose-800', Draft: 'bg-slate-100 text-slate-700', Reviewed: 'bg-amber-50 text-amber-900', Approved: 'bg-teal-50 text-teal-900', Cancelled: 'bg-rose-50 text-rose-800' };

function StatusBadge({ status }: { status: string }) {
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-black ${statusTone[status] ?? 'bg-slate-100 text-slate-700'}`}>{statusLabel[status] ?? status}</span>;
}

export function TeacherFinanceOperationsWorkspace({ teacherId, teacherName, onChanged }: { teacherId: string; teacherName: string; onChanged: () => Promise<void> }) {
  const [ledger, setLedger] = useState<TeacherLedgerLine[]>([]);
  const [ledgerLoading, setLedgerLoading] = useState(false);
  const [ledgerError, setLedgerError] = useState(false);
  const [ledgerPage, setLedgerPage] = useState(1);
  const [ledgerTotal, setLedgerTotal] = useState(0);
  const requestVersion = useRef(0);
  const [historyPage, setHistoryPage] = useState(1);
  const [historyVersion, setHistoryVersion] = useState(0);
  const [history, setHistory] = useState<Awaited<ReturnType<typeof financeService.getTeacherSettlements>> | null>(null);
  const [historyError, setHistoryError] = useState(false);
  const [from, setFrom] = useState(() => cairoCurrentMonthPeriod().first);
  const [to, setTo] = useState(() => cairoCurrentDate());
  const [selectedLineIds, setSelectedLineIds] = useState<string[]>([]);
  const [preview, setPreview] = useState<SettlementPreview | null>(null);
  const [settlement, setSettlement] = useState<TeacherSettlement | null>(null);
  const [isSettlementModalOpen, setIsSettlementModalOpen] = useState(false);
  const [isReversalModalOpen, setIsReversalModalOpen] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [note, setNote] = useState('');
  const [reason, setReason] = useState('');
  const [disposition, setDisposition] = useState<'TeacherDebt' | 'NextSettlementDeduction'>('NextSettlementDeduction');
  const loadLedger = useCallback(async () => {
    const version = ++requestVersion.current;
    setLedgerLoading(true); setLedgerError(false); setSelectedLineIds([]);
    try {
      const result = await financeService.getTeacherLedger(teacherId, { from, to, page: ledgerPage, pageSize: 100 });
      if (version !== requestVersion.current) return;
      setLedger(result.items); setLedgerTotal(result.total);
    } catch {
      if (version === requestVersion.current) { setLedger([]); setLedgerError(true); }
    } finally {
      if (version === requestVersion.current) setLedgerLoading(false);
    }
  }, [from, teacherId, to, ledgerPage]);

  const invalidateLedger = useCallback(() => { requestVersion.current++; }, []);
  useEffect(() => { void loadLedger(); return invalidateLedger; }, [loadLedger, invalidateLedger]);
  useEffect(() => {
    let active = true;
    setHistory(null); setHistoryError(false);
    void financeService.getTeacherSettlements(teacherId, historyPage).then(data => { if (active) setHistory(data); })
      .catch(() => { if (active) setHistoryError(true); });
    return () => { active = false; };
  }, [teacherId, historyPage, historyVersion]);

  const refreshAfterChange = async () => {
    setHistoryVersion(value => value + 1);
    await Promise.all([loadLedger(), onChanged()]);
  };
  const openExistingSettlement = async (id: string) => {
    setIsBusy(true);
    try {
      const data = await financeService.getTeacherSettlement(id);
      if (!data) throw new Error('missing settlement');
      setSettlement(data); setPreview(null); setIsSettlementModalOpen(true);
    } catch { toast.error('تعذر تحميل التسوية'); }
    finally { setIsBusy(false); }
  };

  const selectedLines = useMemo(() => ledger.filter((line) => selectedLineIds.includes(line.id)), [ledger, selectedLineIds]);
  const selectableSettlementLines = useMemo(() => ledger.filter((line) => line.payoutStatus === 'Unpaid' && ['Approved', 'AutoApproved'].includes(line.reviewStatus) && line.teacherShareAmount > line.reversedAmount), [ledger]);
  const selectedForReversal = useMemo(() => selectedLines.filter((line) => !['Reserved', 'Reversed', 'Debt'].includes(line.payoutStatus) && line.teacherShareAmount > line.reversedAmount), [selectedLines]);

  const toggleLine = (line: TeacherLedgerLine) => {
    if (line.payoutStatus === 'Reserved') return;
    setSelectedLineIds((current) => current.includes(line.id) ? current.filter((id) => id !== line.id) : [...current, line.id]);
  };

  const openSettlement = async () => {
    if (!selectedLineIds.length) { toast.error('حدد بنداً واحداً على الأقل لإنشاء التسوية'); return; }
    setIsBusy(true);
    try {
      const result = await financeService.previewTeacherSettlement({ teacherId, periodFrom: from, periodTo: to, note: note || undefined, allocationIds: selectedLineIds });
      setSettlement(null);
      setPreview(result);
      setIsSettlementModalOpen(true);
    } catch (error: any) {
      toast.error(error?.response?.data?.message || error?.message || 'تعذر معاينة التسوية');
    } finally { setIsBusy(false); }
  };

  const createSettlement = async () => {
    if (!preview) return;
    setIsBusy(true);
    try {
      const result = await financeService.createTeacherSettlement({ teacherId, periodFrom: from, periodTo: to, note: note || undefined, allocationIds: selectedLineIds });
      if (!result.success || !result.data?.id) { toast.error(result.message || 'تعذر إنشاء التسوية'); return; }
      const created = await financeService.getTeacherSettlement(result.data.id);
      setSettlement(created);
      setPreview(null);
      toast.success('تم إنشاء مسودة التسوية وحجز البنود المحددة');
      await refreshAfterChange();
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر إنشاء التسوية'); } finally { setIsBusy(false); }
  };

  const transitionSettlement = async (action: 'review' | 'approve' | 'cancel') => {
    if (!settlement) return;
    setIsBusy(true);
    try {
      const result = action === 'review' ? await financeService.reviewTeacherSettlement(settlement.id) : action === 'approve' ? await financeService.approveTeacherSettlement(settlement.id) : await financeService.cancelTeacherSettlement(settlement.id);
      if (!result.success) { toast.error(result.message || 'تعذر تحديث حالة التسوية'); return; }
      setSettlement(await financeService.getTeacherSettlement(settlement.id));
      toast.success(action === 'cancel' ? 'تم إلغاء التسوية وإتاحة البنود مجدداً' : 'تم تحديث حالة التسوية');
      await refreshAfterChange();
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر تحديث التسوية'); } finally { setIsBusy(false); }
  };

  const paySettlement = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!settlement) return;
    const data = new FormData(event.currentTarget);
    const paymentMethod = String(data.get('paymentMethod') || '').trim();
    const transferReference = String(data.get('transferReference') || '').trim();
    if (!paymentMethod || !transferReference) { toast.error('أدخل طريقة الدفع والمرجع'); return; }
    setIsBusy(true);
    try {
      const result = await financeService.payTeacherSettlement(settlement.id, { paymentMethod, transferReference, attachmentUrl: String(data.get('attachmentUrl') || '').trim() || undefined, amount: settlement.netPayableAmount });
      if (!result.success) { toast.error(result.message || 'تعذر تسجيل الدفع'); return; }
      setSettlement(await financeService.getTeacherSettlement(settlement.id));
      toast.success('تم تسجيل الدفع وربط الفاتورة بمرجع التحويل');
      await refreshAfterChange();
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر تسجيل الدفع'); } finally { setIsBusy(false); }
  };

  const submitReversal = async () => {
    if (!reason.trim() || !selectedForReversal.length) { toast.error('حدد بنوداً قابلة للعكس واكتب سبباً واضحاً'); return; }
    setIsBusy(true);
    try {
      const result = await financeService.reverseTeacherAllocations({ lines: selectedForReversal.map((line) => ({ allocationId: line.id, amount: line.teacherShareAmount - line.reversedAmount })), reason: reason.trim(), disposition, idempotencyKey: `admin-reversal:${createClientId()}` });
      if (!result.success) { toast.error(result.message || 'تعذر تسجيل المرتجع'); return; }
      toast.success('تم تسجيل المرتجع مع الاحتفاظ بأصل الحركة');
      setReason(''); setSelectedLineIds([]); setIsReversalModalOpen(false);
      await refreshAfterChange();
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر تسجيل المرتجع'); } finally { setIsBusy(false); }
  };

  return <section className="mt-8 border-t-2 border-[var(--admin-primary)] pt-6" aria-label="عمليات حساب المدرس">
    <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-end">
      <div><p className="flex items-center gap-2 text-xs font-black text-[var(--admin-primary)]"><History className="h-4 w-4" /> سجل قابل للمراجعة</p><h3 className="mt-1 text-lg font-black text-[var(--admin-text)]">حركات وتسويات {teacherName}</h3><p className="mt-1 text-sm text-[var(--admin-muted)]">اختر البنود من السجل، ثم أنشئ تسوية أو نفّذ مرتجعاً موثقاً. لا تُحذف الحركات الأصلية.</p></div>
      <div className="flex flex-wrap gap-2"><button type="button" onClick={() => setIsReversalModalOpen(true)} disabled={!selectedForReversal.length} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-rose-200 px-4 text-sm font-bold text-rose-800 hover:bg-rose-50 disabled:opacity-45"><RotateCcw className="h-4 w-4" /> عكس المحدد</button><button type="button" onClick={() => void openSettlement()} disabled={!selectedLineIds.length || isBusy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white disabled:opacity-50"><ReceiptText className="h-4 w-4" /> معاينة تسوية</button></div>
    </div>

    <div className="mt-5 flex flex-wrap items-end gap-3 border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
      <label className="text-xs font-bold text-[var(--admin-text)]">من<input type="date" value={from} onChange={(event) => { setFrom(event.target.value); setLedgerPage(1); }} className="mt-1 block min-h-10 rounded-lg border border-[var(--admin-border)] bg-white px-2 text-sm" /></label>
      <label className="text-xs font-bold text-[var(--admin-text)]">إلى<input type="date" value={to} onChange={(event) => { setTo(event.target.value); setLedgerPage(1); }} className="mt-1 block min-h-10 rounded-lg border border-[var(--admin-border)] bg-white px-2 text-sm" /></label>
      <label className="min-w-64 flex-1 text-xs font-bold text-[var(--admin-text)]">ملاحظة التسوية (اختيارية)<input value={note} onChange={(event) => setNote(event.target.value)} placeholder="مثال: تسوية شهر يوليو" className="mt-1 block min-h-10 w-full rounded-lg border border-[var(--admin-border)] bg-white px-3 text-sm font-normal" /></label>
      <button type="button" onClick={() => void loadLedger()} disabled={ledgerLoading} className="inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-primary)] hover:bg-white"><RefreshCw className={`h-4 w-4 ${ledgerLoading ? 'animate-spin' : ''}`} /> تحديث السجل</button>
    </div>

    <div className="mt-4 overflow-x-auto border border-[var(--admin-border)]">
      <table className="w-full min-w-[940px] text-right text-sm"><thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr><th className="w-12 px-3 py-3"><span className="sr-only">اختيار</span></th><th className="px-3 py-3 font-black">التاريخ والمصدر</th><th className="px-3 py-3 font-black">المحتوى</th><th className="px-3 py-3 font-black">نصيب المدرس</th><th className="px-3 py-3 font-black">المعكوس</th><th className="px-3 py-3 font-black">الخصم</th><th className="px-3 py-3 font-black">الحالة</th></tr></thead><tbody className="divide-y divide-[var(--admin-border)]">
        {ledgerError ? <tr><td colSpan={7} className="p-6" role="alert">تعذر تحميل الحركات. اضغط تحديث السجل لإعادة المحاولة.</td></tr> : ledgerLoading ? <tr><td colSpan={7} className="px-4 py-10 text-center font-bold text-[var(--admin-muted)]">جارِ تحميل الحركات...</td></tr> : ledger.length === 0 ? <tr><td colSpan={7} className="px-4 py-10 text-center"><WalletCards className="mx-auto h-7 w-7 text-[var(--admin-muted)]" /><p className="mt-2 font-bold text-[var(--admin-text)]">لا توجد حركات خلال الفترة المحددة</p><p className="mt-1 text-xs text-[var(--admin-muted)]">شحن رصيد الطالب لا يظهر هنا لأنه لا ينشئ استحقاق مدرس.</p></td></tr> : ledger.map((line) => <tr key={line.id} className="hover:bg-[var(--admin-hover)]"><td className="px-3 py-3"><input type="checkbox" checked={selectedLineIds.includes(line.id)} disabled={line.payoutStatus === 'Reserved'} onChange={() => toggleLine(line)} aria-label={`اختيار ${line.contentNameSnapshot}`} /></td><td className="px-3 py-3"><p className="font-mono text-xs font-bold text-[var(--admin-text)]">{new Date(line.occurredAt).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' })}</p><p className="mt-1 text-xs text-[var(--admin-muted)]">{incomeSourceLabels[line.sourceType] ?? 'مصدر آخر'}</p></td><td className="max-w-64 px-3 py-3 font-bold text-[var(--admin-text)]">{line.contentNameSnapshot}</td><td className="px-3 py-3 font-mono font-black text-emerald-700">{money(line.teacherShareAmount)}</td><td className="px-3 py-3 font-mono text-xs text-rose-700">{line.reversedAmount > 0 ? money(line.reversedAmount) : '—'}</td><td className="px-3 py-3 text-xs text-[var(--admin-muted)]">{line.discountAmount ? `${money(line.discountAmount)}${line.teacherDiscountAmount ? '، على المدرس جزئياً' : ''}` : '—'}</td><td className="px-3 py-3">{line.reviewStatus === 'PendingReview' ? <span>تحت المراجعة</span> : line.reviewStatus === 'Rejected' ? <span>مرفوض</span> : line.retainedByTeacher && line.payoutStatus === 'Paid' ? <span>محتفظ به من الأكواد</span> : <StatusBadge status={line.payoutStatus} />}</td></tr>)}
      </tbody></table>
    </div>
    <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm"><span>{ledgerTotal} حركة · صفحة {ledgerPage}</span><div><button className="min-h-11 px-3 disabled:opacity-40" disabled={ledgerPage === 1 || ledgerLoading} onClick={() => setLedgerPage(page => page - 1)}>الحركات السابقة</button><button className="min-h-11 px-3 disabled:opacity-40" disabled={ledgerPage * 100 >= ledgerTotal || ledgerLoading} onClick={() => setLedgerPage(page => page + 1)}>الحركات التالية</button></div></div>
    {selectableSettlementLines.length > 0 && <p className="mt-2 text-xs font-bold text-[var(--admin-muted)]">المتاح للتسوية في الفترة: {selectableSettlementLines.length} بند. البنود المحجوزة لا يمكن تعديلها من هذه الشاشة.</p>}

    <section className="mt-6 space-y-3" aria-label="سجل التسويات">
      <h4 className="font-bold">التسويات السابقة والمبالغ المحجوزة</h4>
      {historyError ? <p role="alert">تعذر تحميل التسويات. <button className="min-h-11 px-3 underline" onClick={() => setHistoryVersion(value => value + 1)}>إعادة المحاولة</button></p> : !history ? <p role="status">جارٍ تحميل التسويات...</p> : <>
        <ul className="divide-y divide-[var(--admin-border)]">{history.items.map(item => <li key={item.id} className="flex flex-wrap items-center justify-between gap-3 py-3"><span><StatusBadge status={item.status} /> <span className="ms-2 tabular-nums">صافي الدفع: {money(item.netPayableAmount)}</span></span><button disabled={isBusy} className="min-h-11 px-3 underline" onClick={() => void openExistingSettlement(item.id)}>فتح التسوية</button></li>)}</ul>
        {!history.total && <p className="text-sm text-[var(--admin-muted)]">لا توجد تسويات بعد.</p>}
        {history.total > 20 && <div className="flex gap-3"><button className="min-h-11 px-3" disabled={historyPage === 1} onClick={() => setHistoryPage(page => page - 1)}>التسويات السابقة</button><button className="min-h-11 px-3" disabled={historyPage * 20 >= history.total} onClick={() => setHistoryPage(page => page + 1)}>التسويات التالية</button></div>}
      </>}
    </section>

    <AdminModal open={isSettlementModalOpen} onClose={() => setIsSettlementModalOpen(false)} title={settlement ? 'تسوية المدرس' : 'معاينة تسوية المدرس'} subtitle={settlement ? 'تسلسل التسوية: مسودة، مراجعة، اعتماد، ثم تسجيل دفع وفاتورة.' : 'راجع صافي المستحق والديون قبل حجز البنود.'} maxWidth="max-w-2xl">
      {!settlement && preview && <div className="space-y-4"><div className="grid gap-px overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-border)] sm:grid-cols-3"><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">إجمالي البنود</p><p className="mt-1 font-mono text-lg font-black text-[var(--admin-text)]">{money(preview.grossDueAmount)}</p></div><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">خصم مديونية</p><p className="mt-1 font-mono text-lg font-black text-rose-700">{money(preview.debtDeductionAmount)}</p></div><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">صافي الدفع</p><p className="mt-1 font-mono text-lg font-black text-emerald-700">{money(preview.netPayableAmount)}</p></div></div><div className="max-h-48 overflow-y-auto border border-[var(--admin-border)]"><ul className="divide-y divide-[var(--admin-border)]">{preview.allocations.map((line) => <li key={line.id} className="flex items-center justify-between gap-3 p-3 text-sm"><span className="font-bold text-[var(--admin-text)]">{line.contentNameSnapshot}</span><span className="font-mono font-black">{money(line.teacherShareAmount - line.reversedAmount)}</span></li>)}</ul></div><div className="flex justify-end gap-2"><button type="button" onClick={() => setIsSettlementModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">رجوع</button><button type="button" disabled={isBusy} onClick={() => void createSettlement()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-white"><FileText className="h-4 w-4" /> إنشاء مسودة التسوية</button></div></div>}
      {settlement && <div className="space-y-4"><div className="flex items-center justify-between"><StatusBadge status={settlement.status} /><p className="font-mono text-xl font-black text-emerald-700">{money(settlement.netPayableAmount)}</p></div><div className="border border-[var(--admin-border)]"><ul className="divide-y divide-[var(--admin-border)]">{settlement.lines.map((line) => <li key={line.id} className="flex items-center justify-between gap-3 p-3 text-sm"><span className="font-bold text-[var(--admin-text)]">{line.descriptionSnapshot}</span><span className="font-mono">{money(line.amount)}</span></li>)}</ul></div>{settlement.status === 'Draft' && <div className="flex justify-end gap-2"><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('cancel')} className="min-h-11 rounded-xl border border-rose-200 px-4 text-sm font-bold text-rose-800">إلغاء</button><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('review')} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><CheckCircle2 className="h-4 w-4" /> تأكيد المراجعة</button></div>}{['Reviewed', 'Approved'].includes(settlement.status) && <button type="button" disabled={isBusy} onClick={() => void transitionSettlement('cancel')} className="min-h-11 px-3 underline">إلغاء التسوية وفك الحجز</button>}{settlement.status === 'Reviewed' && <div className="flex justify-end"><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('approve')} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><CheckCircle2 className="h-4 w-4" /> اعتماد التسوية</button></div>}{settlement.status === 'Approved' && <form onSubmit={paySettlement} className="space-y-3 border-t border-[var(--admin-border)] pt-4"><p className="font-black text-[var(--admin-text)]">تسجيل الدفع والفاتورة</p><div className="grid gap-3 sm:grid-cols-2"><input required name="paymentMethod" placeholder="طريقة الدفع" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm" /><input required name="transferReference" placeholder="مرجع التحويل أو الإيصال" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm" /></div><input name="attachmentUrl" type="url" placeholder="رابط مرفق الفاتورة أو الإيصال (اختياري)" className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 text-sm" /><div className="flex justify-end"><button disabled={isBusy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><HandCoins className="h-4 w-4" /> تسجيل دفع {money(settlement.netPayableAmount)}</button></div></form>}{settlement.status === 'Paid' && <p className="rounded-xl bg-emerald-50 p-3 text-sm font-bold text-emerald-900">تم تسجيل الدفع. تحتوي التسوية على {settlement.payments.length} عملية دفع موثقة.</p>}</div>}
    </AdminModal>

    <AdminModal open={isReversalModalOpen} onClose={() => setIsReversalModalOpen(false)} title="عكس بنود مالية" subtitle="لن يتم حذف البيع الأصلي. البنود المدفوعة تتحول إلى مديونية أو خصم من التسوية القادمة." maxWidth="max-w-xl"><div className="space-y-4"><div className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm"><p className="font-black text-[var(--admin-text)]">{selectedForReversal.length} بند محدد</p><p className="mt-1 font-mono text-rose-700">إجمالي العكس: {money(selectedForReversal.reduce((sum, line) => sum + line.teacherShareAmount - line.reversedAmount, 0))}</p></div><label className="block text-sm font-bold text-[var(--admin-text)]">سبب المرتجع<textarea value={reason} onChange={(event) => setReason(event.target.value)} rows={3} className="mt-1.5 w-full rounded-xl border border-[var(--admin-border)] p-3 font-normal" placeholder="مثال: استرداد جزئي بناءً على طلب الطالب" /></label><fieldset><legend className="text-sm font-bold text-[var(--admin-text)]">عند صرف نصيب المدرس سابقاً</legend><label className="mt-2 flex gap-2 text-sm"><input type="radio" checked={disposition === 'NextSettlementDeduction'} onChange={() => setDisposition('NextSettlementDeduction')} /> خصمه من التسوية القادمة</label><label className="mt-2 flex gap-2 text-sm"><input type="radio" checked={disposition === 'TeacherDebt'} onChange={() => setDisposition('TeacherDebt')} /> تسجيله مديونية على المدرس</label></fieldset><div className="flex justify-end gap-2"><button type="button" onClick={() => setIsReversalModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">إلغاء</button><button type="button" disabled={isBusy} onClick={() => void submitReversal()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-rose-700 px-4 text-sm font-black text-white"><RotateCcw className="h-4 w-4" /> تسجيل العكس</button></div></div></AdminModal>


  </section>;
}
