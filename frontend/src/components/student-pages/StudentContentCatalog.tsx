'use client';

import { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  BookOpen,
  BookOpenCheck,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  Filter,
  GraduationCap,
  Layers3,
  LockKeyhole,
  PlayCircle,
  ShoppingBag,
  Sparkles,
  UserRound,
  WalletCards,
  type LucideIcon,
} from 'lucide-react';

import { PurchaseContentModal } from '@/components/balance/PurchaseContentModal';
import { type CodeType } from '@/services/balance-service';
import {
  contentService,
  getContentRootLabel,
  getContentRootPurchaseReference,
  type ContentSectionDto,
  type LessonSummaryDto,
  type PackageDto,
  type TermDto,
} from '@/services/content-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type CatalogLevel = 'packages' | 'terms' | 'months' | 'lessons';

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

type SectionParent = { pkg: PackageDto; term?: TermDto };
type LessonParent = SectionParent & { section?: ContentSectionDto };
type TermGroup = { pkg: PackageDto; terms: TermDto[] };
type SectionGroup = {
  pkg: PackageDto;
  term?: TermDto;
  sections: ContentSectionDto[];
};
type LessonGroup = LessonParent & { lessons: LessonSummaryDto[] };
type LessonCatalogEntry = {
  lesson: LessonSummaryDto;
  parent?: LessonParent;
  catalogIndex: number;
  teacherKey: string;
  teacherName: string;
  subjectKey: string;
  subjectName: string;
};
type CatalogFilterOption = { key: string; label: string };
type TeacherLessonGroup = {
  key: string;
  teacherName: string;
  teacherProfileImageUrl?: string;
  subjectNames: string[];
  entries: LessonCatalogEntry[];
};
type LessonCatalogFiltersProps = {
  subjectOptions: CatalogFilterOption[];
  teacherOptions: CatalogFilterOption[];
  subjectFilter: string;
  teacherFilter: string;
  visibleCount: number;
  onSubjectChange: (subjectKey: string) => void;
  onTeacherChange: (teacherKey: string) => void;
  onReset: () => void;
};

const LEVELS: Array<{
  key: CatalogLevel;
  label: string;
  icon: LucideIcon;
}> = [
  { key: 'packages', label: 'المحتوى', icon: Layers3 },
  { key: 'terms', label: 'الأترام', icon: GraduationCap },
  { key: 'months', label: 'الشهور / الأقسام', icon: CalendarDays },
  { key: 'lessons', label: 'الحصص', icon: BookOpen },
];

const levelLabels: Record<CatalogLevel, string> = {
  packages: 'المحتوى المتاح',
  terms: 'الأترام المتاحة',
  months: 'الشهور والأقسام المتاحة',
  lessons: 'الحصص المتاحة',
};

async function fetchTermGroups(packages: PackageDto[]): Promise<TermGroup[]> {
  const termPackages = packages.filter(
    (pkg) => (pkg.contentMode ?? 'TermWithSections') === 'TermWithSections'
  );
  return Promise.all(
    termPackages.map(async (pkg) => ({
      pkg,
      terms: (await contentService.getTerms(pkg.id)).data.data,
    }))
  );
}

async function fetchSectionGroups(
  packages: PackageDto[],
  termGroups: TermGroup[]
): Promise<SectionGroup[]> {
  const directGroups = packages
    .filter((pkg) => pkg.contentMode === 'SectionWithLessons')
    .map((pkg) => ({
      pkg,
      term: undefined,
      sections: (pkg.directSections ?? []) as ContentSectionDto[],
    }));
  const nestedGroups = await Promise.all(
    termGroups.flatMap((group) =>
      group.terms.map(async (term) => ({
        pkg: group.pkg,
        term,
        sections: (await contentService.getSections(term.id)).data.data,
      }))
    )
  );
  return [...directGroups, ...nestedGroups];
}

async function fetchLessonGroups(
  packages: PackageDto[],
  sectionGroups: SectionGroup[]
): Promise<LessonGroup[]> {
  const nestedGroups = await Promise.all(
    sectionGroups.flatMap((group) =>
      group.sections.map(async (section) => ({
        pkg: group.pkg,
        term: group.term,
        section,
        lessons: (await contentService.getLessons(section.id)).data.data,
      }))
    )
  );
  const directGroups = packages
    .filter(
      (pkg) =>
        pkg.contentMode === 'LessonsOnly' || pkg.contentMode === 'SingleLesson'
    )
    .map((pkg) => ({
      pkg,
      lessons: (pkg.directLessons ?? []).map((lesson) => ({
        ...lesson,
        hasAccess: lesson.hasAccess ?? false,
        isCompleted: false,
        videos: [],
      })),
    }));
  return [...directGroups, ...nestedGroups];
}

