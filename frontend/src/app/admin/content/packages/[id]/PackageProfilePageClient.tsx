'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Calendar, KeyRound, BookOpenText, Link2, ChevronRight } from 'lucide-react';
import {
  AdminShellChrome, AdminStatCard, AdminTabBar, AdminTab,
  PackageDetailsForm, PackageCodeProfileForm, EntityOverviewDashboard,
  AdminPageSkeleton, ContentHierarchyPanel,
  PackageCodeProfileSummary, ContentImageUpload
} from '@/components/admin';
import { HierarchyItem } from '@/components/admin/ContentHierarchyPanel';
import { adminService } from '@/services/admin-service';
import { contentService, TermDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type ActiveTab = 'overview' | 'terms' | 'codeProfile';

const TABS: AdminTab<ActiveTab>[] = [
  { key: 'overview', label: 'نظرة عامة', icon: BookOpenText },
  { key: 'terms', label: 'الأترام', icon: Calendar },
  { key: 'codeProfile', label: 'صفحة الأكواد', icon: KeyRound },
];

export default function PackageProfilePageClient(props: { params: { id: string } }) {
  const params = props.params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>('terms');
  const [pkg, setPkg] = useState<any>(null);
  const [pkgLoading, setPkgLoading] = useState(true);
  const [codeProfileSummary, setCodeProfileSummary] = useState<PackageCodeProfileSummary | null>(null);

  // Terms state
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [termsLoading, setTermsLoading] = useState(true);
  const [termsError, setTermsError] = useState(false);

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

  useEffect(() => { void loadPkg(); }, [loadPkg]);
  useEffect(() => { void loadTerms(); }, [loadTerms]);

  if (pkgLoading) {
    return (
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="جاري التحميل..." subtitle="">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!pkg) {
    return (
      <AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="خطأ" subtitle="الباقة غير موجودة">
        <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
          <p className="text-[var(--admin-muted)]">لا يمكن العثور على الباقة المطلوبة.</p>
          <NeumorphButton onClick={() => router.push('/admin/content')} intent="ghost" size="md" pill>
            <ChevronRight className="h-4 w-4" /> عودة للباقات
          </NeumorphButton>
        </div>
      </AdminShellChrome>
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

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الباقات"
      pageTitle={pkg.name}
      subtitle={pkg.description || 'إدارة محتويات وإعدادات الباقة'}
      action={
        <NeumorphButton onClick={() => router.push('/admin/content')} intent="ghost" size="md" pill>
          <ChevronRight className="h-4 w-4" />
          الباقات
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
        <AdminStatCard variant="accent" icon={BookOpenText}  label="حالة الباقة" value={pkg.isActive !== false ? 'نشطة' : 'مسودة'} />
        <AdminStatCard variant="light"  icon={Calendar}      label="عدد الأترام"  value={terms.length} />
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
        <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />
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

      {activeTab === 'overview' && (
        <div className="space-y-6">
          <EntityOverviewDashboard 
            entityType="باقة" 
            details={{ title: pkg.name, description: pkg.description, price: pkg.price }} 
          />
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm">
            <h3 className="mb-6 text-xl font-black text-[var(--admin-text)]">إعدادات الباقة الأساسية</h3>
            <PackageDetailsForm pkg={pkg} />
          </div>
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
    </AdminShellChrome>
  );
}
