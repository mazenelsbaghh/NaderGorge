"use client";

import { useCallback, useMemo, useState } from "react";
import Link from "next/link";
import {
  ArrowRight,
  BookOpen,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  GraduationCap,
  Layers3,
  LockKeyhole,
  ShoppingBag,
  Sparkles,
  WalletCards,
  type LucideIcon,
} from "lucide-react";

import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { type CodeType } from "@/services/balance-service";
import {
  contentService,
  getContentRootLabel,
  getContentRootPurchaseReference,
  type ContentSectionDto,
  type LessonSummaryDto,
  type PackageDto,
  type TermDto,
} from "@/services/content-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";

type CatalogLevel = "packages" | "terms" | "months" | "lessons";

type PurchaseTarget = {
  contentType: CodeType;
  contentId: string;
  contentName: string;
  price: number;
};

type StudentContentCatalogProps = {
  packages: PackageDto[];
  onPurchaseComplete?: () => void | Promise<void>;
};

type SectionParent = { packageId: string; term?: TermDto };
type TermGroup = { pkg: PackageDto; terms: TermDto[] };
type SectionGroup = { pkg: PackageDto; term?: TermDto; sections: ContentSectionDto[] };
type LessonGroup = { packageId: string; lessons: LessonSummaryDto[] };

const LEVELS: Array<{
  key: CatalogLevel;
  label: string;
  icon: LucideIcon;
}> = [
  { key: "packages", label: "المحتوى", icon: Layers3 },
  { key: "terms", label: "الأترام", icon: GraduationCap },
  { key: "months", label: "الشهور / الأقسام", icon: CalendarDays },
  { key: "lessons", label: "الحصص", icon: BookOpen },
];

const levelLabels: Record<CatalogLevel, string> = {
  packages: "المحتوى المتاح",
  terms: "الأترام المتاحة",
  months: "الشهور والأقسام المتاحة",
  lessons: "الحصص المتاحة",
};

async function fetchTermGroups(packages: PackageDto[]): Promise<TermGroup[]> {
  const termPackages = packages.filter((pkg) => (pkg.contentMode ?? "TermWithSections") === "TermWithSections");
  return Promise.all(termPackages.map(async (pkg) => ({
    pkg,
    terms: (await contentService.getTerms(pkg.id)).data.data,
  })));
}

async function fetchSectionGroups(packages: PackageDto[], termGroups: TermGroup[]): Promise<SectionGroup[]> {
  const directGroups = packages
    .filter((pkg) => pkg.contentMode === "SectionWithLessons")
    .map((pkg) => ({ pkg, term: undefined, sections: (pkg.directSections ?? []) as ContentSectionDto[] }));
  const nestedGroups = await Promise.all(termGroups.flatMap((group) => group.terms.map(async (term) => ({
    pkg: group.pkg,
    term,
    sections: (await contentService.getSections(term.id)).data.data,
  }))));
  return [...directGroups, ...nestedGroups];
}

async function fetchLessonGroups(packages: PackageDto[], sectionGroups: SectionGroup[]): Promise<LessonGroup[]> {
  const nestedGroups = await Promise.all(sectionGroups.flatMap((group) => group.sections.map(async (section) => ({
    packageId: group.pkg.id,
    lessons: (await contentService.getLessons(section.id)).data.data,
  }))));
  const directGroups = packages.filter((pkg) => pkg.contentMode === "LessonsOnly" || pkg.contentMode === "SingleLesson").map((pkg) => ({
    packageId: pkg.id,
    lessons: (pkg.directLessons ?? []).map((lesson) => ({ ...lesson, hasAccess: lesson.hasAccess ?? false, isCompleted: false, videos: [] })),
  }));
  return [...directGroups, ...nestedGroups];
}