export function StudentContentCatalog({
  packages,
  onPurchaseComplete,
}: StudentContentCatalogProps) {
  const [level, setLevel] = useState<CatalogLevel>('packages');
  const [selectedPackage, setSelectedPackage] = useState<PackageDto | null>(
    null
  );
  const [selectedTerm, setSelectedTerm] = useState<TermDto | null>(null);
  const [selectedSection, setSelectedSection] =
    useState<ContentSectionDto | null>(null);
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [purchaseTarget, setPurchaseTarget] = useState<PurchaseTarget | null>(
    null
  );
  const [termParents, setTermParents] = useState<Map<string, PackageDto>>(
    new Map()
  );
  const [sectionParents, setSectionParents] = useState<
    Map<string, SectionParent>
  >(new Map());
  const [lessonParents, setLessonParents] = useState<Map<string, LessonParent>>(
    new Map()
  );
  const [lessonSubjectFilter, setLessonSubjectFilter] = useState('');
  const [lessonTeacherFilter, setLessonTeacherFilter] = useState('');

  const clearLessonFilters = useCallback(() => {
    setLessonSubjectFilter('');
    setLessonTeacherFilter('');
  }, []);

  const loadTerms = useCallback(async (packageId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await contentService.getTerms(packageId);
      setTerms(response.data?.data ?? []);
    } catch {
      setError('تعذر تحميل أترام الباقة حاليًا.');
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
      setError('تعذر تحميل الشهور والأقسام حاليًا.');
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
      setError('تعذر تحميل حصص القسم حاليًا.');
    } finally {
      setLoading(false);
    }
  }, []);

  const availableLevels = useMemo(
    () =>
      new Set<CatalogLevel>(
        packages.length > 0 ? LEVELS.map((item) => item.key) : ['packages']
      ),
    [packages.length]
  );

  const lessonCatalogEntries = useMemo(() => {
    const selectedParent = selectedPackage
      ? {
          pkg: selectedPackage,
          term: selectedTerm ?? undefined,
          section: selectedSection ?? undefined,
        }
      : undefined;

    return lessons.map((lesson, catalogIndex) =>
      createLessonCatalogEntry(
        lesson,
        selectedParent ?? lessonParents.get(lesson.id),
        catalogIndex
      )
    );
  }, [lessonParents, lessons, selectedPackage, selectedSection, selectedTerm]);

  const lessonSubjectOptions = useMemo(
    () =>
      uniqueCatalogOptions(
        lessonCatalogEntries.map((entry) => ({
          key: entry.subjectKey,
          label: entry.subjectName,
        }))
      ),
    [lessonCatalogEntries]
  );

  const lessonTeacherOptions = useMemo(
    () =>
      uniqueCatalogOptions(
        lessonCatalogEntries
          .filter(
            (entry) =>
              !lessonSubjectFilter || entry.subjectKey === lessonSubjectFilter
          )
          .map((entry) => ({
            key: entry.teacherKey,
            label: entry.teacherName,
          }))
      ),
    [lessonCatalogEntries, lessonSubjectFilter]
  );

  const visibleLessonEntries = useMemo(
    () =>
      lessonCatalogEntries.filter(
        (entry) =>
          (!lessonSubjectFilter || entry.subjectKey === lessonSubjectFilter) &&
          (!lessonTeacherFilter || entry.teacherKey === lessonTeacherFilter)
      ),
    [lessonCatalogEntries, lessonSubjectFilter, lessonTeacherFilter]
  );

  const teacherLessonGroups = useMemo(
    () => groupLessonEntriesByTeacher(visibleLessonEntries),
    [visibleLessonEntries]
  );

  const loadCatalogLevel = useCallback(
    async (nextLevel: Exclude<CatalogLevel, 'packages'>) => {
      setLoading(true);
      setError(null);
      clearLessonFilters();
      setSelectedPackage(null);
      setSelectedTerm(null);
      setSelectedSection(null);
      try {
        const termGroups = await fetchTermGroups(packages);
        const allTerms = termGroups.flatMap((group) => group.terms);
        const nextTermParents = new Map(
          termGroups.flatMap((group) =>
            group.terms.map((term) => [term.id, group.pkg] as const)
          )
        );
        setTerms(allTerms);
        setTermParents(nextTermParents);
        if (nextLevel === 'terms') return;

        const sectionGroups = await fetchSectionGroups(packages, termGroups);
        const allSections = sectionGroups.flatMap((group) => group.sections);
        const nextSectionParents = new Map(
          sectionGroups.flatMap((group) =>
            group.sections.map(
              (section) =>
                [section.id, { pkg: group.pkg, term: group.term }] as const
            )
          )
        );
        setSections(allSections);
        setSectionParents(nextSectionParents);
        if (nextLevel === 'months') return;

        const lessonGroups = await fetchLessonGroups(packages, sectionGroups);
        setLessons(lessonGroups.flatMap((group) => group.lessons));
        setLessonParents(
          new Map(
            lessonGroups.flatMap((group) =>
              group.lessons.map(
                (lesson) =>
                  [
                    lesson.id,
                    {
                      pkg: group.pkg,
                      term: group.term,
                      section: group.section,
                    },
                  ] as const
              )
            )
          )
        );
      } catch {
        setError(`تعذر تحميل ${levelLabels[nextLevel]} حاليًا.`);
      } finally {
        setLoading(false);
      }
    },
    [clearLessonFilters, packages]
  );

  const choosePackage = (pkg: PackageDto) => {
    clearLessonFilters();
    setSelectedPackage(pkg);
    setSelectedTerm(null);
    setSelectedSection(null);
    setTerms([]);
    setSections([]);
    setLessons([]);
    setError(null);

    if (
      pkg.contentMode === 'LessonsOnly' ||
      pkg.contentMode === 'SingleLesson'
    ) {
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
        }))
      );
      setLevel('lessons');
      return;
    }

    if (pkg.contentMode === 'SectionWithLessons') {
      setSections(pkg.directSections ?? []);
      setLevel('months');
      return;
    }

    setLevel('terms');
    void loadTerms(pkg.id);
  };

  const chooseTerm = (term: TermDto) => {
    const parentPackage = selectedPackage ?? termParents.get(term.id) ?? null;
    if (!parentPackage) return;
    clearLessonFilters();
    setSelectedPackage(parentPackage);
    setSelectedTerm(term);
    setSelectedSection(null);
    setSections([]);
    setLessons([]);
    setLevel('months');
    void loadSections(term.id);
  };

  const chooseSection = (section: ContentSectionDto) => {
    const parent = sectionParents.get(section.id);
    clearLessonFilters();
    if (!selectedPackage && parent) {
      setSelectedPackage(parent.pkg);
      setSelectedTerm(parent.term ?? null);
    }
    setSelectedSection(section);
    setLessons([]);
    setLevel('lessons');
    void loadLessons(section.id);
  };

  const resetToPackages = () => {
    clearLessonFilters();
    setLevel('packages');
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
    if (nextLevel === 'packages') {
      resetToPackages();
      return;
    }
    const canUseCurrentPath =
      (nextLevel === 'terms' &&
        selectedPackage?.contentMode === 'TermWithSections') ||
      (nextLevel === 'months' &&
        (selectedTerm !== null ||
          selectedPackage?.contentMode === 'SectionWithLessons')) ||
      (nextLevel === 'lessons' &&
        (selectedSection !== null ||
          selectedPackage?.contentMode === 'LessonsOnly' ||
          selectedPackage?.contentMode === 'SingleLesson'));
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

    if (selectedPackage && level === 'terms') {
      await loadTerms(selectedPackage.id);
    } else if (selectedTerm && level === 'months') {
      await loadSections(selectedTerm.id);
    } else if (selectedSection && level === 'lessons') {
      await loadLessons(selectedSection.id);
    }
  };

  return (
    <section
      className="space-y-6"
      aria-labelledby="student-content-catalog-title"
    >
      <div className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">
        <div className="pointer-events-none absolute -left-24 -top-24 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-3xl" />
        <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl text-right">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              <ShoppingBag className="h-3.5 w-3.5" />
              <span>متجر المحتوى التعليمي</span>
            </div>
            <h2
              id="student-content-catalog-title"
              className="text-2xl font-black tracking-tight text-[var(--admin-text)] sm:text-3xl"
            >
              اشتري الجزء الذي تحتاجه مباشرة
            </h2>
            <p className="mt-2 text-sm font-medium leading-7 text-[var(--admin-muted)]">
              اختار باقة كاملة أو ترم أو شهر أو حصة، وشوف السعر وحالة التفعيل
              قبل ما تبدأ الشراء.
            </p>
          </div>
          <div className="flex items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3 text-sm font-black text-[var(--admin-text)]">
            <WalletCards className="h-5 w-5 text-[var(--admin-primary)]" />
            <span>الدفع من رصيدك</span>
          </div>
        </div>

        <nav
          aria-label="مستويات المحتوى"
          className="relative mt-7 overflow-x-auto border-t border-[var(--admin-border)] pt-4"
        >
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
                        ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-sm'
                        : isAvailable
                          ? 'text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)]'
                          : 'cursor-not-allowed text-[var(--admin-muted)]/40'
                    }`}
                  >
                    <Icon className="h-4 w-4" />
                    {item.label}
                  </button>
                  {index < LEVELS.length - 1 ? (
                    <ChevronLeft
                      className="h-4 w-4 text-[var(--admin-border)]"
                      aria-hidden
                    />
                  ) : null}
                </div>
              );
            })}
          </div>
        </nav>
      </div>

      {selectedPackage && level !== 'packages' ? (
        <div className="flex flex-wrap items-center gap-2 text-sm font-bold text-[var(--admin-muted)]">
          <button
            type="button"
            onClick={resetToPackages}
            className="transition hover:text-[var(--admin-primary)]"
          >
            المحتوى
          </button>
          <ChevronLeft className="h-4 w-4" aria-hidden />
          <button
            type="button"
            onClick={() =>
              goToLevel(
                selectedPackage.contentMode === 'TermWithSections'
                  ? 'terms'
                  : selectedPackage.contentMode === 'SectionWithLessons'
                    ? 'months'
                    : 'lessons'
              )
            }
            className="max-w-56 truncate text-[var(--admin-text)] transition hover:text-[var(--admin-primary)]"
          >
            {selectedPackage.name}
          </button>
          {selectedTerm ? (
            <>
              <ChevronLeft className="h-4 w-4" aria-hidden />
              <button
                type="button"
                onClick={() => goToLevel('months')}
                className="max-w-56 truncate text-[var(--admin-text)] transition hover:text-[var(--admin-primary)]"
              >
                {selectedTerm.title}
              </button>
            </>
          ) : null}
          {selectedSection ? (
            <>
              <ChevronLeft className="h-4 w-4" aria-hidden />
              <span className="max-w-56 truncate text-[var(--admin-text)]">
                {selectedSection.title}
              </span>
            </>
          ) : null}
        </div>
      ) : null}

      {error ? (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-5 py-4 text-sm font-bold text-[var(--admin-danger)]"
        >
          <span>{error}</span>
          <button
            type="button"
            onClick={() => {
              if (selectedPackage && level === 'terms')
                void loadTerms(selectedPackage.id);
              else if (selectedTerm && level === 'months')
                void loadSections(selectedTerm.id);
              else if (selectedSection && level === 'lessons')
                void loadLessons(selectedSection.id);
              else if (level !== 'packages') void loadCatalogLevel(level);
            }}
            className="min-h-10 rounded-xl border border-current px-4 transition hover:bg-[var(--admin-danger)]/10"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : null}

      {loading ? <CatalogSkeleton /> : null}

      {!loading && level === 'packages' ? (
        <div className="space-y-4">
          <CatalogHeading
            title={levelLabels.packages}
            description="اختر باقة أو ترمًا أو قسمًا أو حصة مستقلة، ثم استعرض المحتوى أو اشتره مباشرة."
          />
          {packages.length === 0 ? (
            <CatalogEmpty message="لا يوجد محتوى متاح لبياناتك الدراسية حاليًا." />
          ) : (
            <div className="grid gap-4 lg:grid-cols-2">
              {packages.map((pkg) => {
                const rootReference = getContentRootPurchaseReference(pkg);
                const rootPurchaseTarget: PurchaseTarget | null = rootReference
                  ? {
                      ...rootReference,
                      contentName: pkg.name,
                      price: pkg.price ?? 0,
                    }
                  : null;
                return (
                  <PackageCatalogCard
                    key={pkg.id}
                    pkg={pkg}
                    onExplore={() => choosePackage(pkg)}
                    onPurchase={
                      rootPurchaseTarget
                        ? () => setPurchaseTarget(rootPurchaseTarget)
                        : undefined
                    }
                  />
                );
              })}
            </div>
          )}
        </div>
      ) : null}

      {!loading && level === 'terms' ? (
        <CatalogLevelPanel
          title={levelLabels.terms}
          description="كل ترم له سعر مستقل، وبعد شرائه تظهر لك الشهور والحصص التابعة له."
          onBack={() => goToLevel('packages')}
          emptyMessage="لا توجد أترام متاحة داخل هذه الباقة حاليًا."
          isEmpty={terms.length === 0}
        >
          <div className="grid gap-4 lg:grid-cols-2">
            {terms.map((term, index) => {
              const parentPackage = selectedPackage ?? termParents.get(term.id);
              return (
                <TermCatalogCard
                  key={term.id}
                  term={term}
                  pkg={parentPackage}
                  index={index}
                  isPackageOwned={
                    parentPackage?.hasDirectPackageAccess ?? false
                  }
                  onExplore={() => chooseTerm(term)}
                  onPurchase={() =>
                    setPurchaseTarget({
                      contentType: 'Term',
                      contentId: term.id,
                      contentName: term.title,
                      price: term.price ?? 0,
                    })
                  }
                />
              );
            })}
          </div>
        </CatalogLevelPanel>
      ) : null}

      {!loading && level === 'months' ? (
        <CatalogLevelPanel
          title={levelLabels.months}
          description="الشهر هو القسم الدراسي. افتحه لمشاهدة حصصه أو اشتريه منفردًا."
          onBack={() =>
            goToLevel(
              selectedPackage?.contentMode === 'TermWithSections'
                ? 'terms'
                : 'packages'
            )
          }
          emptyMessage="لا توجد شهور أو أقسام متاحة داخل هذا المسار حاليًا."
          isEmpty={sections.length === 0}
        >
          <div className="grid gap-4 lg:grid-cols-2">
            {sections.map((section, index) => {
              const parent = selectedPackage
                ? { pkg: selectedPackage, term: selectedTerm ?? undefined }
                : sectionParents.get(section.id);
              return (
                <SectionCatalogCard
                  key={section.id}
                  section={section}
                  parent={parent}
                  index={index}
                  isParentOwned={
                    (parent?.pkg.hasDirectPackageAccess ?? false) ||
                    parent?.term?.isPurchased === true
                  }
                  onExplore={() => chooseSection(section)}
                  onPurchase={() =>
                    setPurchaseTarget({
                      contentType: 'Month',
                      contentId: section.id,
                      contentName: section.title,
                      price: section.price ?? 0,
                    })
                  }
                />
              );
            })}
          </div>
        </CatalogLevelPanel>
      ) : null}

      {!loading && level === 'lessons' ? (
        <CatalogLevelPanel
          title={levelLabels.lessons}
          description="اختار الحصة التي تريدها وابدأ فورًا، أو اشتريها منفردة إذا لم تكن مفعّلة."
          onBack={() =>
            goToLevel(
              selectedSection
                ? 'months'
                : selectedPackage?.contentMode === 'TermWithSections'
                  ? 'terms'
                  : 'packages'
            )
          }
          emptyMessage="لا توجد حصص متاحة داخل هذا المسار حاليًا."
          isEmpty={lessons.length === 0}
        >
          <div className="space-y-10">
            <LessonCatalogFilters
              subjectOptions={lessonSubjectOptions}
              teacherOptions={lessonTeacherOptions}
              subjectFilter={lessonSubjectFilter}
              teacherFilter={lessonTeacherFilter}
              visibleCount={visibleLessonEntries.length}
              onSubjectChange={(subjectKey) => {
                setLessonSubjectFilter(subjectKey);
                setLessonTeacherFilter('');
              }}
              onTeacherChange={setLessonTeacherFilter}
              onReset={clearLessonFilters}
            />

            {teacherLessonGroups.length > 0 ? (
              teacherLessonGroups.map((teacherGroup, groupIndex) => (
                <TeacherLessonSection
                  key={teacherGroup.key}
                  teacherGroup={teacherGroup}
                  groupIndex={groupIndex}
                  onPurchase={(lesson) =>
                    setPurchaseTarget({
                      contentType: 'Lesson',
                      contentId: lesson.id,
                      contentName: lesson.title,
                      price: lesson.price ?? 0,
                    })
                  }
                />
              ))
            ) : (
              <CatalogEmpty message="لا توجد حصص مطابقة للفلاتر الحالية. جرّب اختيار مادة أو مدرس آخر." />
            )}
          </div>
        </CatalogLevelPanel>
      ) : null}

      <PurchaseContentModal
        isOpen={purchaseTarget !== null}
        onClose={() => setPurchaseTarget(null)}
        onPurchaseSuccess={handlePurchaseComplete}
        contentType={purchaseTarget?.contentType ?? 'Package'}
        contentId={purchaseTarget?.contentId ?? ''}
        contentName={purchaseTarget?.contentName ?? 'المحتوى'}
        price={purchaseTarget?.price ?? 0}
      />
    </section>
  );
}

function CatalogHeading({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="border-b border-[var(--admin-border)] pb-4 text-right">
      <h3 className="text-2xl font-black text-[var(--admin-text)]">{title}</h3>
      <p className="mt-1 text-sm font-medium leading-6 text-[var(--admin-muted)]">
        {description}
      </p>
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
        <button
          type="button"
          onClick={onBack}
          className="inline-flex min-h-10 items-center gap-2 rounded-xl px-3 text-sm font-black text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)]"
        >
          <ArrowRight className="h-4 w-4" />
          رجوع
        </button>
      </div>
      {isEmpty ? <CatalogEmpty message={emptyMessage} /> : children}
    </div>
  );
}

function PackageCatalogCard({
  pkg,
  onExplore,
  onPurchase,
}: {
  pkg: PackageDto;
  onExplore: () => void;
  onPurchase?: () => void;
}) {
  const hasRootAccess =
    pkg.hasRootContentAccess ?? pkg.hasDirectPackageAccess ?? false;
  const hasPartialAccess = !hasRootAccess && pkg.isEnrolled;
  const contentRootLabel = getContentRootLabel(
    pkg.contentMode ?? 'TermWithSections'
  );
  const coverUrl = getCatalogImageUrl(
    pkg.imageUrl,
    pkg.teacherProfileImageUrl,
    '/images/default-package.webp'
  );

  return (
    <article className="group overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="grid min-h-full sm:grid-cols-[10rem_minmax(0,1fr)]">
        <CatalogCover
          imageUrl={coverUrl}
          alt={`غلاف ${pkg.name}`}
          indexLabel={contentRootLabel}
        />
        <div className="flex min-w-0 flex-col p-5 text-right">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <AccessBadge
              state={
                hasRootAccess
                  ? 'owned'
                  : hasPartialAccess
                    ? 'partial'
                    : 'available'
              }
            />
            <SubjectBadge subjectName={getSubjectName(pkg)} />
          </div>
          <h3 className="mt-3 line-clamp-2 text-xl font-black leading-7 text-[var(--admin-text)]">
            {pkg.name}
          </h3>
          <p className="mt-2 line-clamp-2 text-sm font-medium leading-6 text-[var(--admin-muted)]">
            {pkg.description || `استعرض محتوى ${contentRootLabel} قبل البدء.`}
          </p>
          <CatalogTeacher pkg={pkg} />
          <div className="mt-auto flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-4">
            <PriceLabel price={pkg.price} />
            <div className="flex flex-1 flex-wrap justify-end gap-2 sm:flex-none">
              <CatalogOutlineButton
                label="استعرض المحتوى"
                onClick={onExplore}
              />
              {!hasRootAccess && onPurchase ? (
                <PurchaseButton
                  label={`شراء ${contentRootLabel}`}
                  onClick={onPurchase}
                />
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

function TermCatalogCard({
  term,
  pkg,
  index,
  isPackageOwned,
  onExplore,
  onPurchase,
}: {
  term: TermDto;
  pkg?: PackageDto;
  index: number;
  isPackageOwned: boolean;
  onExplore: () => void;
  onPurchase: () => void;
}) {
  const isOwned = isPackageOwned || term.isPurchased === true;
  const coverUrl = getCatalogImageUrl(
    term.imageUrl,
    pkg?.imageUrl,
    pkg?.teacherProfileImageUrl,
    '/images/default-package.webp'
  );

  return (
    <article className="group overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="grid min-h-full sm:grid-cols-[9rem_minmax(0,1fr)]">
        <CatalogCover
          imageUrl={coverUrl}
          alt={`غلاف ${term.title}`}
          indexLabel={String(index + 1).padStart(2, '0')}
        />
        <div className="flex min-w-0 flex-col p-5 text-right">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <AccessBadge state={isOwned ? 'owned' : 'available'} />
            <SubjectBadge subjectName={getSubjectName(pkg)} />
          </div>
          <p className="mt-3 line-clamp-1 text-xs font-bold text-[var(--admin-muted)]">
            {pkg?.name ?? 'المسار التعليمي'}
          </p>
          <h4 className="mt-1 line-clamp-2 text-lg font-black leading-7 text-[var(--admin-text)]">
            {term.title}
          </h4>
          <CatalogTeacher pkg={pkg} />
          <div className="mt-auto flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-4">
            <PriceLabel price={term.price} />
            <div className="flex flex-wrap gap-2">
              <CatalogOutlineButton label="عرض الأقسام" onClick={onExplore} />
              {!isOwned ? (
                <PurchaseButton label="شراء الترم" onClick={onPurchase} />
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

function SectionCatalogCard({
  section,
  parent,
  index,
  isParentOwned,
  onExplore,
  onPurchase,
}: {
  section: ContentSectionDto;
  parent?: SectionParent;
  index: number;
  isParentOwned: boolean;
  onExplore: () => void;
  onPurchase: () => void;
}) {
  const isOwned = isParentOwned || section.isPurchased === true;
  const coverUrl = getCatalogImageUrl(
    section.imageUrl,
    parent?.term?.imageUrl,
    parent?.pkg.imageUrl,
    parent?.pkg.teacherProfileImageUrl,
    '/images/default-package.webp'
  );

  return (
    <article className="group overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="grid min-h-full sm:grid-cols-[9rem_minmax(0,1fr)]">
        <CatalogCover
          imageUrl={coverUrl}
          alt={`غلاف ${section.title}`}
          indexLabel={String(index + 1).padStart(2, '0')}
        />
        <div className="flex min-w-0 flex-col p-5 text-right">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <AccessBadge state={isOwned ? 'owned' : 'available'} />
            <SubjectBadge subjectName={getSubjectName(parent?.pkg)} />
          </div>
          <p className="mt-3 line-clamp-1 text-xs font-bold text-[var(--admin-muted)]">
            {[parent?.pkg.name, parent?.term?.title]
              .filter(Boolean)
              .join(' · ') || 'المسار التعليمي'}
          </p>
          <h4 className="mt-1 line-clamp-2 text-lg font-black leading-7 text-[var(--admin-text)]">
            {section.title}
          </h4>
          <CatalogTeacher pkg={parent?.pkg} />
          <div className="mt-auto flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-4">
            <PriceLabel price={section.price} />
            <div className="flex flex-wrap gap-2">
              <CatalogOutlineButton label="عرض الحصص" onClick={onExplore} />
              {!isOwned ? (
                <PurchaseButton label="شراء الشهر" onClick={onPurchase} />
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

function LessonCatalogFilters({
  subjectOptions,
  teacherOptions,
  subjectFilter,
  teacherFilter,
  visibleCount,
  onSubjectChange,
  onTeacherChange,
  onReset,
}: LessonCatalogFiltersProps) {
  const hasActiveFilter = Boolean(subjectFilter || teacherFilter);

  return (
    <section
      aria-label="تصفية الحصص"
      className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 sm:p-5"
    >
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
            <Filter
              className="h-4 w-4 text-[var(--admin-primary)]"
              aria-hidden
            />
            اختر المادة والمدرس
          </div>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
            المعروض الآن: {visibleCount.toLocaleString('ar-EG-u-nu-latn')} حصة
          </p>
        </div>

        <div className="grid w-full gap-3 sm:grid-cols-2 lg:w-auto lg:min-w-[32rem]">
          <label className="grid gap-1.5 text-xs font-black text-[var(--admin-muted)]">
            المادة
            <select
              value={subjectFilter}
              onChange={(event) => onSubjectChange(event.target.value)}
              className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-base font-black text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)] sm:text-sm"
            >
              <option value="">كل المواد</option>
              {subjectOptions.map((subject) => (
                <option key={subject.key} value={subject.key}>
                  {subject.label}
                </option>
              ))}
            </select>
          </label>

          <label className="grid gap-1.5 text-xs font-black text-[var(--admin-muted)]">
            المدرس
            <select
              value={teacherFilter}
              onChange={(event) => onTeacherChange(event.target.value)}
              className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-base font-black text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)] sm:text-sm"
            >
              <option value="">كل المدرسين</option>
              {teacherOptions.map((teacher) => (
                <option key={teacher.key} value={teacher.key}>
                  {teacher.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {hasActiveFilter ? (
        <button
          type="button"
          onClick={onReset}
          className="mt-3 inline-flex min-h-11 items-center rounded-xl px-3 text-sm font-black text-[var(--admin-primary)] transition hover:bg-[var(--admin-primary-10)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          مسح الفلاتر وعرض كل الحصص
        </button>
      ) : null}
    </section>
  );
}

function TeacherLessonSection({
  teacherGroup,
  groupIndex,
  onPurchase,
}: {
  teacherGroup: TeacherLessonGroup;
  groupIndex: number;
  onPurchase: (lesson: LessonSummaryDto) => void;
}) {
  const headingId = `teacher-lessons-${groupIndex}`;
  const profileImageUrl = teacherGroup.teacherProfileImageUrl
    ? resolveMediaUrl(teacherGroup.teacherProfileImageUrl)
    : null;

  return (
    <section aria-labelledby={headingId} className="space-y-4">
      <header className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
        <div className="flex min-w-0 items-center gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-[var(--admin-primary-20)] bg-[var(--admin-card)] text-[var(--admin-primary)]">
            {profileImageUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={profileImageUrl}
                alt={`صورة ${teacherGroup.teacherName}`}
                loading="lazy"
                decoding="async"
                className="h-full w-full object-cover"
              />
            ) : teacherGroup.teacherName !== 'غير محدد' ? (
              <span className="text-lg font-black">
                {teacherGroup.teacherName.charAt(0)}
              </span>
            ) : (
              <UserRound className="h-5 w-5" aria-hidden />
            )}
          </div>

          <div className="min-w-0">
            <span className="text-xs font-black text-[var(--admin-primary)]">
              حصص المدرس
            </span>
            <h4
              id={headingId}
              className="truncate text-xl font-black text-[var(--admin-text)] sm:text-2xl"
            >
              {teacherGroup.teacherName === 'غير محدد'
                ? 'لم يُحدَّد المدرس'
                : `أ. ${teacherGroup.teacherName}`}
            </h4>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {teacherGroup.subjectNames.map((subjectName) => (
                <span
                  key={subjectName}
                  className="rounded-lg bg-[var(--admin-card)] px-2 py-1 text-xs font-black text-[var(--admin-primary)]"
                >
                  {subjectName === 'غير محدد' ? 'مادة غير محددة' : subjectName}
                </span>
              ))}
            </div>
          </div>
        </div>

        <span className="inline-flex min-h-10 w-fit shrink-0 items-center rounded-xl border border-[var(--admin-primary-20)] bg-[var(--admin-card)] px-3 text-sm font-black text-[var(--admin-text)]">
          {teacherGroup.entries.length.toLocaleString('ar-EG-u-nu-latn')} حصة
        </span>
      </header>

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
        {teacherGroup.entries.map((entry) => (
          <LessonCatalogCard
            key={entry.lesson.id}
            lesson={entry.lesson}
            index={entry.catalogIndex}
            parent={entry.parent}
            onPurchase={() => onPurchase(entry.lesson)}
          />
        ))}
      </div>
    </section>
  );
}

function LessonCatalogCard({
  lesson,
  index,
  parent,
  onPurchase,
}: {
  lesson: LessonSummaryDto;
  index: number;
  parent?: LessonParent;
  onPurchase: () => void;
}) {
  const isOwned = lesson.hasAccess;
  const packageId = parent?.pkg.id;
  const lessonHref = packageId
    ? `/student/packages/${packageId}/lessons/${lesson.id}`
    : `/student/lessons/${lesson.id}`;
  const coverUrl = getCatalogImageUrl(
    parent?.section?.imageUrl,
    parent?.term?.imageUrl,
    parent?.pkg.imageUrl,
    parent?.pkg.teacherProfileImageUrl,
    '/images/lesson-placeholder.webp'
  );
  const path = [parent?.pkg.name, parent?.term?.title, parent?.section?.title]
    .filter(Boolean)
    .join(' · ');

  return (
    <article className="group flex min-h-full flex-col overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] text-right transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] hover:shadow-md">
      <div className="relative aspect-[16/9] overflow-hidden bg-[var(--admin-card-soft)]">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={coverUrl}
          alt={`غلاف الحصة ${lesson.title}`}
          loading="lazy"
          decoding="async"
          className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.03]"
        />
        <div
          className="absolute inset-x-0 bottom-0 h-20 bg-gradient-to-t from-black/65 to-transparent"
          aria-hidden
        />
        <span className="absolute right-3 top-3 inline-flex min-h-8 items-center rounded-lg border border-white/20 bg-black/55 px-2.5 text-xs font-black text-white backdrop-blur-sm">
          الحصة {String(index + 1).padStart(2, '0')}
        </span>
        <span
          className={`absolute bottom-3 left-3 inline-flex min-h-8 items-center gap-1 rounded-lg px-2.5 text-xs font-black shadow-sm ${isOwned ? 'bg-[var(--admin-success)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-warning)] text-[var(--admin-primary-contrast)]'}`}
        >
          {isOwned ? (
            <CheckCircle2 className="h-3.5 w-3.5" />
          ) : (
            <LockKeyhole className="h-3.5 w-3.5" />
          )}
          {isOwned ? 'متاحة لك' : 'متاحة للشراء'}
        </span>
      </div>

      <div className="flex flex-1 flex-col p-5">
        <div className="flex items-start justify-between gap-3">
          <SubjectBadge subjectName={getSubjectName(parent?.pkg)} />
          {lesson.isCompleted ? (
            <span className="shrink-0 rounded-full bg-[var(--admin-success-10)] px-2.5 py-1 text-xs font-black text-[var(--admin-success)]">
              مكتملة
            </span>
          ) : null}
        </div>
        <p
          className="mt-3 line-clamp-1 text-xs font-bold text-[var(--admin-muted)]"
          title={path || undefined}
        >
          {path || 'المسار التعليمي'}
        </p>
        <h4 className="mt-1 line-clamp-2 text-xl font-black leading-8 text-[var(--admin-text)]">
          {lesson.title}
        </h4>
        <p className="mt-2 line-clamp-2 min-h-12 text-sm font-medium leading-6 text-[var(--admin-muted)]">
          {lesson.summary ||
            'كل تفاصيل الحصة والمحتوى المرفق تظهر لك قبل إتمام الشراء.'}
        </p>

        <div className="mt-auto flex items-end justify-between gap-3 border-t border-[var(--admin-border)] pt-4">
          <PriceLabel price={lesson.price} />
          {isOwned ? (
            <Link
              href={lessonHref}
              prefetch={false}
              className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
            >
              <PlayCircle className="h-4 w-4" />
              ابدأ الحصة
            </Link>
          ) : (
            <div className="grid flex-1 grid-cols-2 gap-2 sm:flex sm:flex-none">
              <Link
                href={lessonHref}
                prefetch={false}
                className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[var(--admin-border)] px-3 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
              >
                التفاصيل
              </Link>
              <PurchaseButton label="شراء الحصة" onClick={onPurchase} />
            </div>
          )}
        </div>
      </div>
    </article>
  );
}

function CatalogCover({
  imageUrl,
  alt,
  indexLabel,
}: {
  imageUrl: string;
  alt: string;
  indexLabel: string;
}) {
  return (
    <div className="relative min-h-40 overflow-hidden bg-[var(--admin-card-soft)] sm:min-h-full">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={imageUrl}
        alt={alt}
        loading="lazy"
        decoding="async"
        className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.03]"
      />
      <span className="absolute right-3 top-3 inline-flex min-h-8 items-center rounded-lg border border-white/20 bg-black/55 px-2.5 text-xs font-black text-white backdrop-blur-sm">
        {indexLabel}
      </span>
    </div>
  );
}

function CatalogTeacher({ pkg }: { pkg?: PackageDto }) {
  const teacherName = getTeacherName(pkg);
  const profileImageUrl = pkg?.teacherProfileImageUrl
    ? resolveMediaUrl(pkg.teacherProfileImageUrl)
    : null;

  return (
    <div className="my-4 flex min-w-0 items-center gap-3">
      <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-primary-10)] text-[var(--admin-primary)]">
        {profileImageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={profileImageUrl}
            alt={`صورة ${teacherName}`}
            loading="lazy"
            decoding="async"
            className="h-full w-full object-cover"
          />
        ) : teacherName !== 'غير محدد' ? (
          <span className="text-sm font-black">{teacherName.charAt(0)}</span>
        ) : (
          <UserRound className="h-4 w-4" aria-hidden />
        )}
      </div>
      <div className="min-w-0">
        <span className="block text-[0.68rem] font-bold text-[var(--admin-muted)]">
          المدرس
        </span>
        <span className="block truncate text-sm font-black text-[var(--admin-text)]">
          {teacherName === 'غير محدد'
            ? 'لم يُحدَّد المدرس'
            : `أ. ${teacherName}`}
        </span>
      </div>
    </div>
  );
}

function SubjectBadge({ subjectName }: { subjectName: string }) {
  return (
    <span className="inline-flex min-h-8 max-w-full items-center gap-1.5 rounded-lg bg-[var(--admin-primary-10)] px-2.5 text-xs font-black text-[var(--admin-primary)]">
      <BookOpenCheck className="h-3.5 w-3.5 shrink-0" aria-hidden />
      <span className="truncate">
        المادة: {subjectName === 'غير محدد' ? 'غير محددة' : subjectName}
      </span>
    </span>
  );
}

function AccessBadge({ state }: { state: 'owned' | 'partial' | 'available' }) {
  const isOwned = state === 'owned';
  const isPartial = state === 'partial';
  return (
    <span
      className={`inline-flex min-h-8 items-center gap-1 rounded-lg px-2.5 text-xs font-black ${isOwned ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : isPartial ? 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'}`}
    >
      {isOwned ? (
        <CheckCircle2 className="h-3.5 w-3.5" />
      ) : (
        <LockKeyhole className="h-3.5 w-3.5" />
      )}
      {isOwned ? 'مفعّل' : isPartial ? 'محتوى مفعّل' : 'متاح للشراء'}
    </span>
  );
}

