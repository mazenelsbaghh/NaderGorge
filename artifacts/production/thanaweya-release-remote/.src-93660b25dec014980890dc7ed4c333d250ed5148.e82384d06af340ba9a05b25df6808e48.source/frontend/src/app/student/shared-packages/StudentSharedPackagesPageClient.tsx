'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { AnimatePresence, motion, useReducedMotion, type Transition } from 'framer-motion';
import {
  ArrowLeft,
  ArrowRight,
  BookOpen,
  CheckCircle2,
  CheckIcon,
  ChevronLeft,
  Clock3,
  GraduationCap,
  LoaderCircleIcon,
  PackageOpen,
  RefreshCw,
  ShieldCheck,
  ShoppingCart,
  Sparkles,
  Users,
  type LucideIcon,
} from 'lucide-react';
import toast from 'react-hot-toast';
import {
  Stepper,
  StepperContent,
  StepperIndicator,
  StepperItem,
  StepperNav,
  StepperPanel,
  StepperTitle,
  StepperTrigger,
} from '@/components/reui/stepper';
import {
  sharedPackageService,
  type PurchasedSharedPackage,
  type SharedPackageDetail,
  type SharedPackageListItem,
} from '@/services/shared-package-service';
import { getEducationStageLabel, getGradeLevelLabel } from '@/lib/academic-labels';
import { cn } from '@/lib/utils';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { registerCacheStore } from '@/lib/cache-invalidation';

type SelectionState = Record<string, Record<string, string>>;
type StepState = Record<string, number>;

type TeacherChoice = {
  teacherId: string;
  teacherName: string;
  teacherProfileImageUrl?: string;
  contentCount: number;
};

type SubjectChoiceGroup = {
  subjectKey: string;
  subjectId?: string;
  subjectName: string;
  price: number;
  teachers: TeacherChoice[];
};

