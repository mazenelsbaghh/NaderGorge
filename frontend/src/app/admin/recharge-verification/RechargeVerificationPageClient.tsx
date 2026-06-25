'use client';

import { useState, useEffect } from 'react';
import { 
  Check, 
  X, 
  Image as ImageIcon, 
  FileText, 
  Smartphone, 
  Search, 
  AlertCircle, 
  CheckCircle2, 
  Clock, 
  HelpCircle,
  Link as LinkIcon,
  Maximize2
} from 'lucide-react';
import { 
  AdminShellChrome, 
  AdminDataTable, 
  AdminColumn,
  AdminStatCard,
  AdminModal
} from '@/components/admin';
import { formatRelativeDate, formatDate } from '@/components/admin/admin-utils';
import NeumorphButton from '@/components/ui/neumorph-button';
import { walletService, type AdminRechargeRequestDto, type AdminIncomingSmsLogDto } from '@/services/wallet-service';
import toast from 'react-hot-toast';

export default function RechargeVerificationPageClient() {
  const [requests, setRequests] = useState<AdminRechargeRequestDto[]>([]);
  const [unmatchedSms, setUnmatchedSms] = useState<AdminIncomingSmsLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  
  // Filters
  const [statusFilter, setStatusFilter] = useState<number | 'all'>(0); // Default to Pending (0)
  const [searchQuery, setSearchQuery] = useState('');

  // Modals
  const [viewScreenshotUrl, setViewScreenshotUrl] = useState<string | null>(null);
  const [approveModalRequest, setApproveModalRequest] = useState<AdminRechargeRequestDto | null>(null);
  const [selectedSmsId, setSelectedSmsId] = useState<string>('');
  const [rejectModalRequest, setRejectModalRequest] = useState<AdminRechargeRequestDto | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [actionLoading, setActionLoading] = useState(false);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      setError('');
      
      // Fetch requests and unmatched SMS logs in parallel
      const [reqData, smsData] = await Promise.all([
        walletService.getRechargeRequests(),
        walletService.getUnmatchedSms()
      ]);

      setRequests(reqData || []);
      setUnmatchedSms(smsData || []);
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
        selectedSmsId || undefined
      );

      if (response.success) {
        toast.success('تمت الموافقة على طلب الشحن وتعبئة الرصيد للطالب.');
        setApproveModalRequest(null);
        setSelectedSmsId('');
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

  const getStatusBadge = (status: number) => {
    switch (status) {
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
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-gray-500/10 text-gray-500">
            <HelpCircle className="h-3.5 w-3.5" /> غير معروف
          </span>
        );
    }
  };

  // Calculations for stats
  const pendingCount = requests.filter(r => r.status === 0).length;
  const totalPendingAmount = requests.filter(r => r.status === 0).reduce((acc, r) => acc + r.amount, 0);
  const unmatchedSmsCount = unmatchedSms.length;

  // Filtered requests
  const filteredRequests = requests.filter(r => {
    const matchesStatus = statusFilter === 'all' || r.status === statusFilter;
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
          <div className="text-xs text-[var(--admin-muted)] mt-0.5">من: <span className="font-mono font-semibold">{r.senderPhoneNumber}</span></div>
        </div>
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
          {r.screenshotUrl ? (
            <div className="relative group cursor-pointer" onClick={() => setViewScreenshotUrl(r.screenshotUrl!)}>
              <img 
                src={r.screenshotUrl} 
                alt="proof" 
                className="h-10 w-16 object-cover rounded-lg border border-[var(--admin-border)] hover:opacity-85 transition-opacity" 
              />
              <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center rounded-lg transition-opacity">
                <Maximize2 className="h-3 w-3 text-white" />
              </div>
            </div>
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
        if (r.status !== 0) {
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
        return (
          <div className="flex items-center gap-2">
            <NeumorphButton
              type="button"
              onClick={() => {
                setApproveModalRequest(r);
                // Look for an unmatched SMS log that has exactly the same amount to auto-select
                const match = unmatchedSms.find(log => log.parsedAmount === r.amount);
                if (match) setSelectedSmsId(match.id);
              }}
              intent="primary"
              size="sm"
            >
              <Check className="h-3.5 w-3.5" /> قبول
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => setRejectModalRequest(r)}
              intent="danger"
              size="sm"
            >
              <X className="h-3.5 w-3.5" /> رفض
            </NeumorphButton>
          </div>
        );
      }
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/recharge-verification"
      sectionLabel="المالية والمدفوعات"
      pageTitle="مراجعة وتأكيد طلبات الشحن"
      subtitle="مراجعة طلبات الشحن المرفقة بصور التحويل ومطابقتها يدوياً برسائل التأكيد غير المطابقة."
    >
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
              placeholder="بحث باسم الطالب، الهاتف، القيمة..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="admin-input ps-9 py-1.5 text-xs w-full"
            />
          </div>
        </div>

        {/* Main Content Area - Split layout */}
        <div className="grid gap-6 lg:grid-cols-3">
          {/* Requests Table */}
          <div className="lg:col-span-2 admin-panel rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6 shadow-[0_4px_20px_var(--admin-shadow)]">
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
          <div className="admin-panel rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6 shadow-[0_4px_20px_var(--admin-shadow)] flex flex-col max-h-[700px]">
            <h2 className="text-lg font-black text-[var(--admin-text)] mb-2 flex items-center gap-2">
              <Smartphone className="h-5 w-5 text-[var(--admin-primary)]" />
              الرسائل غير المطابقة ({unmatchedSmsCount})
            </h2>
            <p className="text-xs text-[var(--admin-muted)] leading-relaxed mb-4">
              رسائل تأكيد الإيداع المستلمة من Vodafone Cash ولم يتم ربطها بأي طلب للطالب تلقائياً.
            </p>

            <div className="flex-1 overflow-y-auto flex flex-col gap-3 pr-1">
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
              ) : (
                unmatchedSms.map((sms) => (
                  <div 
                    key={sms.id}
                    className="p-3.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] hover:border-[var(--admin-primary-15)] transition-all flex flex-col gap-1.5"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-black text-[var(--admin-text)] font-mono">
                        {sms.parsedAmount ? `${sms.parsedAmount} ج.م` : 'غير معروف'}
                      </span>
                      <span className="text-[10px] text-[var(--admin-muted)] font-mono">
                        {formatRelativeDate(sms.receivedAt)}
                      </span>
                    </div>

                    {sms.parsedSenderPhone && (
                      <div className="text-[11px] text-[var(--admin-text)] font-semibold">
                        من: <span className="font-mono">{sms.parsedSenderPhone}</span>
                      </div>
                    )}

                    <div className="text-[10px] bg-[var(--admin-card)] text-[var(--admin-text)] p-2 rounded border border-[var(--admin-border)] font-mono whitespace-pre-wrap leading-relaxed max-h-16 overflow-y-auto">
                      {sms.body}
                    </div>

                    <div className="text-[9px] text-[var(--admin-muted)] flex items-center justify-between mt-1">
                      <span>محفظة: {sms.walletLabel}</span>
                      <button 
                        type="button" 
                        onClick={() => {
                          navigator.clipboard.writeText(sms.body);
                          toast.success('تم نسخ الرسالة');
                        }}
                        className="text-[var(--admin-primary)] hover:underline"
                      >
                        نسخ الرسالة
                      </button>
                    </div>
                  </div>
                ))
              )}
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
        }}
        title="قبول طلب الشحن يدوياً"
        subtitle="تأكيد تحويل المبلغ وتعبئة رصيد الطالب مع إمكانية ربطه برسالة تأكيد المعاملة."
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

            {approveModalRequest.screenshotUrl && (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-bold text-[var(--admin-text)]">صورة التحويل المرفقة:</label>
                <div className="relative group max-h-48 overflow-hidden rounded-xl border border-[var(--admin-border)] flex justify-center bg-black/5">
                  <img 
                    src={approveModalRequest.screenshotUrl} 
                    alt="proof preview" 
                    className="max-h-48 object-contain" 
                  />
                  <button
                    type="button"
                    onClick={() => setViewScreenshotUrl(approveModalRequest.screenshotUrl!)}
                    className="absolute bottom-2 right-2 p-2 rounded-lg bg-black/60 text-white hover:bg-black/80 transition-colors"
                  >
                    <Maximize2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            )}

            {/* Match with SMS selector */}
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                <LinkIcon className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                ربط رسالة SMS تأكيدية (اختياري)
              </label>
              
              <select
                value={selectedSmsId}
                onChange={(e) => setSelectedSmsId(e.target.value)}
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
            </div>

            <div className="mt-4 flex items-center justify-end gap-3">
              <NeumorphButton
                type="button"
                intent="ghost"
                onClick={() => {
                  setApproveModalRequest(null);
                  setSelectedSmsId('');
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
                تأكيد الموافقة وتعبئة الرصيد
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
    </AdminShellChrome>
  );
}
