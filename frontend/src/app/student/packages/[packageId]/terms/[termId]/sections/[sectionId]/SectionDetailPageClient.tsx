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
} from "lucide-react";
import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { CodeType } from "@/services/balance-service";
import {
  contentService,
  type ContentSectionDto,
  type LessonSummaryDto,
  type PackageDto,
  type TermDto,
} from "@/services/content-service";
import { usePlatformEvents } from "@/hooks/usePlatformEvents";
import { registerCacheStore, unregisterCacheStore } from "@/lib/cache-invalidation";
import { resolveMediaUrl } from "@/utils/resolve-media-url";

const GRADE_NAMES: Record<string, string> = {
  FirstSecondary: 'الأول الثانوي',
  SecondSecondary: 'الثاني الثانوي',
  SecondaryGrade3: 'الثالث الثانوي',
  FirstBaccalaureate: 'الأول بكالوريا',
  SecondBaccalaureate: 'الثاني بكالوريا',
  PrimaryGrade1: 'الأول الابتدائي',
  PrimaryGrade2: 'الثاني الابتدائي',
  PrimaryGrade3: 'الثالث الابتدائي',
  PrimaryGrade4: 'الرابع الابتدائي',
  PrimaryGrade5: 'الخامس الابتدائي',
  PrimaryGrade6: 'السادس الابتدائي',
  PrepGrade1: 'الأول الإعدادي',
  PrepGrade2: 'الثاني الإعدادي',
  PrepGrade3: 'الثالث الإعدادي',
};

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
      setPkg(pkgRes.data?.data?.find((p: PackageDto) => p.id === packageId) ?? null);
      setTerm(termsRes.data?.data?.find((t: TermDto) => t.id === termId) ?? null);
      setSection(sectRes.data?.data?.find((s: ContentSectionDto) => s.id === sectionId) ?? null);
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
      registerCacheStore(`content:section:${sectionId}`, () => {}, load);
      return () => {
        unregisterCacheStore(`content:section:${sectionId}`);
      };
    }
  }, [load, sectionId]);

  const isEnrolled = pkg?.isEnrolled ?? false;

  /* Determine price to show */
  const displayPrice =
    (section?.price != null && section.price > 0)
      ? section.price
      : (term?.price != null && (term.price ?? 0) > 0)
        ? term.price
        : (pkg?.price || 0);

  const priceLabel =
    (section?.price != null && section.price > 0)
      ? 'سعر القسم'
      : (term?.price != null && (term.price ?? 0) > 0)
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
              {(isEnrolled || (term?.isPurchased ?? false) || (section?.isPurchased ?? false)) && (
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
                  const canAccess = (isEnrolled || lesson.hasAccess) && !lesson.isLocked;
                  return (
                    <button
                      key={lesson.id}
                      type="button"
                      onClick={() => {
                        if (canAccess) {
                          router.push(`/student/packages/${packageId}/lessons/${lesson.id}`);
                        } else if (!isEnrolled) {
                          toast.error("فعّل الباقة أولاً للوصول للحصص.");
                        } else if (lesson.isLocked) {
                          toast.error(lesson.lockedReason || "هذه الحصة مقفولة.");
                        }
                      }}
                      className={`group relative flex w-full items-center gap-4 rounded-2xl border p-4 text-right transition-all ${
                        canAccess
                          ? "border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm hover:-translate-y-0.5 hover:shadow-md hover:border-[var(--admin-primary-30)] cursor-pointer"
                          : "border-[var(--admin-border)] bg-[var(--admin-card-soft)] opacity-70 cursor-not-allowed"
                      }`}
                    >
                      {/* Lesson Number */}
                      <div className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl font-black text-lg ${
                        lesson.isCompleted
                          ? "bg-emerald-500/15 text-emerald-600"
                          : canAccess
                            ? "bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"
                            : "bg-[var(--admin-card-strong)] text-[var(--admin-muted)]"
                      }`}>
                        {lesson.isCompleted ? (
                          <CheckCircle2 className="h-5 w-5" />
                        ) : !canAccess ? (
                          <Lock className="h-4 w-4" />
                        ) : (
                          <span>{String(idx + 1).padStart(2, "0")}</span>
                        )}
                      </div>

                      {/* Lesson Info */}
                      <div className="flex-1 min-w-0">
                        <h3 className={`text-sm font-black leading-snug ${
                          canAccess
                            ? "text-[var(--admin-text)] group-hover:text-[var(--admin-primary)]"
                            : "text-[var(--admin-muted)]"
                        } transition-colors`}>
                          {lesson.title}
                        </h3>
                        {lesson.summary && (
                          <p className="mt-0.5 text-xs text-[var(--admin-muted)] line-clamp-1">
                            {lesson.summary}
                          </p>
                        )}
                      </div>

                      {/* Action */}
                      {canAccess && (
                        <PlayCircle className="h-5 w-5 shrink-0 text-[var(--admin-primary)] opacity-0 transition-opacity group-hover:opacity-100" />
                      )}

                      {/* Buy lesson button / Status */}
                      {!canAccess && lesson.price != null && lesson.price > 0 && (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setPurchaseLesson(lesson);
                          }}
                          className="shrink-0 inline-flex items-center gap-1.5 rounded-xl bg-[var(--admin-primary)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-95 opacity-100"
                        >
                          <ShoppingCart className="h-3 w-3" />
                          {lesson.price} ج.م
                        </button>
                      )}
                      {!canAccess && (lesson.price == null || lesson.price === 0) && !lesson.isLocked && (
                        <span className="shrink-0 text-xs font-black text-emerald-600 dark:text-emerald-400">مجانية</span>
                      )}
                    </button>
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

              {(isEnrolled || (term?.isPurchased ?? false) || (section?.isPurchased ?? false)) ? (
                <div className="rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 p-4 text-center font-black text-sm">
                  🎉 {isEnrolled ? 'الباقة مفعّلة' : (term?.isPurchased ?? false) ? 'الترم مفعّل' : 'القسم مفعّل'} في حسابك بالفعل. يمكنك مشاهدة الحصص مباشرة.
                </div>
              ) : (
                <div className="flex flex-col gap-3">
                  <button
                    type="button"
                    onClick={() => setIsPurchaseModalOpen(true)}
                    className="w-full inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                  >
                    <Sparkles className="h-4 w-4" />
                    {(section?.price != null && section.price > 0) ? 'شراء القسم' : (term?.price != null && (term.price ?? 0) > 0) ? 'شراء الترم' : 'شراء الباقة'}
                  </button>
                  <button
                    type="button"
                    onClick={() => router.push("/student/code-redemption")}
                    className="w-full inline-flex min-h-[50px] items-center justify-center rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-3 text-sm font-bold text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] active:scale-[0.98]"
                  >
                    لدي كود تفعيل
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
          (section?.price != null && section.price > 0)
            ? ("Month" as CodeType)
            : (term?.price != null && (term.price ?? 0) > 0)
              ? ("Term" as CodeType)
              : ("Package" as CodeType)
        }
        contentId={
          (section?.price != null && section.price > 0)
            ? sectionId
            : (term?.price != null && (term.price ?? 0) > 0)
              ? termId
              : packageId
        }
        contentName={
          (section?.price != null && section.price > 0)
            ? (section.title || "القسم")
            : (term?.price != null && (term.price ?? 0) > 0)
              ? (term?.title || "الترم")
              : (pkg?.name || "الباقة الكاملة")
        }
        price={displayPrice as number}
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
