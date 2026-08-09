'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, {
  FinanceBootstrap,
  PlatformExpenseRow,
  WalletTransferReview,
} from '@/services/platform-finance-service';

const englishNumber = new Intl.NumberFormat('en-US');
const moneyFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const money = (value: number) => moneyFormatter.format(value);
const dateTime = (value: string) =>
  new Intl.DateTimeFormat('ar-EG-u-nu-latn', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Africa/Cairo',
  }).format(new Date(value));

function PhoneNumber({ value }: { value?: string | null }) {
  return (
    <bdi className="font-mono font-bold tabular-nums" dir="ltr">
      {value || 'غير مسجل'}
    </bdi>
  );
}

function Money({ value }: { value: number }) {
  return (
    <bdi
      className="inline-flex items-baseline gap-1 whitespace-nowrap font-bold tabular-nums"
      dir="ltr"
    >
      <span>{money(value)}</span>
      <span className="font-sans text-[0.72em] font-bold" dir="rtl">
        ج.م
      </span>
    </bdi>
  );
}

export default function ExpenseManager() {
  const [rows, setRows] = useState<PlatformExpenseRow[]>([]);
  const [reviews, setReviews] = useState<WalletTransferReview[]>([]);
  const [bootstrap, setBootstrap] = useState<FinanceBootstrap | null>(null);
  const [editing, setEditing] = useState<string | null>(null);
  const [beneficiary, setBeneficiary] = useState('');
  const [reason, setReason] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [destinationTreasuryId, setDestinationTreasuryId] = useState('');
  const [error, setError] = useState('');
  const load = async () => {
    try {
      setError('');
      const [expenses, pendingReviews, financeBootstrap] = await Promise.all([
        platformFinanceService.getExpenses(),
        platformFinanceService.getWalletTransferReviews(),
        platformFinanceService.bootstrap(),
      ]);
      setRows(expenses);
      setReviews(pendingReviews);
      setBootstrap(financeBootstrap);
    } catch {
      setError('تعذر تحميل المصروفات أو تحويلات المحافظ');
    }
  };
  useEffect(() => {
    void load();
  }, []);
  async function reverse(id: string) {
    const reason = window.prompt('سبب عكس المصروف؟');
    if (!reason) return;
    try {
      await platformFinanceService.reverseExpense(id, reason);
      await load();
    } catch {
      setError('تعذر عكس المصروف');
    }
  }
  async function backfill() {
    try {
      const result =
        await platformFinanceService.backfillWalletTransferReviews();
      await load();
      if (result.added === 0)
        setError('لا توجد رسائل تحويل قديمة جديدة للمراجعة.');
    } catch {
      setError('تعذر قراءة رسائل التحويل القديمة');
    }
  }
  async function recordExpense(review: WalletTransferReview) {
    if (!categoryId || !beneficiary.trim() || !reason.trim()) {
      setError('اختر التصنيف واكتب المستفيد والسبب أولاً');
      return;
    }
    try {
      await platformFinanceService.recordWalletTransferExpense(review.id, {
        categoryId,
        beneficiaryName: beneficiary,
        reason,
      });
      setEditing(null);
      setBeneficiary('');
      setReason('');
      setCategoryId('');
      await load();
    } catch {
      setError('تعذر تسجيل التحويل كمصروف');
    }
  }
  async function recordInternal(review: WalletTransferReview) {
    if (!destinationTreasuryId) {
      setError('اختر محفظة المنصة المستلمة أولاً');
      return;
    }
    if (review.serviceFee > 0) {
      setError('هذا التحويل به رسوم؛ سجّله كمصروف حتى تُحتسب الرسوم بدقة.');
      return;
    }
    try {
      await platformFinanceService.recordWalletInternalTransfer(
        review.id,
        destinationTreasuryId
      );
      setEditing(null);
      setDestinationTreasuryId('');
      await load();
    } catch {
      setError('تعذر تسجيل التحويل الداخلي');
    }
  }
  const toggleReview = (id: string) => {
    setError('');
    setEditing((current) => (current === id ? null : id));
    setBeneficiary('');
    setReason('');
    setCategoryId('');
    setDestinationTreasuryId('');
  };

  const classificationForm = (review: WalletTransferReview) =>
    editing === review.id ? (
      <div className="grid gap-3 border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 lg:grid-cols-2">
        <div className="grid gap-3 sm:grid-cols-2 lg:col-span-2">
          <label className="grid gap-1.5 text-sm font-bold">
            نوع المصروف
            <select
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              className="admin-input font-normal"
            >
              <option value="">اختر نوع المصروف</option>
              {bootstrap?.categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1.5 text-sm font-bold">
            المستفيد أو الجهة
            <input
              className="admin-input font-normal"
              value={beneficiary}
              onChange={(e) => setBeneficiary(e.target.value)}
              placeholder="مثال: شركة الإنترنت"
            />
          </label>
        </div>
        <label className="grid gap-1.5 text-sm font-bold">
          سبب التحويل
          <input
            className="admin-input font-normal"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="اكتب وصفًا يساعدك في المراجعة لاحقًا"
          />
        </label>
        <div className="flex items-end">
          <button
            type="button"
            className="admin-btn-primary w-full"
            onClick={() => void recordExpense(review)}
          >
            تسجيل الحركة كمصروف
          </button>
        </div>
        <div className="lg:col-span-2 flex items-end gap-3 border-t border-[var(--admin-border)] pt-3 max-sm:flex-col">
          <label className="grid flex-1 gap-1.5 text-sm font-bold max-sm:w-full">
            أو سجّلها كتحويل داخلي
            <select
              value={destinationTreasuryId}
              onChange={(e) => setDestinationTreasuryId(e.target.value)}
              className="admin-input font-normal"
            >
              <option value="">اختر محفظة المنصة المستلمة</option>
              {bootstrap?.treasuryAccounts
                .filter(
                  (account) => account.id !== review.sourceTreasuryAccountId
                )
                .map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
            </select>
          </label>
          <button
            type="button"
            className="admin-btn-ghost max-sm:w-full"
            onClick={() => void recordInternal(review)}
            disabled={review.serviceFee > 0}
            title={
              review.serviceFee > 0
                ? 'لا يمكن تسجيل تحويل به رسوم كتحويل داخلي'
                : undefined
            }
          >
            تسجيل كتحويل داخلي
          </button>
        </div>
      </div>
    ) : null;

  return (
    <section className="admin-panel rounded-2xl p-4 sm:p-6" dir="rtl">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-lg font-black">مصروفات المنصة</h2>
        <button
          className="admin-btn-ghost"
          type="button"
          onClick={() => void load()}
        >
          تحديث البيانات
        </button>
      </div>
      {error ? (
        <p
          role="alert"
          className="mb-3 rounded-lg bg-rose-50 p-3 text-sm font-bold text-rose-700 dark:bg-rose-950/30 dark:text-rose-300"
        >
          {error}
        </p>
      ) : null}
      <div className="mb-8 overflow-hidden rounded-xl border border-amber-300/70 bg-amber-50/50 dark:bg-amber-950/20">
        <div className="flex flex-wrap items-center justify-between gap-4 p-4 sm:p-5">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="font-black">تحويلات تحتاج تصنيفًا</h3>
              <span
                className="inline-flex min-w-7 justify-center rounded-full bg-amber-200 px-2.5 py-0.5 text-xs font-black text-amber-950"
                dir="ltr"
              >
                {englishNumber.format(reviews.length)}
              </span>
            </div>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-[var(--admin-muted)]">
              راجع أرقام المحافظ والمبلغ، ثم حدّد هل الحركة مصروف أم تحويل
              داخلي.
            </p>
          </div>
          <button
            type="button"
            className="admin-btn-ghost shrink-0"
            onClick={() => void backfill()}
          >
            استيراد التحويلات القديمة
          </button>
        </div>
        {reviews.length === 0 ? (
          <p className="border-t border-amber-200 px-4 py-6 text-center text-sm text-[var(--admin-muted)]">
            لا توجد تحويلات معلّقة للتصنيف.
          </p>
        ) : (
          <>
            <div className="hidden overflow-x-auto border-t border-amber-200 lg:block">
              <table className="w-full min-w-[980px] text-sm">
                <thead className="bg-white/60 text-[var(--admin-muted)] dark:bg-black/10">
                  <tr className="text-right">
                    <th className="px-4 py-3 font-bold">المحفظة المُرسلة</th>
                    <th className="px-4 py-3 font-bold">الرقم المستلم</th>
                    <th className="px-4 py-3 font-bold">المبلغ</th>
                    <th className="px-4 py-3 font-bold">الرسوم</th>
                    <th className="px-4 py-3 font-bold">إجمالي الخصم</th>
                    <th className="px-4 py-3 font-bold">التاريخ والمرجع</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody>
                  {reviews.map((review) => (
                    <tr
                      key={review.id}
                      className="border-t border-amber-200 align-top"
                    >
                      <td colSpan={7} className="p-0">
                        <div className="grid grid-cols-[1.35fr_1.15fr_1fr_.85fr_1fr_1.25fr_auto] items-center bg-[var(--admin-card)] transition-colors hover:bg-[var(--admin-card-soft)]">
                          <div className="px-4 py-4">
                            <p className="font-bold">{review.sourceWallet}</p>
                            <p className="mt-1 text-xs text-[var(--admin-muted)]">
                              <PhoneNumber value={review.sourceWalletNumber} />
                            </p>
                          </div>
                          <div className="px-4 py-4">
                            <PhoneNumber
                              value={review.destinationPhoneNumber}
                            />
                          </div>
                          <div className="px-4 py-4">
                            <Money value={review.amount} />
                          </div>
                          <div className="px-4 py-4">
                            <Money value={review.serviceFee} />
                          </div>
                          <div className="px-4 py-4 text-[var(--admin-primary)]">
                            <Money value={review.amount + review.serviceFee} />
                          </div>
                          <div className="px-4 py-4">
                            <p className="whitespace-nowrap text-xs">
                              {dateTime(review.occurredAt)}
                            </p>
                            <p className="mt-1 text-xs text-[var(--admin-muted)]">
                              مرجع:{' '}
                              <bdi className="font-mono tabular-nums" dir="ltr">
                                {review.transferReference || 'غير متاح'}
                              </bdi>
                            </p>
                          </div>
                          <div className="px-4 py-4">
                            <button
                              type="button"
                              className={
                                editing === review.id
                                  ? 'admin-btn-primary whitespace-nowrap'
                                  : 'admin-btn-ghost whitespace-nowrap'
                              }
                              onClick={() => toggleReview(review.id)}
                            >
                              {editing === review.id
                                ? 'إغلاق التصنيف'
                                : 'تحديد المصروف'}
                            </button>
                          </div>
                        </div>
                        {classificationForm(review)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-amber-200 lg:hidden">
              {reviews.map((review) => (
                <article
                  key={review.id}
                  className="border-b border-amber-200 last:border-b-0"
                >
                  <div className="grid grid-cols-2 gap-x-4 gap-y-4 bg-[var(--admin-card)] p-4">
                    <div className="col-span-2 flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="text-xs font-bold text-[var(--admin-muted)]">
                          من المحفظة
                        </p>
                        <p className="mt-1 font-black">{review.sourceWallet}</p>
                        <p className="mt-1 text-sm">
                          <PhoneNumber value={review.sourceWalletNumber} />
                        </p>
                      </div>
                      <p className="whitespace-nowrap text-xs text-[var(--admin-muted)]">
                        {dateTime(review.occurredAt)}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs font-bold text-[var(--admin-muted)]">
                        إلى الرقم
                      </p>
                      <p className="mt-1">
                        <PhoneNumber value={review.destinationPhoneNumber} />
                      </p>
                    </div>
                    <div>
                      <p className="text-xs font-bold text-[var(--admin-muted)]">
                        المبلغ
                      </p>
                      <p className="mt-1">
                        <Money value={review.amount} />
                      </p>
                    </div>
                    <div>
                      <p className="text-xs font-bold text-[var(--admin-muted)]">
                        الرسوم
                      </p>
                      <p className="mt-1">
                        <Money value={review.serviceFee} />
                      </p>
                    </div>
                    <div>
                      <p className="text-xs font-bold text-[var(--admin-muted)]">
                        إجمالي الخصم
                      </p>
                      <p className="mt-1 text-[var(--admin-primary)]">
                        <Money value={review.amount + review.serviceFee} />
                      </p>
                    </div>
                    <div className="col-span-2 flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-3">
                      <p className="min-w-0 break-all text-xs text-[var(--admin-muted)]">
                        المرجع:{' '}
                        <bdi className="font-mono tabular-nums" dir="ltr">
                          {review.transferReference || 'غير متاح'}
                        </bdi>
                      </p>
                      <button
                        type="button"
                        className={
                          editing === review.id
                            ? 'admin-btn-primary shrink-0'
                            : 'admin-btn-ghost shrink-0'
                        }
                        onClick={() => toggleReview(review.id)}
                      >
                        {editing === review.id ? 'إغلاق' : 'تحديد المصروف'}
                      </button>
                    </div>
                  </div>
                  {classificationForm(review)}
                </article>
              ))}
            </div>
          </>
        )}
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-right">
              <th>المستند</th>
              <th>الوصف</th>
              <th>التاريخ</th>
              <th>المبلغ</th>
              <th>المدفوع</th>
              <th>الحالة</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.id}
                className="border-t border-[var(--admin-border)]"
              >
                <td>
                  <bdi dir="ltr" className="tabular-nums">
                    {row.documentNumber}
                  </bdi>
                </td>
                <td>{row.description}</td>
                <td>
                  {new Date(row.occurredAt).toLocaleDateString(
                    'ar-EG-u-nu-latn',
                    { timeZone: 'Africa/Cairo' }
                  )}
                </td>
                <td>
                  <Money value={row.amount} />
                </td>
                <td>
                  <Money value={row.paid} />
                </td>
                <td>
                  {row.status === 5
                    ? 'معكوس'
                    : row.status === 4
                      ? 'مدفوع'
                      : row.status === 3
                        ? 'مدفوع جزئيًا'
                        : row.status === 2
                          ? 'آجل'
                          : 'مسودة'}
                </td>
                <td>
                  {row.status !== 5 && row.status !== 1 ? (
                    <button
                      className="text-rose-600"
                      type="button"
                      onClick={() => void reverse(row.id)}
                    >
                      عكس
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {rows.length === 0 ? (
          <p className="py-8 text-center text-[var(--admin-muted)]">
            لا توجد مصروفات.
          </p>
        ) : null}
      </div>
    </section>
  );
}
