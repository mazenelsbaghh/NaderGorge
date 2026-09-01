"use client";

import { useState, useEffect } from "react";
import { FileText, FlaskConical, Maximize, Minimize, ClipboardCheck, LockKeyhole, RefreshCw, CalendarClock } from "lucide-react";
import { useRouter, useSearchParams } from "next/navigation";
import { useLessonFocusStore } from "@/stores/lesson-focus-store";
import apiClient from "@/services/api-client";
import toast from 'react-hot-toast';
import { installLessonPageProtectionGuard } from "@/utils/video-page-guard";
import { getHomeworkComingSoonLabel } from "@/lib/homework-coming-soon";

import { contentService, type LessonDetailDto, type ResourceDto } from "@/services/content-service";

import { LessonCarousel } from "@/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel";
import { LessonCommentsSection } from "@/components/content/LessonCommentsSection";


export function LessonViewer({
  lesson,
  packageId,
}: {
  lesson: LessonDetailDto;
  packageId?: string;
}) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { isFocusMode, setFocusMode, toggleFocusMode } = useLessonFocusStore();
  
  useEffect(() => {
    setFocusMode(true);
    return () => setFocusMode(false);
  }, [setFocusMode]);

  const hasViewableLessonVideo = !lesson.isLocked
    && lesson.videos.some((video) => video.hasAccess !== false);

  useEffect(() => {
    if (!hasViewableLessonVideo) return;

    // The extension menu is document-wide, so limiting the guard to the
    // player would leave the same download entry available elsewhere here.
    return installLessonPageProtectionGuard(document);
  }, [hasViewableLessonVideo]);

  const [activeVideoIndex, setActiveVideoIndex] = useState(0);

  useEffect(() => {
    if (!lesson.videos.length) return;

    const requestedVideoId = searchParams.get("videoId");
    const requestedIndex = requestedVideoId
      ? lesson.videos.findIndex((video) => video.id === requestedVideoId)
      : -1;
    const firstPlayableIndex = lesson.videos.findIndex((video) => video.hasAccess !== false);
    const nextIndex = requestedIndex >= 0 ? requestedIndex : firstPlayableIndex >= 0 ? firstPlayableIndex : 0;

    setActiveVideoIndex(nextIndex);
  }, [lesson.videos, searchParams]);

  const [downloadingResourceId, setDownloadingResourceId] = useState<string | null>(null);
  const [resources, setResources] = useState<ResourceDto[]>([]);
  const [loadingResources, setLoadingResources] = useState(true);
  const [resourceError, setResourceError] = useState(false);
  const [resourceRetryKey, setResourceRetryKey] = useState(0);

  useEffect(() => {
    if (lesson.id) {
      if (lesson.isVideoOnlyAccess) {
        setResources([]);
        setLoadingResources(false);
        return;
      }

      setLoadingResources(true);
      setResourceError(false);
      contentService.getLessonResources(lesson.id)
        .then((res) => {
          setResources(res.data?.data ?? []);
        })
        .catch(() => {
          setResourceError(true);
        })
        .finally(() => {
          setLoadingResources(false);
        });
    }
  }, [lesson, resourceRetryKey]);

  const handleResourceClick = async (e: React.MouseEvent, resourceId: string) => {
    e.preventDefault();
    if (downloadingResourceId) return;
    setDownloadingResourceId(resourceId);
    try {
      const response = await apiClient.post<{ success: boolean; downloadUrl: string }>(
        `/content/resources/${resourceId}/sign-download`
      );
      if (response.data?.downloadUrl) {
        const backendUrl = process.env.NEXT_PUBLIC_BACKEND_URL || 
          (process.env.NEXT_PUBLIC_API_URL ? process.env.NEXT_PUBLIC_API_URL.replace(/\/api$/, '') : 'http://localhost:5245');
        const fullUrl = `${backendUrl}${response.data.downloadUrl}`;
        window.open(fullUrl, '_blank');
      } else {
        toast.error('تعذر تجهيز الملف. تحقق من اتصالك ثم حاول مرة أخرى.');
      }
    } catch (err) {
      console.error("Error signing download URL:", err);
      // Error is already toasted by apiClient interceptor
    } finally {
      setDownloadingResourceId(null);
    }
  };

  const homeworkComingSoonLabel = getHomeworkComingSoonLabel(
    lesson.homeworkComingSoonOn
  );

  if (lesson.isLocked) {
    return (
      <div className="mx-auto max-w-3xl rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-8">
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <LockKeyhole className="h-6 w-6" aria-hidden="true" />
          </div>
          <div>
            <p className="text-sm font-black text-[var(--admin-primary)]">هذه الحصة هي خطوتك التالية</p>
            <h2 className="mt-1 text-xl font-black text-[var(--admin-text)] sm:text-2xl">أكمل المتطلب الظاهر أدناه لفتحها</h2>
          </div>
        </div>
        <p className="mt-5 rounded-xl bg-[var(--admin-card-soft)] p-4 text-base font-medium leading-8 text-[var(--admin-muted)]">
          {lesson.lockedReason || "يجب النّجاح في الحصة السابقة واجتياز الامتحانات والواجبات المرتبطة بها لتتمكن من استكمال المنصة."}
        </p>
        <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:flex-wrap">
          {lesson.blockingExamId && (
            <button 
              type="button"
              onClick={() => router.push(`/student/exams/${lesson.blockingExamId}?packageId=${packageId}`)} 
              className="admin-btn-primary min-h-12 w-full px-6 sm:w-auto"
            >
              ابدأ الامتحان المطلوب
            </button>
          )}

          {!lesson.blockingExamId && lesson.blockingHomeworkLessonId && packageId && (
            <button 
              type="button"
              onClick={() => router.push(`/student/packages/${packageId}/lessons/${lesson.blockingHomeworkLessonId}`)} 
              className="admin-btn-primary min-h-12 w-full px-6 sm:w-auto"
            >
              حل الواجب المطلوب
            </button>
          )}
          <button type="button" onClick={() => router.back()} className="admin-btn-ghost min-h-12 w-full px-6 sm:w-auto">
            العودة لمسار الدروس
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8 sm:space-y-12 pb-10">
      <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/95 p-5 shadow-sm sm:p-8">
        <div className="flex flex-col items-start gap-5 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0 flex-1">
            <span className="inline-flex rounded-full bg-[var(--admin-primary-15)] px-4 py-1.5 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
              محتوى الدرس
            </span>
            <h1 className="text-3xl font-black text-[var(--admin-text)] sm:text-4xl tracking-tight leading-tight">
              {lesson.title}
            </h1>
            {lesson.summary && (
              <p className="mt-4 text-sm leading-relaxed text-[var(--admin-muted)] sm:text-base max-w-3xl font-medium">
                {lesson.summary}
              </p>
            )}
          </div>

          <div className="flex w-full justify-end sm:w-auto">
            <button
               type="button"
               onClick={toggleFocusMode}
               className="inline-flex min-h-12 items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 font-black text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-primary-15)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:rounded-[20px]"
               title={isFocusMode ? "إضعاف التركيز (إظهار القوائم)" : "وضع التركيز (إخفاء القوائم)"}
               aria-label={isFocusMode ? "إظهار القوائم" : "إخفاء القوائم"}
             >
               {isFocusMode ? <Minimize className="h-5 w-5" /> : <Maximize className="h-5 w-5" />}
               <span>{isFocusMode ? "إظهار القوائم" : "وضع التركيز"}</span>
             </button>
          </div>
        </div>
      </div>

      <div className="flex flex-col gap-8">
        <div className="w-full">
          {lesson.videos.length > 0 ? (
            <LessonCarousel 
              videos={lesson.videos} 
              activeStep={activeVideoIndex} 
              onStepChange={setActiveVideoIndex}
              homeworkId={lesson.homeworkId}
              homeworkComingSoonOn={lesson.homeworkComingSoonOn}
              homeworkPassed={lesson.homeworkPassed}
              examId={lesson.examId}
              examPassed={lesson.examPassed}
              isExamLocked={lesson.isExamLocked}
              examLockedReason={lesson.examLockedReason}
              lessonPrice={lesson.price}
              lessonId={lesson.id}
            />
          ) : (
            <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center font-medium text-[var(--admin-muted)] sm:p-12">
              لا توجد فيديوهات متاحة لهذا الدرس حاليًا.
            </div>
          )}
        </div>

        <div className={`grid gap-6 ${lesson.examId ? 'md:grid-cols-2' : 'md:grid-cols-1'}`}>
          {lesson.examId && (
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
              <div className="flex items-center gap-3">
                <FlaskConical className="h-5 w-5 text-[var(--admin-primary)]" />
                <h3 className="text-xl font-black text-[var(--admin-text)]">اختبار الدرس</h3>
                {lesson.examPassed ? (
                  <span className="rounded-full bg-emerald-100 dark:bg-emerald-900/40 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-400">
                    تم الاجتياز ✓
                  </span>
                ) : lesson.isExamLocked ? (
                  <span className="rounded-full bg-gray-100 dark:bg-gray-800 px-3 py-1 text-xs font-black text-gray-500 dark:text-gray-400">
                    مغلق 🔒
                  </span>
                ) : lesson.examStatus === 'Failed' ? (
                  <span className="rounded-full bg-red-100 dark:bg-red-900/40 px-3 py-1 text-xs font-black text-red-700 dark:text-red-400">
                    لم يتم الاجتياز ✗
                  </span>
                ) : lesson.examStatus === 'InProgress' ? (
                  <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 px-3 py-1 text-xs font-black text-amber-700 dark:text-amber-400">
                    قيد الحل ⏳
                  </span>
                ) : (
                  <span className="rounded-full bg-blue-100 dark:bg-blue-900/40 px-3 py-1 text-xs font-black text-blue-700 dark:text-blue-400">
                    لم يبدأ 📝
                  </span>
                )}
              </div>
              <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
                {lesson.examPassed
                  ? 'لقد اجتزت هذا الاختبار بنجاح. يمكنك مراجعة إجاباتك ونتائجك.'
                  : lesson.isExamLocked
                  ? lesson.examLockedReason || 'هذا الاختبار مغلق حالياً.'
                  : lesson.examStatus === 'Failed'
                  ? 'لقد حصلت على درجة أقل من درجة النجاح في محاولتك السابقة. يمكنك إعادة المحاولة لتحسين نتيجتك واجتياز الدرس.'
                  : lesson.examStatus === 'InProgress'
                  ? 'لديك محاولة نشطة وغير مكتملة في هذا الاختبار. يمكنك استئناف حل الأسئلة الآن.'
                  : 'اختبر استيعابك لهذا الدرس قبل الانتقال إلى المرحلة التالية. الدرجات المسجلة تؤثر على ترتيبك في لوحة الشرف.'
                }
              </p>
              <button
                type="button"
                disabled={lesson.isExamLocked && !lesson.examPassed}
                onClick={() => router.push(`/student/exams/${lesson.examId}?packageId=${packageId}&lessonId=${lesson.id}`)}
                className={`mt-6 w-full rounded-2xl px-4 py-4 text-sm font-black transition-[color,background-color,border-color,opacity,transform,box-shadow] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] ${
                  lesson.examPassed
                    ? 'bg-emerald-600 text-white hover:bg-emerald-700 hover:-translate-y-1'
                    : lesson.isExamLocked
                    ? 'bg-gray-400 text-white opacity-60 cursor-not-allowed'
                    : 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:-translate-y-1'
                }`}
              >
                {lesson.examPassed
                  ? 'راجع الامتحان'
                  : lesson.examStatus === 'Failed'
                  ? 'إعادة الاختبار الآن'
                  : lesson.examStatus === 'InProgress'
                  ? 'استئناف الاختبار'
                  : 'ابدأ الاختبار الآن'}
              </button>
            </div>
          )}

          {/* Homework Card */}
          <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
            <div className="flex items-center gap-3">
              <ClipboardCheck className="h-5 w-5 text-[var(--admin-primary)]" />
              <h3 className="text-xl font-black text-[var(--admin-text)]">واجب الدرس</h3>
              {lesson.homeworkId && (
                <>
                  {lesson.homeworkPassed ? (
                    <span className="rounded-full bg-emerald-100 dark:bg-emerald-900/40 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-400">
                      تم الاجتياز ✓
                    </span>
                  ) : lesson.homeworkStatus === 'Failed' ? (
                    <span className="rounded-full bg-red-100 dark:bg-red-900/40 px-3 py-1 text-xs font-black text-red-700 dark:text-red-400">
                      لم يتم الاجتياز ✗
                    </span>
                  ) : lesson.homeworkStatus === 'PendingReview' ? (
                    <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 px-3 py-1 text-xs font-black text-amber-700 dark:text-amber-400">
                      قيد التصحيح ⏳
                    </span>
                  ) : lesson.homeworkStatus === 'InProgress' ? (
                    <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 px-3 py-1 text-xs font-black text-amber-700 dark:text-amber-400">
                      قيد الحل ⏳
                    </span>
                  ) : (
                    <span className="rounded-full bg-blue-100 dark:bg-blue-900/40 px-3 py-1 text-xs font-black text-blue-700 dark:text-blue-400">
                      لم يبدأ 📝
                    </span>
                  )}
                </>
              )}
              {!lesson.homeworkId && lesson.homeworkComingSoonOn && (
                <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-muted)]">
                  قريبًا
                </span>
              )}
            </div>
            {lesson.homeworkId ? (
              <>
                <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
                  {lesson.homeworkPassed
                    ? 'لقد قمت بحل هذا الواجب بنجاح واجتيازه. يمكنك مراجعة إجاباتك ونتائجك.'
                    : lesson.homeworkStatus === 'Failed'
                    ? 'لقد حصلت على درجة أقل من درجة النجاح في محاولتك السابقة. يجب إعادة حل الواجب للاجتياز.'
                    : lesson.homeworkStatus === 'PendingReview'
                    ? 'لقد قمت بتسليم الواجب وهو بانتظار تصحيح الأسئلة المقالية من المساعد أو المعلم.'
                    : lesson.homeworkStatus === 'InProgress'
                    ? 'لديك محاولة نشطة وغير مكتملة في هذا الواجب. يمكنك استئناف حل الأسئلة الآن.'
                    : 'حل واجب الدرس للتأكد من فهمك للموضوع واستكمال متطلبات الانتقال للدرس التالي.'
                  }
                </p>
                <button
                  type="button"
                  onClick={() => router.push(`/student/homework/${lesson.homeworkId}?packageId=${packageId}&lessonId=${lesson.id}`)}
                  className={`mt-6 w-full rounded-2xl px-4 py-4 text-sm font-black transition-[color,background-color,border-color,opacity,transform,box-shadow] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] ${
                    lesson.homeworkPassed
                      ? 'bg-emerald-600 text-white hover:bg-emerald-700 hover:-translate-y-1'
                      : 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:-translate-y-1'
                  }`}
                >
                  {lesson.homeworkPassed
                    ? 'عرض نتيجة الواجب'
                    : lesson.homeworkStatus === 'Failed'
                    ? 'إعادة حل الواجب الآن'
                    : lesson.homeworkStatus === 'PendingReview'
                    ? 'عرض نتيجة الواجب'
                    : lesson.homeworkStatus === 'InProgress'
                    ? 'استئناف حل الواجب'
                    : 'ابدأ حل الواجب الآن'}
                </button>
              </>
            ) : homeworkComingSoonLabel ? (
              <>
                <p
                  id="homework-coming-soon-description"
                  className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]"
                >
                  المدرس يجهز الواجب الآن. ستقدر تبدأ الحل فور نشره.
                </p>
                <button
                  type="button"
                  disabled
                  aria-describedby="homework-coming-soon-description"
                  className="mt-6 flex min-h-14 w-full cursor-not-allowed items-center justify-center gap-3 rounded-2xl bg-[var(--admin-card-soft)] px-4 py-3 text-sm font-black text-[var(--admin-muted)] opacity-80"
                >
                  <LockKeyhole className="h-5 w-5 shrink-0" aria-hidden="true" />
                  <span className="flex flex-col items-start leading-5">
                    <span>الذهاب للواجب</span>
                    <span className="inline-flex items-center gap-1 text-xs font-bold">
                      <CalendarClock className="h-3.5 w-3.5" aria-hidden="true" />
                      {homeworkComingSoonLabel}
                    </span>
                  </span>
                </button>
              </>
            ) : (
              <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
                لا يوجد واجب متاح لهذا الدرس.
              </p>
            )}
          </div>
        </div>

        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
          <div className="mb-6 flex items-center gap-3">
            <FileText className="h-5 w-5 text-[var(--admin-primary)]" />
            <h3 className="text-xl font-black text-[var(--admin-text)]">المصادر والملفات</h3>
          </div>
          <ul className="space-y-4 text-sm">
            {loadingResources ? (
              <li className="space-y-2 py-4 animate-pulse" aria-label="جارٍ تحميل الملفات">
                <div className="h-12 w-full bg-[var(--admin-card-soft)] rounded-2xl"></div>
                <div className="h-12 w-full bg-[var(--admin-card-soft)] rounded-2xl"></div>
              </li>
            ) : resourceError ? (
              <li role="alert" className="rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] p-4 text-center">
                <p className="font-bold text-[var(--admin-text)]">تعذر تحميل الملفات المرفقة. محتوى الدرس لم يتأثر.</p>
                <button type="button" onClick={() => setResourceRetryKey((value) => value + 1)} className="admin-btn-ghost mt-3 min-h-11 px-4">
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  إعادة المحاولة
                </button>
              </li>
            ) : (
              <>
                {resources.map((res) => (
                  <li key={res.id}>
                    <button
                      type="button"
                      disabled={downloadingResourceId === res.id}
                      onClick={(e) => handleResourceClick(e, res.id)}
                      className="flex w-full text-right items-start gap-4 rounded-[20px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4 font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:items-center sm:px-5 disabled:opacity-50"
                    >
                      <svg className="h-5 w-5 opacity-80 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                      </svg>
                      <span className="flex-1">{res.title}</span>
                      {downloadingResourceId === res.id && (
                        <span className="text-xs font-normal text-[var(--admin-muted)] animate-pulse">جاري التحضير...</span>
                      )}
                    </button>
                  </li>
                ))}
                {resources.length === 0 && (
                  <li className="py-4 text-center font-medium text-[var(--admin-muted)]">لا توجد ملفات مرفقة.</li>
                )}
              </>
            )}
          </ul>
        </div>

        <LessonCommentsSection lessonId={lesson.id} />
      </div>
    </div>
  );
}
