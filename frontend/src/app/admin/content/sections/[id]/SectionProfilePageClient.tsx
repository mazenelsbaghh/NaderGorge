'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { BookOpenText, ChevronRight, Video, Clock3, Users } from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminTabBar, AdminTab, ContentImageUpload, EntityOverviewDashboard, ContentSubscribersTab } from '@/components/admin';
import type { OverviewStat } from '@/components/admin';
import { ContentHierarchyPanel, HierarchyItem } from '@/components/admin/ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import { contentService, LessonSummaryDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type ActiveTab = 'overview' | 'lessons' | 'subscribers';

const TABS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'lessons', label: 'الحصص', icon: BookOpenText },
  { key: 'subscribers', label: 'الطلاب المشتركين', icon: Users },
];

function formatWatchTime(seconds?: number): string {
  if (!seconds || seconds <= 0) return '0 دقيقة';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0 && minutes > 0) return `${hours} ساعة ${minutes} دقيقة`;
  if (hours > 0) return `${hours} ساعة`;
  return `${minutes} دقيقة`;
}

export default function SectionProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('overview');
  const [section, setSection] = useState<any>(null);
  const [sectionLoading, setSectionLoading] = useState(true);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [lessonsLoading, setLessonsLoading] = useState(true);
  const [lessonsError, setLessonsError] = useState(false);

  // Stats state
  const [stats, setStats] = useState<any>(null);
  const [statsLoading, setStatsLoading] = useState(true);

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

  const loadStats = useCallback(async () => {
    try {
      setStatsLoading(true);
      const res = await adminService.getSectionStats(params.id);
      setStats(res);
    } catch {
      // Stats endpoint may not exist yet
    } finally {
      setStatsLoading(false);
    }
  }, [params.id]);

  useEffect(() => { void loadSection(); }, [loadSection]);
  useEffect(() => { void loadLessons(); }, [loadLessons]);
  useEffect(() => { void loadStats(); }, [loadStats]);

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

  // Build overview stats from API response
  const overviewStats: OverviewStat[] = [];
  if (stats) {
    overviewStats.push(
      { label: 'الحصص', value: stats.lessonsCount ?? lessons.length, icon: BookOpenText, tone: 'muted' },
      { label: 'الفيديوهات', value: stats.videosCount ?? 0, icon: Video, tone: 'success' },
      { label: 'إجمالي المشاهدة', value: formatWatchTime(stats.totalWatchTimeSeconds), icon: Clock3, tone: 'warning' },
    );
  }

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
            stats={overviewStats}
            loading={statsLoading}
            onPriceUpdate={async (newPrice) => {
              await adminService.updateSection(params.id, { title: section.title, order: section.order, price: newPrice });
              toast.success('تم تحديث السعر');
              setSection((c: any) => ({ ...c, price: newPrice }));
            }}
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
      {activeTab === 'subscribers' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentSubscribersTab contentType="section" contentId={params.id} contentName={section.title} />
        </div>
      )}
    </AdminShellChrome>
  );
}
