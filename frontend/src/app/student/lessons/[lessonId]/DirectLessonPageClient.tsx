"use client";

import { useEffect, useState, useCallback } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { ArrowRight } from "lucide-react";

import { LessonViewer } from "@/components/content/LessonViewer";
import { contentService, type LessonDetailDto } from "@/services/content-service";
import { PurchaseContentModal } from "@/components/balance/PurchaseContentModal";
import { CodeType } from "@/services/balance-service";
import { Lock, ShoppingCart, Sparkles } from "lucide-react";

export default function DirectLessonPageClient() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const lessonId = params.lessonId as string;

  const [lesson, setLesson] = useState<LessonDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [isPurchaseModalOpen, setIsPurchaseModalOpen] = useState(false);

  const fetchLessonDetail = useCallback(() => {
    if (!lessonId) return;
    contentService
      .getLessonDetail(lessonId)
      .then((res) => {
        if (res.data.data) {
          setLesson(res.data.data);
        } else {
          setError("تعذر تحميل الحصة أو لم يتم العثور عليها.");
        }
      })
      .catch((err) => {
        if (err.response?.status === 403) {
          setError("هذه الحصة غير متاحة الآن أو ما زالت مغلقة.");
        } else {
          setError("تعذر تحميل الحصة أو لم يتم العثور عليها.");
        }
      })
      .finally(() => setLoading(false));
  }, [lessonId]);

  useEffect(() => {
    fetchLessonDetail();
  }, [fetchLessonDetail]);

  const resolvedPackageId = searchParams.get("packageId") || lesson?.packageId;

  const backUrl = resolvedPackageId
    ? `/student/packages/${resolvedPackageId}`
    : "/student";

  const backLabel = "العودة إلى الباقة";

  if (loading) {
    return (
      <div className="mx-auto max-w-6xl space-y-6 animate-pulse">
        <div className="h-28 w-full rounded-[24px] bg-[var(--admin-card-strong)] sm:h-32"></div>
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            <div className="aspect-video w-full rounded-[24px] bg-[var(--admin-card-strong)]"></div>
          </div>
          <div className="h-64 w-full rounded-[24px] bg-[var(--admin-card-strong)]"></div>
        </div>
      </div>
    );
  }

  if (error || !lesson) {
    return (
      <div className="mx-auto max-w-2xl rounded-[24px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-6 text-center sm:p-8">
        <h2 className="mb-4 text-xl font-bold text-[var(--admin-danger)]">الحصة غير متاحة</h2>
        <p className="mb-6 text-sm leading-7 text-[var(--admin-text)] sm:text-base">
          {error}
        </p>
        <button
          type="button"
          onClick={() => router.push(backUrl)}
          className="inline-flex min-h-12 w-full items-center justify-center rounded-full bg-[var(--admin-primary)] px-6 py-3 font-semibold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-danger-10)] sm:w-auto"
        >
          {backLabel}
        </button>
      </div>
    );
  }

  if (lesson && lesson.hasAccess === false) {
    return (
      <div className="mx-auto max-w-2xl pb-12 text-right" dir="rtl">
        <button
          type="button"
          onClick={() => router.push(backUrl)}
          className="mb-6 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] sm:mb-8 sm:w-auto sm:justify-start sm:rounded-full sm:border-transparent sm:bg-transparent sm:px-3"
        >
          <ArrowRight className="h-4 w-4" />
          <span>{backLabel}</span>
        </button>

        <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-xl space-y-6 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <Lock className="h-8 w-8" />
          </div>

          <div className="space-y-2">
            <h1 className="text-2xl font-black text-[var(--admin-text)]">{lesson.title}</h1>
            {lesson.summary && (
              <p className="text-sm text-[var(--admin-muted)] max-w-md mx-auto">{lesson.summary}</p>
            )}
          </div>

          <div className="rounded-2xl bg-[var(--admin-card-soft)] p-6 max-w-sm mx-auto border border-[var(--admin-border)]">
            {lesson.price != null && lesson.price > 0 ? (
              <>
                <span className="text-xs font-bold text-[var(--admin-muted)] block mb-1">سعر الحصة منفردة</span>
                <span className="text-3xl font-black text-[var(--admin-primary)]">{lesson.price} ج.م</span>
                
                <button
                  type="button"
                  onClick={() => setIsPurchaseModalOpen(true)}
                  className="w-full mt-4 inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                >
                  <ShoppingCart className="h-4 w-4" />
                  شراء وتفعيل الحصة الآن
                </button>
              </>
            ) : (
              <>
                <p className="text-sm font-bold text-[var(--admin-muted)] leading-relaxed">
                  هذه الحصة غير متاحة للشراء المنفرد. يرجى تفعيل أو شراء الباقة بالكامل لتتمكن من الوصول إليها.
                </p>
                <button
                  type="button"
                  onClick={() => router.push(resolvedPackageId ? `/student/packages/${resolvedPackageId}` : "/student")}
                  className="w-full mt-4 inline-flex min-h-[50px] items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow transition-all hover:brightness-110 active:scale-[0.98]"
                >
                  <Sparkles className="h-4 w-4" />
                  الانتقال لصفحة الباقة
                </button>
              </>
            )}
          </div>
        </div>

        <PurchaseContentModal
          isOpen={isPurchaseModalOpen}
          onClose={() => setIsPurchaseModalOpen(false)}
          onPurchaseSuccess={() => {
            fetchLessonDetail();
          }}
          contentType={"Lesson" as CodeType}
          contentId={lesson.id}
          contentName={lesson.title}
          price={lesson.price || 0}
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl pb-12">
      <button
        type="button"
        onClick={() => router.push(backUrl)}
        className="mb-6 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] sm:mb-8 sm:w-auto sm:justify-start sm:rounded-full sm:border-transparent sm:bg-transparent sm:px-3"
      >
        <ArrowRight className="h-4 w-4" />
        <span>{backLabel}</span>
      </button>

      <LessonViewer lesson={lesson} packageId={resolvedPackageId} />
    </div>
  );
}
