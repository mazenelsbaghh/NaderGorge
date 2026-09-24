'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { createClientId } from '@/lib/client-id';
import { adminService, type CodeGroupDto } from '@/services/admin-service';
import { financeService } from '@/services/finance-service';
import platformFinanceService from '@/services/platform-finance-service';
import type { CodeBatchAccount, CodeCollectionInput } from './types';
import { teacherMoney } from './TeacherAccountOverview';

const field = 'mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 font-normal';
const button = 'min-h-11 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50';
const errorMessage = (error: unknown) => (error as { response?: { data?: { message?: string } } })?.response?.data?.message || 'تعذر حفظ العملية. أعد المحاولة.';

export function TeacherCodeBatches({ teacherId, teacherName, agreementVersion, onChanged }: { teacherId: string; teacherName: string; agreementVersion: string; onChanged: () => void }) {
  const [open, setOpen] = useState(false);
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [treasuries, setTreasuries] = useState<Array<{ id: string; name: string }>>([]);
  const [groupId, setGroupId] = useState('');
  const [account, setAccount] = useState<CodeBatchAccount | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [recipient, setRecipient] = useState(teacherName);
  const [paidNow, setPaidNow] = useState(false);
  const [amount, setAmount] = useState('');
  const [treasuryId, setTreasuryId] = useState('');
  const [reference, setReference] = useState('');
  const requestVersion = useRef(0);
  const paymentKey = useRef('');

  useEffect(() => {
    const requested = new URLSearchParams(window.location.search).get('codeGroup');
    if (requested) { setGroupId(requested); setOpen(true); }
  }, []);

  useEffect(() => {
    if (!open) return;
    let active = true;
    void Promise.all([adminService.listCodeGroups(), platformFinanceService.bootstrap()]).then(([items, bootstrap]) => {
      if (!active) return;
      setGroups(items.filter(group => group.teacherId === teacherId && group.codeType !== 'Balance'));
      setTreasuries(bootstrap.treasuryAccounts);
    }).catch(() => { if (active) setError('تعذر تحميل دفعات الأكواد والخزائن. اقفل القسم وافتحه للمحاولة تاني.'); });
    return () => { active = false; };
  }, [open, teacherId]);

  const load = useCallback(async () => {
    if (!groupId) { setAccount(null); return; }
    const version = ++requestVersion.current;
    setLoading(true); setAccount(null); setError('');
    try {
      const result = await financeService.getCodeBatchAccount(groupId);
      if (version !== requestVersion.current) return;
      if (result.teacherId !== teacherId) throw new Error('Teacher mismatch');
      setAccount(result); setRecipient(result.recipient || teacherName);
    } catch { if (version === requestVersion.current) setError('تعذر تحميل حساب الدفعة. أعد المحاولة قبل تسجيل أي فلوس.'); }
    finally { if (version === requestVersion.current) setLoading(false); }
  }, [groupId, teacherId, teacherName]);

  useEffect(() => { setPaidNow(false); setAmount(''); setReference(''); paymentKey.current = ''; void load(); const version = requestVersion.current; return () => { requestVersion.current = version + 1; }; }, [load, agreementVersion]);

  const changeTiming = async (trigger: CodeBatchAccount['trigger']) => {
    setBusy(true);
    try {
      const result = await financeService.setCodeGroupFinancialTerms(groupId, { trigger, recipient });
      if (!result.success) throw { response: { data: result } };
      await load();
      toast.success('اتحفظ موعد حساب الدفعة');
    } catch (error) { toast.error(errorMessage(error)); } finally { setBusy(false); }
  };

  const receipt = (maximum: number): CodeCollectionInput | undefined => {
    const value = Number(amount);
    if (!Number.isFinite(value) || value <= 0 || value > maximum || !treasuryId || !reference.trim()) {
      toast.error('أدخل مبلغًا صحيحًا في حدود الباقي، وحدد مكان استلام الفلوس ومرجعها');
      return undefined;
    }
    paymentKey.current ||= `code-receipt:${createClientId()}`;
    return { amount: value, treasuryAccountId: treasuryId, reference: reference.trim(), idempotencyKey: paymentKey.current };
  };

  const confirm = async () => {
    if (!account?.quote || !recipient.trim()) { toast.error('اكتب اسم مستلم الدفعة'); return; }
    const payment = paidNow ? receipt(account.quote.platformShare) : undefined;
    if (paidNow && !payment) return;
    setBusy(true);
    try {
      const result = await financeService.confirmCodeGroupDelivery(groupId, { recipient: recipient.trim(), quoteKey: account.quote.key, payment });
      if (!result.success) throw { response: { data: result } };
      toast.success('اتحسبت الدفعة مرة واحدة. استخدام الطلاب للأكواد مش هيحسبها تاني.');
      paymentKey.current = ''; setAmount(''); setReference('');
      await load(); onChanged();
    } catch (error) { toast.error(errorMessage(error)); await load(); } finally { setBusy(false); }
  };

  const collect = async () => {
    const payment = receipt(account?.delivery?.remaining ?? 0);
    if (!payment) return;
    setBusy(true);
    try {
      const result = await financeService.collectCodeBatchPayment(groupId, payment);
      if (!result.success) throw { response: { data: result } };
      toast.success('اتسجّل السداد واتخصم من الباقي على المدرّس');
      paymentKey.current = ''; setAmount(''); setReference('');
      await load(); onChanged();
    } catch (error) { toast.error(errorMessage(error)); } finally { setBusy(false); }
  };

  const paymentFields = (maximum: number) => <div className="grid gap-3 sm:grid-cols-3">
    <label className="text-sm font-bold">استلمت كام؟<input type="number" min="0.01" max={maximum} step="0.01" value={amount} onChange={event => setAmount(event.target.value)} className={field} /></label>
    <label className="text-sm font-bold">الفلوس وصلت فين؟<select value={treasuryId} onChange={event => setTreasuryId(event.target.value)} className={field}><option value="">اختر خزينة أو محفظة</option>{treasuries.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
    <label className="text-sm font-bold">رقم التحويل أو الإيصال<input value={reference} maxLength={300} onChange={event => setReference(event.target.value)} className={field} /></label>
  </div>;

  return <details id="code-batches" open={open} onToggle={event => setOpen(event.currentTarget.open)} className="mt-4 rounded-xl border border-[var(--admin-border)] p-4">
    <summary className="min-h-9 cursor-pointer font-bold">دفعات الأكواد: حساب وتسليم وسداد</summary>
    <div className="mt-4 space-y-4">
      <p className="text-sm leading-6 text-[var(--admin-muted)]">كل دفعة بتتحاسب مرة واحدة: إما كاملة عند التسليم، أو كل كود عند استخدامه. توزيع الفلوس من اتفاق المدرّس الموجود فوق.</p>
      <label className="block text-sm font-bold">دفعة الأكواد<select disabled={busy} value={groupId} onChange={event => setGroupId(event.target.value)} className={field}><option value="">اختر الدفعة</option>{groups.map(group => <option key={group.id} value={group.id}>{group.name} · {group.codeCount} كود · مستخدم {group.usedCount}</option>)}</select></label>
      {!groups.length && !error && <p className="text-sm text-[var(--admin-muted)]">لا توجد دفعات محتوى مرتبطة بالمدرّس. أكواد شحن الرصيد تُعرض في إدارة الأكواد.</p>}
      {error && <p role="alert" className="text-sm">{error} {groupId && <button type="button" onClick={() => void load()} className="min-h-11 underline">إعادة المحاولة</button>}</p>}
      {loading && <p role="status">جارٍ تحميل حساب الدفعة…</p>}
      {account && <fieldset disabled={busy} className="space-y-4 disabled:opacity-60">
        {!account.started && <div className="flex flex-wrap gap-3">{([
          ['CodeActivation', 'كل كود وقت استخدامه'], ['CodeDelivery', 'الدفعة كلها وقت التسليم'],
        ] as const).map(([trigger, label]) => <label key={trigger} className="min-h-11 rounded-xl border border-[var(--admin-border)] p-3 text-sm"><input type="radio" name={`timing-${groupId}`} checked={account.trigger === trigger} onChange={() => void changeTiming(trigger)} className="me-2" />{label}</label>)}</div>}
        {account.quote && <div className="space-y-3 bg-[var(--admin-card-soft)] p-4 rounded-xl">
          <p className="font-bold">{account.trigger === 'CodeDelivery' ? `معاينة حساب ${account.totalCodes} كود` : 'حساب الكود الواحد عند الاستخدام'}</p>
          <dl className="grid gap-3 sm:grid-cols-3">{[
            ['القيمة بعد الخصم', account.quote.net], ['نصيب المدرّس', account.quote.teacherShare], ['نصيب المنصّة', account.quote.platformShare],
          ].map(([label, value]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 font-bold">{teacherMoney(Number(value))}</dd></div>)}</dl>
          {!account.quote.agreement.agreementId && <p className="text-sm">مفيش اتفاق مسجل للنطاق ده؛ الحساب حاليًا بنسبة المدرّس القديمة. تقدر تضيف اتفاق من القسم اللي فوق.</p>}
        </div>}
        {!account.started && account.trigger === 'CodeDelivery' && account.quote && <>
          <p className="text-sm leading-6">المدرّس بيحتفظ بنصيبه، والمطلوب منه للمنصّة {teacherMoney(account.quote.platformShare)}. بعد التأكيد، تفعيل الطلاب مش هيضيف حساب جديد.</p>
          <label className="block text-sm font-bold">مين استلم الأكواد؟<input value={recipient} maxLength={300} onChange={event => setRecipient(event.target.value)} className={field} /></label>
          {account.quote.platformShare > 0 && <label className="flex min-h-11 items-center gap-2 text-sm font-bold"><input type="checkbox" checked={paidNow} onChange={event => setPaidNow(event.target.checked)} />استلمت منه فلوس بالفعل</label>}
          {paidNow ? paymentFields(account.quote.platformShare) : <p className="text-sm text-[var(--admin-muted)]">المبلغ هيتسجل عليه، وتقدر تسجّل سداده هنا في أي وقت.</p>}
          <button type="button" onClick={() => void confirm()} className={button}>تأكيد تسليم وحساب الدفعة</button>
        </>}
        {account.started && !account.delivery && <p className="text-sm leading-6">بدأ استخدام أو حساب الدفعة. طريقة الحساب مقفولة لحماية الحركات القديمة من التكرار.</p>}
        {account.delivery && <div className="space-y-4">
          <p className="font-bold">تم التسليم إلى {account.delivery.recipient} · {new Date(account.delivery.confirmedAt).toLocaleDateString('ar-EG', { timeZone: 'Africa/Cairo' })}</p>
          {account.delivery.platformAmountDue == null ? <p className="text-sm">ده تسليم قديم من غير تفاصيل تحصيل موثقة. مش هنعتبره مدفوع ولا نضيف عليه مطالبة تلقائيًا.</p> : <>
            <dl className="grid gap-3 sm:grid-cols-3">{[['المطلوب للمنصّة', account.delivery.platformAmountDue], ['دفع', account.delivery.paid], ['الباقي عليه', account.delivery.remaining ?? 0]].map(([label, value]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 font-bold">{teacherMoney(Number(value))}</dd></div>)}</dl>
            {(account.delivery.remaining ?? 0) > 0 ? <><h4 className="font-bold">تسجيل سداد جديد</h4>{paymentFields(account.delivery.remaining!)}<button type="button" onClick={() => void collect()} className={button}>تسجيل المبلغ المستلم</button></> : <p className="text-sm font-bold text-[var(--admin-primary)]">الدفعة مسددة بالكامل</p>}
            {account.delivery.payments.length > 0 && <ul className="divide-y divide-[var(--admin-border)]">{account.delivery.payments.map(payment => <li key={payment.id} className="flex flex-wrap justify-between gap-2 py-3 text-sm"><span>{payment.reference} · {new Date(payment.receivedAt).toLocaleDateString('ar-EG', { timeZone: 'Africa/Cairo' })}</span><strong>{teacherMoney(payment.amount)}</strong></li>)}</ul>}
          </>}
        </div>}
      </fieldset>}
    </div>
  </details>;
}
