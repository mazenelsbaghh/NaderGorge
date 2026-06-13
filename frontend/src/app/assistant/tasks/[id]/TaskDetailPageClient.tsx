'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { assistantService, TaskDetailsDto } from '@/services/assistant-service';
import { useAuthStore } from '@/stores/auth-store';
import { Clock, Send, Paperclip, ArrowRight, Loader2, AlertTriangle } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export default function TaskDetailPageClient() {
  const params = useParams();
  const router = useRouter();
  const taskId = params?.id as string;
  const { user } = useAuthStore();

  const [details, setDetails] = useState<TaskDetailsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [commentText, setCommentText] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [showAttachmentInput, setShowAttachmentInput] = useState(false);
  const [submittingComment, setSubmittingComment] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState(false);

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
    fetchDetails();
  }, [fetchDetails]);

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

  const isAssignee = details?.task.assigneeId === user?.id;

  return (
    <AssistantShellChrome
      activePath="/assistant/tasks"
      sectionLabel="المهام"
      pageTitle={details?.task.title ?? 'تفاصيل المهمة'}
      headerAccessory={
        <NeumorphButton onClick={() => router.push('/assistant/tasks')} intent="ghost" size="sm" className="flex items-center gap-1.5 font-bold">
          <ArrowRight className="h-4 w-4" />
          <span>رجوع</span>
        </NeumorphButton>
      }
    >
      {loading && !details ? (
        <div className="flex h-64 items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-[var(--admin-primary)]" />
        </div>
      ) : details ? (
        <div className="mx-auto max-w-4xl space-y-6 text-right" dir="rtl">
          {/* Top Metadata Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-[var(--admin-card-soft)] p-4 rounded-2xl border border-[var(--admin-border)]">
            <div>
              <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">الحالة</span>
              <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 mt-1 text-xs font-bold ${getStatusConfig(details.task.status).color}`}>
                {getStatusConfig(details.task.status).label}
              </span>
            </div>
            <div>
              <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">الأولوية</span>
              <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 mt-1 text-xs font-bold ${getPriorityConfig(details.task.priority).color}`}>
                {getPriorityConfig(details.task.priority).label}
              </span>
            </div>
            <div>
              <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">المسؤول</span>
              <span className="text-xs font-bold text-[var(--admin-text)] block mt-1">{details.task.assigneeName}</span>
            </div>
            <div>
              <span className="block text-xs font-black text-[var(--admin-muted)] uppercase">تاريخ الاستحقاق</span>
              <span className="text-xs font-bold text-[var(--admin-text)] block mt-1 font-mono">
                {details.task.dueDate ? new Date(details.task.dueDate).toLocaleDateString('ar-EG', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }) : '—'}
              </span>
            </div>
          </div>

          {/* Description */}
          {details.task.description && (
            <div className="bg-[var(--admin-card)] p-5 rounded-2xl border border-[var(--admin-border)]">
              <h4 className="text-xs font-black text-[var(--admin-muted)] mb-2">وصف المهمة</h4>
              <p className="text-sm text-[var(--admin-text)] leading-relaxed whitespace-pre-line">{details.task.description}</p>
            </div>
          )}

          {/* Status Transitions */}
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
                      تقديم للمراجعة 📤
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

          {/* Comment stream section */}
          <div className="bg-[var(--admin-card)] p-5 rounded-2xl border border-[var(--admin-border)]">
            <h4 className="text-sm font-black text-[var(--admin-text)] mb-4 flex items-center gap-1.5">
              <span>مجرى النقاش والتعليقات</span>
              <span className="text-xs font-bold text-[var(--admin-muted)] bg-[var(--admin-border)]/50 px-2.5 py-0.5 rounded-full font-mono">
                {details.comments.length}
              </span>
            </h4>

            {/* Comments List */}
            <div className="space-y-3 max-h-[300px] overflow-y-auto pr-1 mb-4">
              {details.comments.length === 0 ? (
                <p className="text-xs text-[var(--admin-muted)] italic text-center py-6">لا توجد تعليقات أو نقاشات حول هذه المهمة بعد.</p>
              ) : (
                details.comments.map((comment) => (
                  <div key={comment.id} className="p-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] flex flex-col gap-1">
                    <div className="flex justify-between items-center text-xs">
                      <span className="font-extrabold text-[var(--admin-text)]">{comment.userName}</span>
                      <span className="text-xs text-[var(--admin-muted)] flex items-center gap-1 font-mono">
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
              <form onSubmit={handlePostComment} className="mt-4 space-y-3 pt-4 border-t border-[var(--admin-border)]">
                <div className="flex gap-2 items-start">
                  <textarea
                    required
                    value={commentText}
                    onChange={(e) => setCommentText(e.target.value)}
                    placeholder="اكتب تعليقك أو تحديثك هنا..."
                    rows={2}
                    className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
                  />
                  <NeumorphButton type="submit" disabled={submittingComment} intent="primary" size="md" className="shrink-0 rounded-xl px-3 py-3 h-10 w-10 flex items-center justify-center">
                    {submittingComment ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                  </NeumorphButton>
                </div>

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
      ) : (
        <div className="py-16 text-center border border-[var(--admin-border)] rounded-3xl bg-[var(--admin-card-soft)] max-w-4xl mx-auto">
          <AlertTriangle className="mx-auto h-12 w-12 text-[var(--admin-muted)] mb-3 opacity-40" />
          <h3 className="text-lg font-bold text-[var(--admin-text)]">المهمة غير موجودة أو غير مصرح لك بدخولها!</h3>
          <p className="text-sm text-[var(--admin-muted)] mt-1">تأكد من الرابط الصحيح أو من أن المهمة مسندة إليك.</p>
        </div>
      )}
    </AssistantShellChrome>
  );
}
