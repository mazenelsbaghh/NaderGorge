"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ArrowRight } from "lucide-react";

import { LessonViewer } from "@/components/content/LessonViewer";
import { contentService, type LessonDetailDto } from "@/services/content-service";

export default function LessonDetailPageClient() {
  const params = useParams();
  const router = useRouter();
  const packageId = params.packageId as string;
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
          setError("هذا الدرس غير متاح الآن أو ما زال مغلقًا.");
        } else {
          setError("تعذر تحميل الدرس أو لم يتم العثور عليه.");
        }
      })
      .finally(() => setLoading(false));
  }, [lessonId]);

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
      <div className="mx-auto max-w-2xl rounded-[24px] border border-red-200 bg-red-50 p-6 text-center sm:p-8">
        <h2 className="mb-4 text-xl font-bold text-red-600">الدرس غير متاح</h2>
        <p className="mb-6 text-sm leading-7 text-red-900 sm:text-base">
          {error}
          <br />
          <br />
          <span className="font-semibold">الإجراء المطلوب:</span> أكمل المتطلبات السابقة
          أو تواصل مع المدرس إذا كنت تتوقع أن الدرس يجب أن يكون مفتوحًا.
        </p>
        <button
          onClick={() => router.push(`/student/packages/${packageId}`)}
          className="rounded-xl bg-red-600 px-6 py-2 font-semibold text-white transition hover:bg-red-700"
        >
          العودة للباقة
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl pb-12">
      <button
        onClick={() => router.push(`/student/packages/${packageId}`)}
        className="mb-6 inline-flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-primary)] sm:mb-8"
      >
        <ArrowRight className="h-4 w-4" />
        <span>العودة إلى محتوى الباقة</span>
      </button>

      <LessonViewer lesson={lesson} packageId={packageId} />
    </div>
  );
}
