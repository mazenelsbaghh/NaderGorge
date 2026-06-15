"use client";

import { useState, useEffect } from "react";
import { GraduationCap, FileText, Calendar, CheckCircle2, User, Loader2, Sparkles, Star } from "lucide-react";
import { 
  AdminDataTable, 
  AdminColumn, 
  AdminStatCard, 
  AdminSearchToolbar, 
  AdminPageSkeleton,
  AdminModal,
} from "@/components/admin";
import { teacherService, PendingEssayDto } from "@/services/teacher-service";
import toast from "react-hot-toast";
import { sanitizeRichHtml } from '@/lib/sanitize-html';

import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";

export default function TeacherEssaysPageClient() {
  const [essays, setEssays] = useState<PendingEssayDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");

  // Grading Modal State
  const [selectedEssay, setSelectedEssay] = useState<PendingEssayDto | null>(null);
  const [score, setScore] = useState<number | "">("");
  const [feedback, setFeedback] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const fetchEssays = () => {
    setLoading(true);
    teacherService.getEssays()
      .then((res) => {
        if (res.success) {
          setEssays(res.data || []);
        }
      })
      .catch((err) => {
        console.error("Error fetching essays:", err);
        toast.error("فشل في تحميل الإجابات المقالية");
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchEssays();
  }, []);

  const handleOpenGrading = (essay: PendingEssayDto) => {
    setSelectedEssay(essay);
    setScore(essay.aiInitialScore !== undefined && essay.aiInitialScore !== null ? Number(essay.aiInitialScore) : "");
    setFeedback(essay.aiFeedback || "");
  };

  const handleCloseGrading = () => {
    setSelectedEssay(null);
    setScore("");
    setFeedback("");
  };

  const handleSubmitGrade = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedEssay) return;

    const numericScore = Number(score);
    if (isNaN(numericScore) || numericScore < 0 || numericScore > selectedEssay.maxPoints) {
      toast.error(`يجب أن تكون الدرجة بين 0 و ${selectedEssay.maxPoints}`);
      return;
    }

    setSubmitting(true);
    try {
      const res = await teacherService.gradeEssay(selectedEssay.id, {
        score: numericScore,
        feedback,
      });

      if (res.success) {
        toast.success("تم تسجيل التقييم بنجاح");
        handleCloseGrading();
        fetchEssays();
      } else {
        toast.error(res.message || "حدث خطأ أثناء تسجيل التقييم");
      }
    } catch (err) {
      console.error(err);
      toast.error("فشل في إرسال التقييم");
    } finally {
      setSubmitting(false);
    }
  };

  const filteredEssays = essays.filter((e) => {
    const query = searchQuery.toLowerCase();
    return (
      e.studentName.toLowerCase().includes(query) ||
      e.examTitle.toLowerCase().includes(query) ||
      e.questionText.toLowerCase().includes(query)
    );
  });

  const columns: AdminColumn<PendingEssayDto>[] = [
    {
      key: "student",
      label: "الطالب والامتحان",
      render: (e) => (
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] shadow-sm">
            <User className="h-4 w-4" />
          </div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{e.studentName}</div>
            <div className="text-xs text-[var(--admin-muted)] mt-0.5">{e.examTitle}</div>
          </div>
        </div>
      ),
    },
    {
      key: "question",
      label: "السؤال المقالي",
      render: (e) => (
        <div className="max-w-[300px] truncate text-sm text-[var(--admin-text)] font-semibold">
          {e.questionText}
        </div>
      ),
    },
    {
      key: "maxPoints",
      label: "الدرجة القصوى",
      render: (e) => (
        <span className="text-sm font-bold text-[var(--admin-text)]">
          {e.maxPoints} درجات
        </span>
      ),
    },
    {
      key: "aiScore",
      label: "التقييم الأولي للذكاء الاصطناعي",
      render: (e) => (
        <div className="flex items-center gap-1.5">
          {e.aiInitialScore !== undefined && e.aiInitialScore !== null ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/10 px-2.5 py-0.5 text-xs font-bold text-amber-500 border border-amber-500/20">
              <Star className="h-3 w-3 fill-amber-500" />
              {e.aiInitialScore} / {e.maxPoints}
            </span>
          ) : (
            <span className="text-xs text-[var(--admin-muted)]">غير متوفر</span>
          )}
        </div>
      ),
    },
    {
      key: "submittedAt",
      label: "تاريخ التسليم",
      render: (e) => {
        const date = new Date(e.submittedAt);
        return (
          <div className="flex items-center gap-1.5 text-xs text-[var(--admin-muted)]">
            <Calendar className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
            <span>{date.toLocaleDateString("ar-EG", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}</span>
          </div>
        );
      },
    },
    {
      key: "actions",
      label: "الإجراءات",
      render: (e) => (
        <button
          onClick={() => handleOpenGrading(e)}
          className="inline-flex items-center gap-1.5 rounded-xl bg-[var(--admin-primary)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary-contrast)] shadow-md transition hover:scale-[1.03] active:scale-[0.97]"
        >
          <GraduationCap className="h-3.5 w-3.5" />
          بدء التصحيح
        </button>
      ),
    },
  ];

  return (
    <TeacherShellChrome
      activePath="/teacher/essays"
      sectionLabel="تصحيح المقالي"
      pageTitle="مساحة تصحيح الإجابات المقالية"
      subtitle="قم بمراجعة وتصحيح إجابات الطلاب المقالية وتعديل تقييم الذكاء الاصطناعي الأولي وكتابة ملاحظات التوجيه."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Stats Strip */}
        <section className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <AdminStatCard
            variant="accent"
            icon={GraduationCap}
            label="إجابات تحتاج تصحيح"
            value={essays.length}
            subtitle="إجمالي الإجابات المقالية المعلقة"
          />
          <AdminStatCard
            variant="light"
            icon={CheckCircle2}
            label="حالة التقييم"
            value={essays.length === 0 ? "مكتمل" : "مستمر"}
            subtitle="متابعة تصحيح اختبارات الطلاب أولاً بأول"
          />
        </section>

        {/* Search Bar */}
        <AdminSearchToolbar
          value={searchQuery}
          onChange={setSearchQuery}
          placeholder="ابحث عن إجابة طالب بالاسم، الامتحان، أو نص السؤال..."
        />

        {loading ? (
          <AdminPageSkeleton />
        ) : (
          <AdminDataTable
            data={filteredEssays}
            columns={columns}
            loading={loading}
            rowKey={(e) => e.id}
            emptyMessage="رائع! لا توجد إجابات مقالية بانتظار تصحيحك في الوقت الحالي."
          />
        )}

        {/* Grading Modal */}
        <AdminModal
          open={selectedEssay !== null}
          onClose={handleCloseGrading}
          title={selectedEssay ? `تصحيح إجابة الطالب: ${selectedEssay.studentName}` : 'تصحيح إجابة مقالية'}
          subtitle={selectedEssay?.examTitle}
          maxWidth="max-w-3xl"
        >
          {selectedEssay ? (
            <div dir="rtl">
              <div className="mb-3 flex items-center gap-2 text-xs font-black text-[var(--admin-primary)]">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                <span>تقييم إجابة مقالية</span>
              </div>
              <form onSubmit={handleSubmitGrade} className="mt-6 space-y-6">
                {/* Question Text */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
                  <h4 className="text-xs font-black text-[var(--admin-muted)] mb-2">السؤال المطروح:</h4>
                  <p className="text-sm font-semibold text-[var(--admin-text)]">{selectedEssay.questionText}</p>
                </div>

                {/* Student's Answer */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
                  <h4 className="text-xs font-black text-[var(--admin-muted)] mb-2">إجابة الطالب:</h4>
                  <div className="text-sm text-[var(--admin-text)] bg-[var(--admin-bg)] p-4 rounded-xl border border-[var(--admin-border)]/5 leading-relaxed font-mono" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(selectedEssay.answerText || "لم يكتب الطالب إجابة نصية.") }} />

                  {selectedEssay.audioUrl && (
                    <div className="mt-4 p-3 rounded-xl bg-[var(--admin-primary-15)] flex flex-col gap-2">
                      <h5 className="text-xs font-black text-[var(--admin-primary)]">الإجابة الصوتية المرفقة:</h5>
                      <audio src={selectedEssay.audioUrl} controls className="w-full mt-1" />
                    </div>
                  )}
                </div>

                {/* AI Suggestion */}
                {selectedEssay.aiInitialScore !== undefined && selectedEssay.aiInitialScore !== null && (
                  <div className="rounded-2xl border border-amber-500/20 bg-amber-500/5 p-5">
                    <div className="flex items-center gap-1.5 text-amber-500 font-bold text-xs mb-2">
                      <Star className="h-4 w-4 fill-amber-500" />
                      <span>التقييم المقترح بواسطة الذكاء الاصطناعي:</span>
                      <span className="font-mono text-sm ml-1">{selectedEssay.aiInitialScore} / {selectedEssay.maxPoints}</span>
                    </div>
                    {selectedEssay.aiFeedback && (
                      <p className="text-xs text-amber-600 dark:text-amber-400 bg-amber-500/10 p-3 rounded-lg border border-amber-500/10 leading-relaxed">
                        {selectedEssay.aiFeedback}
                      </p>
                    )}
                  </div>
                )}

                {/* Grading Input Fields */}
                <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                  <div className="md:col-span-1 space-y-2">
                    <label htmlFor="score" className="block text-sm font-bold text-[var(--admin-text)]">
                      الدرجة المستحقة (من {selectedEssay.maxPoints})
                    </label>
                    <input
                      id="score"
                      type="number"
                      step="0.1"
                      min="0"
                      max={selectedEssay.maxPoints}
                      value={score}
                      onChange={(e) => setScore(e.target.value === "" ? "" : Number(e.target.value))}
                      className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] font-mono text-center text-lg"
                      required
                    />
                  </div>

                  <div className="md:col-span-2 space-y-2">
                    <label htmlFor="feedback" className="block text-sm font-bold text-[var(--admin-text)]">
                      ملاحظات المعلم وتقييمه
                    </label>
                    <textarea
                      id="feedback"
                      rows={3}
                      value={feedback}
                      onChange={(e) => setFeedback(e.target.value)}
                      placeholder="اكتب التوجيهات أو أسباب حسم الدرجات لتظهر للطالب..."
                      className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] resize-none"
                    />
                  </div>
                </div>

                {/* Submit Buttons */}
                <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
                  <button
                    type="button"
                    onClick={handleCloseGrading}
                    className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-5 py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="inline-flex items-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-lg transition hover:bg-[var(--admin-primary)]/90 hover:scale-[1.02] disabled:opacity-50 disabled:pointer-events-none"
                  >
                    {submitting ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        جاري الحفظ...
                      </>
                    ) : (
                      <>
                        <FileText className="h-4 w-4" />
                        اعتماد ورصد الدرجة
                      </>
                    )}
                  </button>
                </div>
              </form>
            </div>
          ) : null}
        </AdminModal>
      </div>
    </TeacherShellChrome>
  );
}
