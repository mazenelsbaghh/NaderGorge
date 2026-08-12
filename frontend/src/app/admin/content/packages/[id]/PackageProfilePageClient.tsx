'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Calendar, KeyRound, BookOpenText, Link2, ChevronRight, Users, Video, Clock3, DollarSign, Layers, Eye, EyeOff } from 'lucide-react';
import {
  AdminPage, AdminStatCard, AdminTabBar, AdminTab,
  PackageDetailsForm, PackageCodeProfileForm, EntityOverviewDashboard,
  AdminPageSkeleton, ContentHierarchyPanel,
  PackageCodeProfileSummary, ContentImageUpload,
  ContentSubscribersTab, PackageDirectContentPanel
} from '@/components/admin';
import type { OverviewStat } from '@/components/admin';
import { HierarchyItem } from '@/components/admin/ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import { contentService, TermDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type ActiveTab = 'overview' | 'terms' | 'direct' | 'subscribers' | 'codeProfile';

const TABS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'terms', label: 'الأترام', icon: Calendar },
  { key: 'direct', label: 'المحتوى المباشر', icon: Layers },
  { key: 'subscribers', label: 'الطلاب المشتركين', icon: Users },
  { key: 'codeProfile', label: 'صفحة الأكواد', icon: KeyRound },
];

function getPackageTabs(contentMode: string): AdminTab<ActiveTab>[] {
  return contentMode === 'TermWithSections'
    ? TABS
    : TABS.filter((tab) => tab.key !== 'terms');
}

function formatWatchTime(seconds?: number): string {
  if (!seconds || seconds <= 0) return '0 دقيقة';
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours > 0 && minutes > 0) return `${hours} ساعة ${minutes} دقيقة`;
  if (hours > 0) return `${hours} ساعة`;
  return `${minutes} دقيقة`;
}

