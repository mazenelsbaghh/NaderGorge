"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowRight, BookOpenText, PlaySquare, FileText, ClipboardList, BookCheck, MessageSquareText } from "lucide-react";
import { AdminStatCard, AdminTabBar, AdminTab, AddVideoForm, LessonVideoList, AddResourceForm, LessonResourceList, LessonHomeworkList, UnifiedAssessmentBuilder, AdminPageSkeleton, LessonCommentsModerationTab, EntityOverviewDashboard, AttachedExamViewer } from "@/components/admin";
import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";
import { adminService, type LessonCockpitDto } from "@/services/admin-service";
import toast from "react-hot-toast";

type ActiveTab = "overview" | "videos" | "resources" | "homework" | "exam" | "comments";

const TAB_OPTIONS: AdminTab<ActiveTab>[] = [
  { key: "overview", label: "نظرة عامة", icon: BookOpenText },
  { key: "videos", label: "الفيديوهات", icon: PlaySquare },
  { key: "comments", label: "التعليقات", icon: MessageSquareText },
  { key: "resources", label: "المذكرات والملفات", icon: FileText },
  { key: "homework", label: "الواجبات", icon: ClipboardList },
  { key: "exam", label: "الامتحان المرفق", icon: BookCheck },
];

export default function TeacherLessonProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>("overview");
  const [lesson, setLesson] = useState<LessonCockpitDto | null>(null);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await adminService.getLessonCockpit(params.id);
      setLesson(res.data?.data);
    } catch {
      toast.error("تعذر تحميل بيانات الحصة");
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  if (loading) {
    return (
      <TeacherShellChrome
        activePath="/teacher/packages"
        sectionLabel="إدارة المحتوى"
        pageTitle="جاري التحميل..."
        subtitle="الرجاء الانتظار"
      >
        <AdminPageSkeleton />
      </TeacherShellChrome>
    );
  }

  if (!lesson) {
     return (
        <TeacherShellChrome
            activePath="/teacher/packages"
            sectionLabel="إدارة المحتوى"
            pageTitle="خطأ"
            subtitle="الحصة غير موجودة"
        >
            <div className="p-8 text-center text-[var(--admin-muted)]">
                لا يمكن العثور على الحصة المطلوبة
            </div>
        </TeacherShellChrome>
     )
  }

  return (
    <TeacherShellChrome
      activePath="/teacher/packages"
      sectionLabel="إدارة المحتوى ▸ الحصص"
      pageTitle={lesson.title}
      subtitle={lesson.summary || "إدارة محتويات وإعدادات الحصة"}
      action={
        <button
          onClick={() => router.back()}
          className="inline-flex w-fit items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)] transition hover:bg-[var(--admin-hover)]"
        >
          <ArrowRight className="h-4 w-4" /> عودة للقائمة
        </button>
      }
    >
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-4">
        <AdminStatCard variant="accent" icon={BookOpenText} label="معرف الحصة" value={lesson.lessonId.split("-")[0]} />
        <AdminStatCard variant="light" icon={PlaySquare} label="الفيديوهات" value={`${lesson.videos?.length || 0}`} />
        <AdminStatCard variant="muted" icon={MessageSquareText} label="تعليقات قيد المراجعة" value={`${lesson.commentsSummary?.pending || 0}`} />
        <AdminStatCard variant="accent" icon={ClipboardList} label="الواجبات" value={`${lesson.homework?.length || 0}`} />
      </section>

      <div className="mb-8">
        <AdminTabBar tabs={TAB_OPTIONS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === "overview" && (
        <EntityOverviewDashboard 
          entityType="حصة" 
          details={{ title: lesson.title, description: lesson.summary }} 
        />
      )}
      
      {activeTab === "videos" && (
        <div className="space-y-6">
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة فيديو جديد</h3>
            <AddVideoForm lessonId={lesson.lessonId} onSuccess={loadData} />
          </div>
          
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)]">الفيديوهات المرفقة ({lesson.videos?.length || 0})</h3>
            <LessonVideoList videos={lesson.videos || []} lessonId={lesson.lessonId} onRefresh={loadData} />
          </div>
        </div>
      )}

      {activeTab === "comments" && (
        <LessonCommentsModerationTab
          lessonId={lesson.lessonId}
          pendingCount={lesson.commentsSummary?.pending || 0}
          onRefresh={loadData}
        />
      )}

      {activeTab === "resources" && (
        <div className="space-y-6">
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة ملف أو مذكرة</h3>
            <AddResourceForm lessonId={lesson.lessonId} onSuccess={loadData} />
          </div>
          
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)]">الملفات المرفقة ({lesson.resources?.length || 0})</h3>
            <LessonResourceList resources={lesson.resources || []} />
          </div>
        </div>
      )}

      {activeTab === "homework" && (
        <div className="space-y-6">
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-4 text-xl font-bold text-[var(--admin-text)]">إضافة واجب جديد</h3>
            <UnifiedAssessmentBuilder type="homework" lessonId={lesson.lessonId} onSuccess={loadData} />
          </div>
          
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)]">الواجبات المرفقة ({lesson.homework?.length || 0})</h3>
            <LessonHomeworkList homework={lesson.homework || []} />
          </div>
        </div>
      )}

      {activeTab === "exam" && (
        <div className="space-y-6 animate-in slide-in-from-bottom-2 fade-in">
          {lesson.examId ? (
            <AttachedExamViewer examId={lesson.examId} />
          ) : (
            <>
              <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
                <h3 className="mb-6 text-xl font-bold text-[var(--admin-text)] flex items-center gap-3">
                  <BookCheck className="h-6 w-6 text-[var(--admin-primary)]" />
                  إنشاء امتحان مدمج
                </h3>
                <UnifiedAssessmentBuilder type="exam" lessonId={lesson.lessonId} videos={lesson.videos || []} onSuccess={loadData} />
              </div>
            </>
          )}
        </div>
      )}
    </TeacherShellChrome>
  );
}