function PriceLabel({ price = 0 }: { price?: number }) {
  return (
    <div className="shrink-0">
      <span className="block text-[0.68rem] font-bold text-[var(--admin-muted)]">
        السعر
      </span>
      <span className="text-lg font-black text-[var(--admin-primary)]">
        {price > 0
          ? `${price.toLocaleString('ar-EG-u-nu-latn')} ج.م`
          : 'مجانًا'}
      </span>
    </div>
  );
}

function CatalogOutlineButton({
  label,
  onClick,
}: {
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex min-h-11 items-center justify-center gap-1 rounded-xl border border-[var(--admin-border)] px-3.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
    >
      {label}
      <ChevronLeft className="h-4 w-4" />
    </button>
  );
}

function createLessonCatalogEntry(
  lesson: LessonSummaryDto,
  parent: LessonParent | undefined,
  catalogIndex: number
): LessonCatalogEntry {
  const teacherName = getTeacherName(parent?.pkg);
  const subjectName = getSubjectName(parent?.pkg);

  return {
    lesson,
    parent,
    catalogIndex,
    teacherKey: parent?.pkg.teacherId ?? `teacher:${teacherName}`,
    teacherName,
    subjectKey: parent?.pkg.subjectId ?? `subject:${subjectName}`,
    subjectName,
  };
}

