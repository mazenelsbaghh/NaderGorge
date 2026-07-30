'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  Coins,
  DollarSign,
  TrendingUp,
  Clock,
  Plus,
  RefreshCw,
  CheckCircle2,
  XCircle,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Sparkles,
  UserRound,
  ReceiptText,
} from 'lucide-react';
import {
  AdminDataTable,
  AdminColumn,
  AdminModal,
  AdminStatCard,
  AdminTab,
  AdminTabBar,
} from '@/components/admin';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import {
  financeService,
  TeacherAccountDto,
  TeacherTransactionDto,
  TeacherPayoutDto,
  TeacherFinanceDayDto,
} from '@/services/finance-service';
import toast from 'react-hot-toast';

type TabType = 'transactions' | 'payouts';

const FINANCE_TABS: AdminTab<TabType>[] = [
  { key: 'transactions', label: 'حركات عمولات الأكواد المفعلة', icon: Sparkles },
  { key: 'payouts', label: 'تاريخ طلبات السحب', icon: Clock },
];

const formatIsoDate = (date: Date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export default function TeacherFinancePageClient() {
  const [activeTab, setActiveTab] = useState<TabType>('transactions');
  const [account, setAccount] = useState<TeacherAccountDto | null>(null);
  const [accountLoading, setAccountLoading] = useState<boolean>(true);

  // Transactions ledger states
  const [transactions, setTransactions] = useState<TeacherTransactionDto[]>([]);
  const [transactionsLoading, setTransactionsLoading] = useState<boolean>(false);
  const [txPage, setTxPage] = useState<number>(1);
  const [txPageSize] = useState<number>(20);
  const [txTotalCount, setTxTotalCount] = useState<number>(0);
  const [selectedDate, setSelectedDate] = useState<string>('');
  const [calendarMonth, setCalendarMonth] = useState<Date>(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });
  const [calendarDays, setCalendarDays] = useState<TeacherFinanceDayDto[]>([]);
  const [calendarLoading, setCalendarLoading] = useState<boolean>(false);
  const [selectedCalendarDay, setSelectedCalendarDay] = useState<TeacherFinanceDayDto | null>(null);

  // Payout requests ledger states
  const [payouts, setPayouts] = useState<TeacherPayoutDto[]>([]);
  const [payoutsLoading, setPayoutsLoading] = useState<boolean>(false);

  // Request Payout Modal state
  const [showPayoutModal, setShowPayoutModal] = useState<boolean>(false);
  const [payoutAmount, setPayoutAmount] = useState<string>('');
  const [isSubmittingPayout, setIsSubmittingPayout] = useState<boolean>(false);

  // Load account statistics
  const fetchAccountSummary = useCallback(async () => {
    setAccountLoading(true);
    try {
      const summary = await financeService.getTeacherAccountSummary();
      setAccount(summary);
    } catch {
      toast.error('تعذر تحميل إحصائيات الحساب المالية');
    } finally {
      setAccountLoading(false);
    }
  }, []);

  // Load transactions ledger
  const fetchTransactions = useCallback(async () => {
    setTransactionsLoading(true);
    try {
      const paged = await financeService.getTeacherTransactions(
        txPage,
        txPageSize,
        selectedDate ? { date: selectedDate } : undefined
      );
      setTransactions(paged.items);
      setTxTotalCount(paged.totalCount);
    } catch {
      toast.error('تعذر تحميل سجل الأرباح');
    } finally {
      setTransactionsLoading(false);
    }
  }, [selectedDate, txPage, txPageSize]);

  const fetchCalendar = useCallback(async () => {
    setCalendarLoading(true);
    try {
      const from = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth(), 1);
      const to = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth() + 1, 0);
      const rows = await financeService.getTeacherFinanceCalendar(
        formatIsoDate(from),
        formatIsoDate(to)
      );
      setCalendarDays(rows);
    } catch {
      toast.error('تعذر تحميل تقويم الأرباح');
    } finally {
      setCalendarLoading(false);
    }
  }, [calendarMonth]);

  // Load payouts ledger
  const fetchPayouts = useCallback(async () => {
    setPayoutsLoading(true);
    try {
      const data = await financeService.getTeacherPayouts();
      setPayouts(data);
    } catch {
      toast.error('تعذر تحميل سجل طلبات السحب');
    } finally {
      setPayoutsLoading(false);
    }
  }, []);

  // Initial loads
  useEffect(() => {
    fetchAccountSummary();
  }, [fetchAccountSummary]);

  useEffect(() => {
    if (activeTab === 'transactions') {
      fetchTransactions();
    } else if (activeTab === 'payouts') {
      fetchPayouts();
    }
  }, [activeTab, fetchTransactions, fetchPayouts]);

  useEffect(() => {
    fetchCalendar();
  }, [fetchCalendar]);

  // Handle new payout request submission
  const handleSubmitPayout = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!account) return;

    const amountNum = parseFloat(payoutAmount);
    if (isNaN(amountNum) || amountNum <= 0) {
      toast.error('يرجى إدخال مبلغ صحيح أكبر من الصفر');
      return;
    }

    if (amountNum > account.availableBalance) {
      toast.error(`المبلغ المطلوب أكبر من رصيدك الحالي المتاح (${formatEGP(account.availableBalance)})`);
      return;
    }

    setIsSubmittingPayout(true);
    try {
      const res = await financeService.requestTeacherPayout(amountNum);
      if (res.success) {
        toast.success('تم تقديم طلب السحب بنجاح ✅ وسيتم مراجعته وصرفه قريباً');
        setShowPayoutModal(false);
        setPayoutAmount('');
        // Refresh account and payout histories
        fetchAccountSummary();
        if (activeTab === 'payouts') {
          fetchPayouts();
        }
      } else {
        toast.error(res.message || 'تعذر تقديم طلب السحب');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء إرسال طلب السحب');
    } finally {
      setIsSubmittingPayout(false);
    }
  };

  // Formatting helpers
  const formatEGP = (amount: number) => {
    return `${amount.toLocaleString('en-US')} جنيها`;
  };

  const formatCompactEGP = (amount: number) => {
    return `${amount.toLocaleString('en-US')} ج`;
  };

  const formatPercent = (value: number) => {
    return `${value.toLocaleString('en-US')}%`;
  };

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString('ar-EG-u-nu-latn', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  const calendarRowsByDate = new Map(
    calendarDays.map((day) => [day.date.slice(0, 10), day])
  );

  const monthGridDays = (() => {
    const first = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth(), 1);
    const last = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth() + 1, 0);
    const startOffset = (first.getDay() + 1) % 7;
    const cells: Array<Date | null> = Array.from({ length: startOffset }, () => null);
    for (let day = 1; day <= last.getDate(); day += 1) {
      cells.push(new Date(calendarMonth.getFullYear(), calendarMonth.getMonth(), day));
    }
    while (cells.length % 7 !== 0) {
      cells.push(null);
    }
    return cells;
  })();

  const getPayoutStatusBadge = (status: string | number) => {
    const statusStr = typeof status === 'number'
      ? (status === 0 ? 'Pending' : status === 1 ? 'Paid' : status === 2 ? 'Rejected' : status === 3 ? 'Approved' : 'Unknown')
      : status;

    switch (statusStr) {
      case 'Approved':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2.5 py-1 text-xs font-bold text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">
            <CheckCircle2 className="h-3 w-3" />
            جاهز للصرف
          </span>
        );
      case 'Paid':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
            <CheckCircle2 className="h-3 w-3" />
            تم الصرف والاعتماد
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 px-2.5 py-1 text-xs font-bold text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">
            <XCircle className="h-3 w-3" />
            مرفوض
          </span>
        );
      case 'Pending':
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-1 text-xs font-bold text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 animate-pulse">
            <Clock className="h-3 w-3" />
            قيد المراجعة
          </span>
        );
    }
  };

  // Columns definitions
  const transactionColumns: AdminColumn<TeacherTransactionDto>[] = [
    {
      key: 'occurredAt',
      label: 'تاريخ الحركة والتفعيل',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatDate(item.occurredAt ?? item.activatedAt ?? new Date().toISOString())}</span>,
    },
    {
      key: 'contentName',
      label: 'الباقة الكورس المفعلة',
      render: (item) => <span className="font-bold text-[var(--admin-text)]">{item.contentName ?? item.packageName}</span>,
    },
    {
      key: 'studentName',
      label: 'اسم الطالب',
      render: (item) => <span className="font-semibold text-xs text-[var(--admin-text)]">{item.studentName}</span>,
    },
    {
      key: 'serialNumber',
      label: 'الرقم التسلسلي للكود',
      render: (item) => <span className="font-mono text-xs font-bold text-[var(--admin-primary)]">{item.codeSerialNumber ?? item.serialNumber ?? '—'}</span>,
    },
    {
      key: 'price',
      label: 'سعر الباقة الإجمالي',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatEGP(item.grossAmount ?? item.price ?? 0)}</span>,
    },
    {
      key: 'commissionRate',
      label: 'نسبة عمولتك',
      render: (item) => <span className="font-mono text-xs font-bold text-emerald-600">{formatPercent(item.allocationValue ?? item.commissionRate ?? 0)}</span>,
    },
    {
      key: 'commissionEarned',
      label: 'الأرباح المستحقة المضافة',
      render: (item) => (
        <span className="font-mono text-sm font-black text-emerald-600 dark:text-emerald-400">
          +{formatEGP(item.teacherShareAmount ?? item.commissionEarned ?? 0)}
        </span>
      ),
    },
  ];

  const payoutColumns: AdminColumn<TeacherPayoutDto>[] = [
    {
      key: 'createdAt',
      label: 'تاريخ الطلب',
      render: (item) => <span className="font-mono text-sm">{formatDate(item.createdAt)}</span>,
    },
    {
      key: 'amount',
      label: 'المبلغ المطلوب سحبه',
      render: (item) => <span className="font-bold font-mono text-base text-[var(--admin-text)]">{formatEGP(item.amount)}</span>,
    },
    {
      key: 'status',
      label: 'حالة الطلب',
      render: (item) => (
        <div>
          {getPayoutStatusBadge(item.status)}
          {(item.status === 'Rejected' || item.status === 2) && item.rejectionReason && (
            <div className="text-xs text-rose-500 mt-1.5 max-w-[300px] leading-relaxed" title={item.rejectionReason}>
              سبب الرفض: {item.rejectionReason}
            </div>
          )}
        </div>
      ),
    },
    {
      key: 'handledAt',
      label: 'تاريخ الإجراء المعالج',
      render: (item) => (
        <span className="font-mono text-xs text-[var(--admin-muted)]">
          {item.handledAt ? formatDate(item.handledAt) : 'لم يعالج بعد'}
        </span>
      ),
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher/finance"
      sectionLabel="المالية والأرباح"
      pageTitle="سجل الأرباح والمسحوبات الخاصة بك"
      subtitle="تتبع تفاصيل أرباحك وعمولاتك المحتسبة من تفعيل أكواد الباقات الدراسية وطلب سحب الأرصدة المتاحة."
      action={
        <button
          onClick={() => {
            if (account && account.availableBalance > 0) {
              setShowPayoutModal(true);
            } else {
              toast.error('ليس لديك رصيد كافٍ متاح للسحب حالياً');
            }
          }}
          disabled={accountLoading || !account || account.availableBalance <= 0}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-3 py-2 text-xs font-black text-[var(--admin-primary-contrast)] shadow-sm transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50 sm:px-5 sm:py-2.5 sm:text-sm"
        >
          <Plus className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="hidden sm:inline">طلب سحب رصيد جديد</span>
          <span className="sm:hidden">طلب سحب</span>
        </button>
      }
    >
      {/* Stats Overview */}
      <section className="mb-8 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={Coins}
          label="الرصيد المتاح للسحب"
          value={accountLoading ? '...' : formatEGP(account?.availableBalance ?? 0)}
          subtitle="يمكنك تقديم طلب سحب جديد بهذا الرصيد"
        />
        <AdminStatCard
          variant="accent"
          icon={TrendingUp}
          label="إجمالي الأرباح التاريخية"
          value={accountLoading ? '...' : formatEGP(account?.totalEarnings ?? 0)}
          subtitle="مجموع عمولاتك التراكمية على المنصة"
        />
        <AdminStatCard
          variant="muted"
          icon={DollarSign}
          label="نسبة عمولتك الحالية"
          value={accountLoading ? '...' : formatPercent(account?.commissionRate ?? 0)}
          subtitle="نسبة ربحك المضافة لكل تفعيل كود باقة"
        />
      </section>

      <section className="mb-8 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
        <div className="mb-5 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
              <CalendarDays className="h-5 w-5 text-[var(--admin-primary)]" />
              تقويم دخل الشهر
            </h3>
            <p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">
              اضغط على أي يوم لمعرفة الطلاب الذين دفعوا فيه، وسيتم فلترة الجدول لنفس اليوم.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setCalendarMonth((current) => new Date(current.getFullYear(), current.getMonth() - 1, 1))}
              className="grid h-10 w-10 place-items-center rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)]"
              title="الشهر السابق"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
            <span className="min-w-36 text-center text-sm font-black text-[var(--admin-text)]">
              {calendarMonth.toLocaleDateString('ar-EG-u-nu-latn', { month: 'long', year: 'numeric' })}
            </span>
            <button
              type="button"
              onClick={() => setCalendarMonth((current) => new Date(current.getFullYear(), current.getMonth() + 1, 1))}
              className="grid h-10 w-10 place-items-center rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)]"
              title="الشهر التالي"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
          </div>
        </div>

        <div className="grid grid-cols-7 gap-2 text-center text-xs font-black text-[var(--admin-muted)]">
          {['س', 'ح', 'ن', 'ث', 'ر', 'خ', 'ج'].map((label) => <div key={label}>{label}</div>)}
        </div>
        <div className="mt-2 grid grid-cols-7 gap-1 sm:gap-2">
          {monthGridDays.map((date, index) => {
            if (!date) {
              return <div key={`empty-${index}`} className="min-h-16 rounded-lg border border-transparent sm:min-h-20" />;
            }
            const iso = formatIsoDate(date);
            const row = calendarRowsByDate.get(iso);
            const isSelected = selectedDate === iso;
            return (
              <button
                key={iso}
                type="button"
                onClick={() => {
                  setSelectedDate((current) => current === iso ? '' : iso);
                  setSelectedCalendarDay(row ?? {
                    date: iso,
                    grossAmount: 0,
                    teacherShareAmount: 0,
                    platformShareAmount: 0,
                    transactionCount: 0,
                    pendingReviewCount: 0,
                    transactions: [],
                  });
                  setTxPage(1);
                }}
                aria-label={`عرض مدفوعات يوم ${iso}`}
                className={`min-h-16 rounded-lg border p-1.5 text-right transition sm:min-h-20 sm:p-2 ${
                  isSelected
                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-white'
                    : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] hover:border-[var(--admin-primary)]'
                }`}
              >
                <span className="block text-sm font-black">{date.getDate()}</span>
                {row ? (
                  <span className={`mt-2 block text-xs font-bold ${isSelected ? 'text-white' : 'text-emerald-600'}`}>
                    {formatCompactEGP(row.teacherShareAmount)}
                  </span>
                ) : (
                  <span className={`mt-2 block text-xs ${isSelected ? 'text-white/70' : 'text-[var(--admin-muted)]'}`}>0</span>
                )}
                {row?.transactionCount ? (
                  <span className={`mt-1 block text-[10px] font-bold ${isSelected ? 'text-white/80' : 'text-[var(--admin-muted)]'}`}>
                    {row.transactionCount.toLocaleString('en-US')} عملية
                  </span>
                ) : null}
                {row?.pendingReviewCount ? (
                  <span className={`mt-1 block text-[10px] font-bold ${isSelected ? 'text-white' : 'text-amber-600'}`}>
                    {row.pendingReviewCount.toLocaleString('en-US')} مراجعة
                  </span>
                ) : null}
              </button>
            );
          })}
        </div>
        {calendarLoading && <p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">جاري تحديث التقويم...</p>}
      </section>

      <div className="mb-8">
        <AdminTabBar tabs={FINANCE_TABS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {/* Tab: Transactions */}
      {activeTab === 'transactions' && (
        <div>
          <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h4 className="text-sm font-black text-[var(--admin-text)]">تفاصيل العمولات المستحقة المودعة</h4>
              {selectedDate && (
                <button type="button" onClick={() => { setSelectedDate(''); setTxPage(1); }} className="mt-1 text-xs font-bold text-[var(--admin-primary)]">
                  مسح فلتر يوم {selectedDate}
                </button>
              )}
            </div>
            <button
              onClick={fetchTransactions}
              disabled={transactionsLoading}
              className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1.5 text-xs font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${transactionsLoading ? 'animate-spin' : ''}`} />
              تحديث
            </button>
          </div>

          <AdminDataTable
            data={transactions}
            columns={transactionColumns}
            loading={transactionsLoading}
            rowKey={(item) => item.id}
            emptyMessage="لم يتم تسجيل عمولات تفعيل أكواد لحسابك بعد."
          />

          {/* Transactions Pagination */}
          {txTotalCount > txPageSize && (
            <div className="mt-6 flex items-center justify-between border-t border-[var(--admin-border)] pt-4">
              <span className="text-xs font-semibold text-[var(--admin-muted)]">
                عرض {transactions.length} من أصل {txTotalCount} عمولة مسجلة
              </span>
              <div className="flex items-center gap-2">
                <button
                  disabled={txPage === 1 || transactionsLoading}
                  onClick={() => setTxPage((prev) => prev - 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <span className="font-mono text-sm font-bold px-3">
                  صفحة {txPage} من {Math.ceil(txTotalCount / txPageSize)}
                </span>
                <button
                  disabled={txPage * txPageSize >= txTotalCount || transactionsLoading}
                  onClick={() => setTxPage((prev) => prev + 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab: Payouts */}
      {activeTab === 'payouts' && (
        <div>
          <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <h4 className="text-sm font-black text-[var(--admin-text)]">أرشيف وتفاصيل عمليات سحب الرصيد</h4>
            <button
              onClick={fetchPayouts}
              disabled={payoutsLoading}
              className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1.5 text-xs font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${payoutsLoading ? 'animate-spin' : ''}`} />
              تحديث
            </button>
          </div>

          <AdminDataTable
            data={payouts}
            columns={payoutColumns}
            loading={payoutsLoading}
            rowKey={(item) => item.id}
            emptyMessage="لم تقم بتقديم طلبات سحب رصيد حتى الآن."
          />
        </div>
      )}

      {/* Modal: New Payout Request */}
      <AdminModal
        open={showPayoutModal}
        onClose={() => setShowPayoutModal(false)}
        title="تقديم طلب سحب رصيد جديد"
        subtitle="سيتم إرسال الطلب لمراجعة الإدارة وصرفه لك فورياً"
      >
        <form onSubmit={handleSubmitPayout} className="space-y-4">
          <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 flex justify-between items-center">
            <span className="text-xs font-bold text-[var(--admin-muted)]">الرصيد الكلي المتاح حالياً</span>
            <span className="font-mono text-sm font-black text-[var(--admin-text)]">
              {account ? formatEGP(account.availableBalance) : ''}
            </span>
          </div>

          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">المبلغ المطلوب سحبه (جنيها)</label>
            <input
              type="number"
              step="0.01"
              required
              max={account?.availableBalance ?? 0}
              placeholder="أدخل قيمة السحب..."
              value={payoutAmount}
              onChange={(e) => setPayoutAmount(e.target.value)}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
            />
          </div>

          <div className="flex flex-col-reverse gap-2 border-t border-[var(--admin-border)] pt-4 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={() => setShowPayoutModal(false)}
              className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)]"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isSubmittingPayout}
              className="min-h-11 rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
            >
              {isSubmittingPayout ? 'جاري التقديم...' : 'تقديم طلب سحب'}
            </button>
          </div>
        </form>
      </AdminModal>

      <AdminModal
        open={!!selectedCalendarDay}
        onClose={() => setSelectedCalendarDay(null)}
        title={selectedCalendarDay ? `مدفوعات يوم ${formatDate(selectedCalendarDay.date)}` : 'مدفوعات اليوم'}
        subtitle="تفاصيل الطلاب والمعاملات التي دخلت في أرباح هذا اليوم."
        maxWidth="max-w-3xl"
      >
        {selectedCalendarDay && (
          <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-3">
              <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                <p className="text-xs font-bold text-[var(--admin-muted)]">إجمالي المدفوع</p>
                <p className="mt-2 font-mono text-lg font-black text-[var(--admin-text)]">
                  {formatEGP(selectedCalendarDay.grossAmount)}
                </p>
              </div>
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-900/50 dark:bg-emerald-950/20">
                <p className="text-xs font-bold text-emerald-700 dark:text-emerald-300">ربح المدرس</p>
                <p className="mt-2 font-mono text-lg font-black text-emerald-700 dark:text-emerald-300">
                  {formatEGP(selectedCalendarDay.teacherShareAmount)}
                </p>
              </div>
              <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                <p className="text-xs font-bold text-[var(--admin-muted)]">عدد العمليات</p>
                <p className="mt-2 font-mono text-lg font-black text-[var(--admin-text)]">
                  {selectedCalendarDay.transactionCount.toLocaleString('en-US')}
                </p>
              </div>
            </div>

            {selectedCalendarDay.transactions.length > 0 ? (
              <div className="space-y-3">
                {selectedCalendarDay.transactions.map((transaction) => (
                  <div
                    key={transaction.id}
                    className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-sm"
                  >
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div className="flex items-start gap-3">
                        <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-primary)]">
                          <UserRound className="h-5 w-5" />
                        </div>
                        <div>
                          <p className="text-sm font-black text-[var(--admin-text)]">{transaction.studentName}</p>
                          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
                            {transaction.studentPhone || 'لا يوجد رقم هاتف'}
                          </p>
                          <p className="mt-2 flex items-center gap-1.5 text-xs font-bold text-[var(--admin-text)]">
                            <ReceiptText className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                            {transaction.contentName}
                          </p>
                        </div>
                      </div>

                      <div className="text-right">
                        <p className="text-xs font-bold text-[var(--admin-muted)]">ربحك من العملية</p>
                        <p className="font-mono text-lg font-black text-emerald-600">
                          {formatEGP(transaction.teacherShareAmount)}
                        </p>
                        <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
                          دفع الطالب {formatEGP(transaction.paidAmount)}
                        </p>
                      </div>
                    </div>

                    <div className="mt-4 flex flex-wrap gap-2 border-t border-[var(--admin-border)] pt-3">
                      <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">
                        {formatDate(transaction.occurredAt)}
                      </span>
                      {transaction.codeSerialNumber ? (
                        <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">
                          كود #{transaction.codeSerialNumber.toLocaleString('en-US')}
                        </span>
                      ) : null}
                      <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-bold text-blue-700 dark:bg-blue-950/30 dark:text-blue-300">
                        {transaction.sourceType}
                      </span>
                      <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-300">
                        {transaction.reviewStatus}
                      </span>
                      <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">
                        {transaction.payoutStatus}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-8 text-center">
                <p className="text-sm font-black text-[var(--admin-text)]">لا توجد مدفوعات في هذا اليوم.</p>
                <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
                  اختر يومًا آخر من التقويم لعرض بيانات الدفع.
                </p>
              </div>
            )}
          </div>
        )}
      </AdminModal>
    </TeacherPage>
  );
}
