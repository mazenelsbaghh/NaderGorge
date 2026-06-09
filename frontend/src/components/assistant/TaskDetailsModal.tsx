'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { AdminModal } from '@/components/admin/AdminModal';
import NeumorphButton from '@/components/ui/neumorph-button';
import { assistantService, TaskDetailsDto } from '@/services/assistant-service';
import { Clock, Send, Paperclip, Check, X as CloseIcon, HelpCircle, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

interface TaskDetailsModalProps {
  taskId: string | null;
  open: boolean;
  onClose: () => void;
  onStatusUpdated: () => void;
  isManager?: boolean;
  currentUserId?: string;
}

export default function TaskDetailsModal({
  taskId,
  open,
  onClose,
  onStatusUpdated,
  isManager = false,
  currentUserId,
}: TaskDetailsModalProps) {
  const [details, setDetails] = useState<TaskDetailsDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [commentText, setCommentText] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [showAttachmentInput, setShowAttachmentInput] = useState(false);
  const [submittingComment, setSubmittingComment] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState(false);
  const [resolvingId, setResolvingId] = useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [showRejectForm, setShowRejectForm] = useState(false);

  const fetchDetails = useCallback(async () => {
    if (!taskId) return;
    setLoading(true);
    try {
      const res = await assistantService.getOperationsTaskDetails(taskId);
      if (res.data?.success) {
        const rawTask = res.data.data.task;
        const statusMap: Record<string, number> = {
          "New": 1,
          "InProgress": 2,
          "Review": 3,
          "Completed": 4,
          "Paused": 5,
          "Overdue": 6
        };
        const priorityMap: Record<string, number> = {
          "Low": 1,
          "Medium": 2,
          "High": 3,
          "Critical": 4
        };
        const normalizedTask = {
          ...rawTask,
          status: typeof rawTask.status === 'string' ? statusMap[rawTask.status] || 1 : rawTask.status,
          priority: typeof rawTask.priority === 'string' ? priorityMap[rawTask.priority] || 2 : rawTask.priority
        };
        setDetails({
          ...res.data.data,
          task: normalizedTask
        });
      } else {
        toast.error(res.data?.message || 'تعذر تحميل تفاصيل المهمة');
      }
    } catch {
      toast.error('حدث خطأ أثناء تحميل تفاصيل المهمة');
    } finally {
      setLoading(false);
    }
  }, [taskId]);

  useEffect(() => {
    if (open && taskId) {
      fetchDetails();
      setCommentText('');
      setAttachmentUrl('');
      setShowAttachmentInput(false);
      setShowRejectForm(false);
      setRejectionReason('');
    } else {
      setDetails(null);
    }
  }, [open, taskId, fetchDetails]);

  const handlePostComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!taskId || !commentText.trim()) return;

    setSubmittingComment(true);
    try {
      const res = await assistantService.addOperationsTaskComment(
        taskId,
        commentText.trim(),
        attachmentUrl.trim() || undefined
      );

      if (res.data?.success) {
        toast.success('تم إضافة التعليق بنجاح');
        setCommentText('');
        setAttachmentUrl('');
        setShowAttachmentInput(false);
        fetchDetails();
      } else {
        toast.error(res.data?.message || 'تعذر إضافة التعليق');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء إضافة التعليق');
    } finally {
      setSubmittingComment(false);
    }
  };

  const handleUpdateStatus = async (newStatus: number) => {
    if (!taskId) return;
    setUpdatingStatus(true);
    try {
      const res = await assistantService.updateOperationsTaskStatus(taskId, newStatus);
      if (res.data?.success) {
        toast.success('تم تحديث حالة المهمة بنجاح ✅');
        onStatusUpdated();
        fetchDetails();
      } else {
        toast.error(res.data?.message || 'تعذر تحديث الحالة');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء تحديث حالة المهمة');
    } finally {
      setUpdatingStatus(false);
    }
  };

  const handleResolveApproval = async (approve: boolean) => {
    if (!taskId) return;
    setResolvingId(taskId);
    try {
      const res = await assistantService.resolveAdminOperationsTaskApproval(
        taskId,
        approve,
        approve ? undefined : rejectionReason.trim() || undefined
      );

      if (res.data?.success) {
        toast.success(approve ? 'تمت الموافقة بنجاح وإغلاق المهمة 🎉' : 'تم رفض الطلب وإعادة المهمة للمتابعة');
        onStatusUpdated();
        onClose();
      } else {
        toast.error(res.data?.message || 'تعذر معالجة القرار');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء معالجة القرار');
    } finally {
      setResolvingId(null);
      setShowRejectForm(false);
    }
  };

  if (!open) return null;

  const getPriorityConfig = (priority: number | string) => {
    const p = typeof priority === 'string' ? {
      "Low": 1,
      "Medium": 2,
      "High": 3,
      "Critical": 4
    }[priority] || 2 : priority;
    switch (p) {
      case 1:
        return { label: 'منخفضة', color: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400' };
      case 2:
        return { label: 'متوسطة', color: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' };
      case 3:
        return { label: 'عالية', color: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400' };
      case 4:
        return { label: 'حرجة', color: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400' };
      default:
        return { label: 'غير محددة', color: 'bg-gray-100 text-gray-700' };
    }
  };

  const getStatusConfig = (status: number | string) => {
    const s = typeof status === 'string' ? {
      "New": 1,
      "InProgress": 2,
      "Review": 3,
      "Completed": 4,
      "Paused": 5,
      "Overdue": 6
    }[status] || 1 : status;
    switch (s) {
      case 1:
        return { label: 'جديدة', color: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400' };
      case 2:
        return { label: 'قيد التنفيذ', color: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400' };
      case 3:
        return { label: 'تحت المراجعة', color: 'bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400' };
      case 4:
        return { label: 'مكتملة', color: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' };
      case 5:
        return { label: 'متوقفة مؤقتاً', color: 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400' };
      case 6:
        return { label: 'متأخرة', color: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400' };
      default:
        return { label: 'غير معروفة', color: 'bg-gray-100 text-gray-700' };
    }
  };

  const isAssignee = details?.task.assigneeId === currentUserId;

  return (
    <AdminModal open={open} onClose={onClose} title={details?.task.title ?? 'تفاصيل المهمة'} maxWidth="max-w-3xl">
      {loading && !details ? (
        <div className="flex h-64 items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-[var(--admin-primary)]" />
        </div>
      ) : details ? (
        <div className="space-y-6 text-right" dir="rtl">
          {/* Top Metadata Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-[var(--admin-card-soft)] p-4 rounded-2xl border border-[var(--admin-border)]">
            <div>
              <span className="block text-[10px] font-black text-[var(--admin-muted)] uppercase">الحالة</span>
              <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 mt-1 text-xs font-bold ${getStatusConfig(details.task.status).color}`}>
                {getStatusConfig(details.task.status).label}
              </span>
            </div>
            <div>
              <span className="block text-[10px] font-black text-[var(--admin-muted)] uppercase">الأولوية</span>
              <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 mt-1 text-xs font-bold ${getPriorityConfig(details.task.priority).color}`}>
                {getPriorityConfig(details.task.priority).label}
              </span>
            </div>
            <div>
              <span className="block text-[10px] font-black text-[var(--admin-muted)] uppercase">المسؤول</span>
              <span className="text-xs font-bold text-[var(--admin-text)] block mt-1">{details.task.assigneeName}</span>
            </div>
            <div>
              <span className="block text-[10px] font-black text-[var(--admin-muted)] uppercase">تاريخ الاستحقاق</span>
              <span className="text-xs font-bold text-[var(--admin-text)] block mt-1 font-mono">
                {details.task.dueDate ? new Date(details.task.dueDate).toLocaleDateString('ar-EG', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }) : '—'}
              </span>
            </div>
          </div>

          {/* Description */}
          {details.task.description && (
            <div className="bg-[var(--admin-bg)] p-4 rounded-xl border border-[var(--admin-border)]">
              <h4 className="text-xs font-black text-[var(--admin-muted)] mb-1">وصف المهمة</h4>
              <p className="text-sm text-[var(--admin-text)] leading-relaxed whitespace-pre-line">{details.task.description}</p>
            </div>
          )}

          {/* Status Transitions for Assignee / Staff */}
          {isAssignee && details.task.status !== 4 && details.task.status !== 3 && (
            <div className="flex flex-wrap items-center gap-2 bg-[var(--admin-primary-15)] p-4 rounded-2xl border border-[var(--admin-primary)]/20">
              <span className="text-xs font-bold text-[var(--admin-primary)] ml-auto">إجراءات الموظف المسند إليه:</span>
              <div className="flex gap-2">
                {details.task.status === 1 && (
                  <NeumorphButton onClick={() => handleUpdateStatus(2)} disabled={updatingStatus} intent="primary" size="sm" className="font-bold text-xs">
                    البدء بالعمل ▶️
                  </NeumorphButton>
                )}
                {details.task.status === 2 && (
                  <>
                    <NeumorphButton onClick={() => handleUpdateStatus(5)} disabled={updatingStatus} intent="ghost" size="sm" className="font-bold text-xs">
                      إيقاف مؤقت ⏸️
                    </NeumorphButton>
                    <NeumorphButton onClick={() => handleUpdateStatus(3)} disabled={updatingStatus} intent="primary" size="sm" className="font-bold text-xs bg-purple-600 text-white hover:bg-purple-700">
                      طلب المراجعة واكتمال المهمة 📤
                    </NeumorphButton>
                  </>
                )}
                {details.task.status === 5 && (
                  <NeumorphButton onClick={() => handleUpdateStatus(2)} disabled={updatingStatus} intent="primary" size="sm" className="font-bold text-xs">
                    استئناف العمل ▶️
                  </NeumorphButton>
                )}
                {details.task.status === 6 && (
                  <NeumorphButton onClick={() => handleUpdateStatus(2)} disabled={updatingStatus} intent="primary" size="sm" className="font-bold text-xs">
                    معالجة التأخير وبدء العمل 🛠️
                  </NeumorphButton>
                )}
              </div>
            </div>
          )}

          {/* Manager Approvals (Approve / Reject) */}
          {isManager && details.task.status === 3 && (
            <div className="border border-purple-200 bg-purple-50/50 p-4 rounded-2xl dark:border-purple-950/20 dark:bg-purple-950/10">
              <h4 className="text-sm font-extrabold text-purple-900 dark:text-purple-400 mb-2 flex items-center gap-1.5">
                <HelpCircle className="h-4 w-4" />
                طلب الموافقة على إغلاق المهمة
              </h4>
              <p className="text-xs text-purple-700 dark:text-purple-300 mb-4 leading-relaxed">
                قام الموظف {details.task.assigneeName} بطلب اكتمال هذه المهمة. يرجى المراجعة واتخاذ قرار القبول لإغلاق المهمة أو الرفض للمتابعة.
              </p>

              {showRejectForm ? (
                <div className="space-y-3">
                  <label className="block text-xs font-bold text-rose-700 dark:text-rose-400">سبب رفض طلب الاكتمال *</label>
                  <textarea
                    required
                    value={rejectionReason}
                    onChange={(e) => setRejectionReason(e.target.value)}
                    placeholder="اكتب هنا توجيهات التعديل للموظف..."
                    rows={2}
                    className="w-full rounded-xl border border-rose-300 bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none"
                  />
                  <div className="flex gap-2">
                    <NeumorphButton onClick={() => handleResolveApproval(false)} disabled={!!resolvingId} intent="danger" size="sm" className="text-xs font-bold">
                      تأكيد الرفض ❌
                    </NeumorphButton>
                    <NeumorphButton onClick={() => setShowRejectForm(false)} disabled={!!resolvingId} intent="ghost" size="sm" className="text-xs font-bold">
                      إلغاء
                    </NeumorphButton>
                  </div>
                </div>
              ) : (
                <div className="flex gap-2 justify-end">
                  <NeumorphButton onClick={() => handleResolveApproval(true)} disabled={!!resolvingId} intent="primary" size="sm" className="!bg-emerald-500 !text-white hover:!bg-emerald-600 text-xs font-bold">
                    <Check className="h-3.5 w-3.5 inline ml-1" />
                    الموافقة والإغلاق
                  </NeumorphButton>
                  <NeumorphButton onClick={() => setShowRejectForm(true)} disabled={!!resolvingId} intent="danger" size="sm" className="text-xs font-bold">
                    <CloseIcon className="h-3.5 w-3.5 inline ml-1" />
                    رفض وإرجاع للعمل
                  </NeumorphButton>
                </div>
              )}
            </div>
          )}

          {/* Comment stream section */}
          <div>
            <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-1.5">
              <span>مجرى النقاش والتعليقات</span>
              <span className="text-xs font-bold text-[var(--admin-muted)] bg-[var(--admin-border)]/50 px-2 py-0.5 rounded-full font-mono">
                {details.comments.length}
              </span>
            </h4>

            {/* Comments List */}
            <div className="space-y-3 max-h-60 overflow-y-auto pr-1">
              {details.comments.length === 0 ? (
                <p className="text-xs text-[var(--admin-muted)] italic text-center py-4">لا توجد تعليقات أو نقاشات حول هذه المهمة بعد.</p>
              ) : (
                details.comments.map((comment) => (
                  <div key={comment.id} className="p-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] flex flex-col gap-1">
                    <div className="flex justify-between items-center text-xs">
                      <span className="font-extrabold text-[var(--admin-text)]">{comment.userName}</span>
                      <span className="text-[10px] text-[var(--admin-muted)] flex items-center gap-1 font-mono">
                        <Clock className="h-3 w-3" />
                        {new Date(comment.createdAt).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' })}{' '}
                        {new Date(comment.createdAt).toLocaleDateString('ar-EG')}
                      </span>
                    </div>
                    <p className="text-sm text-[var(--admin-text)] mt-1 whitespace-pre-line leading-relaxed">{comment.content}</p>
                    {comment.attachmentUrl && (
                      <div className="mt-2 text-xs">
                        <a
                          href={comment.attachmentUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1 text-[var(--admin-primary)] font-bold hover:underline"
                        >
                          <Paperclip className="h-3.5 w-3.5" />
                          <span>رابط المرفق المضاف</span>
                        </a>
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>

            {/* Add Comment Form */}
            {details.task.status !== 4 && (
              <form onSubmit={handlePostComment} className="mt-4 space-y-2 pt-4 border-t border-[var(--admin-border)]">
                <div className="flex gap-2 items-start">
                  <textarea
                    required
                    value={commentText}
                    onChange={(e) => setCommentText(e.target.value)}
                    placeholder="اكتب تعليقك أو تحديثك هنا..."
                    rows={2}
                    className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
                  />
                  <NeumorphButton type="submit" disabled={submittingComment} intent="primary" size="md" className="shrink-0 rounded-xl px-3 py-3">
                    {submittingComment ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                  </NeumorphButton>
                </div>

                {/* Attachment toggler */}
                <div className="flex justify-start text-xs">
                  <button
                    type="button"
                    onClick={() => setShowAttachmentInput(!showAttachmentInput)}
                    className="text-[var(--admin-primary)] hover:underline flex items-center gap-1 font-bold"
                  >
                    <Paperclip className="h-3.5 w-3.5" />
                    <span>{showAttachmentInput ? 'إلغاء المرفق' : 'إرفاق رابط/ملف'}</span>
                  </button>
                </div>

                {showAttachmentInput && (
                  <input
                    type="url"
                    value={attachmentUrl}
                    onChange={(e) => setAttachmentUrl(e.target.value)}
                    placeholder="https://example.com/document-link"
                    className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
                  />
                )}
              </form>
            )}
          </div>
        </div>
      ) : null}
    </AdminModal>
  );
}
