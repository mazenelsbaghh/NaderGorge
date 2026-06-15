'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Folder, ChevronRight, BookOpenText, Video, Clock3, Layers, Users } from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminTabBar, AdminTab, ContentImageUpload, EntityOverviewDashboard, ContentSubscribersTab } from '@/components/admin';
import type { OverviewStat } from '@/components/admin';
import { ContentHierarchyPanel, HierarchyItem } from '@/components/admin/ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import { contentService, ContentSectionDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type ActiveTab = 'overview' | 'sections' | 'subscribers';

const TABS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'sections', label: 'الشهور / الأقسام', icon: Folder },
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

export default function TermProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('overview');
  const [term, setTerm] = useState<any>(null);
  const [termLoading, setTermLoading] = useState(true);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(true);
  const [sectionsError, setSectionsError] = useState(false);

  // Stats state
  const [stats, setStats] = useState<any>(null);
  const [statsLoading, setStatsLoading] = useState(true);

  const loadTerm = useCallback(async () => {
    try {
      const res = await adminService.getTermById(params.id);
      setTerm(res);
    } catch {
      toast.error('تعذر تحميل تفاصيل الترم');
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

  const loadStats = useCallback(async () => {
    try {
      setStatsLoading(true);
      const res = await adminService.getTermStats(params.id);
      setStats(res);
    } catch {
      // Stats endpoint may not exist yet
    } finally {
      setStatsLoading(false);
    }
  }, [params.id]);

  useEffect(() => { void loadTerm(); }, [loadTerm]);
  useEffect(() => { void loadSections(); }, [loadSections]);
  useEffect(() => { void loadStats(); }, [loadStats]);

  if (termLoading) {
    return (
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!term) {
    return (
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="الترم غير موجود">
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
    imageUrl: s.imageUrl,
    href: `/admin/content/sections/${s.id}`,
  }));

  // Build overview stats from API response
  const overviewStats: OverviewStat[] = [];
  if (stats) {
    overviewStats.push(
      { label: 'الأقسام', value: stats.sectionsCount ?? sections.length, icon: Layers, tone: 'muted' },
      { label: 'الحصص', value: stats.lessonsCount ?? 0, icon: BookOpenText, tone: 'muted' },
      { label: 'الفيديوهات', value: stats.videosCount ?? 0, icon: Video, tone: 'success' },
      { label: 'إجمالي المشاهدة', value: formatWatchTime(stats.totalWatchTimeSeconds), icon: Clock3, tone: 'warning' },
    );
  }

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الباقات ▸ الأترام"
      pageTitle={term.title}
      subtitle={`ترتيب: ${term.order} — ${sections.length} قسم`}
      action={
        <NeumorphButton onClick={() => router.push(`/admin/content/packages/${term.packageId}`)} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الباقة
        </NeumorphButton>
      }
    >
      {/* Always visible term image upload at the top */}
      <div className="mb-8 max-w-3xl">
        <ContentImageUpload
          entityId={term.id}
          contentType="term"
          imageUrl={term.imageUrl}
          label="صورة الترم"
          onUploaded={(imageUrl) => setTerm((current: any) => ({ ...current, imageUrl }))}
        />
      </div>

      <div className="mb-8">
        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {activeTab === 'overview' && (
        <div className="space-y-6">
          <EntityOverviewDashboard
            entityType="ترم"
            details={{ title: term.title, price: term.price }}
            stats={overviewStats}
            loading={statsLoading}
            onPriceUpdate={async (newPrice) => {
              await adminService.updateTerm(params.id, { title: term.title, order: term.order, price: newPrice });
              toast.success('تم تحديث السعر');
              setTerm((c: any) => ({ ...c, price: newPrice }));
            }}
          />
        </div>
      )}

      {activeTab === 'sections' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentHierarchyPanel
            label="الأقسام / الشهور"
            icon={<Folder className="h-5 w-5" />}
            items={sectionItems}
            loading={sectionsLoading}
            loadError={sectionsError}
            hasImage={true}
            emptyDescription="القسم (الشهر) يجمع مجموعة من الحصص تحت عنوان واحد. أضف القسم الأول لهذا الترم."
            addPlaceholder="اسم القسم، مثال: شهر أكتوبر..."
            onCreate={async ({ title, order, price, imageFile }) => {
              const sectionId = await adminService.createSection({ termId: params.id, title, order, price });
              if (imageFile && sectionId?.id) {
                await adminService.uploadContentImage('section', sectionId.id, imageFile);
              }
              toast.success('تمت إضافة القسم.');
              await loadSections();
            }}
            onImageUpload={async (id, file) => {
              await adminService.uploadContentImage('section', id, file);
              await loadSections();
            }}
            onRetry={loadSections}
          />
        </div>
      )}
      {activeTab === 'subscribers' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentSubscribersTab contentType="term" contentId={params.id} contentName={term.title} />
        </div>
      )}
    </AdminShellChrome>
  );
}
