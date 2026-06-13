'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { BookOpenText, ChevronRight } from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminTabBar, AdminTab, ContentImageUpload, EntityOverviewDashboard } from '@/components/admin';
import { ContentHierarchyPanel, HierarchyItem } from '@/components/admin/ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import { contentService, LessonSummaryDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type ActiveTab = 'overview' | 'lessons';

const TABS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'lessons', label: 'الحصص', icon: BookOpenText },
];

export default function SectionProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('overview');
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
      toast.error('تعذر تحميل تفاصيل القسم');
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
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!section) {
    return (
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="القسم غير موجود">
        <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
          <p className="text-[var(--admin-muted)]">لا يمكن العثور على القسم المطلوب.</p>
          <NeumorphButton onClick={() => router.back()} intent="ghost" size="md" pill>
            <ChevronRight className="h-4 w-4" /> عودة
          </NeumorphButton>
        </div>
      </AdminShellChrome>
    );
  }

  const lessonItems: HierarchyItem[] = lessons.map((l) => ({
    id: l.id,
    title: l.title,
    order: l.order,
    price: l.price,
    subtitle: l.summary || undefined,
    href: `/admin/content/lessons/${l.id}`,
  }));

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الباقات ▸ الأترام ▸ الأقسام"
      pageTitle={section.title}
      subtitle={`ترتيب: ${section.order} — ${lessons.length} حصة`}
      action={
        <NeumorphButton onClick={() => router.push(`/admin/content/terms/${section.termId}`)} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الترم
        </NeumorphButton>
      }
    >
      {/* Always visible section image upload at the top */}
      <div className="mb-8 max-w-3xl">
        <ContentImageUpload
          entityId={section.id}
          contentType="section"
          imageUrl={section.imageUrl}
          label="صورة الشهر / القسم"
          onUploaded={(imageUrl) => setSection((current: any) => ({ ...current, imageUrl }))}
        />
      </div>

      <div className="mb-8">
        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === 'overview' && (
        <div className="space-y-6">
          <EntityOverviewDashboard
            entityType="قسم"
            details={{ title: section.title, price: section.price }}
          />
        </div>
      )}

      {activeTab === 'lessons' && (
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
              await adminService.createLesson({ sectionId: params.id, title, summary: summary ?? '', order, price });
              toast.success('تمت إضافة الحصة.');
              await loadLessons();
            }}
            onRetry={loadLessons}
          />
        </div>
      )}
    </AdminShellChrome>
  );
}
