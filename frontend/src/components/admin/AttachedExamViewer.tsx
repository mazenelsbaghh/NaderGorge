'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { adminService, type ExamDashboardDto } from '@/services/admin-service';
import { BookCheck, FileQuestion, GraduationCap, LayoutList, Timer, Plus, BarChart3, Trash2 } from 'lucide-react';
import { AdminPageSkeleton, AdminStatCard } from '@/components/admin';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

export function AttachedExamViewer({ examId, onUnlink }: { examId: string; onUnlink?: () => void }) {
  const router = useRouter();
  const [data, setData] = useState<ExamDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    try {
      const data = await adminService.getExamDashboard(examId);
      setData(data || null);
    } catch {
      toast.error('أخفق تحميل بيانات الامتحان');
    } finally {
      setLoading(false);
    }
  }, [examId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  if (loading) {
    return (
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
        <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
          <BookCheck className="h-6 w-6 text-[var(--admin-primary)]" />
          جاري تحميل بيانات الامتحان...
        </h3>
        <AdminPageSkeleton />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm text-center">
        <p className="text-[var(--admin-muted)]">لم يتم العثور على تفاصيل للامتحان المرفق.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Exam Overview Summary */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm relative overflow-hidden">
        <div className="absolute top-0 right-0 h-full w-2 bg-[var(--admin-primary)]" />
        <div className="flex flex-col md:flex-row md:justify-between md:items-start gap-4 mb-6">
          <div>
            <h3 className="mb-2 text-2xl font-black text-[var(--admin-text)] flex items-center gap-3">
              <BookCheck className="h-6 w-6 text-[var(--admin-primary)]" />
              {data.title}
            </h3>
            {data.description && (
              <p className="text-[var(--admin-muted)] text-sm">{data.description}</p>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-3 shrink-0">
            {onUnlink && (
              <NeumorphButton
                type="button"
                onClick={onUnlink}
                intent="danger"
                size="md"
                pill
              >
                <Trash2 className="w-4 h-4 ml-2" /> إلغاء ربط الامتحان
              </NeumorphButton>
            )}
            <NeumorphButton
              type="button"
              onClick={() => router.push(`/admin/content/exams/${examId}`)}
              intent="primary"
              size="md"
              pill
            >
              <BarChart3 className="w-4 h-4 ml-2" /> عرض البروفايل
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => router.push(`/admin/content/exams/${examId}/add-question`)}
              intent="primary"
              size="md"
              pill
            >
              <Plus className="w-4 h-4 ml-2" /> إدراج أو تعديل الأسئلة
            </NeumorphButton>
          </div>
        </div>
        
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mt-6">
          <AdminStatCard variant="accent" icon={FileQuestion} label="عدد الأسئلة" value={data.questionCount} />
          <AdminStatCard variant="light" icon={GraduationCap} label="الدرجة النهائية" value={data.totalScore} />
          <AdminStatCard variant="muted" icon={GraduationCap} label="درجة النجاح" value={data.passingScore} />
          <AdminStatCard variant="light" icon={Timer} label="زمن الامتحان" value={data.durationMinutes ? `${data.durationMinutes} دقيقة` : 'غير محدد'} />
        </div>
      </div>

      {/* Questions List with Stats */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
        <div className="flex justify-between items-center mb-6">
          <h3 className="text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
            <LayoutList className="h-6 w-6 text-[var(--admin-primary)]" />
            إحصائيات أسئلة الامتحان
          </h3>
        </div>

        <div className="space-y-4">
          {data.questions && data.questions.length > 0 ? (
            data.questions.map((q, idx) => (
                <div 
                  key={q.examQuestionId} 
                  className="group relative rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-background)] p-5 transition-all hover:border-[var(--admin-primary)] hover:shadow-md"
                >
                  <div className="flex flex-col xl:flex-row xl:items-start justify-between gap-6">
                    <div className="flex gap-4 flex-1">
                      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--admin-card-strong)] text-sm font-bold text-[var(--admin-text)] shadow-sm">
                        {idx + 1}
                      </div>
                      <div className="flex-1">
                        <p className="text-[var(--admin-text)] font-semibold text-base leading-relaxed break-words">{q.text}</p>
                        {q.baseText && (
                          <p className="text-[var(--admin-muted)] mt-2 text-sm italic border-r-2 border-[var(--admin-border)] pr-3">
                            {q.baseText}
                          </p>
                        )}
                        <div className="mt-4 flex flex-wrap gap-3">
                          <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-card-strong)] px-2.5 py-1 text-xs font-medium text-[var(--admin-muted)]">
                            {q.type === 'MCQ' ? 'اختيار من متعدد' : q.type === 'FillIn' ? 'أكمل الفراغ' : q.type === 'FindTheMistake' ? 'استخرج الخطأ' : q.type}
                          </span>
                          <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-primary)]/10 px-2.5 py-1 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/20">
                            {q.points} {q.points === 1 ? 'نقطة' : 'نقاط'}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Statistics Container */}
                    <div className="xl:w-64 shrink-0 rounded-xl bg-[var(--admin-card)] border border-[var(--admin-border)] p-4">
                      <div className="flex items-center gap-1.5 mb-2">
                        <BarChart3 className="h-4 w-4 text-[var(--admin-primary)]" />
                        <span className="text-xs font-bold text-[var(--admin-text)]">إحصائيات الإجابات</span>
                      </div>
                      {q.totalAttempts && q.totalAttempts > 0 ? (
                        <div className="space-y-2">
                          <div className="flex items-center justify-between text-xs">
                            <span className="text-[var(--admin-muted)]">نسبة الإجابة الصحيحة:</span>
                            <span className="font-bold text-green-600 dark:text-green-400">{q.correctPercentage}%</span>
                          </div>
                          <div className="w-full h-1.5 bg-[var(--admin-bg)] rounded-full overflow-hidden border border-[var(--admin-border)]">
                            <div 
                              className="h-full bg-green-500 rounded-full" 
                              style={{ width: `${q.correctPercentage}%` }}
                            />
                          </div>
                          <div className="grid grid-cols-3 gap-1 text-[10px] font-mono text-[var(--admin-muted)] text-center pt-1 border-t border-[var(--admin-border)]/50">
                            <div>
                              <div className="font-bold text-green-600 dark:text-green-400">{q.correctCount}</div>
                              <div>صح</div>
                            </div>
                            <div>
                              <div className="font-bold text-red-500">{q.wrongCount}</div>
                              <div>خطأ</div>
                            </div>
                            <div>
                              <div className="font-bold text-[var(--admin-text)]">{q.totalAttempts}</div>
                              <div>إجمالي</div>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="space-y-2">
                          <p className="text-xs font-bold text-[var(--admin-muted)]">لم يتم حل السؤال بعد</p>
                          <p className="text-[10px] leading-relaxed text-[var(--admin-muted)] opacity-85">
                            بمجرد قيام الطلاب بحل هذا السؤال، ستظهر الإحصائيات هنا بالتفصيل.
                          </p>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ))
          ) : (
            <div className="text-center py-10 bg-[var(--admin-background)] rounded-xl border border-dashed border-[var(--admin-border)]">
              <p className="text-[var(--admin-muted)] font-bold text-sm mb-2">لا توجد أسئلة</p>
              <p className="text-xs text-[var(--admin-muted)] opacity-70 mb-4">لم يتم إدراج أي أسئلة حتى الآن. تأكد من إعداد الأسئلة للطلاب.</p>
              <NeumorphButton
                type="button"
                onClick={() => router.push(`/admin/content/exams/${examId}/add-question`)}
                intent="primary"
                size="sm"
                pill
              >
                <Plus className="w-4 h-4 ml-1" /> إضافة أسئلة الآن
              </NeumorphButton>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