function uniqueCatalogOptions(
  catalogOptions: CatalogFilterOption[]
): CatalogFilterOption[] {
  return Array.from(
    new Map(catalogOptions.map((option) => [option.key, option])).values()
  ).sort((first, second) => first.label.localeCompare(second.label, 'ar'));
}

function groupLessonEntriesByTeacher(
  entries: LessonCatalogEntry[]
): TeacherLessonGroup[] {
  const teacherGroups = new Map<string, TeacherLessonGroup>();

  for (const entry of entries) {
    addEntryToTeacherGroup(teacherGroups, entry);
  }

  return Array.from(teacherGroups.values());
}

function addEntryToTeacherGroup(
  teacherGroups: Map<string, TeacherLessonGroup>,
  entry: LessonCatalogEntry
) {
  const existingGroup = teacherGroups.get(entry.teacherKey);
  if (existingGroup) {
    existingGroup.entries.push(entry);
    if (!existingGroup.subjectNames.includes(entry.subjectName)) {
      existingGroup.subjectNames.push(entry.subjectName);
    }
    return;
  }

  teacherGroups.set(entry.teacherKey, {
    key: entry.teacherKey,
    teacherName: entry.teacherName,
    teacherProfileImageUrl: entry.parent?.pkg.teacherProfileImageUrl,
    subjectNames: [entry.subjectName],
    entries: [entry],
  });
}

function getCatalogImageUrl(
  ...candidates: Array<string | null | undefined>
): string {
  const source = candidates
    .find(
      (candidate) =>
        candidate?.trim() && candidate.trim().toLowerCase() !== 'unknown'
    )
    ?.trim();
  if (!source) return '/images/lesson-placeholder.webp';
  return source.startsWith('/images/') ? source : resolveMediaUrl(source);
}

function getTeacherName(pkg?: PackageDto): string {
  const teacherName = pkg?.teacherName?.trim();
  return teacherName && teacherName.toLowerCase() !== 'unknown'
    ? teacherName
    : 'غير محدد';
}

function getSubjectName(pkg?: PackageDto): string {
  const subjectName = pkg?.subjectName?.trim();
  return subjectName && subjectName.toLowerCase() !== 'unknown'
    ? subjectName
    : 'غير محدد';
}

function PurchaseButton({
  label,
  onClick,
}: {
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl bg-[var(--admin-primary)] px-3.5 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
    >
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
      {[1, 2, 3, 4].map((item) => (
        <div
          key={item}
          className="h-44 animate-pulse rounded-[1.5rem] bg-[var(--admin-card-strong)]"
        />
      ))}
    </div>
  );
}