export default function StudentSharedPackagesPageClient() {
  const [items, setItems] = useState<SharedPackageListItem[]>([]);
  const [purchasedItems, setPurchasedItems] = useState<PurchasedSharedPackage[]>([]);
  const [details, setDetails] = useState<Record<string, SharedPackageDetail>>({});
  const [selections, setSelections] = useState<SelectionState>({});
  const [activeSteps, setActiveSteps] = useState<StepState>({});
  const [loading, setLoading] = useState(true);
  const [buyingId, setBuyingId] = useState<string | null>(null);
  const [loadingDetailId, setLoadingDetailId] = useState<string | null>(null);
  const [choosingPackageId, setChoosingPackageId] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [available, purchased] = await Promise.all([
        sharedPackageService.listStudent(),
        sharedPackageService.listPurchasedStudent(),
      ]);
      setItems(available);
      setPurchasedItems(purchased);
    } catch {
      toast.error('تعذر تحميل الباكدجات المشتركة');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const cleanupCacheStore = registerCacheStore('shared-packages:student', () => {}, () => void load());
    void load();
    return cleanupCacheStore;
  }, []);

  const openDetail = async (id: string) => {
    if (details[id]) {
      setActiveSteps((current) => ({ ...current, [id]: current[id] ?? 1 }));
      return details[id];
    }

    setLoadingDetailId(id);
    try {
      const detail = await sharedPackageService.detailStudent(id);
      setDetails((current) => ({ ...current, [id]: detail }));
      // A package may contain only one teacher for a subject, but the student must
      // still explicitly confirm that choice. Never silently select a teacher.
      setSelections((current) => ({ ...current, [id]: current[id] ?? {} }));
      setActiveSteps((current) => ({ ...current, [id]: 1 }));
      return detail;
    } catch {
      toast.error('تعذر تحميل تفاصيل الباكدج');
      return null;
    } finally {
      setLoadingDetailId(null);
    }
  };

  const closeDetail = (id: string) => {
    setDetails((current) => {
      const next = { ...current };
      delete next[id];
      return next;
    });
    setActiveSteps((current) => {
      const next = { ...current };
      delete next[id];
      return next;
    });
    setChoosingPackageId((current) => (current === id ? null : current));
  };

  const startSelection = async (id: string) => {
    const detail = await openDetail(id);
    if (!detail) return;
    setChoosingPackageId(id);
    requestAnimationFrame(() => window.scrollTo({ top: 0, behavior: 'smooth' }));
  };

  const purchase = async (id: string) => {
    const detail = details[id] ?? await openDetail(id);
    if (!detail) return;

    const grouped = groupTeachersBySubject(detail);
    const packageSelections = selections[id] ?? {};
    const missingSubject = grouped.find((group) => !packageSelections[group.subjectKey]);
    if (missingSubject) {
      toast.error(`اختر مدرساً واحداً لمادة ${missingSubject.subjectName}`);
      setSelections((current) => ({ ...current, [id]: packageSelections }));
      setActiveSteps((current) => ({ ...current, [id]: grouped.indexOf(missingSubject) + 1 }));
      return;
    }

    setBuyingId(id);
    try {
      const res = await sharedPackageService.purchaseStudent(id, {
        selections: grouped.map((group) => ({
          subjectId: group.subjectId,
          teacherId: packageSelections[group.subjectKey],
        })),
      });
      if (!res.success) {
        toast.error(res.message || 'تعذر الشراء');
        return;
      }
      toast.success('تم تفعيل الباكدج على حسابك');
      await load();
      setChoosingPackageId(null);
      closeDetail(id);
    } catch (error) {
      const apiMessage = axios.isAxiosError(error) ? error.response?.data?.message : undefined;
      const message = apiMessage
        || (error instanceof Error && error.message.includes('ACADEMIC_SCOPE_DENIED')
          ? 'هذا الباكدج غير متاح لبياناتك الدراسية الحالية.'
          : 'تعذر الشراء. حاول مرة أخرى.');
      toast.error(message);
    } finally {
      setBuyingId(null);
    }
  };

  const selectTeacher = (packageId: string, subjectKey: string, teacherId: string) => {
    setSelections((current) => ({
      ...current,
      [packageId]: {
        ...(current[packageId] ?? {}),
        [subjectKey]: teacherId,
      },
    }));

  };

  const totals = useMemo(() => ({
    available: items.filter((item) => !purchasedItems.some((purchased) => purchased.sharedPackageId === item.id)).length,
    purchased: purchasedItems.length,
    teachers: purchasedItems.reduce((sum, item) => sum + item.teachers.length, 0),
  }), [items, purchasedItems]);

  const availableItems = useMemo(() => {
    const purchasedIds = new Set(purchasedItems.map((item) => item.sharedPackageId));
    return items.filter((item) => !purchasedIds.has(item.id));
  }, [items, purchasedItems]);

  const choosingPackage = choosingPackageId ? items.find((item) => item.id === choosingPackageId) : undefined;
  const choosingDetail = choosingPackageId ? details[choosingPackageId] : undefined;

  if (choosingPackageId && choosingPackage && choosingDetail) {
    const grouped = groupTeachersBySubject(choosingDetail);
    const selectedCount = grouped.filter((group) => selections[choosingPackageId]?.[group.subjectKey]).length;
    const currentStep = activeSteps[choosingPackageId] ?? 1;

    return (
      <SharedPackageSelectionExperience
        item={choosingPackage}
        detail={choosingDetail}
        groups={grouped}
        currentStep={currentStep}
        selectedCount={selectedCount}
        selections={selections[choosingPackageId] ?? {}}
        buying={buyingId === choosingPackageId}
        onBack={() => setChoosingPackageId(null)}
        onStepChange={(step) => setActiveSteps((current) => ({ ...current, [choosingPackageId]: step }))}
        onSelect={(group, teacherId) => selectTeacher(choosingPackageId, group.subjectKey, teacherId)}
        onPurchase={() => void purchase(choosingPackageId)}
      />
    );
  }

  return (
    <div className="space-y-6 pb-10">
      <header className="overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)]">
        <div className="relative grid gap-5 p-5 md:grid-cols-[1fr_auto] md:items-center md:p-6">
          <div className="pointer-events-none absolute inset-0 opacity-60 [background-image:radial-gradient(circle_at_1px_1px,rgba(14,143,143,0.16)_1px,transparent_0)] [background-size:22px_22px]" />
          <div className="relative flex items-start gap-4">
            <span className="grid h-13 w-13 shrink-0 place-items-center rounded-2xl bg-[#0A1D3D] text-white">
              <PackageOpen className="h-6 w-6" />
            </span>
            <div className="min-w-0">
              <div className="mb-2 inline-flex items-center gap-2 rounded-full border border-[#0E8F8F]/25 bg-[#0E8F8F]/10 px-3 py-1 text-xs font-black text-[#0E8F8F]">
                <Sparkles className="h-3.5 w-3.5" />
                اختار مسارك بنفسك
              </div>
              <h1 className="text-3xl font-black leading-tight text-[var(--student-text)] md:text-4xl">الباكدجات العامة</h1>
              <p className="mt-2 max-w-2xl text-sm font-bold leading-7 text-[var(--student-muted)]">
                اختار مدرساً واحداً لكل مادة، راجع المسار، ثم فعّل الباكدج على حسابك.
              </p>
            </div>
          </div>

          <div className="relative grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <HeaderMetric label="متاح" value={totals.available} />
            <HeaderMetric label="مشتركة" value={totals.purchased} />
            <HeaderMetric label="مدرسين" value={totals.teachers} />
            <button type="button" onClick={() => void load()} className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-[var(--student-border)] bg-white px-4 text-sm font-black text-[var(--student-text)] transition hover:border-[#0E8F8F] hover:text-[#0E8F8F]">
              <RefreshCw className="h-4 w-4" /> تحديث
            </button>
          </div>
        </div>
      </header>

      {loading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {[1, 2, 3].map((item) => (
            <div key={item} className="h-72 animate-pulse rounded-2xl bg-[var(--student-card-strong)]" />
          ))}
        </div>
      ) : (
        <>
          <PurchasedSharedPackages items={purchasedItems} />

          <section className="space-y-4">
            <div className="border-b border-[var(--student-border)] pb-3">
              <h2 className="text-2xl font-black text-[var(--student-text)]">باكدجات متاحة للشراء</h2>
              <p className="mt-1 text-sm font-bold text-[var(--student-muted)]">اختار المدرسين قبل الدفع، وبعد الشراء هتظهر في قسم باكدجاتي المشتركة.</p>
            </div>
            <div className="grid gap-5 xl:grid-cols-2">
          {availableItems.map((item) => {
            const detail = details[item.id];
            const grouped = detail ? groupTeachersBySubject(detail) : [];
            const selectedCount = grouped.filter((group) => selections[item.id]?.[group.subjectKey]).length;

            return (
              <article key={item.id} className="overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)]">
                <div className="p-5">
                  <div className="mb-5 flex items-start justify-between gap-4">
                    <div className="min-w-0">
                      <span className="mb-3 inline-flex items-center gap-2 rounded-full bg-emerald-50 px-3 py-1 text-xs font-black text-emerald-700">
                        <CheckCircle2 className="h-3.5 w-3.5" /> متاح الآن
                      </span>
                      <h2 className="text-2xl font-black leading-8 text-[var(--student-text)]">{item.name}</h2>
                      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs font-black text-[var(--student-muted)]">
                        <GraduationCap className="h-4 w-4 text-[#0E8F8F]" />
                        <span>{getEducationStageLabel(item.educationStage)}</span>
                        <span className="h-1 w-1 rounded-full bg-[var(--student-border)]" />
                        <span>{getGradeLevelLabel(item.gradeLevel)}</span>
                      </div>
                    </div>
                    <div className="shrink-0 rounded-2xl bg-[#0A1D3D] px-4 py-3 text-center text-white">
                      <div className="text-xl font-black">{item.price.toLocaleString('ar-EG')}</div>
                      <div className="text-xs font-bold text-white/75">جنيه</div>
                    </div>
                  </div>

                  <p className="line-clamp-2 text-sm font-bold leading-7 text-[var(--student-muted)]">
                    {item.description || 'باكدج مشترك يمنحك وصولاً لمحتوى محدد من الإدارة.'}
                  </p>

                  <div className="mt-5 grid grid-cols-3 gap-2 rounded-2xl bg-[var(--student-card-soft)] p-2">
                    <PackageStat label="مواد" value={detail ? grouped.length : item.teacherCount ?? 0} />
                    <PackageStat label="اخترت" value={selectedCount} />
                    <PackageStat label="السعر" value={item.price.toLocaleString('ar-EG')} />
                  </div>
                </div>

                <div className="grid gap-2 border-t border-[var(--student-border)] p-4 sm:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => void startSelection(item.id)}
                    disabled={loadingDetailId === item.id}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--student-border)] px-4 text-sm font-black text-[var(--student-text)] transition hover:border-[#0E8F8F] hover:text-[#0E8F8F] disabled:opacity-60"
                  >
                    {loadingDetailId === item.id ? <LoaderCircleIcon className="h-4 w-4 animate-spin" /> : <BookOpen className="h-4 w-4" />}
                    اختار المدرسين
                  </button>
                  <button
                    type="button"
                    disabled={buyingId === item.id}
                    onClick={() => void startSelection(item.id)}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#0A1D3D] px-4 text-sm font-black text-white transition hover:bg-[#0E8F8F] disabled:opacity-60"
                  >
                    <ShoppingCart className="h-4 w-4" />
                    ابدأ الاختيار
                  </button>
                </div>
              </article>
            );
          })}
            </div>
            {availableItems.length === 0 && (
              <div className="rounded-2xl border border-dashed border-[var(--student-border)] bg-[var(--student-card)] p-8 text-center text-sm font-bold text-[var(--student-muted)]">
                لا توجد باكدجات جديدة متاحة للشراء حالياً.
              </div>
            )}
          </section>
        </>
      )}

      {!loading && availableItems.length === 0 && purchasedItems.length === 0 && (
        <div className="rounded-2xl border border-dashed border-[var(--student-border)] bg-[var(--student-card)] p-10 text-center text-sm font-bold text-[var(--student-muted)]">
          لا توجد باكدجات عامة متاحة لمرحلتك أو صفك حالياً.
          <p className="mx-auto mt-2 max-w-md text-xs font-medium leading-6">
            الباكدجات غير المطابقة لبياناتك الدراسية لا تظهر في هذه الصفحة.
          </p>
        </div>
      )}
    </div>
  );
}

