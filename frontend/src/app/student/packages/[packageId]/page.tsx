"use client";

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
import { useParams, useRouter } from "next/navigation";
import { motion } from "framer-motion";
import Image from "next/image";
import {
  ArrowRight,
  ChevronLeft,
  Sparkles,
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

export default function PackageProfilePage() {
  const params = useParams();
  const router = useRouter();
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
      .catch((err) => console.error(err))
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

  // Auto-navigate when there is exactly one term — must be before any early returns
  useEffect(() => {
    if (!termsLoading && terms.length === 1 && pkg?.isEnrolled) {
      router.replace(`/student/packages/${packageId}/terms/${terms[0].id}`);
    }
  }, [termsLoading, terms, pkg, packageId, router]);

  /* ── Loading skeleton ── */
  if (loading && !pkg) {
    return (
      <div className="space-y-6 animate-pulse">
        {/* Hero skeleton */}
        <div className="h-[280px] sm:h-[340px] rounded-[28px] bg-[var(--admin-card-strong)]" />
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
      <motion.button
        variants={fadeUp}
        type="button"
        onClick={() => router.push("/student/packages")}
        className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
      >
        <ArrowRight className="h-4 w-4" />
        <span>العودة إلى باقاتي</span>
      </motion.button>

      {/* ── Hero Image ── */}
      <div
        className="relative h-[clamp(18rem,52vh,40rem)] min-h-[18rem] w-full overflow-hidden rounded-[28px] border border-[var(--admin-border)] shadow-[0_24px_60px_var(--admin-shadow)] sm:min-h-[22rem] sm:rounded-[32px] lg:min-h-[26rem]"
        style={{ viewTransitionName: `pkg-image-${packageId}` }}
      >
        <Image
          src={pkg?.imageUrl || "/images/default-package.png"}
          alt={pkg?.name || "باقة"}
          fill
          priority
          quality={85}
          sizes="100vw"
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-[10s] hover:scale-105"
        />
        <div className="absolute inset-0 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--admin-text)_6%,transparent),color-mix(in_srgb,var(--admin-text)_82%,transparent)_66%,color-mix(in_srgb,var(--admin-text)_92%,transparent))]" />
        <div className="absolute inset-x-0 bottom-0 z-10 p-5 sm:p-8 lg:p-12">
          <div className="inline-flex max-w-full flex-wrap items-center gap-2 rounded-full bg-[color:color-mix(in_srgb,var(--admin-text)_24%,transparent)] px-4 py-2 text-[11px] font-black tracking-[0.22em] text-[var(--admin-primary-contrast)] backdrop-blur-md sm:text-[13px]">
            <span>{isEnrolled ? "باقة مفعّلة" : "تحتاج تفعيل"}</span>
            <span className="opacity-55">•</span>
            <span>{terms.length} ترم</span>
            <span className="opacity-55">•</span>
            <span>{pkg?.price || 0} ج.م</span>
          </div>
          <h1
            className="mt-4 text-3xl font-extrabold leading-tight tracking-tight text-[var(--admin-primary-contrast)] drop-shadow-[0_8px_24px_rgba(44,23,8,0.28)] sm:text-5xl lg:text-6xl"
            style={{ viewTransitionName: `pkg-title-${packageId}` }}
          >
            {pkg?.name || "باقة غير معروفة"}
          </h1>
        </div>
      </div>

      {/* ── Info strip + CTA ── */}
      <motion.div variants={fadeUp} className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="max-w-2xl">
          <p className="text-sm leading-7 text-[var(--admin-muted)] sm:text-base">
            {pkg?.description || "تفاصيل هذه الباقة غير متوفرة حالياً."}
          </p>
        </div>
        {!isEnrolled && (
          <div className="flex shrink-0 flex-col gap-2 sm:flex-row">
            <button
              type="button"
              onClick={() => setIsPurchaseModalOpen(true)}
              className="inline-flex items-center justify-center gap-2 rounded-[18px] bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg transition-all hover:-translate-y-0.5 hover:shadow-xl focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
            >
              <Sparkles className="h-4 w-4" />
              شراء الباقة
            </button>
            <button
              type="button"
              onClick={() => router.push("/student/code-redemption")}
              className="inline-flex items-center justify-center rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-3 text-sm font-bold text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
            >
              لدي كود تفعيل
            </button>
          </div>
        )}
      </motion.div>

      {/* ── Terms section heading ── */}
      <motion.div variants={fadeUp}>
        <h2 className="text-xl font-black text-[var(--admin-text)] sm:text-2xl">اختر الترم</h2>
        <p className="mt-1 text-sm text-[var(--admin-muted)]">كل ترم يحتوي على أقسام وحصص. اختر الترم لتبدأ الدراسة.</p>
      </motion.div>

      {/* ── Terms grid ── */}
      <motion.div variants={fadeUp}>
        {termsLoading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-44 animate-pulse rounded-[1.75rem] bg-[var(--admin-card-strong)]" />
            ))}
          </div>
        ) : terms.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
            <p className="font-bold text-[var(--admin-muted)]">لا توجد أترام في هذه الباقة بعد.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {terms.map((term, idx) => {
              // Each term gets a distinct cool palette from the default student theme.
              const palettes = [
                { from: "#475569", to: "#0f766e", pat: "#64748b" },
                { from: "#334155", to: "#0891b2", pat: "#475569" },
                { from: "#0f766e", to: "#38bdf8", pat: "#0e7490" },
                { from: "#1e293b", to: "#64748b", pat: "#334155" },
              ];
              const pal = palettes[idx % palettes.length];
              return (
                <button
                  key={term.id}
                  type="button"
                  onClick={() => router.push(`/student/packages/${packageId}/terms/${term.id}`)}
                  className="group relative flex cursor-pointer flex-col overflow-hidden rounded-[1.75rem] bg-[var(--admin-card)] text-right shadow-md transition-all hover:-translate-y-1.5 hover:shadow-2xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
                  style={{ boxShadow: `0 4px 24px color-mix(in srgb, ${pal.from} 18%, transparent)` }}
                >
                  {/* ── Thumbnail area ── */}
                  <div
                    className="relative h-36 w-full overflow-hidden"
                    style={{ background: `linear-gradient(135deg, ${pal.from} 0%, ${pal.to} 100%)` }}
                  >
                    {/* Geometric pharaonic SVG pattern */}
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

                    {/* Large term ordinal number — decorative */}
                    <span
                      className="absolute -left-2 -top-4 select-none font-black leading-none text-white/[0.08] rtl:-right-2 rtl:left-auto"
                      style={{ fontSize: "clamp(5rem, 14vw, 9rem)" }}
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

                    {/* Term number pill — bottom left */}
                    <div className="absolute bottom-4 left-4 flex h-9 w-9 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm rtl:left-auto rtl:right-4">
                      <span className="text-sm font-black text-white">
                        {String(idx + 1).padStart(2, "0")}
                      </span>
                    </div>

                    {/* Hover shine */}
                    <div className="absolute inset-0 bg-gradient-to-tr from-white/0 via-white/5 to-white/10 opacity-0 transition-opacity duration-500 group-hover:opacity-100" />
                  </div>

                  {/* ── Content area ── */}
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
                        <span />
                      )}
                      <ChevronLeft className="h-4 w-4 text-[var(--admin-muted)] transition-all group-hover:-translate-x-0.5 group-hover:text-[var(--admin-primary)]" />
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </motion.div>

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
