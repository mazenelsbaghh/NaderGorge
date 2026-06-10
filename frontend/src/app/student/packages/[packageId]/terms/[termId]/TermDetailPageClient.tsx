"use client";

/**
 * Term Detail Page — /student/packages/[packageId]/terms/[termId]
 *
 * Layout mirrors the Package Profile Page exactly:
 *   Back button → Full-width cinematic hero (package image + term title)
 *   → Info strip → "اختر القسم" heading → Section cards grid
 *   Clicking a section card expands inline lesson rows
 */

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { motion, AnimatePresence } from "framer-motion";
import Image from "next/image";
import {
  ArrowRight,
  ChevronDown,
  BookOpen,
  Lock,
  RefreshCcw,
  TriangleAlert,
  CheckCircle2,
} from "lucide-react";
import {
  contentService,
  type ContentSectionDto,
  type LessonSummaryDto,
  type PackageDto,
  type TermDto,
} from "@/services/content-service";

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

/* ─── Inline lessons for an expanded section card ─────────────────── */
function SectionLessons({
  sectionId,
  packageId,
  isPackageEnrolled,
}: {
  sectionId: string;
  packageId: string;
  isPackageEnrolled: boolean;
}) {
  const router = useRouter();
  const [lessons, setLessons] = useState<LessonSummaryDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    contentService
      .getLessons(sectionId)
      .then((res) => setLessons(res.data?.data ?? []))
      .catch(() => setError("تعذر تحميل الحصص."))
      .finally(() => setLoading(false));
  }, [sectionId]);

  const go = (lesson: LessonSummaryDto) => {
    if (lesson.isLocked) {
      if (lesson.blockingHomeworkLessonId)
        return router.push(`/student/packages/${packageId}/lessons/${lesson.blockingHomeworkLessonId}`);
      if (lesson.blockingExamId)
        return router.push(`/student/exams/${lesson.blockingExamId}?packageId=${packageId}`);
    }
    router.push(`/student/packages/${packageId}/lessons/${lesson.id}`);
  };

  if (loading)
    return (
      <div className="animate-pulse space-y-3 p-4">
        {[1, 2, 3].map((i) => (
          <div key={i} className="h-14 rounded-xl bg-[var(--admin-card-strong)]" />
        ))}
      </div>
    );

  if (error)
    return (
      <div className="flex items-center gap-3 p-4 text-sm text-[var(--admin-danger)]">
        <TriangleAlert className="h-4 w-4 shrink-0" />
        <span>{error}</span>
      </div>
    );

  if (!lessons || lessons.length === 0)
    return (
      <p className="p-4 text-center text-sm text-[var(--admin-muted)]">
        لا توجد حصص في هذا القسم.
      </p>
    );

  return (
    <ul className="space-y-2 p-4">
      {lessons.map((lesson) => (
        <li
          key={lesson.id}
          className="flex items-center justify-between gap-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3"
        >
          <div className="min-w-0 flex-1">
            <p className="font-bold text-[var(--admin-text)] leading-snug">{lesson.title}</p>
            {lesson.isLocked && lesson.lockedReason && (
              <p className="mt-0.5 text-xs text-[var(--admin-muted)]">{lesson.lockedReason}</p>
            )}
          </div>
          <div className="shrink-0">
            {lesson.hasAccess ? (
              <div className="flex items-center gap-2">
                {lesson.isCompleted && (
                  <CheckCircle2 className="h-4 w-4 text-[var(--admin-success)]" />
                )}
                <button
                  type="button"
                  onClick={() => go(lesson)}
                  className="rounded-lg bg-[var(--admin-primary)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                >
                  {lesson.isLocked
                    ? lesson.blockingHomeworkLessonId
                      ? "الواجب"
                      : "الامتحان"
                    : "مشاهدة"}
                </button>
              </div>
            ) : isPackageEnrolled ? (
              <span className="flex items-center gap-1 text-xs font-bold text-[var(--admin-danger)]">
                <Lock className="h-3.5 w-3.5" />
                مقفول
              </span>
            ) : (
              <button
                type="button"
                onClick={() => router.push(`/student/packages/${packageId}`)}
                className="rounded-lg border border-[var(--admin-primary-20)] bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-primary-20)]"
              >
                شراء الباقة
              </button>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

/* ─── Main page ──────────────────────────────────────────────────────── */
export default function TermDetailPageClient() {
  const params = useParams();
  const router = useRouter();
  const packageId = params.packageId as string;
  const termId = params.termId as string;

  const [pkg, setPkg] = useState<PackageDto | null>(null);
  const [term, setTerm] = useState<TermDto | null>(null);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expandedSection, setExpandedSection] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!packageId || !termId) return;
    setLoading(true);
    setError(null);
    try {
      const [pkgRes, sectRes, termsRes] = await Promise.all([
        contentService.getPackages(),
        contentService.getSections(termId),
        contentService.getTerms(packageId),
      ]);
      setPkg(pkgRes.data?.data?.find((p: PackageDto) => p.id === packageId) ?? null);
      setSections(sectRes.data?.data ?? []);
      setTerm(termsRes.data?.data?.find((t: TermDto) => t.id === termId) ?? null);
    } catch {
      setError("تعذر تحميل محتوى الترم. تحقق من اتصالك وأعد المحاولة.");
    } finally {
      setLoading(false);
    }
  }, [packageId, termId]);

  useEffect(() => {
    void load();
  }, [load]);

  const isEnrolled = pkg?.isEnrolled ?? false;

  // Guard: redirect to package page if not enrolled
  useEffect(() => {
    if (!loading && pkg && !pkg.isEnrolled) {
      router.replace(`/student/packages/${packageId}`);
    }
  }, [loading, pkg, packageId, router]);

  /* ── Loading skeleton ── */
  if (loading) {
    return (
      <div className="space-y-6 animate-pulse pb-10">
        <div className="h-9 w-48 rounded-full bg-[var(--admin-card-strong)]" />
        <div className="h-[clamp(18rem,52vh,40rem)] rounded-[28px] bg-[var(--admin-card-strong)] sm:rounded-2xl" />
        <div className="h-6 w-2/3 rounded-xl bg-[var(--admin-card-strong)]" />
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-52 rounded-[1.75rem] bg-[var(--admin-card-strong)]" />
          ))}
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
      <motion.button
        variants={fadeUp}
        type="button"
        onClick={() => router.push(`/student/packages/${packageId}`)}
        className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
      >
        <ArrowRight className="h-4 w-4" />
        <span>العودة إلى {pkg?.name ?? "الباقة"}</span>
      </motion.button>

      {/* ══════════════════════════════════════════════════════════════════
          HERO — full-width cinematic banner (same as package page)
          Uses the package image as backdrop, term title as heading
          ══════════════════════════════════════════════════════════════════ */}
      <div
        className="relative h-[clamp(18rem,52vh,40rem)] min-h-[18rem] w-full overflow-hidden rounded-[28px] border border-[var(--admin-border)] shadow-[0_24px_60px_var(--admin-shadow)] sm:min-h-[22rem] sm:rounded-2xl lg:min-h-[26rem]"
      >
        <Image
          src={pkg?.imageUrl || "/images/default-package.png"}
          alt={term?.title || "ترم"}
          fill
          priority
          quality={85}
          sizes="100vw"
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-[10s] hover:scale-105"
        />

        {/* Gradient overlay */}
        <div className="absolute inset-0 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--admin-text)_6%,transparent),color-mix(in_srgb,var(--admin-text)_82%,transparent)_66%,color-mix(in_srgb,var(--admin-text)_92%,transparent))]" />
        <div className="absolute inset-x-0 bottom-0 h-28 bg-[linear-gradient(180deg,transparent,color-mix(in_srgb,var(--admin-primary)_16%,transparent))]" />

        {/* Title overlay */}
        <div className="absolute inset-x-0 bottom-0 z-10 p-5 sm:p-8 lg:p-12">
          {/* Breadcrumb pill */}
          <div className="inline-flex max-w-full flex-wrap items-center gap-2 rounded-full bg-[color:color-mix(in_srgb,var(--admin-text)_24%,transparent)] px-4 py-2 text-[11px] font-black tracking-[0.22em] text-[var(--admin-primary-contrast)] backdrop-blur-md sm:text-[13px]">
            <span>{pkg?.name}</span>
            <span className="opacity-55">•</span>
            <span>{sections.length} قسم</span>
            <span className="opacity-55">•</span>
            <span>{isEnrolled ? "مفعّل" : "تحتاج تفعيل"}</span>
          </div>
          <h1
            className="mt-4 text-3xl font-extrabold leading-tight tracking-tight text-[var(--admin-primary-contrast)] drop-shadow-[0_8px_24px_rgba(44,23,8,0.28)] sm:text-5xl lg:text-6xl"
          >
            {term?.title ?? "الترم"}
          </h1>
        </div>
      </div>

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
            className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card)] px-5 py-2.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
          >
            <RefreshCcw className="h-4 w-4" />
            إعادة المحاولة
          </button>
        </motion.div>
      )}

      {/* ── Sections heading ── */}
      {!error && (
        <motion.div variants={fadeUp}>
          <h2 className="text-xl font-black text-[var(--admin-text)] sm:text-2xl">اختر القسم</h2>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">
            كل قسم يحتوي على حصص. اختر القسم لتتصفح الحصص وتبدأ المشاهدة.
          </p>
        </motion.div>
      )}

      {/* ── Sections grid ── */}
      {!error && (
        <motion.div variants={fadeUp}>
          {sections.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
              <BookOpen className="mb-4 h-10 w-10 text-[var(--admin-muted)] opacity-40" />
              <p className="font-bold text-[var(--admin-muted)]">لا توجد أقسام في هذا الترم بعد.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {sections.map((section, idx) => {
                const palettes = [
                  { from: "#334155", to: "#0f766e" },
                  { from: "#475569", to: "#0891b2" },
                  { from: "#1e293b", to: "#64748b" },
                  { from: "#0f766e", to: "#38bdf8" },
                ];
                const pal = palettes[idx % palettes.length];
                const isExpanded = expandedSection === section.id;

                return (
                  <div key={section.id} className="flex flex-col">
                    <button
                      type="button"
                      onClick={() =>
                        setExpandedSection((prev) =>
                          prev === section.id ? null : section.id
                        )
                      }
                      className={`group relative flex w-full cursor-pointer flex-col overflow-hidden rounded-[1.75rem] bg-[var(--admin-card)] text-right transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${
                        isExpanded
                          ? "shadow-xl"
                          : "shadow-md hover:-translate-y-1.5 hover:shadow-2xl"
                      }`}
                      style={{
                        boxShadow: isExpanded
                          ? `0 8px 32px color-mix(in srgb, ${pal.from} 28%, transparent)`
                          : `0 4px 20px color-mix(in srgb, ${pal.from} 15%, transparent)`,
                      }}
                    >
                      {/* Thumbnail */}
                      <div
                        className="relative h-36 w-full overflow-hidden"
                        style={{
                          background: `linear-gradient(135deg, ${pal.from} 0%, ${pal.to} 100%)`,
                        }}
                      >
                        {/* Diamond-grid SVG pattern */}
                        <svg
                          className="absolute inset-0 h-full w-full opacity-[0.14]"
                          xmlns="http://www.w3.org/2000/svg"
                        >
                          <defs>
                            <pattern
                              id={`sp-${termId}-${idx}`}
                              patternUnits="userSpaceOnUse"
                              width="32"
                              height="32"
                            >
                              <path
                                d="M16 1 L31 16 L16 31 L1 16 Z"
                                fill="none"
                                stroke="white"
                                strokeWidth="1.2"
                              />
                              <path
                                d="M16 8 L24 16 L16 24 L8 16 Z"
                                fill="white"
                                opacity="0.5"
                              />
                            </pattern>
                          </defs>
                          <rect width="100%" height="100%" fill={`url(#sp-${termId}-${idx})`} />
                        </svg>

                        {/* Ghost ordinal */}
                        <span
                          className="absolute -bottom-3 -left-2 select-none font-black leading-none text-white/[0.1] rtl:-right-2 rtl:left-auto"
                          style={{ fontSize: "clamp(4.5rem, 13vw, 8rem)" }}
                          aria-hidden
                        >
                          {idx + 1}
                        </span>

                        {/* Status badge */}
                        <span
                          className={`absolute right-4 top-4 rounded-full px-3 py-1 text-[11px] font-black tracking-wider ${
                            isEnrolled
                              ? "bg-white/20 text-white backdrop-blur-sm"
                              : "bg-black/25 text-white/80 backdrop-blur-sm"
                          }`}
                        >
                          {isEnrolled ? "✦ مفتوح" : "مقفول"}
                        </span>

                        {/* Section number pill */}
                        <div className="absolute bottom-4 left-4 flex h-9 w-9 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm rtl:left-auto rtl:right-4">
                          <span className="text-sm font-black text-white">
                            {String(idx + 1).padStart(2, "0")}
                          </span>
                        </div>

                        {/* Shine on hover/expand */}
                        <div
                          className={`absolute inset-0 bg-gradient-to-tr from-white/0 via-white/5 to-white/15 transition-opacity duration-500 ${
                            isExpanded
                              ? "opacity-100"
                              : "opacity-0 group-hover:opacity-100"
                          }`}
                        />
                      </div>

                      {/* Content */}
                      <div className="flex flex-1 flex-col p-5">
                        <h2 className="line-clamp-2 text-base font-black leading-snug text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)] sm:text-lg">
                          {section.title}
                        </h2>
                        <div className="mt-auto flex items-center justify-between pt-3">
                          {section.price != null && section.price > 0 ? (
                            <span className="text-xs font-bold text-[var(--admin-muted)]">
                              {section.price} ج.م
                            </span>
                          ) : (
                            <span />
                          )}
                          <ChevronDown
                            className={`h-4 w-4 text-[var(--admin-muted)] transition-transform duration-300 group-hover:text-[var(--admin-primary)] ${
                              isExpanded
                                ? "rotate-180 text-[var(--admin-primary)]"
                                : ""
                            }`}
                          />
                        </div>
                      </div>
                    </button>

                    {/* Inline lessons */}
                    <AnimatePresence initial={false}>
                      {isExpanded && (
                        <motion.div
                          initial={{ opacity: 0, height: 0 }}
                          animate={{ opacity: 1, height: "auto" }}
                          exit={{ opacity: 0, height: 0 }}
                          transition={{ duration: 0.35, ease: [0.16, 1, 0.3, 1] }}
                          className="overflow-hidden"
                        >
                          <div className="mt-3 rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)]">
                            <SectionLessons
                              sectionId={section.id}
                              packageId={packageId}
                              isPackageEnrolled={isEnrolled}
                            />
                          </div>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                );
              })}
            </div>
          )}
        </motion.div>
      )}
    </motion.div>
  );
}
