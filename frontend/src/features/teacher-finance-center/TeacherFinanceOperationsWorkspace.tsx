'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, ClipboardCheck, FileText, HandCoins, History, ReceiptText, RefreshCw, RotateCcw, Send, WalletCards } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminModal } from '@/components/admin';
import { adminService, type CodeGroupDto } from '@/services/admin-service';
import { financeService } from '@/services/finance-service';
import type { SettlementPreview, TeacherAgreement, TeacherLedgerLine, TeacherSettlement } from './types';
import { cairoCurrentDate, cairoCurrentMonthPeriod } from '@/lib/cairo-time';

const money = (value: number) => `${value.toLocaleString('ar-EG-u-nu-latn', { maximumFractionDigits: 2 })} ج.م`;
const statusLabel: Record<string, string> = { Unpaid: 'متاح', Reserved: 'محجوز', Paid: 'مدفوع', Reversed: 'معكوس', Debt: 'مديونية', Draft: 'مسودة', Reviewed: 'تمت المراجعة', Approved: 'معتمدة', Cancelled: 'ملغاة' };
const statusTone: Record<string, string> = { Unpaid: 'bg-emerald-50 text-emerald-800', Reserved: 'bg-amber-50 text-amber-900', Paid: 'bg-sky-50 text-sky-900', Reversed: 'bg-slate-100 text-slate-700', Debt: 'bg-rose-50 text-rose-800', Draft: 'bg-slate-100 text-slate-700', Reviewed: 'bg-amber-50 text-amber-900', Approved: 'bg-teal-50 text-teal-900', Cancelled: 'bg-rose-50 text-rose-800' };

