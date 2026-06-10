"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { ArrowRight } from "lucide-react";

import { LessonViewer } from "@/components/content/LessonViewer";
import { contentService, type LessonDetailDto } from "@/services/content-service";

export default function DirectLessonPageClient() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const lessonId = params.lessonId as string;

  const [lesson, setLesson] = useState<LessonDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!lessonId) return;

    contentService
      .getLessonDetail(lessonId)
      .then((res) => setLesson(res.data.data))
      .catch((err) => {
        if (err.response?.status === 403) {
          setError("هذه الحصة غير متاحة الآن أو ما زالت مغلقة.");
        } else {
          setError("تعذر تحميل الحصة أو لم يتم العثور عليها.");
        }
      })
      .finally(() => setLoading(false));
  }, [lessonId]);

  const resolvedPackageId = searchParams.get("packageId") || lesson?.packageId;

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
          onClick={() => router.push(resolvedPackageId ? `/student/packages/${resolvedPackageId}` : "/student")}
          className="inline-flex min-h-12 w-full items-center justify-center rounded-full bg-[var(--admin-primary)] px-6 py-3 font-semibold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-danger-10)] sm:w-auto"
        >
          العودة
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl pb-12">
      <button
        type="button"
        onClick={() => router.push(resolvedPackageId ? `/student/packages/${resolvedPackageId}` : "/student")}
        className="mb-6 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] sm:mb-8 sm:w-auto sm:justify-start sm:rounded-full sm:border-transparent sm:bg-transparent sm:px-3"
      >
        <ArrowRight className="h-4 w-4" />
        <span>العودة إلى الباقة</span>
      </button>

      <LessonViewer lesson={lesson} packageId={resolvedPackageId} />
    </div>
  );
}
