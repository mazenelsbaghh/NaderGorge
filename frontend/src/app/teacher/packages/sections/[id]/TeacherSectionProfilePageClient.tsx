"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BookOpenText, ChevronRight } from "lucide-react";
import { AdminPageSkeleton, AdminTabBar, AdminTab, EntityOverviewDashboard } from "@/components/admin";
import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";
import { ContentHierarchyPanel, HierarchyItem } from "@/components/admin/ContentHierarchyPanel";
import { adminService } from "@/services/admin-service";
import { contentService, LessonSummaryDto } from "@/services/content-service";
import toast from "react-hot-toast";
import NeumorphButton from "@/components/ui/neumorph-button";

type ActiveTab = "overview" | "lessons";

const TABS: AdminTab<ActiveTab>[] = [
  { key: "overview", label: "نظرة عامة", icon: BookOpenText },
  { key: "lessons", label: "الحصص", icon: BookOpenText },
];

export default function TeacherSectionProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>("overview");
  const [section, setSection] = useState<any>(null);
  const [sectionLoading, setSectionLoading] = useState(true);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [lessonsLoading, setLessonsLoading] = useState(true);
  const [lessonsError, setLessonsError] = useState(false);

  const loadSection = useCallback(async () => {
    try {
      const res = await adminService.getSectionById(params.id);
      setSection(res);
    } catch {
      toast.error("تعذر تحميل تفاصيل القسم");
    } finally {
      setSectionLoading(false);
    }
  }, [params.id]);

  const loadLessons = useCallback(async () => {
    try {
      setLessonsLoading(true);
      setLessonsError(false);
      const res = await contentService.getLessons(params.id);
      const items = (res.data?.data ?? []) as LessonSummaryDto[];
      setLessons(items.sort((a, b) => a.order - b.order));
    } catch {
      setLessonsError(true);
    } finally {
      setLessonsLoading(false);
    }
  }, [params.id]);

  useEffect(() => { void loadSection(); }, [loadSection]);
  useEffect(() => { void loadLessons(); }, [loadLessons]);

  if (sectionLoading) {
    return (
      <TeacherShellChrome activePath="/teacher/packages" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </TeacherShellChrome>
    );
  }

  if (!section) {
    return (
      <TeacherShellChrome activePath="/teacher/packages" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="القسم غير موجود">
        <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
          <p className="text-[var(--admin-muted)]">لا يمكن العثور على القسم المطلوب.</p>
          <NeumorphButton onClick={() => router.back()} intent="ghost" size="md" pill>
            <ChevronRight className="h-4 w-4" /> عودة
          </NeumorphButton>
        </div>
      </TeacherShellChrome>
    );
  }

  const lessonItems: HierarchyItem[] = lessons.map((l) => ({
    id: l.id,
    title: l.title,
    order: l.order,
    price: l.price,
    subtitle: l.summary || undefined,
    href: `/teacher/packages/lessons/${l.id}`,
  }));

  return (
    <TeacherShellChrome
      activePath="/teacher/packages"
      sectionLabel="إدارة المحتوى ▸ الباقات ▸ الأترام ▸ الأقسام"
      pageTitle={section.title}
      subtitle={`ترتيب: ${section.order} — ${lessons.length} حصة`}
      action={
        <NeumorphButton onClick={() => router.push(`/teacher/packages/terms/${section.termId}`)} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الترم
        </NeumorphButton>
      }
    >
      <div className="mb-8">
        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === "overview" && (
        <EntityOverviewDashboard 
          entityType="قسم" 
          details={{ title: section.title, price: section.price }} 
        />
      )}

      {activeTab === "lessons" && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentHierarchyPanel
            label="الحصص"
            icon={<BookOpenText className="h-5 w-5" />}
            items={lessonItems}
            loading={lessonsLoading}
            loadError={lessonsError}
            emptyDescription="الحصة تحتوي على الفيديوهات والواجبات والامتحانات. أضف الحصة الأولى لهذا القسم."
            addPlaceholder="عنوان الحصة، مثال: مقدمة في التاريخ الفرعوني..."
            hasSummary
            onCreate={async ({ title, order, price, summary }) => {
              await adminService.createLesson({ sectionId: params.id, title, summary: summary ?? "", order, price });
              toast.success("تمت إضافة الحصة.");
              await loadLessons();
            }}
            onRetry={loadLessons}
          />
        </div>
      )}
    </TeacherShellChrome>
  );
}
