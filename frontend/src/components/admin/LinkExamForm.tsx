'use client';

import { devConsole } from '@/utils/dev-console';
import { useState, useEffect } from 'react';
import { BookCheck, Trash2, Users, Clock, AlertCircle, ArrowUpLeft, LayoutDashboard, FileText, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { adminService, ExamDashboardDto, StudentExamResultSummaryDto } from '@/services/admin-service';
import { AdminStatCard } from './AdminStatCard';
import { AdminDataTable } from './AdminDataTable';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

interface LinkExamFormProps {
  lessonId: string;
  currentExamId?: string | null;
  onSuccess?: () => void;
}

export function LinkExamForm({ lessonId, currentExamId, onSuccess }: LinkExamFormProps) {
  const router = useRouter();
  const [examId, setExamId] = useState(currentExamId || '');
  const [saving, setSaving] = useState(false);
  const [examData, setExamData] = useState<ExamDashboardDto | null>(null);
  const [loadingData, setLoadingData] = useState(false);

  useEffect(() => {
    if (!currentExamId) {
      setExamData(null);
      return;
    }

    const abortController = new AbortController();
    
    setLoadingData(true);
    adminService.getExamDashboard(currentExamId)
      .then(data => {
        if (!abortController.signal.aborted) {
          setExamData(data);
        }
      })
      .catch(err => {
        if (!abortController.signal.aborted) {
          devConsole.error("Failed to load exam stats", err);
        }
      })
      .finally(() => {
        if (!abortController.signal.aborted) {
          setLoadingData(false);
        }
      });

    return () => abortController.abort();
  }, [currentExamId]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!examId.trim()) return;

    try {
      setSaving(true);
      await adminService.linkLessonExam(lessonId, examId);
      toast.success('تم ربط الامتحان بنجاح.');
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ أثناء الربط، يرجى المحاولة مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }

  async function handleUnlink() {
    if (!confirm('هل أنت متأكد من إلغاء ربط هذا الامتحان؟')) return;
    try {
      setSaving(true);
      await adminService.linkLessonExam(lessonId, null);
      toast.success('تم إلغاء ربط الامتحان.');
      setExamId('');
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-6">
      {currentExamId ? (
        <div className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-primary-15)] bg-[var(--admin-card)] p-6 shadow-sm overflow-hidden">
          <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between border-b pb-4 gap-4">
            <div className="flex items-center gap-4">
              <div className="rounded-full bg-[var(--admin-primary-15)] p-3 shadow-sm border border-[var(--admin-primary-15)]">
                <BookCheck className="h-6 w-6 text-[var(--admin-primary)]" />
              </div>
              <div>
                <h4 className="font-bold text-lg text-[var(--admin-text)]">
                  {loadingData ? 'جارٍ تحميل البيانات...' : (examData?.title || 'يوجد امتحان مرفق')}
                </h4>
                <p className="text-sm font-mono text-[var(--admin-muted)] mt-1 opacity-70">معرف: {currentExamId}</p>
              </div>
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => router.push(`/admin/content/exams/${currentExamId}/dashboard`)}
                className="flex items-center gap-2 rounded-xl border border-[var(--admin-border)] hover:border-[var(--admin-primary)] bg-[var(--admin-card)] px-4 py-2 font-bold text-[var(--admin-text)] shadow-sm hover:shadow-md transition-all hover:-translate-y-0.5"
              >
                <LayoutDashboard className="h-4 w-4 text-[var(--admin-primary)]" />
                بروفايل الامتحان (الكامل)
              </button>
              <button
                type="button"
                onClick={() => router.push(`/admin/content/exams/${currentExamId}/add-question`)}
                className="flex items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 py-2 font-bold text-white shadow-sm hover:bg-[var(--admin-primary)]/90 transition-all hover:-translate-y-0.5"
              >
                <Plus className="h-4 w-4" />
                إضافة أسئلة أُخرى
              </button>
              <button
                type="button"
                onClick={handleUnlink}
                disabled={saving}
                className="flex items-center gap-2 rounded-xl bg-[var(--admin-danger-10)] px-4 py-2 font-bold text-[var(--admin-danger)] hover:bg-[var(--admin-danger-10)]/80 transition-colors"
                title="إلغاء ربط الامتحان بالحصة"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          </div>
          
          {examData && (
            <div className="animate-in fade-in slide-in-from-bottom-2 duration-500">
              <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mt-2">
                <AdminStatCard icon={FileText} label="عدد الأسئلة" value={examData.questionCount} variant="light" subtitle="سؤال" />
                <AdminStatCard icon={Clock} label="وقت الامتحان" value={examData.durationMinutes || 'مفتوح'} variant="light" subtitle={examData.durationMinutes ? "دقيقة" : undefined} />
                <AdminStatCard icon={BookCheck} label="الدرجة" value={examData.totalScore} variant="light" subtitle={`النجاح من: ${examData.passingScore}`} />
                <AdminStatCard icon={Users} label="عدد المحاولات" value={examData.attempts?.length || 0} variant="light" subtitle="طالب" />
              </div>

              {examData.attempts && examData.attempts.length > 0 && (
                <div className="mt-6 flex flex-col gap-4">
                  <div className="flex justify-between items-center px-2">
                    <h5 className="font-bold text-lg text-[var(--admin-text)] flex items-center gap-2">
                      <Users className="w-5 h-5 text-[var(--admin-primary)]" />
                      أحدث الطلاب المنضمين
                    </h5>
                    <button onClick={() => router.push(`/admin/content/exams/${currentExamId}/dashboard`)} className="text-sm font-bold text-[var(--admin-primary)] hover:underline flex items-center gap-1">
                      عرض الكل <ArrowUpLeft className="w-4 h-4" />
                    </button>
                  </div>
                  
                  <AdminDataTable<StudentExamResultSummaryDto>
                    data={examData.attempts.slice(0, 5)}
                    columns={[
                      {
                        key: 'student',
                        label: 'الطالب',
                        render: (row) => (
                          <div>
                            <p className="font-bold text-base">{row.studentName}</p>
                            <p className="text-xs font-mono text-[var(--admin-muted)] mt-1">{row.studentPhone}</p>
                          </div>
                        )
                      },
                      {
                        key: 'score',
                        label: 'الدرجة',
                        align: 'center',
                        render: (row) => (
                          <div className="font-black text-lg">
                            {row.scoreAchieved.toFixed(1)} <span className="text-sm text-[var(--admin-muted)]">/ {examData.totalScore}</span>
                          </div>
                        )
                      },
                      {
                        key: 'evaluation',
                        label: 'التقييم',
                        render: (row) => <span className="font-bold text-[var(--admin-primary)] px-3 py-1 bg-[var(--admin-primary-15)] rounded-full">{row.evaluation}</span>
                      },
                      {
                        key: 'status',
                        label: 'الحالة',
                        render: (row) => (
                          <div className="flex items-center gap-2">
                            <span className={`inline-flex items-center justify-center px-4 py-1.5 rounded-full text-xs font-black uppercase tracking-wider ${
                              row.isPassed ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
                            }`}>
                              {row.isPassed ? 'ناجح' : 'راسب'}
                            </span>
                            {row.isTimeExpired && (
                              <span className="inline-flex items-center justify-center px-3 py-1.5 rounded-full text-xs font-black uppercase tracking-wider bg-[var(--admin-warning-10)] text-[var(--admin-warning)]" title="نفذ الوقت وتم التسليم تلقائياً">
                                <AlertCircle className="w-3.5 h-3.5 mr-1" /> تأخير
                              </span>
                            )}
                          </div>
                        )
                      }
                    ]}
                    rowKey={(r) => r.studentId + (r.startedAt || '')}
                    pageSize={5}
                    emptyMessage="لا يوجد محاولات حتى الآن."
                  />
                </div>
              )}
            </div>
          )}
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-wrap items-end gap-4">
            <div className="flex-1 space-y-2 min-w-[200px]">
              <label htmlFor="exam-uuid-input" className="text-xs font-bold text-[var(--admin-muted)]">معرف الامتحان (UUID)</label>
              <input
                id="exam-uuid-input"
                type="text"
                value={examId}
                onChange={(e) => setExamId(e.target.value)}
                placeholder="أدخل معرف الامتحان المراد ربطه"
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
                required
              />
            </div>
            <NeumorphButton
              type="submit"
              disabled={saving || !examId.trim()}
              loading={saving}
              intent="primary"
              size="lg"
              pill
              className="whitespace-nowrap"
            >
              ربط الامتحان
            </NeumorphButton>
          </div>
          <p className="text-xs text-[var(--admin-muted)] mt-2">
            * حالياً يتم استخدام معرف الامتحان (UUID). سيتم إضافة قائمة منسدلة مستقبلاً عند بناء نظام الامتحانات المتكامل.
          </p>
        </form>
      )}
    </div>
  );
}
