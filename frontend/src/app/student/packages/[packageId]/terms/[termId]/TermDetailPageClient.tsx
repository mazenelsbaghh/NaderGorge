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
import { useParams } from "next/navigation";
import { motion } from "framer-motion";
import Image from "next/image";
import Link from "next/link";

import {
  ArrowRight,
  ChevronLeft,
  BookOpen,

  RefreshCcw,
  TriangleAlert,

  Sparkles,
  CheckCircle2,
} from "lucide-react";
import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { CodeType } from "@/services/balance-service";

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
  AzhariPrimary1: 'الأول الابتدائي الأزهري',
  AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري',
  AmericanGrade9: 'Grade 9',
  AmericanGrade10: 'Grade 10',
  AmericanGrade11: 'Grade 11',
  AmericanGrade12: 'Grade 12',
};
import {
  contentService,
  type ContentSectionDto,

  type PackageDto,
  type TermDto,
} from "@/services/content-service";

import { registerCacheStore, unregisterCacheStore } from "@/lib/cache-invalidation";
import { resolveMediaUrl } from "@/utils/resolve-media-url";

/* ─── Stagger helpers ─────────────────────────────────────────────────── */
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

/* ─── Main page ──────────────────────────────────────────────────────── */
export default function TermDetailPageClient() {
  const params = useParams();

  const packageId = params.packageId as string;
  const termId = params.termId as string;

  const [pkg, setPkg] = useState<PackageDto | null>(null);
  const [term, setTerm] = useState<TermDto | null>(null);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);

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
    if (termId) {
      registerCacheStore(`content:term:${termId}`, () => {}, load);
      return () => {
        unregisterCacheStore(`content:term:${termId}`);
      };
    }
  }, [load, termId]);

  const isEnrolled = pkg?.isEnrolled ?? false;
  const isTermPurchased = term?.isPurchased ?? false;
  const hasAccess = isEnrolled || isTermPurchased;

  /* ── Loading skeleton ── */
  if (loading) {
    return (
      <div className="space-y-6 animate-pulse pb-10">
        <div className="h-9 w-48 rounded-full bg-[var(--admin-card-strong)]" />
        <div className="aspect-video w-full rounded-[28px] bg-[var(--admin-card-strong)] sm:rounded-2xl" />
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
      <motion.div variants={fadeUp}>
        <Link
          href={`/student/packages/${packageId}`}
          prefetch={false}
          className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
        >
          <ArrowRight className="h-4 w-4" />
          <span>العودة إلى {pkg?.name ?? "الباقة"}</span>
        </Link>
      </motion.div>

      {/* ── Hero Image Banner ── */}
      <div
        className="relative aspect-video w-full overflow-hidden rounded-3xl border border-[var(--admin-border)] shadow-md sm:rounded-2xl"
      >
        <Image
          src={
            term?.imageUrl
              ? resolveMediaUrl(term.imageUrl)
              : pkg?.imageUrl
                ? resolveMediaUrl(pkg.imageUrl)
                : "/images/default-package.webp"
          }
          alt={term?.title || "ترم"}
          fill
          priority
          quality={90}
          sizes="100vw"
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-[10s] hover:scale-105"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-black/20 to-transparent" />
      </div>

      {/* ── Term Title & Info Header Area ── */}
      <div className="space-y-4 text-right animate-[fadeIn_0.3s_ease-out]">
        <div className="flex flex-wrap items-center gap-2">
          {pkg?.name && (
            <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              {pkg.name}
            </span>
          )}
          <span className="rounded-full bg-slate-500/10 text-slate-600 dark:text-slate-400 px-3 py-1 text-xs font-black">
            {sections.length} قسم
          </span>
          <span className={`rounded-full px-3 py-1 text-xs font-black ${
            hasAccess ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : "bg-amber-500/10 text-amber-600 dark:text-amber-400"
          }`}>
            {hasAccess ? "مفعّل" : "تحتاج تفعيل"}
          </span>
        </div>

        <h1
          className="text-3xl font-black text-[var(--admin-text)] sm:text-4xl lg:text-5xl leading-tight"
        >
          {term?.title ?? "الترم"}
        </h1>
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

      {/* ── Two-Column Layout ── */}
      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        {/* Right Column: Main Content (Sections) */}
        <div className="lg:col-span-2 space-y-8">
          {/* ── Sections heading ── */}
          {!error && (
            <div>
              <h2 className="text-xl font-black text-[var(--admin-text)] sm:text-2xl">اختر القسم</h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                كل قسم يحتوي على حصص. اختر القسم لتتصفح الحصص وتبدأ المشاهدة.
              </p>
            </div>
          )}

          {/* ── Sections grid ── */}
          {!error && (
            <div>
              {sections.length === 0 ? (
                <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
                  <BookOpen className="mb-4 h-10 w-10 text-[var(--admin-muted)] opacity-40" />
                  <p className="font-bold text-[var(--admin-muted)]">لا توجد أقسام في هذا الترم بعد.</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                  {sections.map((section, idx) => {
                    const palettes = [
                      { from: "#334155", to: "#0f766e" },
                      { from: "#475569", to: "#0891b2" },
                      { from: "#1e293b", to: "#64748b" },
                      { from: "#0f766e", to: "#38bdf8" },
                    ];
                    const pal = palettes[idx % palettes.length];
                    const sectionPurchased = hasAccess || (section.isPurchased ?? false);
                    const isFree = section.price == null || section.price === 0;

                    return (
                      <div key={section.id} className="flex flex-col">
                      <Link
                          href={`/student/packages/${packageId}/terms/${termId}/sections/${section.id}`}
                          prefetch={false}
                          className="group relative flex w-full cursor-pointer flex-col overflow-hidden rounded-[1.75rem] bg-[var(--admin-card)] text-right shadow-md hover:-translate-y-1.5 hover:shadow-2xl transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                          style={{
                            boxShadow: `0 4px 20px color-mix(in srgb, ${pal.from} 15%, transparent)`,
                          }}
                        >
                          {/* Thumbnail */}
                          <div
                            className="relative aspect-video w-full overflow-hidden"
                            style={{
                              background: `linear-gradient(135deg, ${pal.from} 0%, ${pal.to} 100%)`,
                            }}
                          >
                            {section.imageUrl && (
                              <Image
                                src={resolveMediaUrl(section.imageUrl)}
                                alt={section.title}
                                fill
                                sizes="(max-width: 640px) 100vw, 33vw"
                                className="object-cover"
                              />
                            )}
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
                              className={`absolute right-4 top-4 rounded-full px-3 py-1 text-xs font-black tracking-wider ${
                                sectionPurchased
                                  ? "bg-emerald-500/30 text-white backdrop-blur-sm"
                                  : isFree
                                    ? "bg-white/20 text-white backdrop-blur-sm"
                                    : "bg-black/25 text-white/80 backdrop-blur-sm"
                              }`}
                            >
                              {sectionPurchased ? "✓ تم الشراء" : isFree ? "مجاني" : "مقفول"}
                            </span>

                            {/* Section number pill */}
                            <div className="absolute bottom-4 left-4 flex h-9 w-9 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm rtl:left-auto rtl:right-4">
                              <span className="text-sm font-black text-white">
                                {String(idx + 1).padStart(2, "0")}
                              </span>
                            </div>

                            {/* Shine on hover */}
                            <div
                              className="absolute inset-0 bg-gradient-to-tr from-white/0 via-white/5 to-white/15 opacity-0 group-hover:opacity-100 transition-opacity duration-500"
                            />
                          </div>

                          {/* Content */}
                          <div className="flex flex-1 flex-col p-5">
                            <h2 className="line-clamp-2 text-base font-black leading-snug text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)] sm:text-lg">
                              {section.title}
                            </h2>
                            <div className="mt-auto flex items-center justify-between pt-3">
                              {sectionPurchased ? (
                                <span className="text-xs font-black text-emerald-600 dark:text-emerald-400">✓ مفعّل</span>
                              ) : isFree ? (
                                <span className="text-xs font-black text-emerald-600 dark:text-emerald-400">مجاني</span>
                              ) : section.price != null && section.price > 0 ? (
                                <span className="text-xs font-bold text-[var(--admin-muted)]">
                                  {section.price} ج.م
                                </span>
                              ) : (
                                <span />
                              )}
                              <ChevronLeft className="h-4 w-4 text-[var(--admin-muted)] transition-all group-hover:-translate-x-0.5 group-hover:text-[var(--admin-primary)]" />
                            </div>
                          </div>
                      </Link>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Left Column: Sidebar (Actions + Teacher Info) */}
        <div className="space-y-6">
          {/* Purchase / Enrollment Action Card */}
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm space-y-4 text-right">
            <div>
              <span className="text-xs font-bold text-[var(--admin-muted)]">
                {term?.price != null && term.price > 0 ? 'سعر الترم' : 'سعر الباقة'}
              </span>
              {(() => {
                const price = term?.price != null && term.price > 0 ? term.price : (pkg?.price ?? 0);
                return price > 0 ? (
                  <p className="text-3xl font-black text-[var(--admin-primary)] mt-1">{price} ج.م</p>
                ) : (
                  <p className="text-3xl font-black text-emerald-600 dark:text-emerald-400 mt-1">مجاني</p>
                );
              })()}
            </div>
            
            {hasAccess ? (
              <div className="rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 p-4 text-center font-black text-sm">
                <CheckCircle2 className="inline h-4 w-4 mr-1" /> {isTermPurchased && !isEnrolled ? 'هذا الترم مفعّل في حسابك بالفعل.' : 'هذه الباقة مفعّلة في حسابك بالفعل.'} يمكنك البدء في دراسة الأقسام مباشرة.
              </div>
            ) : (
              <div className="flex flex-col gap-3">
                <button
                  type="button"
                  onClick={() => setIsPurchaseModalOpen(true)}
                  className="w-full inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                >
                  <Sparkles className="h-4 w-4" />
                  {term?.price != null && term.price > 0 ? 'شراء الترم' : 'شراء الباقة'}
                </button>
                <Link
                  href="/student/code-redemption"
                  prefetch={false}
                  className="w-full inline-flex min-h-[50px] items-center justify-center rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-3 text-sm font-bold text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] active:scale-[0.98]"
                >
                  لدي كود تفعيل
                </Link>
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

      {/* Purchase modal */}
      <PurchaseContentModal
        isOpen={isPurchaseModalOpen}
        onClose={() => setIsPurchaseModalOpen(false)}
        onPurchaseSuccess={() => void load()}
        contentType={
          term?.price != null && term.price > 0
            ? ("Term" as CodeType)
            : ("Package" as CodeType)
        }
        contentId={
          term?.price != null && term.price > 0
            ? termId
            : packageId
        }
        contentName={
          term?.price != null && term.price > 0
            ? (term.title || "الترم")
            : (pkg?.name || "الباقة الكاملة")
        }
        price={
          term?.price != null && term.price > 0
            ? term.price
            : (pkg?.price ?? 0)
        }
      />
    </motion.div>
  );
}
