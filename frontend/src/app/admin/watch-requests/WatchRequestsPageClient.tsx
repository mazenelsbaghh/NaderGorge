'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { adminService, type AdminWatchRequestDto } from '@/services/admin-service';
import { Check, X, Clock, AlertCircle } from 'lucide-react';
import { formatRelativeDate } from '@/components/admin/admin-utils';
import { 
  AdminShellChrome, 
  AdminDataTable, 
  AdminColumn,
  AdminPageSkeleton,
  AdminStatCard,
  AdminModal
} from '@/components/admin';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export default function WatchRequestsPageClient() {
  const [requests, setRequests] = useState<AdminWatchRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState('');

  // Custom modal states
  const [activeModal, setActiveModal] = useState<'approve' | 'reject' | null>(null);
  const [selectedRequest, setSelectedRequest] = useState<AdminWatchRequestDto | null>(null);
  const [reasonText, setReasonText] = useState('');
  const [validationError, setValidationError] = useState('');

  useEffect(() => {
    fetchRequests();
  }, []);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      setError('');
      const response = await adminService.getWatchRequests();
      setRequests(response.data || []);
    } catch (err: any) {
      devConsole.error(err);
      setError('فشل في تحميل الطلبات.');
    } finally {
      setLoading(false);
    }
  };

  const handleApproveClick = (req: AdminWatchRequestDto) => {
    setSelectedRequest(req);
    setReasonText('تمت الموافقة بواسطة الإدارة');
    setValidationError('');
    setActiveModal('approve');
  };

  const handleRejectClick = (req: AdminWatchRequestDto) => {
    setSelectedRequest(req);
    setReasonText('');
    setValidationError('');
    setActiveModal('reject');
  };

  const handleApproveSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    setActionLoading(selectedRequest.id);
    setActiveModal(null);
    try {
      await adminService.approveWatchRequest(selectedRequest.id, reasonText.trim());
      await fetchRequests();
      toast.success('تم قبول طلب المشاهدة الإضافية.');
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في الموافقة على الطلب');
    } finally {
      setActionLoading(null);
      setSelectedRequest(null);
      setReasonText('');
    }
  };

  const handleRejectSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    if (!reasonText.trim()) {
      setValidationError('سبب الرفض إجباري.');
      return;
    }

    setActionLoading(selectedRequest.id);
    setActiveModal(null);
    try {
      await adminService.rejectWatchRequest(selectedRequest.id, reasonText.trim());
      await fetchRequests();
      toast.success('تم رفض طلب المشاهدة الإضافية.');
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في رفض الطلب');
    } finally {
      setActionLoading(null);
      setSelectedRequest(null);
      setReasonText('');
    }
  };

  const pendingCount = requests.filter(r => r.status === 0).length;
  const approvedCount = requests.filter(r => r.status === 1).length;
  const rejectedCount = requests.filter(r => r.status === 2).length;

  const columns: AdminColumn<AdminWatchRequestDto>[] = [
    {
      key: 'student',
      label: 'الطالب',
      render: (req) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">{req.studentName}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">{req.studentPhone}</div>
        </div>
      )
    },
    {
      key: 'video',
      label: 'الفيديو',
      render: (req) => (
        <div className="text-sm font-bold text-[var(--admin-text)] max-w-sm truncate whitespace-normal leading-relaxed">
          {req.videoTitle}
        </div>
      )
    },
    {
      key: 'watchCount',
      label: 'المشاهدات الحالية',
      render: (req) => {
        const isUnlimited = req.maxWatchCount === 0;
        return (
          <div className="flex flex-col items-start gap-1">
            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold ${
              req.reachedLimit 
                ? 'bg-rose-500/10 text-rose-600 dark:text-rose-500' 
                : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-500'
            }`}>
              {req.currentWatchCount} / {isUnlimited ? '∞' : req.maxWatchCount}
            </span>
            {req.reachedLimit && (
              <span className="text-[10px] text-rose-500 font-bold">وصل للحد الأقصى</span>
            )}
          </div>
        );
      }
    },
    {
      key: 'date',
      label: 'التاريخ',
      render: (req) => (
        <span className="text-sm text-[var(--admin-muted)] font-medium">
          {formatRelativeDate(req.createdAt)}
        </span>
      )
    },

    {
      key: 'status',
      label: 'الحالة',
      render: (req) => {
        if (req.status === 0) {
          return (
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-yellow-500/10 text-yellow-600 dark:text-yellow-500">
              <Clock className="w-3.5 h-3.5 ml-1.5" /> قيد المراجعة
            </span>
          );
        }
        if (req.status === 1) {
          return (
            <div className="flex flex-col items-start gap-1">
              <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-500/10 text-emerald-600 dark:text-emerald-500">
                <Check className="w-3.5 h-3.5 ml-1.5" /> تمت الموافقة
              </span>
              {req.reason && (
                <span className="text-xs font-semibold text-[var(--admin-muted)] max-w-[150px] truncate block leading-normal" title={req.reason}>
                  السبب: {req.reason}
                </span>
              )}
            </div>
          );
        }
        return (
          <div className="flex flex-col items-start gap-1">
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-red-500/10 text-red-600 dark:text-red-500">
              <X className="w-3.5 h-3.5 ml-1.5" /> مرفوض
            </span>
            {req.reason && (
              <span className="text-xs font-semibold text-rose-500 dark:text-rose-400 max-w-[150px] truncate block leading-normal" title={req.reason}>
                السبب: {req.reason}
              </span>
            )}
          </div>
        );
      }
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (req) => (
        req.status === 0 ? (
          <div className="flex items-center justify-end gap-2">
            <NeumorphButton
              type="button"
              onClick={() => handleApproveClick(req)}
              disabled={actionLoading !== null}
              intent="primary"
              size="sm"
            >
              {actionLoading === req.id ? (
                <span className="w-4 h-4 block border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></span>
              ) : (
                <Check className="w-4 h-4 ml-1" />
              )}
              موافقة
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => handleRejectClick(req)}
              disabled={actionLoading !== null}
              intent="danger"
              size="sm"
            >
              {actionLoading === req.id ? (
                <span className="w-4 h-4 block border-2 border-red-500 border-t-transparent rounded-full animate-spin"></span>
              ) : (
                <X className="w-4 h-4 ml-1" />
              )}
              رفض
            </NeumorphButton>
          </div>
        ) : (
          <span className="text-[var(--admin-muted)] text-sm text-left w-full block ml-8 opacity-50">-</span>
        )
      )
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/watch-requests"
      sectionLabel="المحتوى الأكاديمي"
      pageTitle="طلبات المشاهدة الإضافية"
      subtitle="مراجعة ومعالجة طلبات الطلاب لزيادة مرات مشاهدة الفيديوهات المقفلة."
    >
      {error && (
        <div className="mb-6 bg-red-500/10 border border-red-500 text-red-500 p-4 rounded-xl flex items-center shadow-sm">
          <AlertCircle className="w-5 h-5 ml-2" />
          <span className="font-bold">{error}</span>
        </div>
      )}

      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <>
          <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
            <AdminStatCard
              variant="accent"
              icon={Clock}
              label="طلبات جديدة"
              value={pendingCount}
              subtitle="لم يتم الرد عليها بعد"
            />
            <AdminStatCard
              variant="light"
              icon={Check}
              label="تمت الموافقة"
              value={approvedCount}
              subtitle="الطلبات المقبولة"
            />
            <AdminStatCard
              variant="muted"
              icon={X}
              label="الطلبات المرفوضة"
              value={rejectedCount}
              subtitle="تم رفضها لعدم الاستحقاق"
            />
          </section>

          <AdminDataTable
            data={requests}
            columns={columns}
            loading={loading}
            rowKey={(r) => r.id}
            emptyMessage="لا توجد طلبات مشاهدة إضافية حالياً."
          />
        </>
      )}

      <AdminModal
        open={activeModal !== null}
        onClose={() => {
          if (actionLoading) return;
          setActiveModal(null);
          setSelectedRequest(null);
          setReasonText('');
          setValidationError('');
        }}
        title={activeModal === 'approve' ? 'موافقة على طلب المشاهدة' : 'رفض طلب المشاهدة'}
      >
        <form onSubmit={activeModal === 'approve' ? handleApproveSubmit : handleRejectSubmit} className="space-y-5 text-right">
          <div>
            <p className="text-sm text-[var(--admin-muted)] mb-3 leading-relaxed">
              {activeModal === 'approve' 
                ? `هل أنت متأكد من الموافقة على طلب الطالب ${selectedRequest?.studentName} لمشاهدة فيديو "${selectedRequest?.videoTitle}"؟`
                : `برجاء كتابة سبب رفض طلب الطالب ${selectedRequest?.studentName} لمشاهدة فيديو "${selectedRequest?.videoTitle}".`
              }
            </p>
            
            <label htmlFor="reason-input" className="block text-xs font-bold text-[var(--admin-text)] mb-2">
              السبب {activeModal === 'reject' ? <span className="text-rose-500 font-black">* (إجباري ويظهر للطالب)</span> : '(اختياري)'}
            </label>
            <textarea
              id="reason-input"
              rows={3}
              value={reasonText}
              onChange={(e) => {
                setReasonText(e.target.value);
                if (e.target.value.trim()) {
                  setValidationError('');
                }
              }}
              placeholder={activeModal === 'reject' ? "اكتب سبب الرفض بالتفصيل هنا ليظهر للطالب..." : "اكتب ملاحظة أو سبب الموافقة..."}
              className={`w-full bg-[var(--admin-surface)] p-3.5 rounded-2xl text-[var(--admin-text)] border ${
                validationError ? 'border-rose-500 focus:border-rose-500 focus:ring-rose-500/20' : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary-15)]'
              } outline-none focus:ring-2 resize-none transition-all duration-200 text-sm`}
              required={activeModal === 'reject'}
            />
            {validationError && (
              <p className="text-xs text-rose-500 font-bold mt-1">{validationError}</p>
            )}
          </div>
          
          <div className="flex gap-3 justify-end pt-4 border-t border-[var(--admin-border)]">
            <button
              type="button"
              onClick={() => {
                setActiveModal(null);
                setSelectedRequest(null);
                setReasonText('');
                setValidationError('');
              }}
              className="admin-btn-ghost py-2.5 px-5"
              disabled={actionLoading !== null}
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={actionLoading !== null || (activeModal === 'reject' && !reasonText.trim())}
              className={`rounded-2xl px-6 py-2.5 text-sm font-bold transition-all duration-200 ${
                activeModal === 'approve'
                  ? 'bg-emerald-600 hover:bg-emerald-700 text-white shadow-[0_4px_12px_rgba(16,185,129,0.15)] disabled:opacity-50'
                  : 'bg-rose-600 hover:bg-rose-700 text-white shadow-[0_4px_12px_rgba(244,63,94,0.15)] disabled:opacity-50 disabled:cursor-not-allowed'
              }`}
            >
              {actionLoading !== null ? 'جاري الحفظ...' : activeModal === 'approve' ? 'تأكيد القبول' : 'تأكيد الرفض'}
            </button>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
