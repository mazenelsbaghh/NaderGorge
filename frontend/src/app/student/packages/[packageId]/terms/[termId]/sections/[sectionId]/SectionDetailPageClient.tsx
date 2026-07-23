"use client";

/**
 * Section Detail Page — /student/packages/[packageId]/terms/[termId]/sections/[sectionId]
 *
 * Layout mirrors the Term Detail Page:
 *   Back button → Full-width hero (section image)
 *   → Two-column: Lessons list (right) | Sidebar (left)
 */

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { motion } from "framer-motion";
import Image from "next/image";
import toast from "react-hot-toast";
import {
  ArrowRight,
  BookOpen,
  Lock,
  RefreshCcw,
  TriangleAlert,
  CheckCircle2,
  Sparkles,
  PlayCircle,
  ShoppingCart,
  FileEdit,
  ClipboardList,
} from "lucide-react";
import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { CodeType } from "@/services/balance-service";
import {
  contentService,
  CONTENT_CACHE_KEYS,
  type ContentSectionDto,
  type LessonSummaryDto,
  type PackageDto,
  type TermDto,
} from "@/services/content-service";
import { usePlatformEvents } from "@/hooks/usePlatformEvents";
import { registerCacheStore } from "@/lib/cache-invalidation";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { GRADE_LEVEL_LABELS } from "@/lib/academic-labels";

const GRADE_NAMES = GRADE_LEVEL_LABELS;

/* ─── Animation helpers ──────────────────────────────────────────────── */
const stagger = {
  hidden: {},
  visible: { transition: { staggerChildren: 0.08, delayChildren: 0.15 } },
};
const fadeUp = {
  hidden: { opacity: 0, y: 18 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.5, ease: [0.16, 1, 0.3, 1] as const },
  },
};

