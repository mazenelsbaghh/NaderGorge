'use client';

import { useEffect, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import {
  ArrowLeft,
  BookOpen,
  Check,
  ChevronDown,
  CirclePlay,
  LockKeyhole,
  PackageOpen,
  X,
} from 'lucide-react';

import {
  studentService,
  type PublicPackageDetailDto,
} from '@/services/student-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type PublicPackagePageClientProps = { packageId: string };

function priceLabel(price: number) {
  return price > 0 ? `${price.toLocaleString('ar-EG')} ج.م` : 'مجانًا';
}

export default function PublicPackagePageClient({
  packageId,
}: PublicPackagePageClientProps) {
  const [packageDetail, setPackageDetail] =
    useState<PublicPackageDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isPurchaseDialogOpen, setIsPurchaseDialogOpen] = useState(false);

  useEffect(() => {
    studentService
      .getPublicPackage(packageId)
      .then(setPackageDetail)
      .catch(() => setPackageDetail(null))
      .finally(() => setIsLoading(false));
  }, [packageId]);

  if (isLoading)
    return (
      <main className="min-h-screen bg-[var(--public-canvas)] px-5 pb-16 pt-28">
        <div className="mx-auto h-[34rem] max-w-6xl animate-pulse rounded-3xl bg-[var(--public-surface-muted)]" />
      </main>
    );
  if (!packageDetail)
    return (
      <main className="min-h-screen bg-[var(--public-canvas)] px-5 pb-16 pt-28 text-center text-[var(--public-text)]">
        <PackageOpen className="mx-auto h-10 w-10 text-[var(--public-accent)]" />
        <h1 className="mt-4 text-2xl font-black">الباقة غير متاحة</h1>
        <Link
          href="/teachers"
          className="mt-6 inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--public-primary)] px-5 font-black text-[var(--public-surface)]"
        >
          العودة للمدرسين <ArrowLeft className="h-4 w-4" />
        </Link>
      </main>
    );

  const totalLessons = packageDetail.terms
    .flatMap((term) => term.sections)
    .reduce((total, section) => total + section.lessons.length, 0);
  return (
    <main
      className="min-h-screen bg-[var(--public-canvas)] px-5 pb-16 pt-28 text-[var(--public-text)] sm:px-8 sm:pt-32"
      dir="rtl"
    >
      <div className="mx-auto max-w-6xl">
        <Link
          href={`/teachers/${packageDetail.teacherId}`}
          className="inline-flex min-h-11 items-center gap-2 text-sm font-black text-[var(--public-accent)] transition hover:text-[var(--public-primary)]"
        >
          <ArrowRightIcon /> العودة لصفحة المدرس
        </Link>
        <section className="mt-5 overflow-hidden rounded-3xl border border-[var(--public-border)] bg-[var(--public-surface)] shadow-[0_8px_14px_rgba(10,29,61,.08)]">
          <div className="grid lg:grid-cols-[minmax(0,1.25fr)_minmax(20rem,.75fr)]">
            <div className="relative min-h-72 bg-[var(--public-primary)]">
              {packageDetail.imageUrl ? (
                <Image
                  src={resolveMediaUrl(packageDetail.imageUrl)}
                  alt={packageDetail.name}
                  fill
                  unoptimized
                  className="object-cover"
                />
              ) : (
                <div className="grid h-full place-items-center text-7xl font-black text-white/25">
                  {packageDetail.name.charAt(0)}
                </div>
              )}
              <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(10,29,61,.66),transparent)]" />
            </div>
            <div className="flex flex-col p-7 sm:p-9">
              <span className="w-fit rounded-full bg-[var(--landing-teal-soft)] px-3 py-1 text-xs font-black text-[var(--public-accent)]">
                {packageDetail.subjectName}
              </span>
              <h1 className="mt-4 text-3xl font-black leading-tight text-[var(--public-text)] sm:text-4xl">
                {packageDetail.name}
              </h1>
              <p className="mt-4 whitespace-pre-line text-sm font-bold leading-7 text-[var(--public-text-muted)]">
                {packageDetail.description || 'تفاصيل الباقة ستظهر هنا.'}
              </p>
              <div className="mt-6 flex flex-wrap gap-3 text-sm font-black">
                <span className="rounded-xl bg-[var(--public-surface-muted)] px-3 py-2">
                  {packageDetail.terms.length} أترام
                </span>
                <span className="rounded-xl bg-[var(--public-surface-muted)] px-3 py-2">
                  {totalLessons} حصة
                </span>
              </div>
              <div className="mt-auto pt-7">
                <p className="text-2xl font-black text-[var(--public-text)]">
                  {priceLabel(packageDetail.price)}
                </p>
                <button
                  type="button"
                  onClick={() => setIsPurchaseDialogOpen(true)}
                  className="mt-3 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[var(--public-accent)] px-5 text-sm font-black text-white transition hover:bg-[var(--public-accent-hover)]"
                >
                  <LockKeyhole className="h-4 w-4" /> اشترك في الباقة
                </button>
              </div>
            </div>
          </div>
        </section>

        <section className="mt-8">
          <div className="flex items-end justify-between gap-4">
            <div>
              <h2 className="text-2xl font-black">محتوى الباقة</h2>
              <p className="mt-2 text-sm font-bold text-[var(--public-text-muted)]">
                استعرض الخطة كاملة قبل الاشتراك.
              </p>
            </div>
            <BookOpen className="h-6 w-6 text-[var(--public-accent)]" />
          </div>
          <div className="mt-5 space-y-4">
            {packageDetail.terms.map((term, termIndex) => (
              <details
                key={term.id}
                open={termIndex === 0}
                className="group rounded-2xl border border-[var(--public-border)] bg-[var(--public-surface)]"
              >
                <summary className="flex cursor-pointer list-none items-center justify-between gap-4 p-5">
                  <div>
                    <p className="text-xs font-black text-[var(--public-accent)]">
                      الترم {termIndex + 1}
                    </p>
                    <h3 className="mt-1 text-lg font-black">{term.title}</h3>
                  </div>
                  <ChevronDown className="h-5 w-5 text-[var(--public-text)] transition group-open:rotate-180" />
                </summary>
                <div className="border-t border-[var(--public-border)] px-5 pb-5 pt-4">
                  {term.sections.length ? (
                    <div className="space-y-3">
                      {term.sections.map((section) => (
                        <div
                          key={section.id}
                          className="rounded-xl bg-[var(--public-surface-muted)] p-4"
                        >
                          <h4 className="font-black">{section.title}</h4>
                          <div className="mt-3 grid gap-2 sm:grid-cols-2">
                            {section.lessons.map((lesson) => (
                              <button
                                key={lesson.id}
                                type="button"
                                onClick={() => setIsPurchaseDialogOpen(true)}
                                className="flex min-h-11 items-center gap-2 rounded-lg bg-[var(--public-surface)] px-3 text-right text-sm font-bold text-[var(--public-text-muted)] transition hover:text-[var(--public-accent)]"
                              >
                                <CirclePlay className="h-4 w-4 shrink-0 text-[var(--public-accent)]" />
                                {lesson.title}
                              </button>
                            ))}
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm font-bold text-[var(--public-text-muted)]">
                      سيُضاف محتوى هذا الترم قريبًا.
                    </p>
                  )}
                </div>
              </details>
            ))}
          </div>
        </section>
      </div>

      {isPurchaseDialogOpen && (
        <div
          className="fixed inset-0 z-[100] grid place-items-center bg-[color-mix(in_srgb,var(--public-primary)_72%,transparent)] p-4"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget)
              setIsPurchaseDialogOpen(false);
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="purchase-required-title"
            className="w-full max-w-md rounded-3xl bg-[var(--public-surface)] p-6 shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <span className="grid h-11 w-11 place-items-center rounded-xl bg-[var(--landing-gold-soft)] text-[var(--public-achievement)]">
                  <LockKeyhole className="h-5 w-5" />
                </span>
                <h2
                  id="purchase-required-title"
                  className="mt-4 text-2xl font-black"
                >
                  لازم تشترك أولًا
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setIsPurchaseDialogOpen(false)}
                aria-label="إغلاق"
                className="grid h-10 w-10 place-items-center rounded-full text-[var(--public-text)] hover:bg-[var(--public-surface-muted)]"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <p className="mt-4 text-sm font-bold leading-7 text-[var(--public-text-muted)]">
              أنشئ حسابك ثم اشترِ الباقة أو فعّلها بكود، وبعدها تقدر تفتح كل
              الحصص وتبدأ الدراسة.
            </p>
            <div className="mt-6 grid gap-3 sm:grid-cols-2">
              <Link
                href="/register"
                className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[var(--public-primary)] px-4 text-sm font-black text-[var(--public-surface)]"
              >
                <Check className="h-4 w-4" /> إنشاء حساب
              </Link>
              <button
                type="button"
                onClick={() => setIsPurchaseDialogOpen(false)}
                className="min-h-12 rounded-xl border border-[var(--public-border)] px-4 text-sm font-black text-[var(--public-text)]"
              >
                متابعة التصفح
              </button>
            </div>
          </section>
        </div>
      )}
    </main>
  );
}

function ArrowRightIcon() {
  return <ArrowLeft className="h-4 w-4" />;
}
