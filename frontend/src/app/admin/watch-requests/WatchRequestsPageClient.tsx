'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { adminService, type AdminWatchRequestDto } from '@/services/admin-service';
import { Check, X, Clock, AlertCircle, BookOpen, GraduationCap, History, MessageSquareText, Timer } from 'lucide-react';
import { formatRelativeDate } from '@/components/admin/admin-utils';
import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import { 
  AdminPage,
  AdminDataTable, 
  AdminColumn,
  AdminPageSkeleton,
  AdminStatCard,
  AdminModal
} from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export default function WatchRequestsPageClient({ mode }: { mode?: 'admin' | 'assistant' }) {
  const [requests, setRequests] = useState<AdminWatchRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState('');

  // Custom modal states
  const [activeModal, setActiveModal] = useState<'approve' | 'reject' | 'edit' | null>(null);
  const [selectedRequest, setSelectedRequest] = useState<AdminWatchRequestDto | null>(null);
  const [reasonText, setReasonText] = useState('');
  const [validationError, setValidationError] = useState('');
  const [editStatus, setEditStatus] = useState<1 | 2>(1);
  const [addedViews, setAddedViews] = useState<number>(1);

  const formatDuration = (seconds?: number | null) => {
    if (!seconds || seconds < 0) return 'غير متاحة';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    return hours > 0
      ? `${hours} س ${minutes} د ${remainingSeconds} ث`
      : `${minutes} د ${remainingSeconds} ث`;
  };

  const RequestDetails = ({ request }: { request: AdminWatchRequestDto }) => (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
      <h3 className="mb-3 text-sm font-black text-[var(--admin-text)]">تفاصيل الطلب</h3>
      <div className="grid grid-cols-1 gap-2 text-xs sm:grid-cols-2">
        <div className="rounded-xl bg-[var(--admin-card)] p-3 sm:col-span-2"><span className="text-[var(--admin-muted)]">سبب طلب الطالب</span><p className="mt-1 whitespace-pre-wrap font-bold leading-relaxed text-[var(--admin-text)]">{request.studentReason || 'لم يُسجل سبب'}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">الفيديو</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.videoTitle}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="inline-flex items-center gap-1 text-[var(--admin-muted)]"><Timer className="h-3.5 w-3.5" />مدة الفيديو</span><p className="mt-1 font-bold text-[var(--admin-text)]">{formatDuration(request.videoDurationSeconds)}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">المدرس</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.teacherName || 'غير محدد'}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">الكورس / الباقة</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.packageName || 'غير محددة'}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">الترم</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.termTitle || 'غير محدد'}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">الحصة</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.lessonTitle || 'غير محددة'}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="text-[var(--admin-muted)]">العدد الأساسي للمشاهدات</span><p className="mt-1 font-bold text-[var(--admin-text)]">{request.baseWatchCount === 0 ? 'غير محدود' : request.baseWatchCount}</p></div>
        <div className="rounded-xl bg-[var(--admin-card)] p-3"><span className="inline-flex items-center gap-1 text-[var(--admin-muted)]"><History className="h-3.5 w-3.5" />هل طلب سابقاً؟</span><p className={`mt-1 font-bold ${request.hasPreviousRequest ? 'text-amber-600' : 'text-emerald-600'}`}>{request.hasPreviousRequest ? 'نعم، له طلب سابق لهذا الفيديو' : 'لا، هذا أول طلب'}</p></div>
      </div>
    </section>
  );

  useEffect(() => {
    fetchRequests();
  }, []);

  const fetchRequests = async (showLoading = true) => {
    try {
      if (showLoading) setLoading(true);
      setError('');
      const response = await adminService.getWatchRequests();
      setRequests(response.data || []);
    } catch (err: any) {
      devConsole.error(err);
      setError('فشل في تحميل الطلبات.');
    } finally {
      if (showLoading) setLoading(false);
    }
  };

  usePlatformEvents({
    onExtraWatchRequestCreated: () => {
      void fetchRequests(false);
    },
    onExtraWatchRequestUpdated: (payload) => {
      if (!payload.requestId) {
        void fetchRequests(false);
        return;
      }
      setRequests((current) => current.map((request) => request.id === payload.requestId
        ? {
            ...request,
            status: payload.status === 'Approved' ? 1 : 2,
            reason: payload.reason ?? request.reason,
            resolvedAt: new Date().toISOString(),
            maxWatchCount: payload.status === 'Approved' ? payload.allowedWatchCount : request.maxWatchCount,
            reachedLimit: payload.status === 'Approved' ? false : request.reachedLimit,
          }
        : request));
    },
  });

  const handleApproveClick = (req: AdminWatchRequestDto) => {
    setSelectedRequest(req);
    setReasonText('تمت الموافقة بواسطة الإدارة');
    setAddedViews(1);
    setValidationError('');
    setActiveModal('approve');
  };

  const updateRequestImmediately = (requestId: string, status: 1 | 2, reason: string, viewIncrease = 0) => {
    setRequests((current) => current.map((request) => {
      if (request.id !== requestId) return request;
      const nextMax = status === 1 && request.maxWatchCount > 0
        ? request.maxWatchCount + viewIncrease
        : request.maxWatchCount;
      return {
        ...request,
        status,
        reason,
        resolvedAt: new Date().toISOString(),
        maxWatchCount: nextMax,
        reachedLimit: status === 1 ? false : request.reachedLimit,
      };
    }));
  };

  const handleRejectClick = (req: AdminWatchRequestDto) => {
    setSelectedRequest(req);
    setReasonText('');
    setValidationError('');
    setActiveModal('reject');
  };

  const handleEditClick = (req: AdminWatchRequestDto) => {
    setSelectedRequest(req);
    setReasonText(req.reason || '');
    setEditStatus(req.status as 1 | 2);
    setAddedViews(1);
    setValidationError('');
    setActiveModal('edit');
  };

  const handleApproveSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    setActionLoading(selectedRequest.id);
    setActiveModal(null);
    try {
      await adminService.approveWatchRequest(selectedRequest.id, reasonText.trim(), addedViews);
      updateRequestImmediately(selectedRequest.id, 1, reasonText.trim() || 'تمت الموافقة بواسطة الإدارة', addedViews);
      toast.success('تم قبول طلب المشاهدة الإضافية.');
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في الموافقة على الطلب');
    } finally {
      setActionLoading(null);
      setSelectedRequest(null);
      setReasonText('');
      setAddedViews(1);
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
      updateRequestImmediately(selectedRequest.id, 2, reasonText.trim());
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

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    if (editStatus === 2 && !reasonText.trim()) {
      setValidationError('سبب الرفض إجباري.');
      return;
    }

    setActionLoading(selectedRequest.id);
    setActiveModal(null);
    try {
      if (editStatus === 1) {
        await adminService.approveWatchRequest(selectedRequest.id, reasonText.trim(), addedViews);
        updateRequestImmediately(selectedRequest.id, 1, reasonText.trim() || 'تمت الموافقة بواسطة الإدارة', addedViews);
        toast.success('تم تعديل القرار إلى مقبول وزيادة المشاهدات.');
      } else {
        await adminService.rejectWatchRequest(selectedRequest.id, reasonText.trim());
        updateRequestImmediately(selectedRequest.id, 2, reasonText.trim());
        toast.success('تم تعديل القرار إلى مرفوض.');
      }
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في تعديل القرار');
    } finally {
      setActionLoading(null);
      setSelectedRequest(null);
      setReasonText('');
      setAddedViews(1);
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
      key: 'academicContext',
      label: 'المحتوى الدراسي',
      render: (req) => (
        <div className="min-w-72 space-y-1 text-sm leading-relaxed">
          <div className="flex items-center gap-1.5 font-bold text-[var(--admin-text)]">
            <GraduationCap className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
            <span>{req.teacherName || 'مدرس غير محدد'}</span>
          </div>
          <div className="text-xs font-semibold text-[var(--admin-text)]">
            {req.packageName || 'باقة غير محددة'} <span className="text-[var(--admin-muted)]">•</span> {req.termTitle || 'ترم غير محدد'}
          </div>
          <div className="text-xs text-[var(--admin-muted)]">
            {req.sectionTitle || 'قسم غير محدد'} <span>•</span> {req.lessonTitle || 'حصة غير محددة'}
          </div>
          <div className="flex items-start gap-1.5 pt-1 font-bold text-[var(--admin-text)]">
            <BookOpen className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--admin-primary)]" />
            <span>{req.videoTitle}</span>
          </div>
        </div>
      )
    },
    {
      key: 'studentReason',
      label: 'سبب الطالب',
      render: (req) => (
        <div className="flex max-w-60 items-start gap-2 text-sm text-[var(--admin-text)]">
          <MessageSquareText className="mt-0.5 h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
          <span className="line-clamp-3 leading-relaxed" title={req.studentReason}>{req.studentReason || 'لم يُسجل سبب'}</span>
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
              <span className="text-sm text-rose-500 font-bold">وصل للحد الأقصى</span>
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
          <div className="flex items-center justify-end gap-2">
            <NeumorphButton
              type="button"
              onClick={() => handleEditClick(req)}
              disabled={actionLoading !== null}
              intent="ghost"
              size="sm"
            >
              تعديل القرار
            </NeumorphButton>
          </div>
        )
      )
    }
  ];

  const Shell = mode === 'assistant' ? AssistantShellChrome : AdminPage;
  const shellActivePath = mode === 'assistant' ? '/assistant/watch-requests' : '/admin/watch-requests';
  const isAssistantWorkspace = mode === 'assistant';

  return (
    <Shell
      activePath={shellActivePath as any}
      sectionLabel={isAssistantWorkspace ? 'طلبات الطلاب' : 'المحتوى الأكاديمي'}
      pageTitle="طلبات المشاهدة الإضافية"
      subtitle="راجع سبب الطالب وسياق الفيديو كاملاً قبل اعتماد زيادة المشاهدات أو رفضها."
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
        title={
          activeModal === 'approve' 
            ? 'موافقة على طلب المشاهدة' 
            : activeModal === 'reject' 
              ? 'رفض طلب المشاهدة' 
              : 'تعديل قرار طلب المشاهدة'
        }
      >
        <form 
          onSubmit={
            activeModal === 'approve' 
              ? handleApproveSubmit 
              : activeModal === 'reject' 
                ? handleRejectSubmit 
                : handleEditSubmit
          } 
          className="space-y-5 text-right"
        >
          {selectedRequest && <RequestDetails request={selectedRequest} />}
          {activeModal === 'edit' ? (
            // Edit Decision Modal
            <div className="space-y-4">
              <p className="text-sm text-[var(--admin-muted)] mb-3 leading-relaxed">
                تعديل قرار طلب الطالب {selectedRequest?.studentName} لمشاهدة فيديو &quot;{selectedRequest?.videoTitle}&quot;.
              </p>

              <div>
                <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">القرار الجديد</label>
                <div className="flex gap-4 animate-fadeIn">
                  <label className="flex items-center gap-2 text-sm text-[var(--admin-text)] font-semibold cursor-pointer">
                    <input 
                      type="radio" 
                      name="editStatus" 
                      checked={editStatus === 1} 
                      onChange={() => setEditStatus(1)}
                      className="accent-[var(--admin-primary)]"
                    />
                    مقبول وزيادة المشاهدات
                  </label>
                  <label className="flex items-center gap-2 text-sm text-[var(--admin-text)] font-semibold cursor-pointer">
                    <input 
                      type="radio" 
                      name="editStatus" 
                      checked={editStatus === 2} 
                      onChange={() => setEditStatus(2)}
                      className="accent-[var(--admin-primary)]"
                    />
                    مرفوض
                  </label>
                </div>
              </div>

              {editStatus === 1 && (
                <div className="animate-slideDown">
                  <label htmlFor="added-views-input-edit" className="block text-xs font-bold text-[var(--admin-text)] mb-2">
                    عدد المشاهدات الإضافية لزيادتها
                  </label>
                  <input
                    id="added-views-input-edit"
                    type="number"
                    min={1}
                    value={addedViews}
                    onChange={(e) => setAddedViews(Math.max(1, parseInt(e.target.value) || 1))}
                    className="w-full bg-[var(--admin-surface)] p-3 rounded-2xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary-15)] outline-none focus:ring-2 transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 text-sm font-bold"
                  />
                </div>
              )}

              <div>
                <label htmlFor="reason-input-edit" className="block text-xs font-bold text-[var(--admin-text)] mb-2">
                  السبب {editStatus === 2 ? <span className="text-rose-500 font-black">* (إجباري ويظهر للطالب)</span> : '(اختياري)'}
                </label>
                <textarea
                  id="reason-input-edit"
                  rows={3}
                  value={reasonText}
                  onChange={(e) => {
                    setReasonText(e.target.value);
                    if (e.target.value.trim()) setValidationError('');
                  }}
                  placeholder={editStatus === 2 ? "اكتب سبب الرفض بالتفصيل هنا..." : "اكتب ملاحظة أو سبب الموافقة..."}
                  className={`w-full bg-[var(--admin-surface)] p-3.5 rounded-2xl text-[var(--admin-text)] border ${
                    validationError ? 'border-rose-500 focus:border-rose-500 focus:ring-rose-500/20' : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary-15)]'
                  } outline-none focus:ring-2 resize-none transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 text-sm`}
                  required={editStatus === 2}
                />
                {validationError && (
                  <p className="text-xs text-rose-500 font-bold mt-1">{validationError}</p>
                )}
              </div>
            </div>
          ) : (
            // Approve / Reject Modal
            <div>
              <p className="text-sm text-[var(--admin-muted)] mb-3 leading-relaxed">
                {activeModal === 'approve' 
                  ? `هل أنت متأكد من الموافقة على طلب الطالب ${selectedRequest?.studentName} لمشاهدة فيديو "${selectedRequest?.videoTitle}"؟`
                  : `برجاء كتابة سبب رفض طلب الطالب ${selectedRequest?.studentName} لمشاهدة فيديو "${selectedRequest?.videoTitle}".`
                }
              </p>

              {activeModal === 'approve' && (
                <div className="mb-4">
                  <label htmlFor="added-views-input" className="block text-xs font-bold text-[var(--admin-text)] mb-2">
                    عدد المشاهدات الإضافية لزيادتها
                  </label>
                  <input
                    id="added-views-input"
                    type="number"
                    min={1}
                    value={addedViews}
                    onChange={(e) => setAddedViews(Math.max(1, parseInt(e.target.value) || 1))}
                    className="w-full bg-[var(--admin-surface)] p-3 rounded-2xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary-15)] outline-none focus:ring-2 transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 text-sm font-bold"
                  />
                </div>
              )}
              
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
                } outline-none focus:ring-2 resize-none transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 text-sm`}
                required={activeModal === 'reject'}
              />
              {validationError && (
                <p className="text-xs text-rose-500 font-bold mt-1">{validationError}</p>
              )}
            </div>
          )}
          
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
              disabled={
                actionLoading !== null || 
                (activeModal === 'reject' && !reasonText.trim()) || 
                (activeModal === 'edit' && editStatus === 2 && !reasonText.trim())
              }
              className={`rounded-2xl px-6 py-2.5 text-sm font-bold transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 ${
                activeModal === 'approve' || (activeModal === 'edit' && editStatus === 1)
                  ? 'bg-emerald-600 hover:bg-emerald-700 text-white shadow-[0_4px_12px_rgba(16,185,129,0.15)] disabled:opacity-50'
                  : 'bg-rose-600 hover:bg-rose-700 text-white shadow-[0_4px_12px_rgba(244,63,94,0.15)] disabled:opacity-50 disabled:cursor-not-allowed'
              }`}
            >
              {actionLoading !== null 
                ? 'جاري الحفظ...' 
                : activeModal === 'approve' 
                  ? 'تأكيد القبول' 
                  : activeModal === 'reject' 
                    ? 'تأكيد الرفض' 
                    : 'حفظ التغييرات'}
            </button>
          </div>
        </form>
      </AdminModal>
    </Shell>
  );
}