function StatusBadge({ status }: { status: string }) {
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-black ${statusTone[status] ?? 'bg-slate-100 text-slate-700'}`}>{statusLabel[status] ?? status}</span>;
}

export function TeacherFinanceOperationsWorkspace({ teacherId, teacherName, agreements, onChanged }: { teacherId: string; teacherName: string; agreements: TeacherAgreement[]; onChanged: () => Promise<void> }) {
  const [ledger, setLedger] = useState<TeacherLedgerLine[]>([]);
  const [ledgerLoading, setLedgerLoading] = useState(false);
  const [from, setFrom] = useState(() => cairoCurrentMonthPeriod().first);
  const [to, setTo] = useState(() => cairoCurrentDate());
  const [selectedLineIds, setSelectedLineIds] = useState<string[]>([]);
  const [preview, setPreview] = useState<SettlementPreview | null>(null);
  const [settlement, setSettlement] = useState<TeacherSettlement | null>(null);
  const [isSettlementModalOpen, setIsSettlementModalOpen] = useState(false);
  const [isReversalModalOpen, setIsReversalModalOpen] = useState(false);
  const [isCodeModalOpen, setIsCodeModalOpen] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [note, setNote] = useState('');
  const [reason, setReason] = useState('');
  const [disposition, setDisposition] = useState<'TeacherDebt' | 'NextSettlementDeduction'>('NextSettlementDeduction');
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState('');
  const [codeTrigger, setCodeTrigger] = useState<'CodeDelivery' | 'CodeActivation'>('CodeActivation');
  const [agreementId, setAgreementId] = useState('');
  const [recipient, setRecipient] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');

  const loadLedger = useCallback(async () => {
    if (!teacherId) return;
    setLedgerLoading(true);
    try {
      const result = await financeService.getTeacherLedger(teacherId, { from, to, page: 1, pageSize: 100 });
      setLedger(result.items);
      setSelectedLineIds((current) => current.filter((id) => result.items.some((item) => item.id === id && item.payoutStatus !== 'Reserved')));
    } catch {
      toast.error('تعذر تحميل دفتر حساب المدرس');
    } finally {
      setLedgerLoading(false);
    }
  }, [from, teacherId, to]);

  useEffect(() => { void loadLedger(); }, [loadLedger]);

  const selectedLines = useMemo(() => ledger.filter((line) => selectedLineIds.includes(line.id)), [ledger, selectedLineIds]);
  const selectableSettlementLines = useMemo(() => ledger.filter((line) => line.payoutStatus === 'Unpaid'), [ledger]);
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
      await Promise.all([loadLedger(), onChanged()]);
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
      await Promise.all([loadLedger(), onChanged()]);
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
      await Promise.all([loadLedger(), onChanged()]);
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر تسجيل الدفع'); } finally { setIsBusy(false); }
  };

  const submitReversal = async () => {
    if (!reason.trim() || !selectedForReversal.length) { toast.error('حدد بنوداً قابلة للعكس واكتب سبباً واضحاً'); return; }
    setIsBusy(true);
    try {
      const result = await financeService.reverseTeacherAllocations({ lines: selectedForReversal.map((line) => ({ allocationId: line.id, amount: line.teacherShareAmount - line.reversedAmount })), reason: reason.trim(), disposition, idempotencyKey: `admin-reversal:${crypto.randomUUID()}` });
      if (!result.success) { toast.error(result.message || 'تعذر تسجيل المرتجع'); return; }
      toast.success('تم تسجيل المرتجع مع الاحتفاظ بأصل الحركة');
      setReason(''); setSelectedLineIds([]); setIsReversalModalOpen(false);
      await Promise.all([loadLedger(), onChanged()]);
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر تسجيل المرتجع'); } finally { setIsBusy(false); }
  };

  const openCodeFinance = async () => {
    setIsCodeModalOpen(true);
    try {
      const allGroups = await adminService.listCodeGroups();
      setGroups((allGroups ?? []).filter((group) => group.teacherId === teacherId && group.codeType !== 'Balance'));
    } catch { toast.error('تعذر تحميل دفعات أكواد المدرس'); }
  };

  const saveCodeTerms = async (confirmDelivery: boolean) => {
    if (!selectedGroupId) { toast.error('اختر دفعة الأكواد'); return; }
    if (confirmDelivery && !recipient.trim()) { toast.error('أدخل اسم مستلم دفعة الأكواد'); return; }
    setIsBusy(true);
    try {
      const terms = await financeService.setCodeGroupFinancialTerms(selectedGroupId, { trigger: codeTrigger, agreementId: agreementId || undefined, recipient: recipient.trim() || undefined });
      if (!terms.success) { toast.error(terms.message || 'تعذر حفظ شروط الدفعة'); return; }
      if (confirmDelivery && codeTrigger === 'CodeDelivery') {
        const confirmed = await financeService.confirmCodeGroupDelivery(selectedGroupId, { recipient: recipient.trim(), attachmentUrl: attachmentUrl.trim() || undefined, deliveredAt: new Date().toISOString() });
        if (!confirmed.success) { toast.error(confirmed.message || 'تم حفظ الشروط لكن تعذر تأكيد التسليم'); return; }
        toast.success('تم تأكيد التسليم وتسجيل الحركة مرة واحدة');
      } else toast.success('تم حفظ شروط حساب دفعة الأكواد');
      setIsCodeModalOpen(false);
      await Promise.all([loadLedger(), onChanged()]);
    } catch (error: any) { toast.error(error?.response?.data?.message || 'تعذر حفظ شروط دفعة الأكواد'); } finally { setIsBusy(false); }
  };

  return <section className="mt-8 border-t-2 border-[var(--admin-primary)] pt-6" aria-label="عمليات حساب المدرس">
    <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-end">
      <div><p className="flex items-center gap-2 text-xs font-black text-[var(--admin-primary)]"><History className="h-4 w-4" /> سجل قابل للمراجعة</p><h3 className="mt-1 text-lg font-black text-[var(--admin-text)]">حركات وتسويات {teacherName}</h3><p className="mt-1 text-sm text-[var(--admin-muted)]">اختر البنود من السجل، ثم أنشئ تسوية أو نفّذ مرتجعاً موثقاً. لا تُحذف الحركات الأصلية.</p></div>
      <div className="flex flex-wrap gap-2"><button type="button" onClick={openCodeFinance} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"><ClipboardCheck className="h-4 w-4" /> دفعة أكواد</button><button type="button" onClick={() => setIsReversalModalOpen(true)} disabled={!selectedForReversal.length} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-rose-200 px-4 text-sm font-bold text-rose-800 hover:bg-rose-50 disabled:opacity-45"><RotateCcw className="h-4 w-4" /> عكس المحدد</button><button type="button" onClick={() => void openSettlement()} disabled={!selectedLineIds.length || isBusy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white disabled:opacity-50"><ReceiptText className="h-4 w-4" /> معاينة تسوية</button></div>
    </div>

    <div className="mt-5 flex flex-wrap items-end gap-3 border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
      <label className="text-xs font-bold text-[var(--admin-text)]">من<input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className="mt-1 block min-h-10 rounded-lg border border-[var(--admin-border)] bg-white px-2 text-sm" /></label>
      <label className="text-xs font-bold text-[var(--admin-text)]">إلى<input type="date" value={to} onChange={(event) => setTo(event.target.value)} className="mt-1 block min-h-10 rounded-lg border border-[var(--admin-border)] bg-white px-2 text-sm" /></label>
      <label className="min-w-64 flex-1 text-xs font-bold text-[var(--admin-text)]">ملاحظة التسوية (اختيارية)<input value={note} onChange={(event) => setNote(event.target.value)} placeholder="مثال: تسوية شهر يوليو" className="mt-1 block min-h-10 w-full rounded-lg border border-[var(--admin-border)] bg-white px-3 text-sm font-normal" /></label>
      <button type="button" onClick={() => void loadLedger()} disabled={ledgerLoading} className="inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-primary)] hover:bg-white"><RefreshCw className={`h-4 w-4 ${ledgerLoading ? 'animate-spin' : ''}`} /> تحديث السجل</button>
    </div>

    <div className="mt-4 overflow-x-auto border border-[var(--admin-border)]">
      <table className="w-full min-w-[940px] text-right text-sm"><thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr><th className="w-12 px-3 py-3"><span className="sr-only">اختيار</span></th><th className="px-3 py-3 font-black">التاريخ والمصدر</th><th className="px-3 py-3 font-black">المحتوى</th><th className="px-3 py-3 font-black">نصيب المدرس</th><th className="px-3 py-3 font-black">المعكوس</th><th className="px-3 py-3 font-black">الخصم</th><th className="px-3 py-3 font-black">الحالة</th></tr></thead><tbody className="divide-y divide-[var(--admin-border)]">
        {ledgerLoading ? <tr><td colSpan={7} className="px-4 py-10 text-center font-bold text-[var(--admin-muted)]">جارِ تحميل الحركات...</td></tr> : ledger.length === 0 ? <tr><td colSpan={7} className="px-4 py-10 text-center"><WalletCards className="mx-auto h-7 w-7 text-[var(--admin-muted)]" /><p className="mt-2 font-bold text-[var(--admin-text)]">لا توجد حركات خلال الفترة المحددة</p><p className="mt-1 text-xs text-[var(--admin-muted)]">شحن رصيد الطالب لا يظهر هنا لأنه لا ينشئ استحقاق مدرس.</p></td></tr> : ledger.map((line) => <tr key={line.id} className="hover:bg-[var(--admin-hover)]"><td className="px-3 py-3"><input type="checkbox" checked={selectedLineIds.includes(line.id)} disabled={line.payoutStatus === 'Reserved'} onChange={() => toggleLine(line)} aria-label={`اختيار ${line.contentNameSnapshot}`} /></td><td className="px-3 py-3"><p className="font-mono text-xs font-bold text-[var(--admin-text)]">{new Date(line.occurredAt).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' })}</p><p className="mt-1 text-xs text-[var(--admin-muted)]">{line.sourceType}</p></td><td className="max-w-64 px-3 py-3 font-bold text-[var(--admin-text)]">{line.contentNameSnapshot}</td><td className="px-3 py-3 font-mono font-black text-emerald-700">{money(line.teacherShareAmount)}</td><td className="px-3 py-3 font-mono text-xs text-rose-700">{line.reversedAmount > 0 ? money(line.reversedAmount) : '—'}</td><td className="px-3 py-3 text-xs text-[var(--admin-muted)]">{line.discountAmount ? `${money(line.discountAmount)}${line.teacherDiscountAmount ? '، على المدرس جزئياً' : ''}` : '—'}</td><td className="px-3 py-3"><StatusBadge status={line.payoutStatus} /></td></tr>)}
      </tbody></table>
    </div>
    {selectableSettlementLines.length > 0 && <p className="mt-2 text-xs font-bold text-[var(--admin-muted)]">المتاح للتسوية في الفترة: {selectableSettlementLines.length} بند. البنود المحجوزة لا يمكن تعديلها من هذه الشاشة.</p>}

    <AdminModal open={isSettlementModalOpen} onClose={() => setIsSettlementModalOpen(false)} title={settlement ? 'تسوية المدرس' : 'معاينة تسوية المدرس'} subtitle={settlement ? 'تسلسل التسوية: مسودة، مراجعة، اعتماد، ثم تسجيل دفع وفاتورة.' : 'راجع صافي المستحق والديون قبل حجز البنود.'} maxWidth="max-w-2xl">
      {!settlement && preview && <div className="space-y-4"><div className="grid gap-px overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-border)] sm:grid-cols-3"><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">إجمالي البنود</p><p className="mt-1 font-mono text-lg font-black text-[var(--admin-text)]">{money(preview.grossDueAmount)}</p></div><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">خصم مديونية</p><p className="mt-1 font-mono text-lg font-black text-rose-700">{money(preview.debtDeductionAmount)}</p></div><div className="bg-white p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">صافي الدفع</p><p className="mt-1 font-mono text-lg font-black text-emerald-700">{money(preview.netPayableAmount)}</p></div></div><div className="max-h-48 overflow-y-auto border border-[var(--admin-border)]"><ul className="divide-y divide-[var(--admin-border)]">{preview.allocations.map((line) => <li key={line.id} className="flex items-center justify-between gap-3 p-3 text-sm"><span className="font-bold text-[var(--admin-text)]">{line.contentNameSnapshot}</span><span className="font-mono font-black">{money(line.teacherShareAmount - line.reversedAmount)}</span></li>)}</ul></div><div className="flex justify-end gap-2"><button type="button" onClick={() => setIsSettlementModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">رجوع</button><button type="button" disabled={isBusy} onClick={() => void createSettlement()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-white"><FileText className="h-4 w-4" /> إنشاء مسودة التسوية</button></div></div>}
      {settlement && <div className="space-y-4"><div className="flex items-center justify-between"><StatusBadge status={settlement.status} /><p className="font-mono text-xl font-black text-emerald-700">{money(settlement.netPayableAmount)}</p></div><div className="border border-[var(--admin-border)]"><ul className="divide-y divide-[var(--admin-border)]">{settlement.lines.map((line) => <li key={line.id} className="flex items-center justify-between gap-3 p-3 text-sm"><span className="font-bold text-[var(--admin-text)]">{line.descriptionSnapshot}</span><span className="font-mono">{money(line.amount)}</span></li>)}</ul></div>{settlement.status === 'Draft' && <div className="flex justify-end gap-2"><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('cancel')} className="min-h-11 rounded-xl border border-rose-200 px-4 text-sm font-bold text-rose-800">إلغاء</button><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('review')} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><CheckCircle2 className="h-4 w-4" /> تأكيد المراجعة</button></div>}{settlement.status === 'Reviewed' && <div className="flex justify-end"><button type="button" disabled={isBusy} onClick={() => void transitionSettlement('approve')} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><CheckCircle2 className="h-4 w-4" /> اعتماد التسوية</button></div>}{settlement.status === 'Approved' && <form onSubmit={paySettlement} className="space-y-3 border-t border-[var(--admin-border)] pt-4"><p className="font-black text-[var(--admin-text)]">تسجيل الدفع والفاتورة</p><div className="grid gap-3 sm:grid-cols-2"><input required name="paymentMethod" placeholder="طريقة الدفع" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm" /><input required name="transferReference" placeholder="مرجع التحويل أو الإيصال" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm" /></div><input name="attachmentUrl" type="url" placeholder="رابط مرفق الفاتورة أو الإيصال (اختياري)" className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 text-sm" /><div className="flex justify-end"><button disabled={isBusy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><HandCoins className="h-4 w-4" /> تسجيل دفع {money(settlement.netPayableAmount)}</button></div></form>}{settlement.status === 'Paid' && <p className="rounded-xl bg-emerald-50 p-3 text-sm font-bold text-emerald-900">تم تسجيل الدفع. تحتوي التسوية على {settlement.payments.length} عملية دفع موثقة.</p>}</div>}
    </AdminModal>

    <AdminModal open={isReversalModalOpen} onClose={() => setIsReversalModalOpen(false)} title="عكس بنود مالية" subtitle="لن يتم حذف البيع الأصلي. البنود المدفوعة تتحول إلى مديونية أو خصم من التسوية القادمة." maxWidth="max-w-xl"><div className="space-y-4"><div className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm"><p className="font-black text-[var(--admin-text)]">{selectedForReversal.length} بند محدد</p><p className="mt-1 font-mono text-rose-700">إجمالي العكس: {money(selectedForReversal.reduce((sum, line) => sum + line.teacherShareAmount - line.reversedAmount, 0))}</p></div><label className="block text-sm font-bold text-[var(--admin-text)]">سبب المرتجع<textarea value={reason} onChange={(event) => setReason(event.target.value)} rows={3} className="mt-1.5 w-full rounded-xl border border-[var(--admin-border)] p-3 font-normal" placeholder="مثال: استرداد جزئي بناءً على طلب الطالب" /></label><fieldset><legend className="text-sm font-bold text-[var(--admin-text)]">عند صرف نصيب المدرس سابقاً</legend><label className="mt-2 flex gap-2 text-sm"><input type="radio" checked={disposition === 'NextSettlementDeduction'} onChange={() => setDisposition('NextSettlementDeduction')} /> خصمه من التسوية القادمة</label><label className="mt-2 flex gap-2 text-sm"><input type="radio" checked={disposition === 'TeacherDebt'} onChange={() => setDisposition('TeacherDebt')} /> تسجيله مديونية على المدرس</label></fieldset><div className="flex justify-end gap-2"><button type="button" onClick={() => setIsReversalModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">إلغاء</button><button type="button" disabled={isBusy} onClick={() => void submitReversal()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-rose-700 px-4 text-sm font-black text-white"><RotateCcw className="h-4 w-4" /> تسجيل العكس</button></div></div></AdminModal>

    <AdminModal open={isCodeModalOpen} onClose={() => setIsCodeModalOpen(false)} title="شروط دفعة الأكواد" subtitle="تحديد وقت الحساب قبل التأكيد. أكواد الرصيد مستبعدة لأنها لا تنشئ مستحق مدرس." maxWidth="max-w-2xl"><div className="space-y-4"><label className="block text-sm font-bold">دفعة الأكواد<select value={selectedGroupId} onChange={(event) => setSelectedGroupId(event.target.value)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-white px-3 font-normal"><option value="">اختر الدفعة</option>{groups.map((group) => <option key={group.id} value={group.id}>{group.name}، {group.codeCount} كود، مستخدم {group.usedCount}</option>)}</select></label>{groups.length === 0 && <p className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm text-[var(--admin-muted)]">لا توجد دفعات أكواد غير رصيد مرتبطة بهذا المدرس.</p>}<div className="grid gap-3 sm:grid-cols-2"><label className="rounded-xl border border-[var(--admin-border)] p-3 text-sm font-bold"><input type="radio" checked={codeTrigger === 'CodeActivation'} onChange={() => setCodeTrigger('CodeActivation')} className="me-2" /> الحساب عند تفعيل كل كود</label><label className="rounded-xl border border-[var(--admin-border)] p-3 text-sm font-bold"><input type="radio" checked={codeTrigger === 'CodeDelivery'} onChange={() => setCodeTrigger('CodeDelivery')} className="me-2" /> الحساب عند تأكيد التسليم</label></div><label className="block text-sm font-bold">اتفاق خاص للدفعة (اختياري)<select value={agreementId} onChange={(event) => setAgreementId(event.target.value)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-white px-3 font-normal"><option value="">اتركه للحساب بالاتفاق المنطبق</option>{agreements.filter((agreement) => agreement.isActive && agreement.trigger === codeTrigger).map((agreement) => <option key={agreement.id} value={agreement.id}>{agreement.allocationMode}، {agreement.allocationValue}{agreement.allocationMode === 'Percentage' ? '%' : ' ج.م'}</option>)}</select></label>{codeTrigger === 'CodeDelivery' && <><label className="block text-sm font-bold">المستلم<input value={recipient} onChange={(event) => setRecipient(event.target.value)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 font-normal" placeholder="اسم الشخص أو الجهة المستلمة" /></label><label className="block text-sm font-bold">رابط دليل التسليم (اختياري)<input type="url" value={attachmentUrl} onChange={(event) => setAttachmentUrl(event.target.value)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 font-normal" placeholder="رابط صورة أو إيصال التسليم" /></label></>}<div className="flex flex-wrap justify-end gap-2"><button type="button" onClick={() => void saveCodeTerms(false)} disabled={isBusy} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">حفظ الشروط</button>{codeTrigger === 'CodeDelivery' && <button type="button" onClick={() => void saveCodeTerms(true)} disabled={isBusy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"><Send className="h-4 w-4" /> تأكيد التسليم والحساب</button>}</div></div></AdminModal>
  </section>;
}
