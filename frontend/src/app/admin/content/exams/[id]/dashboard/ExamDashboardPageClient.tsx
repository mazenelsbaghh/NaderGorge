"use client"

import { useCallback, useEffect, useState } from 'react';
import { adminService, ExamDashboardDto, StudentExamResultSummaryDto } from '@/services/admin-service';
import { useRouter } from 'next/navigation';
import { FileText, Clock, BookCheck, Users, AlertCircle, Trash2, Plus } from 'lucide-react';
import { AdminShellChrome, AdminStatCard, AdminDataTable, AdminBackButton } from '@/components/admin';
import { ConfirmModal } from '@/components/ui/admin-modal';
import toast from 'react-hot-toast';
import { sanitizeRichHtml } from '@/lib/sanitize-html';

export default function ExamDashboardPageClient(props: { params: { id: string } }) {
  const params = props.params;
  const examId = params.id;
  const router = useRouter();

  const [dashboard, setDashboard] = useState<ExamDashboardDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);
  const [deletingQuestionId, setDeletingQuestionId] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<{ open: boolean; id: string | null }>({ open: false, id: null });

  const loadDashboard = useCallback(() => {
    if (!examId) return;
    adminService.getExamDashboard(examId)
      .then(data => {
        setDashboard(data ?? null);
      })
      .catch(() => setError(true))
      .finally(() => setIsLoading(false));
  }, [examId]);

  useEffect(() => {
    loadDashboard();
  }, [loadDashboard]);

  const handleDeleteQuestion = async () => {
    if (!deleteConfirm.id) return;
    const examQuestionId = deleteConfirm.id;
    setDeleteConfirm({ open: false, id: null });
    setDeletingQuestionId(examQuestionId);
    try {
      await adminService.deleteExamQuestion(examId, examQuestionId);
      toast.success('تم حذف السؤال بنجاح');
      loadDashboard();
    } catch {
      toast.error('فشل في حذف السؤال');
    } finally {
      setDeletingQuestionId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-2 border-primary/20 border-t-primary"></div>
      </div>
    );
  }

  if (error || !dashboard) {
    return (
      <div className="p-8 text-center text-red-500">
        <p>حدث خطأ أثناء تحميل بيانات الامتحان</p>
      </div>
    );
  }

  return (
    <AdminShellChrome 
      activePath="/admin/content"
      sectionLabel="إدارة الامتحانات"
      pageTitle="بروفايل الامتحان" 
      subtitle={dashboard.title}
      action={<AdminBackButton />}
    >
      <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
        
        {/* Description */}
        {dashboard.description && (
          <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-8 shadow-sm backdrop-blur-xl">
            <h3 className="text-sm font-bold uppercase tracking-[0.25em] text-[var(--admin-muted)] mb-2">وصف الامتحان</h3>
            <p className="text-[var(--admin-text)] text-lg leading-relaxed">{dashboard.description}</p>
          </div>
        )}

        {/* Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          <AdminStatCard 
            icon={BookCheck} 
            label="الدرجة النهائية" 
            value={dashboard.totalScore} 
            variant="accent" 
          />
          <AdminStatCard 
            icon={BookCheck} 
            label="درجة النجاح" 
            value={dashboard.passingScore} 
            variant="light" 
          />
          <AdminStatCard 
            icon={FileText} 
            label="عدد الأسئلة" 
            value={dashboard.questionCount} 
            variant="muted" 
          />
          <AdminStatCard 
            icon={Clock} 
            label="وقت الامتحان" 
            value={dashboard.durationMinutes || 'مفتوح'} 
            subtitle={dashboard.durationMinutes ? "دقيقة" : undefined}
            variant="light" 
          />
        </div>

        {/* Questions List */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
                <FileText className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-[var(--admin-text)]">أسئلة الامتحان</h2>
                <p className="text-sm text-[var(--admin-muted)] font-mono mt-1">{dashboard.questions?.length ?? 0} سؤال</p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => router.push(`/admin/content/exams/${examId}/add-question`)}
              className="inline-flex items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 py-2.5 text-sm font-bold text-white transition hover:brightness-110"
            >
              <Plus className="h-4 w-4" />
              إضافة سؤال
            </button>
          </div>

          {dashboard.questions?.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-[var(--admin-border)] p-8 text-center text-[var(--admin-muted)] font-bold">
              لا يوجد أسئلة في هذا الامتحان بعد.
            </div>
          ) : (
            <div className="space-y-3">
              {dashboard.questions?.map((q, idx) => (
                <div key={q.examQuestionId} className="flex items-start justify-between gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
                  <div className="flex items-start gap-3 min-w-0">
                    <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-[var(--admin-primary-15)] text-xs font-black text-[var(--admin-primary)]">
                      {idx + 1}
                    </span>
                    <div className="min-w-0">
                      <p className="font-bold text-[var(--admin-text)] truncate" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.text) }} />
                      <div className="mt-1 flex items-center gap-3 flex-wrap">
                        <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-black uppercase tracking-wider ${
                          q.type === 'FindTheMistake'
                            ? 'bg-purple-500/10 text-purple-600'
                            : q.type === 'Essay'
                            ? 'bg-orange-500/10 text-orange-600'
                            : 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                        }`}>
                          {q.type === 'FindTheMistake' ? 'اكتشف الغلطة' : q.type === 'Essay' ? 'مقال' : 'اختيار'}
                        </span>
                        <span className="text-xs text-[var(--admin-muted)] font-mono">{q.points} نقطة</span>
                        {q.type === 'FindTheMistake' && !q.baseText && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-red-500/10 px-2.5 py-0.5 text-xs font-black text-red-600">
                            <AlertCircle className="h-3 w-3" /> baseText مفقود
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <button
                    type="button"
                    disabled={deletingQuestionId === q.examQuestionId}
                    onClick={() => setDeleteConfirm({ open: true, id: q.examQuestionId })}
                    className="shrink-0 flex h-9 w-9 items-center justify-center rounded-xl border border-red-200 bg-red-50 text-red-500 transition hover:bg-red-100 disabled:opacity-50"
                    title="حذف السؤال"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Attempts Table */}
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
              <Users className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">محاولات الطلاب</h2>
              <p className="text-sm text-[var(--admin-muted)] font-mono mt-1">
                إجمالي المحاولات المسجلة: {dashboard.attempts.length}
              </p>
            </div>
          </div>
          
          <AdminDataTable<StudentExamResultSummaryDto>
            data={dashboard.attempts}
            columns={[
              {
                key: 'student',
                label: 'الطالب',
                render: (row) => (
                  <div>
                    <p className="font-bold text-base text-[var(--admin-text)]">{row.studentName}</p>
                    <p className="text-xs font-mono text-[var(--admin-primary)] mt-1">{row.studentPhone || '---'}</p>
                  </div>
                )
              },
              {
                key: 'time',
                label: 'التوقيت',
                render: (row) => (
                  <div className="space-y-1">
                    <div className="text-xs text-[var(--admin-muted)]">
                      <span className="font-bold inline-block w-12 text-[var(--admin-text)] opacity-70">البدء:</span> 
                      {row.startedAt ? new Date(row.startedAt).toLocaleString('ar-EG', { dateStyle: 'short', timeStyle: 'short' }) : '---'}
                    </div>
                    <div className="text-xs text-[var(--admin-muted)]">
                      <span className="font-bold inline-block w-12 text-[var(--admin-text)] opacity-70">التسليم:</span> 
                      {row.submittedAt ? new Date(row.submittedAt).toLocaleString('ar-EG', { dateStyle: 'short', timeStyle: 'short' }) : '---'}
                    </div>
                  </div>
                )
              },
              {
                key: 'score',
                label: 'النتيجة',
                align: 'center',
                render: (row) => (
                  <div className="font-black text-xl text-[var(--admin-text)]">
                    {row.scoreAchieved.toFixed(1)} <span className="text-sm font-semibold opacity-50">/ {dashboard.totalScore}</span>
                  </div>
                )
              },
              {
                key: 'evaluation',
                label: 'التقييم',
                align: 'center',
                render: (row) => (
                  <span className="font-bold tracking-wide text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-4 py-1.5 rounded-full inline-block">
                    {row.evaluation}
                  </span>
                )
              },
              {
                key: 'status',
                label: 'الحالة',
                render: (row) => (
                  <div className="flex flex-col gap-2 items-start">
                    <span className={`inline-flex items-center justify-center px-4 py-1.5 rounded-full text-xs font-black uppercase tracking-wider ${
                      row.isPassed ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
                    }`}>
                      {row.isPassed ? 'ناجح' : 'راسب'}
                    </span>
                    {row.isTimeExpired && (
                      <span className="inline-flex items-center justify-center px-3 py-1.5 rounded-full text-xs font-black uppercase tracking-wider bg-orange-500/10 text-orange-600" title="نفذ الوقت وتم التسليم تلقائياً">
                        <AlertCircle className="w-3.5 h-3.5 mr-1" /> تأخير
                      </span>
                    )}
                  </div>
                )
              }
            ]}
            rowKey={(r) => r.studentId + (r.startedAt || '')}
            pageSize={15}
            emptyMessage="لا يوجد محاولات لهذا الامتحان حتى الآن."
          />
        </div>
      </div>

      <ConfirmModal
        open={deleteConfirm.open}
        onClose={() => setDeleteConfirm({ open: false, id: null })}
        onConfirm={handleDeleteQuestion}
        title="حذف السؤال"
        description="هل أنت متأكد من حذف هذا السؤال نهائياً؟ لا يمكن التراجع عن هذا الإجراء."
        confirmLabel="حذف"
        cancelLabel="إلغاء"
        variant="danger"
        loading={deletingQuestionId !== null}
      />
    </AdminShellChrome>
  );
}
