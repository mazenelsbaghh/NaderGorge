'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { adminService, type HomeworkDashboardDto } from '@/services/admin-service';
import { ClipboardList, FileQuestion, GraduationCap, LayoutList, Plus, BarChart3, Users, Power } from 'lucide-react';
import { AdminPageSkeleton, AdminStatCard } from '@/components/admin';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { normalizeQuestionRichText } from '@/lib/question-text';

export function AttachedHomeworkViewer({
  homeworkId,
  surface = 'admin',
}: {
  homeworkId: string;
  surface?: 'admin' | 'teacher';
}) {
  const router = useRouter();
  const [data, setData] = useState<HomeworkDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const homeworkBasePath = surface === 'teacher' ? '/teacher/packages/homework' : '/admin/content/homework';

  const loadData = useCallback(async () => {
    try {
      const data = await adminService.getHomeworkDashboard(homeworkId);
      setData(data || null);
    } catch {
      toast.error('أخفق تحميل بيانات الواجب');
    } finally {
      setLoading(false);
    }
  }, [homeworkId]);

  const toggleStatus = async () => {
    if (!data) return;
    try {
      await adminService.setHomeworkStatus(homeworkId, !data.isActive);
      setData({ ...data, isActive: !data.isActive });
      toast.success(data.isActive ? 'تم تعطيل الواجب، وسيظل محفوظاً.' : 'تم تفعيل الواجب.');
    } catch {
      toast.error('تعذر تحديث حالة الواجب.');
    }
  };

  useEffect(() => {
    loadData();
  }, [loadData]);

  if (loading) {
    return (
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
        <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
          <ClipboardList className="h-6 w-6 text-[var(--admin-primary)]" />
          جاري تحميل بيانات الواجب...
        </h3>
        <AdminPageSkeleton />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm text-center">
        <p className="text-[var(--admin-muted)]">لم يتم العثور على تفاصيل للواجب المرفق.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Homework Overview Summary */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm relative overflow-hidden">
        <div className="absolute top-0 right-0 h-full w-2 bg-[var(--admin-primary)]" />
        <div className="flex flex-col md:flex-row md:justify-between md:items-start gap-4 mb-6">
          <div>
            <h3 className="mb-2 text-2xl font-black text-[var(--admin-text)] flex items-center gap-3">
              <ClipboardList className="h-6 w-6 text-[var(--admin-primary)]" />
              {data.title}
            </h3>
            {data.description && (
              <p className="text-[var(--admin-muted)] text-sm">{data.description}</p>
            )}
          </div>
          <div className="flex flex-wrap gap-3">
            <NeumorphButton type="button" onClick={toggleStatus} intent={data.isActive ? 'danger' : 'primary'} size="md" pill>
              <Power className="w-4 h-4 ml-2" /> {data.isActive ? 'تعطيل الواجب' : 'تفعيل الواجب'}
            </NeumorphButton>
            <NeumorphButton type="button" onClick={() => router.push(`${homeworkBasePath}/${homeworkId}/add-question`)} intent="primary" size="md" pill className="shrink-0">
              <Plus className="w-4 h-4 ml-2" /> إدراج أو تعديل الأسئلة
            </NeumorphButton>
          </div>
        </div>
        
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mt-6">
          <AdminStatCard variant="accent" icon={FileQuestion} label="عدد الأسئلة" value={data.questionCount} />
          <AdminStatCard variant="light" icon={GraduationCap} label="الدرجة النهائية" value={data.totalScore} />
          <AdminStatCard variant="muted" icon={GraduationCap} label="درجة النجاح" value={data.passingScore} />
          <AdminStatCard variant="light" icon={ClipboardList} label="إلزامي" value={data.isMandatory ? 'نعم' : 'لا'} />
        </div>
      </div>

      {/* Questions List with Stats */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
        <div className="flex justify-between items-center mb-6">
          <h3 className="text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
            <LayoutList className="h-6 w-6 text-[var(--admin-primary)]" />
            تفاصيل أسئلة الواجب
          </h3>
        </div>

        <div className="space-y-4">
          {data.questions && data.questions.length > 0 ? (
            data.questions.map((q, idx) => (
                <div 
                  key={q.homeworkQuestionId} 
                  className="group relative rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-background)] p-5 transition-all hover:border-[var(--admin-primary)] hover:shadow-md"
                >
                  <div className="flex flex-col xl:flex-row xl:items-start justify-between gap-6">
                    <div className="flex gap-4 flex-1">
                      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--admin-card-strong)] text-sm font-bold text-[var(--admin-text)] shadow-sm">
                        {idx + 1}
                      </div>
                      <div className="flex-1">
                        <div className="text-[var(--admin-text)] font-semibold text-base leading-relaxed break-words" dangerouslySetInnerHTML={{ __html: normalizeQuestionRichText(q.text) }} />
                        {q.imageUrl && (
                          <div className="mt-3 overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                              src={resolveMediaUrl(q.imageUrl)}
                              alt={`صورة سؤال الواجب ${idx + 1}`}
                              className="max-h-64 w-full object-contain"
                            />
                          </div>
                        )}
                        {q.baseText && (
                          <p className="text-[var(--admin-muted)] mt-2 text-sm italic border-r-2 border-[var(--admin-border)] pr-3">
                            {q.baseText}
                          </p>
                        )}
                        <div className="mt-4 flex flex-wrap gap-3">
                          <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-card-strong)] px-2.5 py-1 text-xs font-medium text-[var(--admin-muted)]">
                            {q.type === 'MCQ' ? 'اختيار من متعدد' : q.type === 'Essay' ? 'مقال' : q.type === 'FindTheMistake' ? 'استخرج الخطأ' : q.type}
                          </span>
                          <span className="inline-flex items-center gap-1.5 rounded-md bg-[var(--admin-primary)]/10 px-2.5 py-1 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/20">
                            {q.points} {q.points === 1 ? 'نقطة' : 'نقاط'}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Statistics Container */}
                    <div className="xl:w-64 shrink-0 rounded-xl bg-[var(--admin-card)] border border-[var(--admin-border)] p-4">
                      <BarChart3 className="mb-3 h-5 w-5 text-[var(--admin-primary)]" />
                      <p className="text-sm font-black text-[var(--admin-text)]">إحصائيات التسليم</p>
                      <p className="mt-2 text-xs leading-5 text-[var(--admin-muted)]">
                        إجمالي التسليمات: {data.submissions?.length || 0}
                      </p>
                    </div>
                  </div>
                </div>
              ))
          ) : (
            <div className="text-center py-10 bg-[var(--admin-background)] rounded-xl border border-dashed border-[var(--admin-border)]">
              <p className="text-[var(--admin-muted)] font-bold text-sm mb-2">لا توجد أسئلة</p>
              <p className="text-xs text-[var(--admin-muted)] opacity-70 mb-4">لم يتم إدراج أي أسئلة للواجب حتى الآن.</p>
              <NeumorphButton
                type="button"
                onClick={() => router.push(`${homeworkBasePath}/${homeworkId}/add-question`)}
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

      {/* Student submissions */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
        <div className="mb-6 flex items-center justify-between gap-4">
          <h3 className="flex items-center gap-3 text-xl font-bold text-[var(--admin-text)]">
            <Users className="h-6 w-6 text-[var(--admin-primary)]" />
            تسليمات الطلاب
          </h3>
          <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-muted)]">
            {data.submissions?.length || 0} تسليم
          </span>
        </div>
        {data.submissions && data.submissions.length > 0 ? (
          <div className="overflow-x-auto rounded-2xl border border-[var(--admin-border)]">
            <table className="w-full min-w-[680px] text-right text-sm">
              <thead className="bg-[var(--admin-card-soft)] text-xs font-black text-[var(--admin-muted)]">
                <tr>
                  <th className="px-4 py-3">الطالب</th>
                  <th className="px-4 py-3">الحالة</th>
                  <th className="px-4 py-3">الدرجة</th>
                  <th className="px-4 py-3">التقييم</th>
                  <th className="px-4 py-3">تاريخ التسليم</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--admin-border)]">
                {data.submissions.map((submission) => (
                  <tr key={`${submission.studentId}-${submission.startedAt}`} className="text-[var(--admin-text)]">
                    <td className="px-4 py-4">
                      <p className="font-black">{submission.studentName}</p>
                      <p className="mt-1 text-xs text-[var(--admin-muted)]">{submission.studentPhone}</p>
                    </td>
                    <td className="px-4 py-4 font-bold">{submission.status}</td>
                    <td className="px-4 py-4 font-black">{submission.scoreAchieved}</td>
                    <td className="px-4 py-4 text-[var(--admin-muted)]">{submission.evaluation}</td>
                    <td className="px-4 py-4 text-[var(--admin-muted)]">
                      {submission.submittedAt
                        ? new Date(submission.submittedAt).toLocaleDateString('ar-EG')
                        : 'لم يتم التسليم'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-background)] px-5 py-8 text-center font-bold text-[var(--admin-muted)]">
            لا توجد تسليمات لهذا الواجب حتى الآن.
          </p>
        )}
      </div>
    </div>
  );
}
