"use client";

import { use, useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Folder, ChevronRight, BookOpenText } from "lucide-react";
import { AdminShellChrome, AdminPageSkeleton, AdminTabBar, AdminTab, EntityOverviewDashboard } from "@/components/admin";
import { ContentHierarchyPanel, HierarchyItem } from "@/components/admin/ContentHierarchyPanel";
import { adminService } from "@/services/admin-service";
import { contentService, ContentSectionDto } from "@/services/content-service";
import toast from "react-hot-toast";
import NeumorphButton from "@/components/ui/neumorph-button";

type ActiveTab = "overview" | "sections";

const TABS: AdminTab<ActiveTab>[] = [
  { key: "overview", label: "نظرة عامة", icon: BookOpenText },
  { key: "sections", label: "الشهور / الأقسام", icon: Folder },
];

export default function TeacherTermProfilePage(props: { params: Promise<{ id: string }> }) {
  const params = use(props.params);
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>("overview");
  const [term, setTerm] = useState<any>(null);
  const [termLoading, setTermLoading] = useState(true);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(true);
  const [sectionsError, setSectionsError] = useState(false);

  const loadTerm = useCallback(async () => {
    try {
      const res = await adminService.getTermById(params.id);
      setTerm(res);
    } catch {
      toast.error("تعذر تحميل تفاصيل الترم");
    } finally {
      setTermLoading(false);
    }
  }, [params.id]);

  const loadSections = useCallback(async () => {
    try {
      setSectionsLoading(true);
      setSectionsError(false);
      const res = await contentService.getSections(params.id);
      const items = (res.data?.data ?? []) as ContentSectionDto[];
      setSections(items.sort((a, b) => a.order - b.order));
    } catch {
      setSectionsError(true);
    } finally {
      setSectionsLoading(false);
    }
  }, [params.id]);

  useEffect(() => { void loadTerm(); }, [loadTerm]);
  useEffect(() => { void loadSections(); }, [loadSections]);

  if (termLoading) {
    return (
      <AdminShellChrome activePath="/teacher/packages" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!term) {
    return (
      <AdminShellChrome activePath="/teacher/packages" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="الترم غير موجود">
        <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
          <p className="text-[var(--admin-muted)]">لا يمكن العثور على الترم المطلوب.</p>
          <NeumorphButton onClick={() => router.back()} intent="ghost" size="md" pill>
            <ChevronRight className="h-4 w-4" /> عودة
          </NeumorphButton>
        </div>
      </AdminShellChrome>
    );
  }

  const sectionItems: HierarchyItem[] = sections.map((s) => ({
    id: s.id,
    title: s.title,
    order: s.order,
    price: s.price,
    href: `/teacher/packages/sections/${s.id}`,
  }));

  return (
    <AdminShellChrome
      activePath="/teacher/packages"
      sectionLabel="إدارة المحتوى ▸ الباقات ▸ الأترام"
      pageTitle={term.title}
      subtitle={`ترتيب: ${term.order} — ${sections.length} قسم`}
      action={
        <NeumorphButton onClick={() => router.push(`/teacher/packages/packages/${term.packageId}`)} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الباقة
        </NeumorphButton>
      }
    >
      <div className="mb-8">
        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === "overview" && (
        <EntityOverviewDashboard 
          entityType="ترم" 
          details={{ title: term.title, price: term.price }} 
        />
      )}

      {activeTab === "sections" && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentHierarchyPanel
            label="الأقسام / الشهور"
            icon={<Folder className="h-5 w-5" />}
            items={sectionItems}
            loading={sectionsLoading}
            loadError={sectionsError}
            emptyDescription="القسم (الشهر) يجمع مجموعة من الحصص تحت عنوان واحد. أضف القسم الأول لهذا الترم."
            addPlaceholder="اسم القسم، مثال: شهر أكتوبر..."
            onCreate={async ({ title, order, price }) => {
              await adminService.createSection({ termId: params.id, title, order, price });
              toast.success("تمت إضافة القسم.");
              await loadSections();
            }}
            onRetry={loadSections}
          />
        </div>
      )}
    </AdminShellChrome>
  );
}
