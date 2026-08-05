"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { BookOpen, CalendarDays, ChevronLeft, Clapperboard, GraduationCap, Layers, type LucideIcon } from "lucide-react";

import {
  PackagesGrid,
  PackagesOverview,
} from "@/components/student-pages/PackagesOverview";
import { StudentContentCatalog } from "@/components/student-pages/StudentContentCatalog";
import { type PackageDto, contentService } from "@/services/content-service";
import { studentService, type QuickAccessItemDto } from "@/services/student-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { usePlatformQuery } from "@/components/providers/QueryProvider";
import { queryKeys } from "@/lib/query-keys";
import { useAuthStore } from "@/stores/auth-store";

type AccessTab = "packages" | "terms" | "months" | "lessons" | "videos";
type QuickAccessType = 1 | 2 | 3 | 4;

const ACCESS_TABS: { key: AccessTab; label: string; icon: LucideIcon; accessType?: QuickAccessType }[] = [
  { key: "packages", label: "باقاتي", icon: Layers },
  { key: "terms", label: "ترماتي", icon: GraduationCap, accessType: 1 },
  { key: "months", label: "شهوري", icon: CalendarDays, accessType: 2 },
  { key: "lessons", label: "حصصي", icon: BookOpen, accessType: 3 },
  { key: "videos", label: "فيديوهاتي", icon: Clapperboard, accessType: 4 },
];

const ACCESS_TYPE_NAMES: Record<QuickAccessType, QuickAccessItemDto["accessType"]> = {
  1: "Term",
  2: "Month",
  3: "Lesson",
  4: "Video",
};

function matchesAccessType(item: QuickAccessItemDto, type: QuickAccessType) {
  return item.accessType === type || item.accessType === ACCESS_TYPE_NAMES[type];
}

export default function PackagesPageClient() {
  const [activeTab, setActiveTab] = useState<AccessTab>("packages");
  const userId = useAuthStore((state) => state.user?.id);
  const userBoundary = userId ?? 'pending';
  const packagesQueryFn = useCallback(
    async ({ signal }: { signal: AbortSignal }) => {
      const response = await contentService.getPackages({ signal });
      return response.data?.data ?? [];
    },
    []
  );
  const quickAccessQueryFn = useCallback(
    ({ signal }: { signal: AbortSignal }) => studentService.getQuickAccess(signal),
    []
  );
  const packagesQuery = usePlatformQuery<PackageDto[]>({
    queryKey: queryKeys.student.packages(userBoundary),
    queryFn: packagesQueryFn,
    staleTime: 30_000,
    enabled: Boolean(userId),
  });
  const quickAccessQuery = usePlatformQuery<QuickAccessItemDto[]>({
    queryKey: queryKeys.student.quickAccess(userBoundary),
    queryFn: quickAccessQueryFn,
    staleTime: 30_000,
    enabled: Boolean(userId),
  });
  const packages = packagesQuery.data ?? [];
  const quickAccess = quickAccessQuery.data ?? [];
  const loading =
    !userId ||
    (packagesQuery.data === undefined && packagesQuery.error === null) ||
    (quickAccessQuery.data === undefined && quickAccessQuery.error === null);
  const hasLoadError = Boolean(packagesQuery.error || quickAccessQuery.error);
  const refetch = () => {
    void Promise.all([
      packagesQuery.refetch(),
      quickAccessQuery.refetch(),
    ]).catch(() => undefined);
  };

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-72 rounded-2xl bg-[var(--admin-card-strong)]" />
        <div className="grid gap-6 xl:grid-cols-2">
          {[1, 2].map((i) => (
            <div key={i} className="h-80 rounded-[30px] bg-[var(--admin-card-strong)]" />
          ))}
        </div>
      </div>
    );
  }

  if (hasLoadError && packagesQuery.data === undefined && quickAccessQuery.data === undefined) {
    return (
      <div role="alert" className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-6 text-center">
        <p className="font-bold text-[var(--admin-danger)]">تعذر تحميل الباقات حاليًا.</p>
        <button type="button" onClick={refetch} className="mt-4 min-h-11 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)]">
          إعادة المحاولة
        </button>
      </div>
    );
  }

  const directPackages = packages.filter((pkg) => pkg.hasDirectPackageAccess ?? pkg.isEnrolled);
  const packagesNeedingActivation = packages.filter((pkg) => !(pkg.hasDirectPackageAccess ?? pkg.isEnrolled));
  const overviewPackages = packages.map((pkg) => ({
    ...pkg,
    isEnrolled: pkg.hasDirectPackageAccess ?? pkg.isEnrolled,
  }));
  const activeTabMeta = ACCESS_TABS.find((tab) => tab.key === activeTab) ?? ACCESS_TABS[0];
  const activeAccessType = activeTabMeta.accessType;
  const activeItems = activeAccessType
    ? quickAccess.filter((item) => matchesAccessType(item, activeAccessType))
    : [];

  return (
    <div className="space-y-12 pb-10">
      {hasLoadError && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-4 py-3 text-sm font-bold text-[var(--admin-warning)]">
          <span>تعذر تحديث بعض البيانات؛ يتم عرض آخر نسخة متاحة.</span>
          <button type="button" onClick={refetch} className="min-h-11 rounded-xl border border-current px-4">
            إعادة المحاولة
          </button>
        </div>
      )}
      <PackagesOverview packages={overviewPackages} />

      <StudentContentCatalog
        packages={packages}
        onPurchaseComplete={async () => {
          await packagesQuery.refetch();
        }}
      />

      <section className="space-y-6">
        <div className="flex gap-2 overflow-x-auto rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-2">
          {ACCESS_TABS.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.key;
            const tabAccessType = tab.accessType;
            const count = tabAccessType
              ? quickAccess.filter((item) => matchesAccessType(item, tabAccessType)).length
              : directPackages.length;

            return (
              <button
                key={tab.key}
                type="button"
                onClick={() => setActiveTab(tab.key)}
                className={`inline-flex min-h-11 shrink-0 items-center gap-2 rounded-xl px-4 text-sm font-black transition ${
                  isActive
                    ? "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]"
                    : "text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)]"
                }`}
              >
                <Icon className="h-4 w-4" />
                <span>{tab.label}</span>
                <span className={`rounded-full px-2 py-0.5 text-xs ${isActive ? "bg-white/15" : "bg-[var(--admin-card-soft)]"}`}>
                  {count}
                </span>
              </button>
            );
          })}
        </div>

        {activeTab === "packages" ? (
          <PackagesGrid
            title="باقاتي"
            description="الباقات الكاملة المفتوحة لك، ادخل مباشرة إلى المسار الكامل."
            packages={directPackages}
            actionLabel="دخول الباقة"
            emptyTitle="لا توجد باقات مفعّلة ومتاحة لصفك حالياً"
            emptyDescription="أي باقة تشتريها أو تفتحها بكود ستظهر هنا فقط إذا كانت ما زالت مطابقة لمرحلتك وصفك وموادك الحالية."
            getHref={(packageId) => `/student/packages/${packageId}`}
          />
        ) : (
          <QuickAccessGrid
            title={activeTabMeta.label}
            items={activeItems}
            emptyTitle={`لا توجد ${activeTabMeta.label} بعد`}
            emptyDescription="العناصر التي تشتريها أو تفتحها بالكود تظهر هنا عند استمرار مطابقتها لبياناتك الدراسية الحالية."
          />
        )}
      </section>

      {activeTab === "packages" && (
        <PackagesGrid
          title="باقات تحتاج تفعيل"
          description="هذه الباقات متاحة على المنصة لكنها تحتاج كود أو شراء للوصول إليها."
          packages={packagesNeedingActivation}
          actionLabel="استعرض الباقة"
          emptyTitle="لا توجد باقات إضافية متاحة لصفك"
          emptyDescription="الباقات غير المطابقة لمرحلتك أو صفك لا تظهر في هذه القائمة."
          getHref={(packageId) => `/student/packages/${packageId}`}
        />
      )}
    </div>
  );
}