export function StudentContentCatalog({ packages, onPurchaseComplete }: StudentContentCatalogProps) {
  const [level, setLevel] = useState<CatalogLevel>("packages");
  const [selectedPackage, setSelectedPackage] = useState<PackageDto | null>(null);
  const [selectedTerm, setSelectedTerm] = useState<TermDto | null>(null);
  const [selectedSection, setSelectedSection] = useState<ContentSectionDto | null>(null);
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [purchaseTarget, setPurchaseTarget] = useState<PurchaseTarget | null>(null);
  const [termPackageIds, setTermPackageIds] = useState<Map<string, string>>(new Map());
  const [sectionParents, setSectionParents] = useState<Map<string, SectionParent>>(new Map());
  const [lessonPackageIds, setLessonPackageIds] = useState<Map<string, string>>(new Map());

  const loadTerms = useCallback(async (packageId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await contentService.getTerms(packageId);
      setTerms(response.data?.data ?? []);
    } catch {
      setError("تعذر تحميل أترام الباقة حاليًا.");
    } finally {
      setLoading(false);
    }
  }, []);

  const loadSections = useCallback(async (termId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await contentService.getSections(termId);
      setSections(response.data?.data ?? []);
    } catch {
      setError("تعذر تحميل الشهور والأقسام حاليًا.");
    } finally {
      setLoading(false);
    }
  }, []);

  const loadLessons = useCallback(async (sectionId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await contentService.getLessons(sectionId);
      setLessons(response.data?.data ?? []);
    } catch {
      setError("تعذر تحميل حصص القسم حاليًا.");
    } finally {
      setLoading(false);
    }
  }, []);

  const availableLevels = useMemo(
    () => new Set<CatalogLevel>(packages.length > 0 ? LEVELS.map((item) => item.key) : ["packages"]),
    [packages.length],
  );

  const loadCatalogLevel = useCallback(async (nextLevel: Exclude<CatalogLevel, "packages">) => {
    setLoading(true);
    setError(null);
    setSelectedPackage(null);
    setSelectedTerm(null);
    setSelectedSection(null);
    try {
      const termGroups = await fetchTermGroups(packages);
      const allTerms = termGroups.flatMap((group) => group.terms);
      const nextTermPackageIds = new Map(termGroups.flatMap((group) => group.terms.map((term) => [term.id, group.pkg.id] as const)));
      setTerms(allTerms);
      setTermPackageIds(nextTermPackageIds);
      if (nextLevel === "terms") return;

      const sectionGroups = await fetchSectionGroups(packages, termGroups);
      const allSections = sectionGroups.flatMap((group) => group.sections);
      const nextSectionParents = new Map(sectionGroups.flatMap((group) => group.sections.map((section) => [section.id, { packageId: group.pkg.id, term: group.term }] as const)));
      setSections(allSections);
      setSectionParents(nextSectionParents);
      if (nextLevel === "months") return;

      const lessonGroups = await fetchLessonGroups(packages, sectionGroups);
      setLessons(lessonGroups.flatMap((group) => group.lessons));
      setLessonPackageIds(new Map(lessonGroups.flatMap((group) => group.lessons.map((lesson) => [lesson.id, group.packageId] as const))));
    } catch {
      setError(`تعذر تحميل ${levelLabels[nextLevel]} حاليًا.`);
    } finally {
      setLoading(false);
    }
  }, [packages]);

  const choosePackage = (pkg: PackageDto) => {
    setSelectedPackage(pkg);
    setSelectedTerm(null);
    setSelectedSection(null);
    setTerms([]);
    setSections([]);
    setLessons([]);
    setError(null);

    if (pkg.contentMode === "LessonsOnly" || pkg.contentMode === "SingleLesson") {
      setLessons(
        (pkg.directLessons ?? []).map((lesson) => ({
          id: lesson.id,
          title: lesson.title,
          summary: lesson.summary,
          order: lesson.order,
          hasAccess: lesson.hasAccess ?? false,
          isCompleted: false,
          price: lesson.price ?? 0,
          videos: [],
        })),
      );
      setLevel("lessons");
      return;
    }

    if (pkg.contentMode === "SectionWithLessons") {
      setSections(pkg.directSections ?? []);
      setLevel("months");
      return;
    }

    setLevel("terms");
    void loadTerms(pkg.id);
  };

  const chooseTerm = (term: TermDto) => {
    const parentPackage = selectedPackage ?? packages.find((pkg) => pkg.id === termPackageIds.get(term.id)) ?? null;
    if (!parentPackage) return;
    setSelectedPackage(parentPackage);
    setSelectedTerm(term);
    setSelectedSection(null);
    setSections([]);
    setLessons([]);
    setLevel("months");
    void loadSections(term.id);
  };

  const chooseSection = (section: ContentSectionDto) => {
    const parent = sectionParents.get(section.id);
    if (!selectedPackage && parent) {
      setSelectedPackage(packages.find((pkg) => pkg.id === parent.packageId) ?? null);
      setSelectedTerm(parent.term ?? null);
    }
    setSelectedSection(section);
    setLessons([]);
    setLevel("lessons");
    void loadLessons(section.id);
  };

  const resetToPackages = () => {
    setLevel("packages");
    setSelectedPackage(null);
    setSelectedTerm(null);
    setSelectedSection(null);
    setTerms([]);
    setSections([]);
    setLessons([]);
    setError(null);
  };

  const goToLevel = (nextLevel: CatalogLevel) => {
    if (!availableLevels.has(nextLevel)) return;
    if (nextLevel === "packages") {
      resetToPackages();
      return;
    }
    const canUseCurrentPath =
      (nextLevel === "terms" && selectedPackage?.contentMode === "TermWithSections") ||
      (nextLevel === "months" && (selectedTerm !== null || selectedPackage?.contentMode === "SectionWithLessons")) ||
      (nextLevel === "lessons" && (selectedSection !== null || selectedPackage?.contentMode === "LessonsOnly" || selectedPackage?.contentMode === "SingleLesson"));
    if (!canUseCurrentPath) {
      setLevel(nextLevel);
      void loadCatalogLevel(nextLevel);
      return;
    }
    setLevel(nextLevel);
    setError(null);
  };

  const handlePurchaseComplete = async () => {
    setPurchaseTarget(null);
    await onPurchaseComplete?.();

    if (selectedPackage && level === "terms") {
      await loadTerms(selectedPackage.id);
    } else if (selectedTerm && level === "months") {
      await loadSections(selectedTerm.id);
    } else if (selectedSection && level === "lessons") {
      await loadLessons(selectedSection.id);
    }
  };

  return (
    <section className="space-y-6" aria-labelledby="student-content-catalog-title">
      <div className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">
        <div className="pointer-events-none absolute -left-24 -top-24 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-3xl" />
        <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl text-right">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              <ShoppingBag className="h-3.5 w-3.5" />
              <span>متجر المحتوى التعليمي</span>
            </div>
            <h2 id="student-content-catalog-title" className="text-2xl font-black tracking-tight text-[var(--admin-text)] sm:text-3xl">
              اشتري الجزء الذي تحتاجه مباشرة
            </h2>
            <p className="mt-2 text-sm font-medium leading-7 text-[var(--admin-muted)]">
              اختار باقة كاملة أو ترم أو شهر أو حصة، وشوف السعر وحالة التفعيل قبل ما تبدأ الشراء.
            </p>
          </div>
          <div className="flex items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3 text-sm font-black text-[var(--admin-text)]">
            <WalletCards className="h-5 w-5 text-[var(--admin-primary)]" />
            <span>الدفع من رصيدك</span>
          </div>
        </div>

        <nav aria-label="مستويات المحتوى" className="relative mt-7 overflow-x-auto border-t border-[var(--admin-border)] pt-4">
          <div className="flex min-w-max items-center gap-2">
            {LEVELS.map((item, index) => {
              const Icon = item.icon;
              const isAvailable = availableLevels.has(item.key);
              const isActive = level === item.key;
              return (
                <div key={item.key} className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => goToLevel(item.key)}
                    disabled={!isAvailable}
                    className={`inline-flex min-h-11 items-center gap-2 rounded-xl px-3.5 text-sm font-black transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${
                      isActive
                        ? "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-sm"
                        : isAvailable
                          ? "text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)]"
                          : "cursor-not-allowed text-[var(--admin-muted)]/40"
                    }`}
                  >
                    <Icon className="h-4 w-4" />
                    {item.label}
                  </button>
                  {index < LEVELS.length - 1 ? <ChevronLeft className="h-4 w-4 text-[var(--admin-border)]" aria-hidden /> : null}
                </div>
              );
            })}
          </div>
        </nav>
      </div>

      {selectedPackage && level !== "packages" ? (
        <div className="flex flex-wrap items-center gap-2 text-sm font-bold text-[var(--admin-muted)]">
          <button type="button" onClick={resetToPackages} className="transition hover:text-[var(--admin-primary)]">
            المحتوى
          </button>
          <ChevronLeft className="h-4 w-4" aria-hidden />
          <button type="button" onClick={() => goToLevel(selectedPackage.contentMode === "TermWithSections" ? "terms" : selectedPackage.contentMode === "SectionWithLessons" ? "months" : "lessons")} className="max-w-56 truncate text-[var(--admin-text)] transition hover:text-[var(--admin-primary)]">
            {selectedPackage.name}
          </button>
          {selectedTerm ? (
            <>
              <ChevronLeft className="h-4 w-4" aria-hidden />
              <button type="button" onClick={() => goToLevel("months")} className="max-w-56 truncate text-[var(--admin-text)] transition hover:text-[var(--admin-primary)]">
                {selectedTerm.title}
              </button>
            </>
          ) : null}
          {selectedSection ? (
            <>
              <ChevronLeft className="h-4 w-4" aria-hidden />
              <span className="max-w-56 truncate text-[var(--admin-text)]">{selectedSection.title}</span>
            </>
          ) : null}
        </div>
      ) : null}

      {error ? (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-5 py-4 text-sm font-bold text-[var(--admin-danger)]">
          <span>{error}</span>
          <button
            type="button"
            onClick={() => {
              if (selectedPackage && level === "terms") void loadTerms(selectedPackage.id);
              else if (selectedTerm && level === "months") void loadSections(selectedTerm.id);
              else if (selectedSection && level === "lessons") void loadLessons(selectedSection.id);
              else if (level !== "packages") void loadCatalogLevel(level);
            }}
            className="min-h-10 rounded-xl border border-current px-4 transition hover:bg-[var(--admin-danger)]/10"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : null}

      {loading ? <CatalogSkeleton /> : null}

      {!loading && level === "packages" ? (
        <div className="space-y-4">
          <CatalogHeading title={levelLabels.packages} description="اختر باقة أو ترمًا أو قسمًا أو حصة مستقلة، ثم استعرض المحتوى أو اشتره مباشرة." />
          {packages.length === 0 ? <CatalogEmpty message="لا يوجد محتوى متاح لبياناتك الدراسية حاليًا." /> : (
            <div className="grid gap-4 lg:grid-cols-2">
              {packages.map((pkg) => {
                const rootReference = getContentRootPurchaseReference(pkg);
                const rootPurchaseTarget: PurchaseTarget | null = rootReference
                  ? { ...rootReference, contentName: pkg.name, price: pkg.price ?? 0 }
                  : null;
                return (
                  <PackageCatalogCard
                    key={pkg.id}
                    pkg={pkg}
                    onExplore={() => choosePackage(pkg)}
                    onPurchase={rootPurchaseTarget ? () => setPurchaseTarget(rootPurchaseTarget) : undefined}
                  />
                );
              })}
            </div>
          )}
        </div>
      ) : null}

      {!loading && level === "terms" ? (
        <CatalogLevelPanel
          title={levelLabels.terms}
          description="كل ترم له سعر مستقل، وبعد شرائه تظهر لك الشهور والحصص التابعة له."
          onBack={() => goToLevel("packages")}
          emptyMessage="لا توجد أترام متاحة داخل هذه الباقة حاليًا."
          isEmpty={terms.length === 0}
        >
          <div className="grid gap-4 md:grid-cols-2">
            {terms.map((term, index) => (
              <TermCatalogCard
                key={term.id}
                term={term}
                index={index}
                isPackageOwned={selectedPackage?.hasDirectPackageAccess ?? false}
                onExplore={() => chooseTerm(term)}
                onPurchase={() => setPurchaseTarget({ contentType: "Term", contentId: term.id, contentName: term.title, price: term.price ?? 0 })}
              />
            ))}
          </div>
        </CatalogLevelPanel>
      ) : null}

      {!loading && level === "months" ? (
        <CatalogLevelPanel
          title={levelLabels.months}
          description="الشهر هو القسم الدراسي. افتحه لمشاهدة حصصه أو اشتريه منفردًا."
          onBack={() => goToLevel(selectedPackage?.contentMode === "TermWithSections" ? "terms" : "packages")}
          emptyMessage="لا توجد شهور أو أقسام متاحة داخل هذا المسار حاليًا."
          isEmpty={sections.length === 0}
        >
          <div className="grid gap-4 md:grid-cols-2">
            {sections.map((section, index) => (
              <SectionCatalogCard
                key={section.id}
                section={section}
                index={index}
                isParentOwned={(selectedPackage?.hasDirectPackageAccess ?? false) || selectedTerm?.isPurchased === true}
                onExplore={() => chooseSection(section)}
                onPurchase={() => setPurchaseTarget({ contentType: "Month", contentId: section.id, contentName: section.title, price: section.price ?? 0 })}
              />
            ))}
          </div>
        </CatalogLevelPanel>
      ) : null}

      {!loading && level === "lessons" ? (
        <CatalogLevelPanel
          title={levelLabels.lessons}
          description="اختار الحصة التي تريدها وابدأ فورًا، أو اشتريها منفردة إذا لم تكن مفعّلة."
          onBack={() => goToLevel(selectedSection ? "months" : selectedPackage?.contentMode === "TermWithSections" ? "terms" : "packages")}
          emptyMessage="لا توجد حصص متاحة داخل هذا المسار حاليًا."
          isEmpty={lessons.length === 0}
        >
          <div className="space-y-3">
            {lessons.map((lesson, index) => (
              <LessonCatalogCard
                key={lesson.id}
                lesson={lesson}
                index={index}
                packageId={selectedPackage?.id ?? lessonPackageIds.get(lesson.id)}
                onPurchase={() => setPurchaseTarget({ contentType: "Lesson", contentId: lesson.id, contentName: lesson.title, price: lesson.price ?? 0 })}
              />
            ))}
          </div>
        </CatalogLevelPanel>
      ) : null}

      <PurchaseContentModal
        isOpen={purchaseTarget !== null}
        onClose={() => setPurchaseTarget(null)}
        onPurchaseSuccess={handlePurchaseComplete}
        contentType={purchaseTarget?.contentType ?? "Package"}
        contentId={purchaseTarget?.contentId ?? ""}
        contentName={purchaseTarget?.contentName ?? "المحتوى"}
        price={purchaseTarget?.price ?? 0}
      />
    </section>
  );
}