export default function PackageProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('overview');
  const [pkg, setPkg] = useState<any>(null);
  const [pkgLoading, setPkgLoading] = useState(true);
  const [codeProfileSummary, setCodeProfileSummary] = useState<PackageCodeProfileSummary | null>(null);

  // Terms state
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [termsLoading, setTermsLoading] = useState(true);
  const [termsError, setTermsError] = useState(false);

  // Stats state
  const [stats, setStats] = useState<any>(null);
  const [statsLoading, setStatsLoading] = useState(true);
  const [togglingActive, setTogglingActive] = useState(false);
  const packageContentMode = pkg?.contentMode;

  const loadPkg = useCallback(async () => {
    try {
      const res = await adminService.getPackageById(params.id);
      setPkg(res);
    } catch {
      toast.error('تعذر تحميل تفاصيل الباقة');
    } finally {
      setPkgLoading(false);
    }
  }, [params.id]);

  const loadTerms = useCallback(async () => {
    try {
      setTermsLoading(true);
      setTermsError(false);
      const res = await contentService.getTerms(params.id);
      const items = (res.data?.data ?? []) as TermDto[];
      setTerms(items.sort((a, b) => a.order - b.order));
    } catch {
      setTermsError(true);
    } finally {
      setTermsLoading(false);
    }
  }, [params.id]);

  const loadStats = useCallback(async () => {
    try {
      setStatsLoading(true);
      const res = await adminService.getPackageStats(params.id);
      setStats(res);
    } catch {
      // Stats endpoint may not exist yet — gracefully degrade
    } finally {
      setStatsLoading(false);
    }
  }, [params.id]);

  useEffect(() => { void loadPkg(); }, [loadPkg]);
  useEffect(() => {
    if (pkgLoading) return;
    if (packageContentMode === 'TermWithSections' || packageContentMode == null) {
      void loadTerms();
    }
  }, [loadTerms, packageContentMode, pkgLoading]);
  useEffect(() => { void loadStats(); }, [loadStats]);

  const archivePackage = async () => {
    if (!pkg || togglingActive) return;
    if (!window.confirm(`ستُؤرشف الباقة "${pkg.name}". لن تظهر للطلاب الجدد، مع الاحتفاظ بكل المحتوى والاشتراكات الحالية.`)) return;
    setTogglingActive(true);
    try {
      await adminService.updatePackage(pkg.id, {
        name: pkg.name,
        description: pkg.description,
        price: pkg.price,
        isActive: false,
      });
      setPkg((currentPackage: any) => ({ ...currentPackage, isActive: false }));
      toast.success('تمت أرشفة الباقة.');
    } catch {
      toast.error('تعذر أرشفة الباقة');
    } finally {
      setTogglingActive(false);
    }
  };

  const restorePackage = async () => {
    if (!pkg || togglingActive) return;
    setTogglingActive(true);
    try {
      await adminService.updatePackage(pkg.id, { name: pkg.name, description: pkg.description, price: pkg.price, isActive: true });
      setPkg((currentPackage: any) => ({ ...currentPackage, isActive: true }));
      toast.success('تمت استعادة الباقة وظهرت للطلاب.');
    } catch {
      toast.error('تعذر استعادة الباقة');
    } finally {
      setTogglingActive(false);
    }
  };

  if (pkgLoading) {
    return (
      <AdminPage activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </AdminPage>
    );
  }

  if (!pkg) {
    return (
      <AdminPage activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="الباقة غير موجودة">
        <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
          <p className="text-[var(--admin-muted)]">لا يمكن العثور على الباقة المطلوبة.</p>
          <NeumorphButton onClick={() => router.push('/admin/content')} intent="ghost" size="md" pill>
            <ChevronRight className="h-4 w-4" /> عودة للباقات
          </NeumorphButton>
        </div>
      </AdminPage>
    );
  }

  const termItems: HierarchyItem[] = terms.map((t) => ({
    id: t.id,
    title: t.title,
    order: t.order,
    price: t.price,
    imageUrl: t.imageUrl,
    href: `/admin/content/terms/${t.id}`,
  }));
  const contentMode = packageContentMode ?? 'TermWithSections';
  const packageTabs = getPackageTabs(contentMode);
  const directLessons = pkg.directLessons ?? [];

  // Build overview stats from API response
  const overviewStats: OverviewStat[] = [];
  if (stats) {
    overviewStats.push(
      { label: 'طلاب اقتنوا محتوى داخل الباقة (تاريخي)', value: stats.enrolledStudentsCount ?? 0, icon: Users, tone: 'primary' },
      contentMode === 'TermWithSections'
        ? { label: 'الأترام', value: stats.termsCount ?? terms.length, icon: Calendar, tone: 'muted' }
        : { label: 'الحصص', value: directLessons.length, icon: BookOpenText, tone: 'muted' },
      { label: 'الأقسام', value: stats.sectionsCount ?? 0, icon: Layers, tone: 'muted' },
      { label: 'الحصص', value: stats.lessonsCount ?? 0, icon: BookOpenText, tone: 'muted' },
      { label: 'الفيديوهات', value: stats.videosCount ?? 0, icon: Video, tone: 'success' },
      { label: 'إجمالي المشاهدة', value: formatWatchTime(stats.totalWatchTimeSeconds), icon: Clock3, tone: 'warning' },
      { label: 'الإيرادات', value: stats.totalRevenue != null ? `${stats.totalRevenue} ج.م` : '—', icon: DollarSign, tone: 'primary' },
    );
  }

  return (
    <AdminPage
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الباقات"
      pageTitle={pkg.name}
      subtitle={pkg.description || 'إدارة محتويات وإعدادات الباقة'}
      action={
        <NeumorphButton onClick={() => router.back()} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الرجوع خطوة
        </NeumorphButton>
      }
    >
      {/* Always visible package image upload at the top */}
      <div className="mb-8 max-w-3xl">
        <ContentImageUpload
          entityId={pkg.id}
          contentType="package"
          imageUrl={pkg.imageUrl}
          label="صورة الباقة"
          onUploaded={(imageUrl) => setPkg((current: any) => ({ ...current, imageUrl }))}
        />
      </div>

      {/* Stats */}
      <div className="mb-10 grid grid-cols-2 gap-4 md:grid-cols-4">
        <button
          type="button"
          onClick={pkg.isActive === false ? restorePackage : archivePackage}
          disabled={togglingActive}
          className={`rounded-2xl border p-4 text-center transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:brightness-95 active:scale-[0.98] cursor-pointer ${
            pkg.isActive !== false
              ? 'border-emerald-200 bg-emerald-50 dark:border-emerald-800/40 dark:bg-emerald-950/30'
              : 'border-slate-300 bg-slate-100 dark:border-slate-700 dark:bg-slate-900/30'
          } ${togglingActive ? 'opacity-50' : ''}`}
        >
          <div className="flex items-center justify-center gap-2">
            {pkg.isActive !== false
              ? <Eye className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
              : <EyeOff className="h-5 w-5 text-slate-600 dark:text-slate-400" />
            }
            <span className={`text-lg font-black ${pkg.isActive !== false ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-600 dark:text-slate-400'}`}>
              {pkg.isActive !== false ? 'نشطة' : 'مؤرشفة'}
            </span>
          </div>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
            {pkg.isActive !== false ? 'ظاهرة للطلاب — اضغط للأرشفة' : 'مخفية عن الطلاب — اضغط للاستعادة'}
          </p>
        </button>
        <AdminStatCard
          variant="light"
          icon={contentMode === 'TermWithSections' ? Calendar : BookOpenText}
          label={contentMode === 'TermWithSections' ? 'عدد الأترام' : 'عدد الحصص'}
          value={contentMode === 'TermWithSections' ? terms.length : directLessons.length}
        />
        <AdminStatCard variant="muted"  icon={Link2}         label="السعر"        value={`${pkg.price} ج`} />
        <AdminStatCard
          variant="light"
          icon={KeyRound}
          label="صفحة الأكواد"
          value={
            codeProfileSummary?.isUsingFallback ? 'افتراضية'
            : codeProfileSummary?.status === 'Published' ? 'منشورة'
            : codeProfileSummary?.status === 'Draft' ? 'مسودة'
            : 'افتراضية'
          }
        />
      </div>

      {/* Tabs */}
      <div className="mb-8">
        <AdminTabBar tabs={packageTabs} activeTab={activeTab} onSelect={setActiveTab} />
      </div>

      {/* Terms tab — uses shared ContentHierarchyPanel */}
      {activeTab === 'terms' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentHierarchyPanel
            label="الأترام"
            icon={<Calendar className="h-5 w-5" />}
            items={termItems}
            loading={termsLoading}
            loadError={termsError}
            hasImage={true}
            emptyDescription="الترم هو الوحدة الكبرى التي تجمع الأقسام والدروس. أضف الترم الأول لهذه الباقة."
            addPlaceholder="اسم الترم، مثال: الفصل الدراسي الأول..."
            onCreate={async ({ title, order, price, imageFile }) => {
              const termId = await adminService.createTerm({ packageId: params.id, title, order, price });
              if (imageFile && termId) {
                await adminService.uploadContentImage('term', termId, imageFile);
              }
              toast.success('تمت إضافة الترم.');
              await loadTerms();
            }}
            onUpdate={async (id, { title, order, price }) => {
              await adminService.updateTerm(id, { title, order, price });
              toast.success('تم تحديث الترم.');
              await loadTerms();
            }}
            onImageUpload={async (id, file) => {
              await adminService.uploadContentImage('term', id, file);
              await loadTerms();
            }}
            onDelete={async (id) => {
              await adminService.deleteTerm(id);
              toast.success('تم حذف الترم.');
              await loadTerms();
            }}
            deleteConfirmText={(item) => `سيتم حذف الترم "${item.title}" وجميع أقسامه ودروسه وفيديوهاته بشكل دائم.`}
            onRetry={loadTerms}
          />
        </div>
      )}

      {activeTab === 'direct' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <div className="mb-6">
            <h3 className="text-xl font-black text-[var(--admin-text)]">محتوى الكورس المباشر</h3>
            <p className="mt-2 text-sm text-[var(--admin-muted)]">
              أضف الأقسام أو الحصص مباشرة حسب نوع الكورس المختار عند إنشائه.
            </p>
          </div>
          <PackageDirectContentPanel
            packageId={params.id}
            mode={contentMode}
            rootTermId={pkg.rootTermId}
            rootSectionId={pkg.rootSectionId}
            sections={pkg.directSections}
            lessons={pkg.directLessons}
            basePath="/admin/content"
            onChanged={loadPkg}
          />
        </div>
      )}

      {activeTab === 'overview' && (
        <div className="space-y-6">
          <EntityOverviewDashboard 
            entityType="باقة" 
            details={{ title: pkg.name, description: pkg.description, price: pkg.price }}
            stats={overviewStats}
            loading={statsLoading}
          >
            {/* Content hierarchy summary */}
            {stats && (
              <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
                <h3 className="mb-5 text-lg font-black text-[var(--admin-text)]">ملخص هيكل المحتوى</h3>
                <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                  {contentMode === 'TermWithSections' && (
                    <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4 text-center">
                      <p className="text-2xl font-black text-[var(--admin-primary)]">{stats.termsCount ?? terms.length}</p>
                      <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">ترم</p>
                    </div>
                  )}
                  <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4 text-center">
                    <p className="text-2xl font-black text-[var(--admin-primary)]">{stats.sectionsCount ?? 0}</p>
                    <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">قسم</p>
                  </div>
                  <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4 text-center">
                    <p className="text-2xl font-black text-[var(--admin-primary)]">{stats.lessonsCount ?? 0}</p>
                    <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">حصة</p>
                  </div>
                  <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4 text-center">
                    <p className="text-2xl font-black text-[var(--admin-primary)]">{stats.videosCount ?? 0}</p>
                    <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">فيديو</p>
                  </div>
                </div>
                <div className="mt-5 flex flex-wrap gap-3">
                  {contentMode === 'TermWithSections' && (
                    <button
                      onClick={() => setActiveTab('terms')}
                      className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-primary)] hover:text-white"
                    >
                      <Calendar className="h-4 w-4" />
                      إدارة الأترام
                    </button>
                  )}
                  <button
                    onClick={() => setActiveTab('codeProfile')}
                    className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)]"
                  >
                    <KeyRound className="h-4 w-4" />
                    صفحة الأكواد
                  </button>
                </div>
              </div>
            )}
          </EntityOverviewDashboard>
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
            <h3 className="mb-6 text-xl font-black text-[var(--admin-text)]">إعدادات الباقة الأساسية</h3>
            <PackageDetailsForm pkg={pkg} onSuccess={loadPkg} />
          </div>
        </div>
      )}

      {activeTab === 'subscribers' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <ContentSubscribersTab contentType="package" contentId={params.id} contentName={pkg.name} />
        </div>
      )}

      {activeTab === 'codeProfile' && (
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
          <h3 className="mb-2 text-xl font-black text-[var(--admin-text)]">صفحة الأكواد</h3>
          <p className="mb-6 text-sm text-[var(--admin-muted)]">
            عدّل الرسائل الظاهرة للطلاب عند فتح صفحة تفعيل كود هذه الباقة.
          </p>
          <PackageCodeProfileForm
            packageId={pkg.id}
            packageName={pkg.name}
            onProfileStateChange={setCodeProfileSummary}
          />
        </div>
      )}
    </AdminPage>
  );
}