export default function SectionDetailPageClient() {
  const params = useParams();
  const router = useRouter();
  const packageId = params.packageId as string;
  const termId = params.termId as string;
  const sectionId = params.sectionId as string;

  const [pkg, setPkg] = useState<PackageDto | null>(null);
  const [term, setTerm] = useState<TermDto | null>(null);
  const [section, setSection] = useState<ContentSectionDto | null>(null);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);
  const [purchaseLesson, setPurchaseLesson] = useState<LessonSummaryDto | null>(null);

  const load = useCallback(async () => {
    if (!packageId || !termId || !sectionId) return;
    setLoading(true);
    setError(null);
    try {
      const [pkgRes, termsRes, sectRes, lessonsRes] = await Promise.all([
        contentService.getPackages(),
        contentService.getTerms(packageId),
        contentService.getSections(termId),
        contentService.getLessons(sectionId),
      ]);
      setPkg(pkgRes.data?.data?.find((p: PackageDto) => p.id.toLowerCase() === packageId.toLowerCase()) ?? null);
      setTerm(termsRes.data?.data?.find((t: TermDto) => t.id.toLowerCase() === termId.toLowerCase()) ?? null);
      setSection(sectRes.data?.data?.find((s: ContentSectionDto) => s.id.toLowerCase() === sectionId.toLowerCase()) ?? null);
      setLessons(
        (lessonsRes.data?.data ?? [])
          .sort((a: LessonSummaryDto, b: LessonSummaryDto) => a.order - b.order)
      );
    } catch {
      setError("تعذر تحميل محتوى القسم. تحقق من اتصالك وأعد المحاولة.");
    } finally {
      setLoading(false);
    }
  }, [packageId, termId, sectionId]);

  useEffect(() => {
    void load();
    if (sectionId) {
      const cleanupSectionCache = registerCacheStore(`content:section:${sectionId}`, () => {}, load);
      const cleanupLessonsCache = registerCacheStore(CONTENT_CACHE_KEYS.lessons, () => {}, load);
      return () => {
        cleanupSectionCache();
        cleanupLessonsCache();
      };
    }
  }, [load, sectionId]);

  const hasDirectPackageAccess = pkg?.hasDirectPackageAccess ?? false;

  const sectionPrice = section?.price ?? null;
  const termPrice = term?.price ?? null;
  const packagePrice = pkg?.price ?? 0;

  const displayPrice =
    sectionPrice ?? termPrice ?? packagePrice;

  const priceLabel =
    sectionPrice !== null
      ? 'سعر القسم'
      : termPrice !== null
        ? 'سعر الترم'
        : 'سعر الباقة';

  /* ── Realtime ── */
  usePlatformEvents();

  /* ── Loading skeleton ── */
  if (loading) {
    return (
      <div className="space-y-6 animate-pulse pb-10">
        <div className="h-9 w-48 rounded-full bg-[var(--admin-card-strong)]" />
        <div className="aspect-video w-full rounded-[28px] bg-[var(--admin-card-strong)]" />
        <div className="h-6 w-2/3 rounded-xl bg-[var(--admin-card-strong)]" />
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-20 rounded-2xl bg-[var(--admin-card-strong)]" />
            ))}
          </div>
          <div className="h-72 rounded-3xl bg-[var(--admin-card-strong)]" />
        </div>
      </div>
    );
  }

  return (
    <motion.div
      className="space-y-8 pb-10"
      variants={stagger}
      initial="hidden"
      animate="visible"
    >
      {/* ── Back button ── */}
      <motion.div variants={fadeUp}>
        <button
          type="button"
          onClick={() => router.push(`/student/packages/${packageId}/terms/${termId}`)}
          className="group inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] shadow-sm transition-all hover:bg-[var(--admin-card-strong)] hover:shadow-md"
        >
          <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
          العودة إلى الترم
        </button>
      </motion.div>

      {/* ── Hero banner ── */}
      <motion.div variants={fadeUp}>
        <div
          className="relative aspect-video w-full overflow-hidden rounded-[28px] bg-gradient-to-br from-slate-800 to-teal-700 shadow-xl sm:rounded-2xl"
          style={{ viewTransitionName: `section-image-${sectionId}` }}
        >
          {section?.imageUrl && (
            <Image
              src={resolveMediaUrl(section.imageUrl)}
              alt={section.title}
              fill
              priority
              className="object-cover"
              sizes="100vw"
            />
          )}
          {/* Gradient overlay */}
          <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/30 to-transparent" />

          {/* Text overlay */}
          <div className="absolute inset-x-0 bottom-0 flex flex-col gap-2 p-6 sm:p-10">
            <span className="text-xs font-bold text-white/70 tracking-wider uppercase">
              {pkg?.name} — {term?.title}
            </span>
            <h1 className="text-2xl font-black text-white sm:text-4xl text-wrap-balance">
              {section?.title || "القسم"}
            </h1>
            <div className="flex flex-wrap items-center gap-3 text-sm text-white/70">
              <span className="inline-flex items-center gap-1.5">
                <BookOpen className="h-4 w-4" />
                {lessons.length} حصة
              </span>
              {(hasDirectPackageAccess || (term?.isPurchased ?? false) || (section?.isPurchased ?? false)) && (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/20 px-3 py-0.5 text-xs font-bold text-emerald-300 backdrop-blur-sm">
                  <CheckCircle2 className="h-3 w-3" />
                  مفعّل
                </span>
              )}
            </div>
          </div>
        </div>
      </motion.div>

      {/* ── Error state ── */}
      {error && (
        <motion.div
          variants={fadeUp}
          className="flex flex-col items-center gap-4 rounded-[2rem] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-10 text-center"
        >
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-card)] text-[var(--admin-danger)]">
            <TriangleAlert className="h-7 w-7" />
          </div>
          <p className="font-bold text-[var(--admin-danger)]">{error}</p>
          <button
            type="button"
            onClick={() => void load()}
            className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card)] px-5 py-2.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-strong)]"
          >
            <RefreshCcw className="h-4 w-4" />
            إعادة المحاولة
          </button>
        </motion.div>
      )}

      {/* ── Two-Column Layout ── */}
      {!error && (
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
          {/* Right Column: Lessons List */}
          <div className="lg:col-span-2 space-y-6">
            <div>
              <h2 className="text-xl font-black text-[var(--admin-text)] sm:text-2xl">الحصص</h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                اختر الحصة لبدء المشاهدة والدراسة.
              </p>
            </div>

            {lessons.length === 0 ? (
              <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
                <BookOpen className="mb-4 h-10 w-10 text-[var(--admin-muted)] opacity-40" />
                <p className="font-bold text-[var(--admin-muted)]">لا توجد حصص في هذا القسم بعد.</p>
              </div>
            ) : (
              <div className="space-y-3">
                {lessons.map((lesson, idx) => {
                  const unlockedVideos = lesson.videos?.filter((video) => video.hasAccess) ?? [];
                  const hasVideoOnlyAccess = unlockedVideos.length > 0 && !lesson.hasAccess;
                  const hasContentAccess = hasDirectPackageAccess || lesson.hasAccess || hasVideoOnlyAccess;
                  const canBuyLesson = !hasDirectPackageAccess && !lesson.hasAccess;
                  const canAccess = hasContentAccess && (!lesson.isLocked || hasVideoOnlyAccess);
                  const sortedVideos = [...(lesson.videos ?? [])].sort((a, b) => a.order - b.order);
                  const openVideoCount = sortedVideos.filter((video) => hasDirectPackageAccess || lesson.hasAccess || video.hasAccess).length;
                  return (
                    <div
                      key={lesson.id}
                      onClick={() => {
                        if (canAccess) {
                          const videoQuery = hasVideoOnlyAccess ? `?videoId=${unlockedVideos[0]?.id}` : "";
                          router.push(`/student/packages/${packageId}/lessons/${lesson.id}${videoQuery}`);
                        } else if (lesson.isLocked && hasContentAccess) {
                          // Has access but locked by exam/homework
                          if (lesson.blockingExamId) {
                            router.push(`/student/exams/${lesson.blockingExamId}`);
                          } else if (lesson.blockingHomeworkLessonId) {
                            router.push(`/student/packages/${packageId}/lessons/${lesson.blockingHomeworkLessonId}`);
                          } else {
                            toast.error(lesson.lockedReason || "هذه الحصة مقفولة.");
                          }
                        } else if (!hasContentAccess) {
                          setPurchaseLesson(lesson);
                        }
                      }}
                      className={`group relative flex w-full flex-col gap-4 rounded-2xl border p-4 text-right transition-all cursor-pointer sm:p-5 ${
                        canAccess
                          ? "border-[var(--admin-border)] bg-[var(--admin-card)] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)]"
                          : lesson.isLocked && hasContentAccess
                            ? "border-amber-500/30 bg-amber-500/5"
                            : "border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:-translate-y-0.5 hover:border-[var(--admin-primary-30)] opacity-90"
                      }`}
                    >
                      <div className="flex w-full items-start gap-4">
                        {/* Lesson Number */}
                        <div className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl font-black text-lg ${
                          lesson.isCompleted
                            ? "bg-emerald-500/15 text-emerald-600"
                            : canAccess
                              ? "bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"
                              : lesson.isLocked && hasContentAccess
                                ? "bg-amber-500/15 text-amber-600"
                                : "bg-[var(--admin-card-strong)] text-[var(--admin-muted)]"
                        }`}>
                          {lesson.isCompleted ? (
                            <CheckCircle2 className="h-5 w-5" />
                          ) : lesson.isLocked && hasContentAccess ? (
                            <Lock className="h-4 w-4" />
                          ) : !canAccess ? (
                            <Lock className="h-4 w-4" />
                          ) : (
                            <span>{String(idx + 1).padStart(2, "0")}</span>
                          )}
                        </div>

                        {/* Lesson Info */}
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                            <div className="min-w-0">
                              <h3 className={`text-base font-black leading-snug ${
                                canAccess
                                  ? "text-[var(--admin-text)] group-hover:text-[var(--admin-primary)]"
                                  : lesson.isLocked && hasContentAccess
                                    ? "text-amber-700 dark:text-amber-400"
                                    : "text-[var(--admin-muted)]"
                              } transition-colors`}>
                                {lesson.title}
                              </h3>
                              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs font-bold text-[var(--admin-muted)]">
                                <span>{sortedVideos.length || 0} فيديو</span>
                                {sortedVideos.length > 0 && (
                                  <span className="rounded-full bg-[var(--admin-card-strong)] px-2 py-0.5">
                                    {openVideoCount} مفتوح
                                  </span>
                                )}
                                {hasVideoOnlyAccess && (
                                  <span className="rounded-full bg-emerald-500/10 px-2 py-0.5 text-emerald-700">
                                    وصول جزئي بالكود
                                  </span>
                                )}
                              </div>
                            </div>

                            <div className="flex shrink-0 flex-wrap items-center gap-2">
                              {canAccess && (
                                <span className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[var(--admin-primary-15)] px-3 text-xs font-black text-[var(--admin-primary)]">
                                  <PlayCircle className="h-3.5 w-3.5" />
                                  دخول
                                </span>
                              )}
                              {canBuyLesson && (
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setPurchaseLesson(lesson);
                                  }}
                                  className={`inline-flex min-h-9 items-center gap-1.5 rounded-xl px-3 text-xs font-black transition hover:brightness-110 active:scale-95 ${
                                    (lesson.price ?? 0) > 0
                                      ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                      : 'bg-emerald-600 text-white'
                                  }`}
                                >
                                  <ShoppingCart className="h-3.5 w-3.5" />
                                  {(lesson.price ?? 0) > 0 ? `شراء الحصة ${lesson.price} ج.م` : 'تفعيل الحصة مجانا'}
                                </button>
                              )}
                            </div>
                          </div>

                          {/* Show lock reason for exam/homework locked lessons */}
                          {lesson.isLocked && hasContentAccess && lesson.lockedReason && (
                            <p className="mt-2 text-xs text-amber-600 dark:text-amber-400 line-clamp-1 font-bold">
                              <Lock className="inline h-3.5 w-3.5 mr-1" /> {lesson.lockedReason}
                            </p>
                          )}
                          {lesson.summary && !(lesson.isLocked && hasContentAccess) && (
                            <p className="mt-2 text-xs leading-6 text-[var(--admin-muted)] line-clamp-2">
                              {lesson.summary}
                            </p>
                          )}
                        </div>
                      </div>

                      {sortedVideos.length > 0 && (
                        <div className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2">
                          <div className="flex gap-2 overflow-x-auto pb-1">
                            {sortedVideos.map((video) => {
                              const isVideoOpen = hasDirectPackageAccess || lesson.hasAccess || video.hasAccess;
                              const statusLabel = isVideoOpen ? (video.isUnlockedByCode ? "مفتوح بالكود" : "مفتوح") : "مقفول";
                              return (
                                <button
                                  key={video.id}
                                  type="button"
                                  onClick={(event) => {
                                    event.stopPropagation();
                                    if (isVideoOpen && (!lesson.isLocked || hasVideoOnlyAccess)) {
                                      router.push(`/student/packages/${packageId}/lessons/${lesson.id}?videoId=${video.id}`);
                                    } else if (canBuyLesson) {
                                      setPurchaseLesson(lesson);
                                    }
                                  }}
                                  className={`group/video flex min-h-14 min-w-[170px] max-w-[230px] shrink-0 items-center justify-between gap-3 rounded-xl border px-3 py-2 text-right transition active:scale-[0.98] ${
                                    isVideoOpen
                                      ? "border-emerald-500/35 bg-emerald-500/10 text-emerald-800 hover:bg-emerald-500/15"
                                      : "border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary-30)]"
                                  }`}
                                  title={video.videoTypeName ? `${video.title} - ${video.videoTypeName}` : video.title}
                                >
                                  <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${
                                    isVideoOpen
                                      ? "bg-emerald-600 text-white"
                                      : "bg-[var(--admin-card-strong)] text-[var(--admin-muted)]"
                                  }`}>
                                    {isVideoOpen ? <PlayCircle className="h-4 w-4" /> : <Lock className="h-4 w-4" />}
                                  </span>
                                  <span className="min-w-0 flex-1">
                                    <span className="block truncate text-xs font-black text-current">{video.title}</span>
                                    <span className="mt-1 flex items-center gap-1.5">
                                      {video.videoTypeName && (
                                        <span className={`max-w-[88px] truncate rounded-full px-2 py-0.5 text-[10px] font-black ${
                                          isVideoOpen
                                            ? "bg-white/70 text-emerald-900"
                                            : "bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"
                                        }`}>
                                          {video.videoTypeName}
                                        </span>
                                      )}
                                      <span className={`rounded-full px-2 py-0.5 text-[10px] font-black ${
                                        isVideoOpen
                                          ? "bg-emerald-700 text-white"
                                          : "bg-[var(--admin-card-strong)] text-[var(--admin-muted)]"
                                      }`}>
                                        {statusLabel}
                                      </span>
                                    </span>
                                  </span>
                                </button>
                              );
                            })}
                          </div>
                        </div>
                      )}

                      {/* Exam/Homework lock indicator */}
                      {lesson.isLocked && hasContentAccess && (
                        <span className="self-start inline-flex items-center gap-1.5 rounded-xl bg-amber-500/15 px-3 py-1.5 text-xs font-black text-amber-600 dark:text-amber-400">
                          {lesson.blockingExamId ? <><FileEdit className="inline h-3.5 w-3.5" /> اذهب للامتحان</> : lesson.blockingHomeworkLessonId ? <><ClipboardList className="inline h-3.5 w-3.5" /> أكمل الواجب</> : <><Lock className="inline h-3.5 w-3.5" /> مقفول</>}
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Left Column: Sidebar */}
          <div className="space-y-6">
            {/* Price / Enrollment Card */}
            <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm space-y-4 text-right">
              <div>
                <span className="text-xs font-bold text-[var(--admin-muted)]">{priceLabel}</span>
                {(displayPrice as number) > 0 ? (
                  <p className="text-3xl font-black text-[var(--admin-primary)] mt-1">{displayPrice} ج.م</p>
                ) : (
                  <p className="text-3xl font-black text-emerald-600 dark:text-emerald-400 mt-1">مجاني</p>
                )}
              </div>

              {(hasDirectPackageAccess || (term?.isPurchased ?? false) || (section?.isPurchased ?? false)) ? (
                <div className="rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 p-4 text-center font-black text-sm">
                  <CheckCircle2 className="inline h-4 w-4 mr-1" /> {hasDirectPackageAccess ? 'الباقة مفعّلة' : (term?.isPurchased ?? false) ? 'الترم مفعّل' : 'القسم مفعّل'} في حسابك بالفعل. يمكنك مشاهدة الحصص مباشرة.
                </div>
              ) : (
                <div className="flex flex-col gap-3">
                  <button
                    type="button"
                    onClick={() => setIsPurchaseModalOpen(true)}
                    className="w-full inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                  >
                    <Sparkles className="h-4 w-4" />
                    {displayPrice > 0
                      ? (sectionPrice !== null ? 'شراء القسم' : termPrice !== null ? 'شراء الترم' : 'شراء الباقة')
                      : 'تفعيل مجاني'
                    }
                  </button>
                </div>
              )}
            </div>

            {/* Teacher Card */}
            {pkg?.teacherName && (
              <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm flex flex-col gap-4 text-right">
                <h3 className="text-xs font-black text-[var(--admin-muted)]">مدرس المادة</h3>
                <div className="flex items-center gap-4">
                  {pkg.teacherProfileImageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={resolveMediaUrl(pkg.teacherProfileImageUrl)}
                      alt={pkg.teacherName}
                      className="h-14 w-14 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
                    />
                  ) : (
                    <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-black text-lg shadow-inner">
                      {pkg.teacherName.charAt(0)}
                    </div>
                  )}
                  <div>
                    <h4 className="font-black text-base text-[var(--admin-text)]">أ. {pkg.teacherName}</h4>
                    {pkg.teacherSpecialization && (
                      <p className="text-xs text-[var(--admin-primary)] font-black mt-0.5">
                        {pkg.teacherSpecialization
                          .split(",")
                          .map((s) => GRADE_NAMES[s.trim()] || s.trim())
                          .join(" ، ")}
                      </p>
                    )}
                  </div>
                </div>
                {pkg.teacherBio && (
                  <p className="text-xs text-[var(--admin-muted)] leading-relaxed border-t border-[var(--admin-border)]/10 pt-3 font-medium whitespace-pre-line">
                    {pkg.teacherBio}
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Purchase modal */}
      <PurchaseContentModal
        isOpen={isPurchaseModalOpen}
        onClose={() => setIsPurchaseModalOpen(false)}
        onPurchaseSuccess={() => void load()}
        contentType={
          sectionPrice !== null
            ? ("Month" as CodeType)
            : termPrice !== null
              ? ("Term" as CodeType)
              : ("Package" as CodeType)
        }
        contentId={
          sectionPrice !== null
            ? sectionId
            : termPrice !== null
              ? termId
              : packageId
        }
        contentName={
          sectionPrice !== null
            ? (section?.title || "القسم")
            : termPrice !== null
              ? (term?.title || "الترم")
              : (pkg?.name || "الباقة الكاملة")
        }
        price={displayPrice}
      />

      {/* Lesson-level purchase modal */}
      <PurchaseContentModal
        isOpen={!!purchaseLesson}
        onClose={() => setPurchaseLesson(null)}
        onPurchaseSuccess={() => {
          setPurchaseLesson(null);
          void load();
        }}
        contentType={"Lesson" as CodeType}
        contentId={purchaseLesson?.id || ''}
        contentName={purchaseLesson?.title || 'الحصة'}
        price={purchaseLesson?.price || 0}
      />
    </motion.div>
  );
}
