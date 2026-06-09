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
  ChevronLeft,
  ChevronRight,
  Sparkles,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminModal,
  AdminStatCard,
} from '@/components/admin';
import {
  financeService,
  TeacherAccountDto,
  TeacherTransactionDto,
  TeacherPayoutDto,
} from '@/services/finance-service';
import toast from 'react-hot-toast';

type TabType = 'transactions' | 'payouts';

export default function TeacherFinancePage() {
  const [activeTab, setActiveTab] = useState<TabType>('transactions');
  const [account, setAccount] = useState<TeacherAccountDto | null>(null);
  const [accountLoading, setAccountLoading] = useState<boolean>(true);

  // Transactions ledger states
  const [transactions, setTransactions] = useState<TeacherTransactionDto[]>([]);
  const [transactionsLoading, setTransactionsLoading] = useState<boolean>(false);
  const [txPage, setTxPage] = useState<number>(1);
  const [txPageSize] = useState<number>(20);
  const [txTotalCount, setTxTotalCount] = useState<number>(0);

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
      const paged = await financeService.getTeacherTransactions(txPage, txPageSize);
      setTransactions(paged.items);
      setTxTotalCount(paged.totalCount);
    } catch {
      toast.error('تعذر تحميل سجل الأرباح');
    } finally {
      setTransactionsLoading(false);
    }
  }, [txPage, txPageSize]);

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

  // Handle new payout request submission
  const handleSubmitPayout = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!account) return;

    const amountNum = parseFloat(payoutAmount);
    if (isNaN(amountNum) || amountNum <= 0) {
      toast.error('يرجى إدخال مبلغ صحيح أكبر من الصفر');
      return;
    }

    if (amountNum > account.currentBalance) {
      toast.error(`المبلغ المطلوب أكبر من رصيدك الحالي المتاح (${formatEGP(account.currentBalance)})`);
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
    return `${amount.toLocaleString('ar-EG')} جنيها`;
  };

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString('ar-EG', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  const getPayoutStatusBadge = (status: string | number) => {
    const statusStr = typeof status === 'number'
      ? (status === 0 ? 'Pending' : status === 1 ? 'Paid' : status === 2 ? 'Rejected' : 'Unknown')
      : status;

    switch (statusStr) {
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
      key: 'activatedAt',
      label: 'تاريخ الحركة والتفعيل',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatDate(item.activatedAt)}</span>,
    },
    {
      key: 'packageName',
      label: 'الباقة الكورس المفعلة',
      render: (item) => <span className="font-bold text-[var(--admin-text)]">{item.packageName}</span>,
    },
    {
      key: 'studentName',
      label: 'اسم الطالب',
      render: (item) => <span className="font-semibold text-xs text-[var(--admin-text)]">{item.studentName}</span>,
    },
    {
      key: 'serialNumber',
      label: 'الرقم التسلسلي للكود',
      render: (item) => <span className="font-mono text-xs font-bold text-[var(--admin-primary)]">{item.serialNumber}</span>,
    },
    {
      key: 'price',
      label: 'سعر الباقة الإجمالي',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatEGP(item.price)}</span>,
    },
    {
      key: 'commissionRate',
      label: 'نسبة عمولتك',
      render: (item) => <span className="font-mono text-xs font-bold text-emerald-600">%{item.commissionRate}</span>,
    },
    {
      key: 'commissionEarned',
      label: 'الأرباح المستحقة المضافة',
      render: (item) => (
        <span className="font-mono text-sm font-black text-emerald-600 dark:text-emerald-400">
          +{formatEGP(item.commissionEarned)}
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
    <AdminShellChrome
      activePath="/teacher/finance"
      sectionLabel="المالية والأرباح"
      pageTitle="سجل الأرباح والمسحوبات الخاصة بك"
      subtitle="تتبع تفاصيل أرباحك وعمولاتك المحتسبة من تفعيل أكواد الباقات الدراسية وطلب سحب الأرصدة المتاحة."
      action={
        <button
          onClick={() => {
            if (account && account.currentBalance > 0) {
              setShowPayoutModal(true);
            } else {
              toast.error('ليس لديك رصيد كافٍ متاح للسحب حالياً');
            }
          }}
          disabled={accountLoading || !account || account.currentBalance <= 0}
          className="rounded-xl bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-black text-[var(--admin-primary-contrast)] shadow-[0_4px_12px_var(--admin-shadow)] hover:opacity-90 transition flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Plus className="h-4 w-4" />
          طلب سحب رصيد جديد
        </button>
      }
    >
      {/* Stats Overview */}
      <section className="mb-8 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={Coins}
          label="الرصيد المتاح للسحب"
          value={accountLoading ? '...' : formatEGP(account?.currentBalance ?? 0)}
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
          value={accountLoading ? '...' : `%${account?.commissionRate ?? 0}`}
          subtitle="نسبة ربحك المضافة لكل تفعيل كود باقة"
        />
      </section>

      {/* Tabs selectors */}
      <div className="mb-8 flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            onClick={() => setActiveTab('transactions')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'transactions'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Sparkles className="h-4 w-4" />
            حركات عمولات الأكواد المفعلة
          </button>
          <button
            onClick={() => setActiveTab('payouts')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'payouts'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Clock className="h-4 w-4" />
            تاريخ طلبات السحب
          </button>
        </div>
      </div>

      {/* Tab: Transactions */}
      {activeTab === 'transactions' && (
        <div>
          <div className="mb-4 flex items-center justify-between">
            <h4 className="text-sm font-black text-[var(--admin-text)]">تفاصيل العمولات المستحقة المودعة</h4>
            <button
              onClick={fetchTransactions}
              disabled={transactionsLoading}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1.5 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] flex items-center gap-1.5 transition"
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
          <div className="mb-4 flex items-center justify-between">
            <h4 className="text-sm font-black text-[var(--admin-text)]">أرشيف وتفاصيل عمليات سحب الرصيد</h4>
            <button
              onClick={fetchPayouts}
              disabled={payoutsLoading}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1.5 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] flex items-center gap-1.5 transition"
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
              {account ? formatEGP(account.currentBalance) : ''}
            </span>
          </div>

          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">المبلغ المطلوب سحبه (جنيها)</label>
            <input
              type="number"
              step="0.01"
              required
              max={account?.currentBalance ?? 0}
              placeholder="أدخل قيمة السحب..."
              value={payoutAmount}
              onChange={(e) => setPayoutAmount(e.target.value)}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
            />
          </div>

          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)] pt-4">
            <button
              type="button"
              onClick={() => setShowPayoutModal(false)}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)]"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isSubmittingPayout}
              className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
            >
              {isSubmittingPayout ? 'جاري التقديم...' : 'تقديم طلب سحب'}
            </button>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
