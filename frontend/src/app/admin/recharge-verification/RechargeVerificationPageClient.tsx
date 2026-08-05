'use client';

import { useState, useEffect, useMemo } from 'react';
import {
  Check,
  X,
  FileText,
  Smartphone,
  Search,
  AlertCircle,
  CheckCircle2,
  Clock,
  HelpCircle,
  Link as LinkIcon,
  Maximize2,
  ChevronDown,
  MessageSquareText,
  WalletCards
} from 'lucide-react';
import {
  AdminPage,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal
} from '@/components/admin';
import { formatRelativeDate, formatDate } from '@/components/admin/admin-utils';
import NeumorphButton from '@/components/ui/neumorph-button';
import { walletService, type AdminRechargeRequestDto, type AdminIncomingSmsLogDto, type WalletDto } from '@/services/wallet-service';
import toast from 'react-hot-toast';

type RechargeStatusValue = AdminRechargeRequestDto['status'];
type RechargeStatusFilter = 0 | 1 | 2 | 3 | 4 | 5 | 'all';
type UnmatchedSmsAmountGroup = { key: string; amount?: number; items: AdminIncomingSmsLogDto[] };
type UnmatchedSmsWalletGroup = { id: string; label: string; phoneNumber: string; amountGroups: UnmatchedSmsAmountGroup[] };

const ASSET_BASE_URL = (
  process.env.NEXT_PUBLIC_ASSETS_URL ||
  process.env.NEXT_PUBLIC_ASSET_BASE_URL ||
  'https://assets.massar-academy.net'
).replace(/\/$/, '');

const normalizeRechargeStatus = (status: RechargeStatusValue): number | null => {
  if (typeof status === 'number') return status;

  const normalized = status.toLowerCase();
  switch (normalized) {
    case 'pending':
      return 0;
    case 'matched':
      return 1;
    case 'approved':
      return 2;
    case 'rejected':
      return 3;
    case 'expired':
      return 4;
    case 'cancelled':
      return 5;
    default:
      return null;
  }
};

const isRechargeStatus = (status: RechargeStatusValue, expected: number) =>
  normalizeRechargeStatus(status) === expected;

const resolveAssetUrl = (url?: string | null) => {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/uploads/')) return `${ASSET_BASE_URL}${url}`;
  return url;
};