function CatalogHeading({ title, description }: { title: string; description: string }) {
  return (
    <div className="border-b border-[var(--admin-border)] pb-4 text-right">
      <h3 className="text-2xl font-black text-[var(--admin-text)]">{title}</h3>
      <p className="mt-1 text-sm font-medium leading-6 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}

function CatalogLevelPanel({
  title,
  description,
  onBack,
  emptyMessage,
  isEmpty,
  children,
}: {
  title: string;
  description: string;
  onBack: () => void;
  emptyMessage: string;
  isEmpty: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[var(--admin-border)] pb-4">
        <CatalogHeading title={title} description={description} />
        <button type="button" onClick={onBack} className="inline-flex min-h-10 items-center gap-2 rounded-xl px-3 text-sm font-black text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)]">
          <ArrowRight className="h-4 w-4" />
          رجوع
        </button>
      </div>
      {isEmpty ? <CatalogEmpty message={emptyMessage} /> : children}
    </div>
  );
}

function PackageCatalogCard({ pkg, onExplore, onPurchase }: { pkg: PackageDto; onExplore: () => void; onPurchase?: () => void }) {
  const hasRootAccess = pkg.hasRootContentAccess ?? pkg.hasDirectPackageAccess ?? false;
  const hasPartialAccess = !hasRootAccess && pkg.isEnrolled;
  const contentRootLabel = getContentRootLabel(pkg.contentMode ?? "TermWithSections");
  return (
    <article className="group overflow-hidden rounded-[1.75rem] border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm transition hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="flex min-h-52 flex-col gap-5 p-5 sm:flex-row">
        <div className="relative h-44 shrink-0 overflow-hidden rounded-2xl bg-[var(--admin-card-soft)] sm:h-auto sm:w-40">
          {pkg.imageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={resolveMediaUrl(pkg.imageUrl)} alt={`غلاف ${pkg.name}`} className="h-full w-full object-cover transition duration-500 group-hover:scale-105" />
          ) : (
            <div className="flex h-full min-h-36 items-center justify-center bg-[var(--admin-primary-10)] text-[var(--admin-primary)]">
              <Layers3 className="h-10 w-10" />
            </div>
          )}
        </div>
        <div className="flex min-w-0 flex-1 flex-col text-right">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className={`rounded-full px-2.5 py-1 text-xs font-black ${hasRootAccess ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : hasPartialAccess ? "bg-amber-500/10 text-amber-700 dark:text-amber-300" : "bg-[var(--admin-primary-10)] text-[var(--admin-primary)]"}`}>
              {hasRootAccess ? "مفعّل" : hasPartialAccess ? "محتوى مفعّل" : "متاح للشراء"}
            </span>
            <span className="text-xs font-bold text-[var(--admin-muted)]">{contentRootLabel} · {pkg.subjectName || "مسار تعليمي"}</span>
          </div>
          <h3 className="mt-3 line-clamp-2 text-xl font-black leading-7 text-[var(--admin-text)]">{pkg.name}</h3>
          <p className="mt-2 line-clamp-2 text-sm font-medium leading-6 text-[var(--admin-muted)]">{pkg.description || `استعرض محتوى ${contentRootLabel} قبل البدء.`}</p>
          <div className="mt-auto flex flex-wrap items-center justify-between gap-3 pt-5">
            <span className="text-lg font-black text-[var(--admin-primary)]">{pkg.price > 0 ? `${pkg.price} ج.م` : "مجانًا"}</span>
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={onExplore} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-[var(--admin-border)] px-3.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-soft)]">
                استعرض المحتوى
                <ChevronLeft className="h-4 w-4" />
              </button>
              {!hasRootAccess && onPurchase ? (
                <button type="button" onClick={onPurchase} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl bg-[var(--admin-primary)] px-3.5 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:brightness-110">
                  <Sparkles className="h-4 w-4" />
                  شراء {contentRootLabel}
                </button>
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

function TermCatalogCard({ term, index, isPackageOwned, onExplore, onPurchase }: { term: TermDto; index: number; isPackageOwned: boolean; onExplore: () => void; onPurchase: () => void }) {
  const isOwned = isPackageOwned || term.isPurchased === true;
  return (
    <article className="flex flex-col justify-between rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-right shadow-sm transition hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="flex items-start justify-between gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary-10)] text-sm font-black text-[var(--admin-primary)]">{String(index + 1).padStart(2, "0")}</span>
        <div>
          <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-black ${isOwned ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : "bg-amber-500/10 text-amber-700 dark:text-amber-300"}`}>
            {isOwned ? <CheckCircle2 className="h-3.5 w-3.5" /> : <LockKeyhole className="h-3.5 w-3.5" />}
            {isOwned ? "مفتوح" : "متاح للشراء"}
          </span>
          <h4 className="mt-3 text-lg font-black leading-7 text-[var(--admin-text)]">{term.title}</h4>
        </div>
      </div>
      <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
        <span className="font-black text-[var(--admin-primary)]">{term.price && term.price > 0 ? `${term.price} ج.م` : "مجانًا"}</span>
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={onExplore} className="relative z-10 inline-flex min-h-10 cursor-pointer touch-manipulation items-center gap-1 rounded-xl border border-sky-500/50 bg-sky-50 px-3 text-sm font-black text-sky-800 shadow-sm transition hover:bg-sky-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2 active:scale-[0.98] dark:bg-sky-950/40 dark:text-sky-200">
            الأقسام
            <ChevronLeft className="h-4 w-4" />
          </button>
          {!isOwned ? <PurchaseButton label="شراء الترم" onClick={onPurchase} /> : null}
        </div>
      </div>
    </article>
  );
}

function SectionCatalogCard({ section, index, isParentOwned, onExplore, onPurchase }: { section: ContentSectionDto; index: number; isParentOwned: boolean; onExplore: () => void; onPurchase: () => void }) {
  const isOwned = isParentOwned || section.isPurchased === true;
  return (
    <article className="flex min-h-44 flex-col justify-between rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-right shadow-sm transition hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="flex items-start gap-4">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-10)] text-[var(--admin-primary)]">
          <CalendarDays className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <span className="text-xs font-black text-[var(--admin-muted)]">شهر / قسم {index + 1}</span>
          <h4 className="mt-1 text-lg font-black leading-7 text-[var(--admin-text)]">{section.title}</h4>
          <span className={`mt-2 inline-flex items-center gap-1 text-xs font-black ${isOwned ? "text-emerald-600 dark:text-emerald-400" : "text-amber-700 dark:text-amber-300"}`}>
            {isOwned ? <CheckCircle2 className="h-3.5 w-3.5" /> : <LockKeyhole className="h-3.5 w-3.5" />}
            {isOwned ? "مفعّل" : "يمكن شراؤه منفردًا"}
          </span>
        </div>
      </div>
      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <span className="font-black text-[var(--admin-primary)]">{section.price && section.price > 0 ? `${section.price} ج.م` : "مجانًا"}</span>
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={onExplore} className="relative z-10 inline-flex min-h-10 cursor-pointer touch-manipulation items-center gap-1 rounded-xl border border-sky-500/50 bg-sky-50 px-3 text-sm font-black text-sky-800 shadow-sm transition hover:bg-sky-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2 active:scale-[0.98] dark:bg-sky-950/40 dark:text-sky-200">
            الحصص
            <ChevronLeft className="h-4 w-4" />
          </button>
          {!isOwned ? <PurchaseButton label="شراء الشهر" onClick={onPurchase} /> : null}
        </div>
      </div>
    </article>
  );
}

function LessonCatalogCard({ lesson, index, packageId, onPurchase }: { lesson: LessonSummaryDto; index: number; packageId?: string; onPurchase: () => void }) {
  const isOwned = lesson.hasAccess;
  const lessonHref = packageId ? `/student/packages/${packageId}/lessons/${lesson.id}` : `/student/lessons/${lesson.id}`;
  return (
    <article className="flex flex-col items-start justify-between gap-4 rounded-[1.25rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-right shadow-sm transition hover:border-[var(--admin-primary-30)] hover:shadow-md sm:flex-row sm:items-center">
      <div className="flex min-w-0 items-center gap-4">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-card-soft)] text-sm font-black text-[var(--admin-muted)]">{String(index + 1).padStart(2, "0")}</span>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h4 className="truncate text-base font-black text-[var(--admin-text)]">{lesson.title}</h4>
            {lesson.isCompleted ? <span className="rounded-full bg-emerald-500/10 px-2 py-0.5 text-sm font-black text-emerald-600 dark:text-emerald-400">مكتملة</span> : null}
          </div>
          {lesson.summary ? <p className="mt-1 line-clamp-1 text-xs font-medium text-[var(--admin-muted)]">{lesson.summary}</p> : null}
          <span className={`mt-2 inline-flex items-center gap-1 text-xs font-black ${isOwned ? "text-emerald-600 dark:text-emerald-400" : "text-amber-700 dark:text-amber-300"}`}>
            {isOwned ? <CheckCircle2 className="h-3.5 w-3.5" /> : <LockKeyhole className="h-3.5 w-3.5" />}
            {isOwned ? "متاحة لك" : "متاحة للشراء"}
          </span>
        </div>
      </div>
      <div className="flex w-full shrink-0 flex-wrap items-center justify-between gap-3 sm:w-auto sm:justify-end">
        <span className="font-black text-[var(--admin-primary)]">{(lesson.price ?? 0) > 0 ? `${lesson.price} ج.م` : "مجانًا"}</span>
        {isOwned ? (
          <Link href={lessonHref} prefetch={false} className="inline-flex min-h-10 items-center gap-1 rounded-xl bg-[var(--admin-primary)] px-3.5 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:brightness-110">
            ابدأ الحصة
            <ChevronLeft className="h-4 w-4" />
          </Link>
        ) : (
          <div className="flex gap-2">
            <Link href={lessonHref} prefetch={false} className="inline-flex min-h-10 items-center rounded-xl border border-[var(--admin-border)] px-3 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-soft)]">التفاصيل</Link>
            <PurchaseButton label="شراء الحصة" onClick={onPurchase} />
          </div>
        )}
      </div>
    </article>
  );
}

function PurchaseButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="inline-flex min-h-10 items-center gap-1.5 rounded-xl bg-[var(--admin-primary)] px-3.5 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:brightness-110">
      <Sparkles className="h-4 w-4" />
      {label}
    </button>
  );
}

function CatalogEmpty({ message }: { message: string }) {
  return (
    <div className="rounded-[1.5rem] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-14 text-center">
      <BookOpen className="mx-auto mb-3 h-9 w-9 text-[var(--admin-muted)] opacity-50" />
      <p className="font-bold text-[var(--admin-muted)]">{message}</p>
    </div>
  );
}

function CatalogSkeleton() {
  return (
    <div className="grid gap-4 md:grid-cols-2" aria-label="جار تحميل المحتوى">
      {[1, 2, 3, 4].map((item) => <div key={item} className="h-44 animate-pulse rounded-[1.5rem] bg-[var(--admin-card-strong)]" />)}
    </div>
  );
}
