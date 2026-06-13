"use client";

import { devConsole } from '@/utils/dev-console';
/**
 * Package Profile Page — /student/packages/[packageId]
 *
 * Layout (top → bottom):
 *   1. Full-width hero image with overlay gradient + title
 *   2. Two-column body:
 *      Right (RTL): Package info + stats + CTA
 *      Left  (RTL): Content structure accordion
 *
 * Token system: --admin-* (same as AdminShellChrome)
 */

import { useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { motion } from "framer-motion";
import Image from "next/image";
import Link from "next/link";
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
  AzhariPrimary1: 'الأول الابتدائي الأزهري',
  AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري',
  AmericanGrade9: 'Grade 9',
  AmericanGrade10: 'Grade 10',
  AmericanGrade11: 'Grade 11',
  AmericanGrade12: 'Grade 12',
};
import {
  ArrowRight,
  ChevronLeft,
  Sparkles,
  CheckCircle2,
} from "lucide-react";
import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { CodeType } from "@/services/balance-service";
import {
  contentService,
  type TermDto,
  type PackageDto,
} from "@/services/content-service";

/* ── Stagger helpers ─────────────────────────────────────────────────── */
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

export default function PackageProfilePageClient() {
  const params = useParams();
  const packageId = params.packageId as string;

  const [pkg, setPkg] = useState<PackageDto | null>(null);
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [termsLoading, setTermsLoading] = useState(true);
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);

  const loadPackageData = useCallback((options?: { showLoading?: boolean }) => {
    if (!packageId) return;

    const cachedPackage = contentService.peekCachedPackageById(packageId);
    if (cachedPackage) {
      setPkg(cachedPackage);
    }

    if (options?.showLoading || !cachedPackage) {
      setLoading(true);
    }
    setTermsLoading(true);

    Promise.all([
      contentService.getPackages(),
      contentService.getTerms(packageId),
    ])
      .then(([pkgRes, termRes]) => {
        const found = pkgRes.data?.data?.find(
          (p: PackageDto) => p.id === packageId
        );
        setPkg(found ?? cachedPackage ?? null);
        setTerms(termRes.data.data);
      })
      .catch((err) => devConsole.error(err))
      .finally(() => {
        setLoading(false);
        setTermsLoading(false);
      });
  }, [packageId]);

  useEffect(() => {
    let cancelled = false;

    queueMicrotask(() => {
      if (!cancelled) {
        loadPackageData();
      }
    });

    return () => {
      cancelled = true;
    };
  }, [loadPackageData]);



  /* ── Loading skeleton ── */
  if (loading && !pkg) {
    return (
      <div className="space-y-6 animate-pulse">
        {/* Hero skeleton */}
        <div className="aspect-video w-full rounded-[28px] bg-[var(--admin-card-strong)]" />
        {/* Two-column skeleton */}
        <div className="grid gap-8 lg:grid-cols-[1fr_1fr]">
          <div className="space-y-4">
            <div className="h-10 w-3/4 rounded-xl bg-[var(--admin-card-strong)]" />
            <div className="h-6 w-full rounded-lg bg-[var(--admin-card-strong)]" />
            <div className="h-6 w-2/3 rounded-lg bg-[var(--admin-card-strong)]" />
            <div className="grid grid-cols-3 gap-3 pt-4">
              {[1, 2, 3].map((i) => (
                <div key={i} className="h-24 rounded-2xl bg-[var(--admin-card-strong)]" />
              ))}
            </div>
          </div>
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-20 rounded-2xl bg-[var(--admin-card-strong)]" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  const isEnrolled = pkg?.isEnrolled ?? false;

  return (
    <motion.div
      className="space-y-8"
      variants={stagger}
      initial="hidden"
      animate="visible"
    >
      {/* ── Back button ── */}
      <motion.div variants={fadeUp}>
        <Link
          href="/student/packages"
          prefetch={false}
          className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
        >
          <ArrowRight className="h-4 w-4" />
          <span>العودة إلى باقاتي</span>
        </Link>
      </motion.div>

      {/* ── Hero Image Banner ── */}
      <div
        className="relative aspect-video w-full overflow-hidden rounded-3xl border border-[var(--admin-border)] shadow-md sm:rounded-2xl"
        style={{ viewTransitionName: `pkg-image-${packageId}` }}
      >
        <Image
          src={pkg?.imageUrl ? resolveMediaUrl(pkg.imageUrl) : "/images/default-package.webp"}
          alt={pkg?.name || "باقة"}
          fill
          priority
          quality={90}
          sizes="100vw"
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-[10s] hover:scale-105"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-black/20 to-transparent" />
      </div>

      {/* ── Package Title & Info Header Area ── */}
      <div className="space-y-4 text-right">
        <div className="flex flex-wrap items-center gap-2">
          {pkg?.subjectName && (
            <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              {pkg.subjectName}
            </span>
          )}
          <span className={`rounded-full px-3 py-1 text-xs font-black ${
            isEnrolled ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : "bg-amber-500/10 text-amber-600 dark:text-amber-400"
          }`}>
            {isEnrolled ? "باقة مفعّلة" : "تحتاج تفعيل"}
          </span>
          <span className="rounded-full bg-slate-500/10 text-slate-600 dark:text-slate-400 px-3 py-1 text-xs font-black">
            {terms.length} ترم
          </span>
        </div>

        <h1
          className="text-3xl font-black text-[var(--admin-text)] sm:text-4xl lg:text-5xl leading-tight"
          style={{ viewTransitionName: `pkg-title-${packageId}` }}
        >
          {pkg?.name || "باقة غير معروفة"}
        </h1>
      </div>

      {/* ── Two-Column Layout ── */}
      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        {/* Right Column: Main Content (Description + Terms) */}
        <div className="lg:col-span-2 space-y-8">
          {/* Description */}
          <div className="space-y-2 text-right">
            <h3 className="text-lg font-black text-[var(--admin-text)]">تفاصيل الباقة</h3>
            <p className="text-sm leading-7 text-[var(--admin-muted)] sm:text-base whitespace-pre-line">
              {pkg?.description || "تفاصيل هذه الباقة غير متوفرة حالياً."}
            </p>
          </div>

          {/* Terms Section */}
          <div className="space-y-4">
            <div className="text-right">
              <h2 className="text-xl font-black text-[var(--admin-text)] sm:text-2xl">اختر الترم</h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">كل ترم يحتوي على أقسام وحصص. اختر الترم لتبدأ الدراسة.</p>
            </div>

            {/* Terms grid */}
            {termsLoading ? (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                {[1, 2].map((i) => (
                  <div key={i} className="h-44 animate-pulse rounded-[1.75rem] bg-[var(--admin-card-strong)]" />
                ))}
              </div>
            ) : terms.length === 0 ? (
              <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center bg-[var(--admin-card)]/30">
                <p className="font-bold text-[var(--admin-muted)]">لا توجد أترام في هذه الباقة بعد.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                {terms.map((term, idx) => {
                  const palettes = [
                    { from: "#475569", to: "#0f766e", pat: "#64748b" },
                    { from: "#334155", to: "#0891b2", pat: "#475569" },
                    { from: "#0f766e", to: "#38bdf8", pat: "#0e7490" },
                    { from: "#1e293b", to: "#64748b", pat: "#334155" },
                  ];
                  const pal = palettes[idx % palettes.length];
                  return (
                    <Link
                      key={term.id}
                      href={`/student/packages/${packageId}/terms/${term.id}`}
                      prefetch={false}
                      className="group relative flex cursor-pointer flex-col overflow-hidden rounded-[1.75rem] bg-[var(--admin-card)] text-right shadow-sm border border-[var(--admin-border)] transition-all hover:-translate-y-1.5 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
                    >
                      {/* Thumbnail area */}
                      <div
                        className="relative aspect-video w-full overflow-hidden"
                        style={{ background: `linear-gradient(135deg, ${pal.from} 0%, ${pal.to} 100%)` }}
                      >
                        {term.imageUrl && (
                          <Image
                            src={resolveMediaUrl(term.imageUrl)}
                            alt={term.title}
                            fill
                            sizes="(max-width: 640px) 100vw, 33vw"
                            className="object-cover"
                          />
                        )}
                        {/* Geometric pattern */}
                        <svg
                          className="absolute inset-0 h-full w-full opacity-[0.12]"
                          xmlns="http://www.w3.org/2000/svg"
                          width="80"
                          height="80"
                        >
                          <defs>
                            <pattern id={`p${idx}`} patternUnits="userSpaceOnUse" width="40" height="40">
                              <path
                                d="M20 0 L40 20 L20 40 L0 20 Z"
                                fill="none"
                                stroke="white"
                                strokeWidth="1.5"
                              />
                              <circle cx="20" cy="20" r="4" fill="white" />
                              <line x1="20" y1="0" x2="20" y2="6" stroke="white" strokeWidth="1" />
                              <line x1="20" y1="34" x2="20" y2="40" stroke="white" strokeWidth="1" />
                              <line x1="0" y1="20" x2="6" y2="20" stroke="white" strokeWidth="1" />
                              <line x1="34" y1="20" x2="40" y2="20" stroke="white" strokeWidth="1" />
                            </pattern>
                          </defs>
                          <rect width="100%" height="100%" fill={`url(#p${idx})`} />
                        </svg>

                        {/* Large term ordinal number */}
                        <span
                          className="absolute -left-2 -top-4 select-none font-black leading-none text-white/[0.08] rtl:-right-2 rtl:left-auto"
                          style={{ fontSize: "clamp(5rem, 14vw, 9rem)" }}
                          aria-hidden
                        >
                          {idx + 1}
                        </span>

                        {/* Status badge */}
                        <span
                          className={`absolute right-4 top-4 rounded-full px-3 py-1 text-xs font-black tracking-wider ${
                            isEnrolled
                              ? "bg-white/20 text-white backdrop-blur-sm"
                              : "bg-black/25 text-white/80 backdrop-blur-sm"
                          }`}
                        >
                          {isEnrolled ? "✦ مفتوح" : "مقفول"}
                        </span>

                        {/* Term number pill */}
                        <div className="absolute bottom-4 left-4 flex h-9 w-9 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm rtl:left-auto rtl:right-4">
                          <span className="text-sm font-black text-white">
                            {String(idx + 1).padStart(2, "0")}
                          </span>
                        </div>
                      </div>

                      {/* Content area */}
                      <div className="flex flex-1 flex-col p-5">
                        <h3 className="line-clamp-2 text-base font-black leading-snug text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)] sm:text-lg">
                          {term.title}
                        </h3>

                        <div className="mt-auto flex items-center justify-between pt-3">
                          {term.price != null && term.price > 0 ? (
                            <span className="text-xs font-bold text-[var(--admin-muted)]">
                              {term.price} ج.م
                            </span>
                          ) : (
                            <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400">مجانًا</span>
                          )}
                          <ChevronLeft className="h-4 w-4 text-[var(--admin-muted)] transition-all group-hover:-translate-x-0.5 group-hover:text-[var(--admin-primary)]" />
                        </div>
                      </div>
                    </Link>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Left Column: Sidebar (Actions + Teacher Info) */}
        <div className="space-y-6">
          {/* Purchase / Enrollment Action Card */}
          <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm space-y-4 text-right">
            <div>
              <span className="text-xs font-bold text-[var(--admin-muted)]">سعر الباقة</span>
              <p className="text-3xl font-black text-[var(--admin-primary)] mt-1">{pkg?.price || 0} ج.م</p>
            </div>
            
            {isEnrolled ? (
              <div className="rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 p-4 text-center font-black text-sm">
                <CheckCircle2 className="inline h-4 w-4 mr-1" /> هذه الباقة مفعّلة في حسابك بالفعل. يمكنك البدء في دراسة الأترام مباشرة.
              </div>
            ) : (
              <div className="flex flex-col gap-3">
                <button
                  type="button"
                  onClick={() => setIsPurchaseModalOpen(true)}
                  className="w-full inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                >
                  <Sparkles className="h-4 w-4" />
                  شراء الباقة
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
        onPurchaseSuccess={() => loadPackageData()}
        contentType={"Package" as CodeType}
        contentId={packageId}
        contentName={pkg?.name || "الباقة الكاملة"}
        price={pkg?.price || 0}
      />
    </motion.div>
  );
}