/** Reusable workspace for admins and authorized staff who reconcile recharge requests. */
export function RechargeVerificationWorkspace() {
  const [requests, setRequests] = useState<AdminRechargeRequestDto[]>([]);
  const [unmatchedSms, setUnmatchedSms] = useState<AdminIncomingSmsLogDto[]>([]);
  const [wallets, setWallets] = useState<WalletDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // Filters
  const [statusFilter, setStatusFilter] = useState<RechargeStatusFilter>(0); // Default to Pending (0)
  const [searchQuery, setSearchQuery] = useState('');

  // Modals
  const [viewScreenshotUrl, setViewScreenshotUrl] = useState<string | null>(null);
  const [approveModalRequest, setApproveModalRequest] = useState<AdminRechargeRequestDto | null>(null);
  const [selectedSmsId, setSelectedSmsId] = useState<string>('');
  const [selectedWalletId, setSelectedWalletId] = useState<string>('');
  const [rejectModalRequest, setRejectModalRequest] = useState<AdminRechargeRequestDto | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [actionLoading, setActionLoading] = useState(false);
  const [expandedWalletId, setExpandedWalletId] = useState<string | null>(null);
  const [expandedAmountKey, setExpandedAmountKey] = useState<string | null>(null);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      setError('');

      // Fetch requests and unmatched SMS logs in parallel
      const [reqData, smsData, walletData] = await Promise.all([
        walletService.getRechargeRequests(),
        walletService.getUnmatchedSms(),
        walletService.getWallets()
      ]);

      setRequests(reqData || []);
      setUnmatchedSms(smsData || []);
      setWallets(walletData || []);
    } catch (err: any) {
      console.error(err);
      setError('فشل في تحميل بيانات طلبات الشحن والتحويلات.');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!approveModalRequest) return;

    setActionLoading(true);
    try {
      const response = await walletService.resolveRechargeRequest(
        approveModalRequest.id,
        true,
        undefined,
        selectedSmsId || undefined,
        selectedWalletId || undefined
      );

      if (response.success) {
        toast.success('تمت الموافقة على طلب الشحن وتعبئة الرصيد للطالب.');
        setApproveModalRequest(null);
        setSelectedSmsId('');
        setSelectedWalletId('');
        fetchData();
      } else {
        toast.error(response.message || 'فشل في قبول الطلب.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'فشل في قبول الطلب.');
    } finally {
      setActionLoading(false);
    }
  };

  const openApproveModal = (rechargeRequest: AdminRechargeRequestDto) => {
    setApproveModalRequest(rechargeRequest);
    setSelectedWalletId(rechargeRequest.walletId);
    const match = unmatchedSms.find(log =>
      log.parsedAmount === rechargeRequest.amount
      && log.walletId === rechargeRequest.walletId);
    setSelectedSmsId(match?.id ?? '');
  };

  const handleReject = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rejectModalRequest) return;
    if (!rejectionReason.trim()) {
      toast.error('يرجى تحديد سبب الرفض.');
      return;
    }

    setActionLoading(true);
    try {
      const response = await walletService.resolveRechargeRequest(
        rejectModalRequest.id,
        false,
        rejectionReason.trim()
      );

      if (response.success) {
        toast.success('تم رفض طلب الشحن وتنبيه الطالب.');
        setRejectModalRequest(null);
        setRejectionReason('');
        fetchData();
      } else {
        toast.error(response.message || 'فشل في رفض الطلب.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'فشل في رفض الطلب.');
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (status: RechargeStatusValue) => {
    switch (normalizeRechargeStatus(status)) {
      case 0:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-amber-500/10 text-amber-600 dark:text-amber-500">
            <Clock className="h-3.5 w-3.5" /> معلق للمراجعة
          </span>
        );
      case 1:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-500/10 text-emerald-600 dark:text-emerald-500">
            <CheckCircle2 className="h-3.5 w-3.5" /> مطابق تلقائياً
          </span>
        );
      case 2:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-blue-500/10 text-blue-600 dark:text-blue-500">
            <Check className="h-3.5 w-3.5" /> مقبول يدوياً
          </span>
        );
      case 3:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-rose-500/10 text-rose-600 dark:text-rose-500">
            <X className="h-3.5 w-3.5" /> مرفوض
          </span>
        );
      case 4:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-gray-500/10 text-gray-500">
            <AlertCircle className="h-3.5 w-3.5" /> منتهي الصلاحية
          </span>
        );
      case 5:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-slate-500/10 px-2.5 py-1 text-xs font-bold text-slate-600">
            <X className="h-3.5 w-3.5" /> ملغي من الطالب
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-gray-500/10 text-gray-500">
            <HelpCircle className="h-3.5 w-3.5" /> غير معروف
          </span>
        );
    }
  };

  // Calculations for stats
  const pendingCount = requests.filter(r => isRechargeStatus(r.status, 0)).length;
  const totalPendingAmount = requests.filter(r => isRechargeStatus(r.status, 0)).reduce((acc, r) => acc + r.amount, 0);
  const unmatchedSmsCount = unmatchedSms.length;
  const unmatchedSmsByWallet = useMemo<UnmatchedSmsWalletGroup[]>(() => {
    const wallets = new Map<string, { id: string; label: string; phoneNumber: string; amounts: Map<string, UnmatchedSmsAmountGroup> }>();

    for (const sms of unmatchedSms) {
      const wallet = wallets.get(sms.walletId) ?? {
        id: sms.walletId,
        label: sms.walletLabel,
        phoneNumber: sms.walletPhoneNumber,
        amounts: new Map<string, UnmatchedSmsAmountGroup>(),
      };
      const amountKey = sms.parsedAmount === undefined ? 'unknown' : String(sms.parsedAmount);
      const amount = wallet.amounts.get(amountKey) ?? { key: amountKey, amount: sms.parsedAmount, items: [] };
      amount.items.push(sms);
      wallet.amounts.set(amountKey, amount);
      wallets.set(sms.walletId, wallet);
    }

    return [...wallets.values()]
      .map((wallet) => ({
        id: wallet.id,
        label: wallet.label,
        phoneNumber: wallet.phoneNumber,
        amountGroups: [...wallet.amounts.values()]
          .sort((left, right) => (right.amount ?? -1) - (left.amount ?? -1)),
      }))
      .sort((left, right) => left.label.localeCompare(right.label, 'ar'));
  }, [unmatchedSms]);

  // Filtered requests
  const filteredRequests = requests.filter(r => {
    const matchesStatus = statusFilter === 'all' || isRechargeStatus(r.status, statusFilter);
    const matchesSearch =
      r.studentName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      r.studentPhoneNumber.includes(searchQuery) ||
      r.senderPhoneNumber.includes(searchQuery) ||
      r.walletLabel.toLowerCase().includes(searchQuery.toLowerCase()) ||
      r.walletPhoneNumber.includes(searchQuery) ||
      r.amount.toString().includes(searchQuery);
    return matchesStatus && matchesSearch;
  });

  // Table columns definition
  const columns: AdminColumn<AdminRechargeRequestDto>[] = [
    {
      key: 'student',
      label: 'الطالب',
      render: (r) => (
        <div>
          <div className="font-bold text-[var(--admin-text)] text-sm">{r.studentName}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-0.5 font-mono">{r.studentPhoneNumber}</div>
        </div>
      )
    },
    {
      key: 'transferInfo',
      label: 'تفاصيل التحويل',
      render: (r) => (
        <div>
          <div className="font-mono font-bold text-sm text-[var(--admin-text)]">{r.amount} ج.م</div>
          <div className="text-xs text-[var(--admin-muted)] mt-0.5">الرصيد: <span className="font-semibold">{r.teacherName ? `للمدرس ${r.teacherName}` : 'عام'}</span></div>
        </div>
      )
    },
    {
      key: 'senderPhoneNumber',
      label: 'رقم المحول منه',
      render: (r) => (
        <span dir="ltr" className="font-mono text-sm font-bold text-[var(--admin-text)]">
          {r.senderPhoneNumber || 'غير مسجل'}
        </span>
      )
    },
    {
      key: 'wallet',
      label: 'المحفظة المستهدفة',
      render: (r) => (
        <div>
          <div className="font-bold text-[var(--admin-text)] text-xs">{r.walletLabel}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-0.5 font-mono">{r.walletPhoneNumber}</div>
        </div>
      )
    },
    {
      key: 'screenshot',
      label: 'صورة المعاملة',
      render: (r) => (
        <div className="flex items-center justify-center">
          {resolveAssetUrl(r.screenshotUrl) ? (
            <button
              type="button"
              className="group relative cursor-pointer rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
              onClick={() => setViewScreenshotUrl(resolveAssetUrl(r.screenshotUrl))}
              aria-label={`فتح صورة إثبات معاملة ${r.studentName}`}
            >
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={resolveAssetUrl(r.screenshotUrl) || undefined}
                alt=""
                className="h-10 w-16 object-cover rounded-lg border border-[var(--admin-border)] hover:opacity-85 transition-opacity"
              />
              <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center rounded-lg transition-opacity">
                <Maximize2 className="h-3 w-3 text-white" />
              </div>
            </button>
          ) : (
            <span className="text-xs text-[var(--admin-muted)] italic">لا توجد صورة</span>
          )}
        </div>
      )
    },
    {
      key: 'date',
      label: 'تاريخ الطلب',
      render: (r) => (
        <div className="flex flex-col">
          <span className="text-xs text-[var(--admin-text)]">{formatRelativeDate(r.createdAt)}</span>
          <span className="text-[10px] text-[var(--admin-muted)] font-mono mt-0.5">{formatDate(r.createdAt, { timeStyle: 'short', dateStyle: 'short' })}</span>
        </div>
      )
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (r) => getStatusBadge(r.status)
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (r) => {
        const isPending = isRechargeStatus(r.status, 0);
        const isRejected = isRechargeStatus(r.status, 3);
        const isManualApproval = isRechargeStatus(r.status, 2) && !r.matchedSmsLogId;
        if (!isPending && !isRejected && !isManualApproval) {
          if (isRechargeStatus(r.status, 5)) {
            return <div className="max-w-48 text-right text-xs font-bold text-rose-600">سبب الإلغاء: {r.rejectionReason || 'غير مسجل'}</div>;
          }
          if (r.resolvedAt) {
            return (
              <div className="text-right text-[10px] text-[var(--admin-muted)]">
                بواسطة: {r.resolvedByUserName || 'النظام'}
                <div className="font-mono mt-0.5">{formatDate(r.resolvedAt, { timeStyle: 'short' })}</div>
              </div>
            );
          }
          return null;
        }
        if (isManualApproval) {
          return <NeumorphButton type="button" onClick={() => openApproveModal(r)} intent="ghost" size="sm">
            <WalletCards className="h-3.5 w-3.5" /> تعديل المحفظة
          </NeumorphButton>;
        }
        return (
          <div className="flex items-center gap-2">
            <NeumorphButton
              type="button"
              onClick={() => openApproveModal(r)}
              intent="primary"
              size="sm"
            >
              <Check className="h-3.5 w-3.5" /> {isRejected ? 'تعديل وقبول' : 'قبول'}
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => {
                setRejectModalRequest(r);
                setRejectionReason(r.rejectionReason ?? '');
              }}
              intent="danger"
              size="sm"
            >
              <X className="h-3.5 w-3.5" /> {isRejected ? 'تعديل سبب الرفض' : 'رفض'}
            </NeumorphButton>
          </div>
        );
      }
    }
  ];

  return (
    <>
      <div className="flex flex-col gap-6">
        {/* Stats */}
        {loading && requests.length === 0 ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {[1, 2, 3].map(i => (
              <div key={i} className="h-28 animate-pulse rounded-2xl bg-[var(--admin-card)] border border-[var(--admin-border)]" />
            ))}
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <AdminStatCard
              variant="light"
              icon={Clock}
              label="طلبات بانتظار المراجعة"
              value={pendingCount}
              subtitle="بحاجة لتأكيد ومراجعة من المشرفين"
            />
            <AdminStatCard
              variant="accent"
              icon={FileText}
              label="إجمالي المبالغ المعلقة"
              value={`${totalPendingAmount.toLocaleString('en-US')} ج.م`}
              subtitle="القيمة المالية لطلبات الشحن المعلقة"
            />
            <AdminStatCard
              variant="light"
              icon={Smartphone}
              label="رسائل غير مطابقة (SMS)"
              value={unmatchedSmsCount}
              subtitle="رسائل إيداع مستلمة لم يتم ربطها آلياً"
            />
          </div>
        )}

        {/* Filters & Actions bar */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          {/* Status Tabs */}
          <div className="flex flex-wrap gap-1 bg-[var(--admin-card-strong)] border border-[var(--admin-border)] p-1 rounded-xl">
            <button
              onClick={() => setStatusFilter(0)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 0
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              المعلقة ({pendingCount})
            </button>
            <button
              onClick={() => setStatusFilter(1)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 1
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              المطابقة آلياً
            </button>
            <button
              onClick={() => setStatusFilter(2)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 2
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              المقبولة يدوياً
            </button>
            <button
              onClick={() => setStatusFilter(3)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 3
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              المرفوضة
            </button>
            <button
              onClick={() => setStatusFilter(4)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 4
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              المنتهية الصلاحية
            </button>
            <button
              onClick={() => setStatusFilter(5)}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 5
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              الملغاة
            </button>
            <button
              onClick={() => setStatusFilter('all')}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                statusFilter === 'all'
                  ? 'bg-[var(--admin-primary)] text-white shadow'
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              الكل
            </button>
          </div>

          {/* Search bar */}
          <div className="relative w-full sm:w-72">
            <span className="absolute inset-y-0 start-0 flex items-center ps-3 text-[var(--admin-muted)] pointer-events-none">
              <Search className="h-4 w-4" />
            </span>
            <input
              type="text"
              placeholder="بحث بالطالب أو رقم المحول أو القيمة..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="admin-input ps-9 py-1.5 text-xs w-full"
            />
          </div>
        </div>

        {/* Main Content Area - Split layout */}
        <div className="grid min-h-0 gap-6 lg:grid-cols-3 lg:items-start">
          {/* Requests Table */}
          <div className="min-w-0 lg:col-span-2 admin-panel rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6 shadow-[0_4px_20px_var(--admin-shadow)]">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-black text-[var(--admin-text)]">قائمة طلبات الشحن</h2>
              <NeumorphButton type="button" onClick={fetchData} intent="ghost" size="sm">
                تحديث
              </NeumorphButton>
            </div>

            <AdminDataTable
              data={filteredRequests}
              columns={columns}
              loading={loading}
              rowKey={(item) => item.id}
              emptyMessage="لا توجد طلبات شحن مطابقة للتصفية الحالية."
              errorMessage={error}
              onRetry={fetchData}
            />
          </div>

          {/* Unmatched SMS Panel */}
          <div className="admin-panel flex min-h-0 min-w-0 flex-col overflow-visible rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-[0_4px_20px_var(--admin-shadow)] sm:p-6 lg:h-[clamp(28rem,calc(100dvh-18rem),700px)] lg:overflow-hidden">
            <h2 className="mb-2 flex shrink-0 items-center gap-2 text-lg font-black text-[var(--admin-text)]">
              <Smartphone className="h-5 w-5 text-[var(--admin-primary)]" />
              الرسائل غير المطابقة ({unmatchedSmsCount})
            </h2>
            <p className="mb-4 shrink-0 text-xs leading-relaxed text-[var(--admin-muted)]">
              رسائل تأكيد الإيداع المستلمة من Vodafone Cash ولم يتم ربطها بأي طلب للطالب تلقائياً.
            </p>

            <div
              role="region"
              aria-label="الرسائل غير المطابقة"
              tabIndex={0}
              className="flex min-h-0 flex-none touch-pan-y flex-col gap-2 overflow-visible pr-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] lg:flex-1 lg:overflow-y-auto lg:overscroll-contain lg:[scrollbar-color:var(--admin-border)_transparent] lg:[scrollbar-gutter:stable] lg:[scrollbar-width:thin] lg:[-webkit-overflow-scrolling:touch]"
            >
              {loading ? (
                [1, 2, 3].map(i => (
                  <div key={i} className="h-20 animate-pulse bg-[var(--admin-card-strong)] rounded-xl border border-[var(--admin-border)]" />
                ))
              ) : unmatchedSms.length === 0 ? (
                <div className="flex flex-col items-center justify-center text-center p-8 border border-dashed border-[var(--admin-border)] rounded-xl bg-[var(--admin-card-strong)]">
                  <CheckCircle2 className="h-8 w-8 text-emerald-500 mb-2" />
                  <span className="text-xs font-bold text-[var(--admin-text)]">كل الرسائل مطابقة!</span>
                  <span className="text-[10px] text-[var(--admin-muted)] mt-1">لا توجد رسائل معلقة في النظام.</span>
                </div>
              ) : unmatchedSmsByWallet.map((wallet) => {
                const isWalletOpen = expandedWalletId === wallet.id;
                return <div key={wallet.id} className="overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)]">
                  <button
                    type="button"
                    aria-expanded={isWalletOpen}
                    onClick={() => {
                      setExpandedWalletId(isWalletOpen ? null : wallet.id);
                      setExpandedAmountKey(null);
                    }}
                    className="flex min-h-12 w-full items-center justify-between gap-3 px-3.5 text-right hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]"
                  >
                    <span className="flex min-w-0 items-center gap-2"><WalletCards className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" /><span className="min-w-0"><span className="block truncate text-sm font-black text-[var(--admin-text)]">{wallet.label}</span><span className="block font-mono text-[10px] text-[var(--admin-muted)]" dir="ltr">{wallet.phoneNumber}</span></span></span>
                    <span className="flex shrink-0 items-center gap-2"><span className="rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-xs font-black text-[var(--admin-primary)]">{wallet.amountGroups.reduce((sum, group) => sum + group.items.length, 0)}</span><ChevronDown className={`h-4 w-4 text-[var(--admin-muted)] transition-transform ${isWalletOpen ? 'rotate-180' : ''}`} /></span>
                  </button>
                  {isWalletOpen && <div className="border-t border-[var(--admin-border)] p-2">
                    {wallet.amountGroups.map((group) => {
                      const amountKey = `${wallet.id}:${group.key}`;
                      const isAmountOpen = expandedAmountKey === amountKey;
                      return <div key={amountKey} className="mb-2 last:mb-0 overflow-hidden rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)]">
                        <button
                          type="button"
                          aria-expanded={isAmountOpen}
                          onClick={() => setExpandedAmountKey(isAmountOpen ? null : amountKey)}
                          className="flex min-h-11 w-full items-center justify-between gap-3 px-3 text-right hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]"
                        >
                          <span className="font-mono text-sm font-black text-[var(--admin-text)]">{group.amount === undefined ? 'مبلغ غير معروف' : `${group.amount} ج.م`}</span>
                          <span className="flex items-center gap-2"><span className="text-xs font-bold text-[var(--admin-muted)]">{group.items.length} رسالة</span><ChevronDown className={`h-4 w-4 text-[var(--admin-muted)] transition-transform ${isAmountOpen ? 'rotate-180' : ''}`} /></span>
                        </button>
                        {isAmountOpen && <div className="space-y-2 border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2">
                          {group.items.map((sms) => <article key={sms.id} className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                            <div className="flex items-center justify-between gap-3"><span className="text-[11px] font-semibold text-[var(--admin-text)]">من: <span className="font-mono">{sms.parsedSenderPhone || sms.sender}</span></span><span className="shrink-0 font-mono text-[10px] text-[var(--admin-muted)]">{formatRelativeDate(sms.receivedAt)}</span></div>
                            <p className="mt-2 whitespace-pre-wrap break-words rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 font-mono text-[11px] leading-relaxed text-[var(--admin-text)]">{sms.body}</p>
                            <button type="button" onClick={() => { navigator.clipboard.writeText(sms.body); toast.success('تم نسخ الرسالة'); }} className="mt-2 inline-flex min-h-8 items-center gap-1 text-xs font-bold text-[var(--admin-primary)] hover:underline"><MessageSquareText className="h-3.5 w-3.5" />نسخ الرسالة</button>
                          </article>)}
                        </div>}
                      </div>;
                    })}
                  </div>}
                </div>;
              })}
            </div>
          </div>
        </div>
      </div>

      {/* Screenshot Viewer Modal */}
      <AdminModal
        open={!!viewScreenshotUrl}
        onClose={() => setViewScreenshotUrl(null)}
        title="معاينة إثبات التحويل"
        maxWidth="max-w-3xl"
      >
        <div className="mt-4 flex items-center justify-center bg-black/5 rounded-xl p-2 border border-[var(--admin-border)] max-h-[80vh] overflow-auto">
          {viewScreenshotUrl && (
            /* eslint-disable-next-line @next/next/no-img-element */
            <img
              src={viewScreenshotUrl}
              alt="proof detail"
              className="max-w-full h-auto rounded-lg shadow-lg"
            />
          )}
        </div>
        <div className="mt-4 flex justify-end">
          <NeumorphButton type="button" onClick={() => setViewScreenshotUrl(null)} intent="ghost">
            إغلاق
          </NeumorphButton>
        </div>
      </AdminModal>

      {/* Approve and Match Modal */}
      <AdminModal
        open={!!approveModalRequest}
        onClose={() => {
          setApproveModalRequest(null);
          setSelectedSmsId('');
          setSelectedWalletId('');
        }}
        title={approveModalRequest && isRechargeStatus(approveModalRequest.status, 2) ? 'تصحيح محفظة التحويل' : 'قبول طلب الشحن يدوياً'}
        subtitle="اختر المحفظة التي استقبلت التحويل فعلياً، ويمكن ربط رسالة التأكيد إن وجدت."
      >
        {approveModalRequest && (
          <form onSubmit={handleApprove} className="mt-4 flex flex-col gap-4">
            <div className="p-4 rounded-xl bg-[var(--admin-card-strong)] border border-[var(--admin-border)] flex flex-col gap-2">
              <div className="flex justify-between text-xs">
                <span className="text-[var(--admin-muted)]">اسم الطالب:</span>
                <span className="font-bold text-[var(--admin-text)]">{approveModalRequest.studentName}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-[var(--admin-muted)]">رقم الطالب:</span>
                <span className="font-mono font-bold text-[var(--admin-text)]">{approveModalRequest.studentPhoneNumber}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-[var(--admin-muted)]">المبلغ المطلوب شحنه:</span>
                <span className="font-mono font-black text-sm text-[var(--admin-primary)]">{approveModalRequest.amount} ج.م</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-[var(--admin-muted)]">رقم المحوّل منه:</span>
                <span className="font-mono font-bold text-[var(--admin-text)]">{approveModalRequest.senderPhoneNumber}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-[var(--admin-muted)]">المحفظة المختارة:</span>
                <span className="font-bold text-[var(--admin-text)]">{approveModalRequest.walletLabel} ({approveModalRequest.walletPhoneNumber})</span>
              </div>
            </div>

            {resolveAssetUrl(approveModalRequest.screenshotUrl) && (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-bold text-[var(--admin-text)]">صورة التحويل المرفقة:</label>
                <div className="relative group max-h-48 overflow-hidden rounded-xl border border-[var(--admin-border)] flex justify-center bg-black/5">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={resolveAssetUrl(approveModalRequest.screenshotUrl) || undefined}
                    alt="proof preview"
                    className="max-h-48 object-contain"
                  />
                  <button
                    type="button"
                    onClick={() => setViewScreenshotUrl(resolveAssetUrl(approveModalRequest.screenshotUrl))}
                    className="absolute bottom-2 right-2 p-2 rounded-lg bg-black/60 text-white hover:bg-black/80 transition-colors"
                  >
                    <Maximize2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                <WalletCards className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                المحفظة التي استقبلت التحويل
              </label>
              <select
                required
                value={selectedWalletId}
                disabled={Boolean(selectedSmsId)}
                onChange={(event) => setSelectedWalletId(event.target.value)}
                className="admin-input text-xs disabled:opacity-70"
              >
                <option value="">-- اختر المحفظة --</option>
                {wallets.filter(wallet => wallet.isActive).map(wallet => (
                  <option key={wallet.id} value={wallet.id}>
                    {wallet.label} — {wallet.phoneNumber}
                  </option>
                ))}
              </select>
              {selectedSmsId ? <span className="text-[10px] text-[var(--admin-muted)]">تم تحديد المحفظة تلقائياً من رسالة SMS المختارة.</span> : null}
            </div>

            {/* Match with SMS selector */}
            {!isRechargeStatus(approveModalRequest.status, 2) && <div className="flex flex-col gap-1.5">
              <label className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                <LinkIcon className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                ربط رسالة SMS تأكيدية (اختياري)
              </label>

              <select
                value={selectedSmsId}
                onChange={(event) => {
                  const smsId = event.target.value;
                  setSelectedSmsId(smsId);
                  const sms = unmatchedSms.find(item => item.id === smsId);
                  if (sms) setSelectedWalletId(sms.walletId);
                }}
                className="admin-input text-xs"
              >
                <option value="">-- موافقة مباشرة بدون ربط رسالة SMS --</option>
                {unmatchedSms.map(sms => {
                  const isAmountMatch = sms.parsedAmount === approveModalRequest.amount;
                  const isPhoneMatch = sms.parsedSenderPhone === approveModalRequest.senderPhoneNumber;

                  let badge = '';
                  if (isAmountMatch && isPhoneMatch) badge = ' (مطابقة للمبلغ والهاتف ★)';
                  else if (isAmountMatch) badge = ' (مطابقة للمبلغ)';
                  else if (isPhoneMatch) badge = ' (مطابقة للهاتف)';

                  return (
                    <option key={sms.id} value={sms.id}>
                      {sms.parsedAmount ? `${sms.parsedAmount} ج.م` : 'مبلغ غير معروف'} - {sms.parsedSenderPhone || 'بدون هاتف'} [{sms.walletLabel}]{badge}
                    </option>
                  );
                })}
              </select>
              <span className="text-[10px] text-[var(--admin-muted)]">
                سيؤدي اختيار رسالة إلى تمييزها كرسالة مطابقة ولن تظهر في قائمة الرسائل غير المطابقة.
              </span>
            </div>}

            <div className="mt-4 flex items-center justify-end gap-3">
              <NeumorphButton
                type="button"
                intent="ghost"
                onClick={() => {
                  setApproveModalRequest(null);
                  setSelectedSmsId('');
                  setSelectedWalletId('');
                }}
                disabled={actionLoading}
              >
                إلغاء
              </NeumorphButton>
              <NeumorphButton
                type="submit"
                intent="primary"
                loading={actionLoading}
              >
                {isRechargeStatus(approveModalRequest.status, 2) ? 'حفظ تصحيح المحفظة' : 'تأكيد الموافقة وتعبئة الرصيد'}
              </NeumorphButton>
            </div>
          </form>
        )}
      </AdminModal>

      {/* Reject Modal */}
      <AdminModal
        open={!!rejectModalRequest}
        onClose={() => {
          setRejectModalRequest(null);
          setRejectionReason('');
        }}
        title="رفض طلب الشحن"
        subtitle="تحديد سبب رفض طلب الشحن ليظهر للطالب في لوحة التحكم الخاصة به."
      >
        {rejectModalRequest && (
          <form onSubmit={handleReject} className="mt-4 flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-bold text-[var(--admin-text)]">اسم الطالب:</label>
              <span className="text-sm font-semibold text-[var(--admin-text)]">{rejectModalRequest.studentName}</span>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-bold text-[var(--admin-text)]">المبلغ:</label>
              <span className="text-sm font-mono font-bold text-rose-500">{rejectModalRequest.amount} ج.م</span>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-bold text-[var(--admin-text)]">سبب الرفض *</label>
              <textarea
                required
                rows={3}
                placeholder="مثال: صورة إثبات المعاملة غير واضحة، يرجى إعادة الرفع برقم مرجعي صحيح."
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
                className="admin-input text-xs resize-none"
              />
            </div>

            <div className="mt-4 flex items-center justify-end gap-3">
              <NeumorphButton
                type="button"
                intent="ghost"
                onClick={() => {
                  setRejectModalRequest(null);
                  setRejectionReason('');
                }}
                disabled={actionLoading}
              >
                إلغاء
              </NeumorphButton>
              <NeumorphButton
                type="submit"
                intent="danger"
                loading={actionLoading}
              >
                تأكيد الرفض
              </NeumorphButton>
            </div>
          </form>
        )}
      </AdminModal>
    </>
  );
}

export default function RechargeVerificationPageClient() {
  return (
    <AdminPage
      activePath="/admin/recharge-verification"
      sectionLabel="المالية والمدفوعات"
      pageTitle="مراجعة وتأكيد طلبات الشحن"
      subtitle="مراجعة طلبات الشحن المرفقة بصور التحويل ومطابقتها يدوياً برسائل التأكيد غير المطابقة."
    >
      <RechargeVerificationWorkspace />
    </AdminPage>
  );
}