function QuickAccessGrid({
  title,
  items,
  emptyTitle,
  emptyDescription,
}: {
  title: string;
  items: QuickAccessItemDto[];
  emptyTitle: string;
  emptyDescription: string;
}) {
  if (items.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] p-10 text-center">
        <h2 className="text-xl font-black text-[var(--admin-text)]">{emptyTitle}</h2>
        <p className="mx-auto mt-2 max-w-lg text-sm font-medium leading-7 text-[var(--admin-muted)]">{emptyDescription}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="border-b border-[var(--admin-border)] pb-4">
        <h2 className="text-2xl font-black text-[var(--admin-text)]">{title}</h2>
        <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">اختار أي عنصر وسنفتح لك مكانه داخل المسار مباشرة.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {items.map((item) => (
          <Link
            key={`${item.accessType}-${item.url}`}
            href={item.url}
            className="group overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm transition hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)]"
          >
            <div className="flex min-h-36 gap-4 p-4">
              <div className="relative h-24 w-24 shrink-0 overflow-hidden rounded-xl bg-[var(--admin-card-soft)]">
                {item.imageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={resolveMediaUrl(item.imageUrl)} alt="" className="h-full w-full object-cover" />
                ) : (
                  <div className="flex h-full w-full items-center justify-center text-[var(--admin-primary)]">
                    <Clapperboard className="h-8 w-8" />
                  </div>
                )}
              </div>

              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <span className="rounded-full bg-[var(--admin-primary-10)] px-2.5 py-1 text-xs font-black text-[var(--admin-primary)]">
                    {item.badge || "مفتوح"}
                  </span>
                  <ChevronLeft className="mt-1 h-4 w-4 shrink-0 text-[var(--admin-muted)] transition group-hover:-translate-x-1 group-hover:text-[var(--admin-primary)]" />
                </div>
                <h3 className="mt-3 line-clamp-2 text-base font-black leading-6 text-[var(--admin-text)]">{item.title}</h3>
                <p className="mt-2 line-clamp-2 text-xs font-medium leading-5 text-[var(--admin-muted)]">{item.pathBreadcrumb}</p>
                {item.teacherName ? (
                  <div className="mt-3 flex items-center gap-2 text-xs font-bold text-[var(--admin-text)]">
                    {item.teacherProfileImageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={resolveMediaUrl(item.teacherProfileImageUrl)} alt="" className="h-6 w-6 rounded-full object-cover" />
                    ) : null}
                    <span className="truncate">{item.teacherName}</span>
                  </div>
                ) : null}
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