function SharedPackageSelectionExperience({
  item,
  detail,
  groups,
  currentStep,
  selectedCount,
  selections,
  buying,
  onBack,
  onStepChange,
  onSelect,
  onPurchase,
}: {
  item: SharedPackageListItem;
  detail: SharedPackageDetail;
  groups: SubjectChoiceGroup[];
  currentStep: number;
  selectedCount: number;
  selections: Record<string, string>;
  buying: boolean;
  onBack: () => void;
  onStepChange: (step: number) => void;
  onSelect: (group: SubjectChoiceGroup, teacherId: string) => void;
  onPurchase: () => void;
}) {
  const reduceMotion = useReducedMotion();
  const reviewStep = groups.length + 1;
  const activeGroup = groups[currentStep - 1];
  const ready = groups.length > 0 && selectedCount === groups.length;
  const canMoveToStep = (step: number) => {
    if (step <= currentStep) return true;
    if (step === reviewStep) return ready;
    return groups.slice(0, step - 1).every((group) => Boolean(selections[group.subjectKey]));
  };
  const handleStepChange = (step: number) => {
    if (canMoveToStep(step)) {
      onStepChange(step);
      return;
    }
    const missing = groups.findIndex((group) => !selections[group.subjectKey]);
    toast.error(`اختر مدرساً لمادة ${groups[missing]?.subjectName ?? 'الحالية'} أولاً`);
  };

  const transition: Transition = reduceMotion ? { duration: 0 } : { duration: 0.24, ease: [0.22, 1, 0.36, 1] };

  return (
    <motion.div
      initial={reduceMotion ? false : { opacity: 0, y: 18 }}
      animate={{ opacity: 1, y: 0 }}
      transition={transition}
      className="space-y-6 pb-10"
    >
      <section className="overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)]">
        <div className="relative grid gap-6 p-5 md:grid-cols-[1fr_280px] md:p-7">
          <div className="pointer-events-none absolute inset-0 opacity-70 [background-image:linear-gradient(rgba(14,143,143,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(14,143,143,0.08)_1px,transparent_1px)] [background-size:28px_28px]" />
          <div className="relative">
            <button
              type="button"
              onClick={onBack}
              className="mb-5 inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--student-border)] bg-white px-4 text-sm font-black text-[var(--student-text)] transition hover:border-[#0E8F8F] hover:text-[#0E8F8F]"
            >
              <ArrowRight className="h-4 w-4" /> رجوع للباكدجات
            </button>

            <div className="mb-4 inline-flex items-center gap-2 rounded-full bg-[#0E8F8F]/10 px-3 py-1 text-xs font-black text-[#0E8F8F]">
              <Sparkles className="h-3.5 w-3.5" /> اختر مدرس كل مادة
            </div>
            <h1 className="max-w-3xl text-3xl font-black leading-tight text-[var(--student-text)] md:text-5xl">
              {item.name}
            </h1>
            <p className="mt-3 max-w-2xl text-sm font-bold leading-7 text-[var(--student-muted)]">
              {item.description || 'اختار المدرس الأنسب لكل مادة قبل الدفع. كل اختيار يفتح المحتوى المرتبط به بعد التفعيل.'}
            </p>

            <div className="mt-6 flex flex-wrap gap-2">
              <SelectionHeroPill icon={GraduationCap} label={getEducationStageLabel(item.educationStage)} />
              <SelectionHeroPill icon={BookOpen} label={getGradeLevelLabel(item.gradeLevel)} />
              <SelectionHeroPill icon={Users} label={`${groups.length.toLocaleString('ar-EG')} مواد`} />
              <SelectionHeroPill icon={CheckCircle2} label={`${selectedCount.toLocaleString('ar-EG')} تم اختيارها`} />
            </div>
          </div>

          <div className="relative rounded-2xl bg-[#0A1D3D] p-5 text-white">
            <div className="absolute inset-x-5 top-0 h-px bg-[#D4A017]" />
            <div className="text-sm font-bold text-white/70">إجمالي الباكدج</div>
            <div className="mt-2 text-4xl font-black">{item.price.toLocaleString('ar-EG')}</div>
            <div className="mt-1 text-sm font-bold text-white/70">جنيه</div>
            <div className="mt-6 grid grid-cols-2 gap-2">
              <div className="rounded-xl bg-white/10 p-3">
                <div className="text-xl font-black">{groups.length.toLocaleString('ar-EG')}</div>
                <div className="text-xs font-bold text-white/70">مواد</div>
              </div>
              <div className="rounded-xl bg-white/10 p-3">
                <div className="text-xl font-black">{selectedCount.toLocaleString('ar-EG')}</div>
                <div className="text-xs font-bold text-white/70">اختيارات</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <Stepper
        value={currentStep}
        onValueChange={handleStepChange}
        indicators={{
          completed: <CheckIcon className="size-3.5" />,
          loading: <LoaderCircleIcon className="size-3.5 animate-spin" />,
        }}
        className="space-y-6"
      >
        <StepperNav className="gap-3 overflow-x-auto pb-2">
          {groups.map((group, index) => {
            const isSelected = Boolean(selections[group.subjectKey]);
            return (
              <StepperItem key={group.subjectKey} step={index + 1} className="min-w-[176px] flex-1">
                <StepperTrigger className="flex h-full w-full items-center gap-3 rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-3 text-start data-[state=active]:border-[#0A1D3D] data-[state=active]:bg-white">
                  <StepperIndicator>{index + 1}</StepperIndicator>
                  <span className="min-w-0">
                    <StepperTitle className="line-clamp-1 text-start">{group.subjectName}</StepperTitle>
                    <span className={cn("mt-1 block text-xs font-black", isSelected ? "text-[#0E8F8F]" : "text-[var(--student-muted)]")}>
                      {isSelected ? 'تم الاختيار' : `${group.teachers.length} مدرسين`}
                    </span>
                  </span>
                </StepperTrigger>
              </StepperItem>
            );
          })}

          <StepperItem step={reviewStep} className="min-w-[176px] flex-1">
            <StepperTrigger className="flex h-full w-full items-center gap-3 rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-3 text-start data-[state=active]:border-[#0A1D3D] data-[state=active]:bg-white">
              <StepperIndicator>{reviewStep}</StepperIndicator>
              <span>
                <StepperTitle>المراجعة</StepperTitle>
                <span className={cn("mt-1 block text-xs font-black", ready ? "text-[#0E8F8F]" : "text-[var(--student-muted)]")}>
                  {ready ? 'جاهز للدفع' : 'اختيارات ناقصة'}
                </span>
              </span>
            </StepperTrigger>
          </StepperItem>
        </StepperNav>

        <StepperPanel>
          <AnimatePresence mode="wait">
            {activeGroup ? (
              <StepperContent key={activeGroup.subjectKey} value={currentStep}>
                <motion.section
                  initial={reduceMotion ? false : { opacity: 0, y: 14, filter: 'blur(8px)' }}
                  animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
                  exit={reduceMotion ? { opacity: 0 } : { opacity: 0, y: -10, filter: 'blur(6px)' }}
                  transition={transition}
                  className="grid gap-5 lg:grid-cols-[320px_1fr]"
                >
                  <SubjectFocusPanel group={activeGroup} selectedTeacherId={selections[activeGroup.subjectKey]} />
                  <div className="grid gap-4 md:grid-cols-2">
                    {activeGroup.teachers.map((teacher, teacherIndex) => (
                      <TeacherSelectionCard
                        key={`${activeGroup.subjectKey}-${teacher.teacherId}`}
                        teacher={teacher}
                        selected={selections[activeGroup.subjectKey] === teacher.teacherId}
                        index={teacherIndex}
                        reduceMotion={Boolean(reduceMotion)}
                        onSelect={() => onSelect(activeGroup, teacher.teacherId)}
                      />
                    ))}
                  </div>
                  <div className="flex justify-end lg:col-start-2">
                    <button
                      type="button"
                      disabled={!selections[activeGroup.subjectKey]}
                      onClick={() => handleStepChange(Math.min(currentStep + 1, reviewStep))}
                      className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[#0A1D3D] px-5 text-sm font-black text-white transition hover:bg-[#0E8F8F] disabled:cursor-not-allowed disabled:opacity-45"
                    >
                      {currentStep === groups.length ? 'مراجعة اختياراتي' : 'التالي'}
                      <ArrowLeft className="h-4 w-4" />
                    </button>
                  </div>
                </motion.section>
              </StepperContent>
            ) : (
              <StepperContent key="review" value={reviewStep}>
                <motion.div
                  initial={reduceMotion ? false : { opacity: 0, y: 14 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={reduceMotion ? { opacity: 0 } : { opacity: 0, y: -10 }}
                  transition={transition}
                >
                  <ReviewStep
                    packageName={detail.name}
                    packagePrice={detail.price}
                    groups={groups}
                    selections={selections}
                    buying={buying}
                    onBackToMissing={onStepChange}
                    onPurchase={onPurchase}
                  />
                </motion.div>
              </StepperContent>
            )}
          </AnimatePresence>
        </StepperPanel>
      </Stepper>
    </motion.div>
  );
}

function SelectionHeroPill({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return (
    <span className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[var(--student-border)] bg-white px-3 text-xs font-black text-[var(--student-text)]">
      <Icon className="h-4 w-4 text-[#0E8F8F]" />
      {label}
    </span>
  );
}

function SubjectFocusPanel({ group, selectedTeacherId }: { group: SubjectChoiceGroup; selectedTeacherId?: string }) {
  const selectedTeacher = group.teachers.find((teacher) => teacher.teacherId === selectedTeacherId);

  return (
    <aside className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-5">
      <div className="inline-flex items-center gap-2 rounded-full bg-[#0E8F8F]/10 px-3 py-1 text-xs font-black text-[#0E8F8F]">
        <BookOpen className="h-3.5 w-3.5" /> المادة الحالية
      </div>
      <h2 className="mt-4 text-3xl font-black leading-tight text-[var(--student-text)]">{group.subjectName}</h2>
      <p className="mt-3 text-sm font-bold leading-7 text-[var(--student-muted)]">
        اختار مدرسًا واحدًا لهذه المادة، ثم اضغط «التالي» عندما تكون جاهزًا للمتابعة.
      </p>

      <div className="mt-6 grid gap-3">
        <div className="flex items-center justify-between rounded-xl bg-[var(--student-card-soft)] px-4 py-3">
          <span className="text-sm font-black text-[var(--student-muted)]">المدرسين</span>
          <span className="text-sm font-black text-[var(--student-text)]">{group.teachers.length.toLocaleString('ar-EG')}</span>
        </div>
        <div className="rounded-xl border border-[#0E8F8F]/20 bg-[#0E8F8F]/10 px-4 py-3">
          <span className="text-xs font-black text-[#0E8F8F]">اختيارك الحالي</span>
          <p className="mt-1 text-sm font-black text-[var(--student-text)]">{selectedTeacher?.teacherName ?? 'لم تختار مدرس بعد'}</p>
        </div>
      </div>
    </aside>
  );
}

function TeacherSelectionCard({
  teacher,
  selected,
  index,
  reduceMotion,
  onSelect,
}: {
  teacher: TeacherChoice;
  selected: boolean;
  index: number;
  reduceMotion: boolean;
  onSelect: () => void;
}) {
  return (
    <motion.button
      type="button"
      onClick={onSelect}
      initial={reduceMotion ? false : { opacity: 0, y: 18, scale: 0.98 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={reduceMotion ? { duration: 0 } : { duration: 0.22, delay: index * 0.04, ease: [0.22, 1, 0.36, 1] }}
      className={cn(
        "group relative overflow-hidden rounded-2xl border bg-[var(--student-card)] p-4 text-start transition hover:-translate-y-1 hover:border-[#0E8F8F] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0E8F8F]",
        selected ? "border-[#0E8F8F] ring-2 ring-[#0E8F8F]/20" : "border-[var(--student-border)]"
      )}
    >
      <div className="absolute inset-x-4 top-0 h-px bg-[#D4A017] opacity-0 transition group-hover:opacity-100" />
      <div className="flex items-start gap-4">
        <TeacherChoicePhoto teacher={teacher} selected={selected} />
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h3 className="truncate text-xl font-black text-[var(--student-text)]">{teacher.teacherName}</h3>
              <p className="mt-1 flex items-center gap-1 text-xs font-bold text-[var(--student-muted)]">
                <Clock3 className="h-3.5 w-3.5" />
                {teacher.contentCount.toLocaleString('ar-EG')} عنصر محتوى داخل الاختيار
              </p>
            </div>
            <span className={cn(
              "grid h-9 w-9 shrink-0 place-items-center rounded-xl border transition",
              selected ? "border-[#0E8F8F] bg-[#0E8F8F] text-white" : "border-[var(--student-border)] text-[var(--student-muted)] group-hover:border-[#0E8F8F] group-hover:text-[#0E8F8F]"
            )}>
              {selected ? <CheckIcon className="h-5 w-5" /> : <ChevronLeft className="h-5 w-5" />}
            </span>
          </div>

          <div className="mt-5 flex items-center justify-between rounded-xl bg-[var(--student-card-soft)] px-3 py-2">
            <span className="text-xs font-black text-[var(--student-muted)]">{selected ? 'مختار لهذه المادة' : 'اضغط للاختيار'}</span>
            <span className="text-xs font-black text-[#0E8F8F]">اختر هذا المدرس</span>
          </div>
        </div>
      </div>
    </motion.button>
  );
}

function TeacherChoicePhoto({ teacher, selected }: { teacher: TeacherChoice; selected: boolean }) {
  return (
    <span className={cn(
      "relative grid h-24 w-24 shrink-0 place-items-center overflow-hidden rounded-2xl border bg-[var(--student-card-soft)]",
      selected ? "border-[#0E8F8F]" : "border-[var(--student-border)]"
    )}>
      {teacher.teacherProfileImageUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={resolveMediaUrl(teacher.teacherProfileImageUrl)} alt="" className="h-full w-full object-cover" />
      ) : (
        <GraduationCap className="h-9 w-9 text-[#0E8F8F]" />
      )}
      <span className="absolute bottom-2 right-2 rounded-lg bg-[#0A1D3D] px-2 py-1 text-[11px] font-black text-white">
        أ.
      </span>
    </span>
  );
}

function PurchasedSharedPackages({ items }: { items: PurchasedSharedPackage[] }) {
  if (items.length === 0) {
    return null;
  }

  return (
    <section className="space-y-4">
      <div className="border-b border-[var(--student-border)] pb-3">
        <h2 className="text-2xl font-black text-[var(--student-text)]">باكدجاتي المشتركة</h2>
        <p className="mt-1 text-sm font-bold text-[var(--student-muted)]">
          اضغط على المدرس لفتح المحتوى الذي تم تفعيله داخل الباكدج.
        </p>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        {items.map((item) => (
          <article key={item.id} className="overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)]">
            <div className="grid gap-4 p-5 md:grid-cols-[96px_1fr_auto] md:items-center">
              <div className="grid h-24 w-24 place-items-center overflow-hidden rounded-2xl bg-[var(--student-card-soft)]">
                {item.imageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={resolveMediaUrl(item.imageUrl)} alt="" className="h-full w-full object-cover" />
                ) : (
                  <PackageOpen className="h-9 w-9 text-[#0E8F8F]" />
                )}
              </div>
              <div className="min-w-0">
                <span className="mb-2 inline-flex items-center gap-2 rounded-full bg-[#0E8F8F]/10 px-3 py-1 text-xs font-black text-[#0E8F8F]">
                  <CheckCircle2 className="h-3.5 w-3.5" /> مفعّل
                </span>
                <h3 className="text-xl font-black text-[var(--student-text)]">{item.name}</h3>
                <p className="mt-1 line-clamp-2 text-sm font-bold leading-6 text-[var(--student-muted)]">
                  {item.description || 'باكدج مشترك مفعّل على حسابك.'}
                </p>
              </div>
              <div className="rounded-2xl bg-[#0A1D3D] px-4 py-3 text-center text-white">
                <div className="text-lg font-black">{item.price.toLocaleString('ar-EG')}</div>
                <div className="text-xs font-bold text-white/75">جنيه</div>
              </div>
            </div>

            <div className="grid gap-3 border-t border-[var(--student-border)] bg-[var(--student-card-soft)] p-4 sm:grid-cols-2">
              {item.teachers.map((teacher) => (
                <Link
                  key={`${item.id}-${teacher.teacherId}-${teacher.subjectId ?? 'platform'}`}
                  href={teacher.contentUrl}
                  className="group flex min-h-20 items-center gap-3 rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-3 transition hover:border-[#0E8F8F] hover:bg-white"
                >
                  {teacher.teacherProfileImageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={resolveMediaUrl(teacher.teacherProfileImageUrl)} alt="" className="h-12 w-12 rounded-xl object-cover" />
                  ) : (
                    <span className="grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-[#0E8F8F]/10 text-[#0E8F8F]">
                      <GraduationCap className="h-5 w-5" />
                    </span>
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-black text-[var(--student-text)]">أ. {teacher.teacherName}</span>
                    <span className="mt-1 block truncate text-xs font-bold text-[var(--student-muted)]">
                      {teacher.subjectName || 'محتوى عام'}، {teacher.contentName}
                    </span>
                  </span>
                  <ChevronLeft className="h-5 w-5 shrink-0 text-[var(--student-muted)] transition group-hover:-translate-x-1 group-hover:text-[#0E8F8F]" />
                </Link>
              ))}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function HeaderMetric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-[var(--student-border)] bg-white px-4 py-3 text-center">
      <div className="text-xl font-black text-[var(--student-text)]">{value.toLocaleString('ar-EG')}</div>
      <div className="text-xs font-bold text-[var(--student-muted)]">{label}</div>
    </div>
  );
}

function PackageStat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-xl bg-[var(--student-card)] px-3 py-2 text-center">
      <div className="text-base font-black text-[var(--student-text)]">{value}</div>
      <div className="text-[11px] font-bold text-[var(--student-muted)]">{label}</div>
    </div>
  );
}

function ReviewStep({
  packageName,
  packagePrice,
  groups,
  selections,
  buying,
  onBackToMissing,
  onPurchase,
}: {
  packageName: string;
  packagePrice: number;
  groups: SubjectChoiceGroup[];
  selections: Record<string, string>;
  buying: boolean;
  onBackToMissing: (step: number) => void;
  onPurchase: () => void;
}) {
  const missingIndex = groups.findIndex((group) => !selections[group.subjectKey]);
  const ready = missingIndex === -1;

  return (
    <section className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-4">
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="inline-flex items-center gap-2 rounded-full bg-[#D4A017]/15 px-3 py-1 text-xs font-black text-[#8A6500]">
            <ShieldCheck className="h-3.5 w-3.5" /> مراجعة قبل الدفع
          </div>
          <h3 className="mt-2 text-xl font-black text-[var(--student-text)]">{packageName}</h3>
        </div>
        <div className="rounded-xl bg-[#0A1D3D] px-4 py-2 text-sm font-black text-white">
          الإجمالي {packagePrice.toLocaleString('ar-EG')} جنيه
        </div>
      </div>

      <div className="grid gap-2">
        {groups.map((group, index) => {
          const teacher = group.teachers.find((item) => item.teacherId === selections[group.subjectKey]);
          return (
            <button
              key={group.subjectKey}
              type="button"
              onClick={() => onBackToMissing(index + 1)}
              className="flex min-h-14 items-center justify-between gap-3 rounded-xl border border-[var(--student-border)] bg-[var(--student-card-soft)] px-3 text-start transition hover:border-[#0E8F8F]"
            >
              <span className="min-w-0">
                <span className="block text-sm font-black text-[var(--student-text)]">{group.subjectName}</span>
                <span className={cn("block truncate text-xs font-bold", teacher ? "text-[#0E8F8F]" : "text-rose-600")}>
                  {teacher ? teacher.teacherName : 'لم يتم اختيار مدرس'}
                </span>
              </span>
            </button>
          );
        })}
      </div>

      <div className="mt-4 grid gap-2 sm:grid-cols-[1fr_auto]">
        {!ready ? (
          <button
            type="button"
            onClick={() => onBackToMissing(missingIndex + 1)}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-4 text-sm font-black text-rose-700"
          >
            <ArrowLeft className="h-4 w-4" /> أكمل الاختيارات الناقصة
          </button>
        ) : (
          <div className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-emerald-50 px-4 text-sm font-black text-emerald-700">
            <CheckCircle2 className="h-4 w-4" /> كل الاختيارات جاهزة
          </div>
        )}

        <button
          type="button"
          disabled={!ready || buying}
          onClick={onPurchase}
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#0A1D3D] px-5 text-sm font-black text-white transition hover:bg-[#0E8F8F] disabled:opacity-50"
        >
          {buying ? <LoaderCircleIcon className="h-4 w-4 animate-spin" /> : <ShoppingCart className="h-4 w-4" />}
          {buying ? 'جاري التفعيل...' : 'ادفع وفعّل الباكدج'}
        </button>
      </div>
    </section>
  );
}

function groupTeachersBySubject(detail: SharedPackageDetail): SubjectChoiceGroup[] {
  const groups = new Map<string, SubjectChoiceGroup>();

  detail.teachers.forEach((teacher) => {
    const subjectKey = teacher.subjectId ?? 'platform';
    if (!groups.has(subjectKey)) {
      const subjectItems = detail.items.filter((item) => (item.subjectId ?? 'platform') === subjectKey);
      groups.set(subjectKey, {
        subjectKey,
        subjectId: teacher.subjectId,
        subjectName: teacher.subjectName || 'محتوى عام',
        price: subjectItems[0]?.price ?? 0,
        teachers: [],
      });
    }

    const group = groups.get(subjectKey)!;
    if (!group.teachers.some((item) => item.teacherId === teacher.teacherId)) {
      const contentCount = detail.items.filter((item) =>
        item.teacherId === teacher.teacherId && (item.subjectId ?? 'platform') === subjectKey
      ).length;
      group.teachers.push({
        teacherId: teacher.teacherId,
        teacherName: teacher.teacherName,
        teacherProfileImageUrl: teacher.teacherProfileImageUrl,
        contentCount,
      });
    }
  });

  return Array.from(groups.values());
}
