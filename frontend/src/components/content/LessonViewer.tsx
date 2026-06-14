"use client";

import { useState, useEffect } from "react";
import { FileText, FlaskConical, Maximize, Minimize } from "lucide-react";
import { useRouter } from "next/navigation";
import { useLessonFocusStore } from "@/stores/lesson-focus-store";
import apiClient from "@/services/api-client";
import toast from 'react-hot-toast';

import { contentService, type LessonDetailDto, type ResourceDto } from "@/services/content-service";

import { LessonCarousel } from "@/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel";
import { LessonCommentsSection } from "@/components/content/LessonCommentsSection";
import { shuffleArray } from "@/lib/utils";
import { AnimatedStepper } from "@/components/ui/animated-stepper";
import { FindTheMistakeInteract } from "@/components/exams/FindTheMistakeInteract";

export function LessonViewer({
  lesson,
  packageId,
}: {
  lesson: LessonDetailDto;
  packageId?: string;
}) {
  const router = useRouter();
  const { isFocusMode, setFocusMode, toggleFocusMode } = useLessonFocusStore();
  
  useEffect(() => {
    setFocusMode(true);
    return () => setFocusMode(false);
  }, [setFocusMode]);

  const [activeVideoIndex, setActiveVideoIndex] = useState(0);
  const [homeworkAnswers, setHomeworkAnswers] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [homeworkSubmitted, setHomeworkSubmitted] = useState(false);
  const [shuffledQuestions, setShuffledQuestions] = useState<Exclude<LessonDetailDto['homework'], undefined>['questions']>([]);
  const [downloadingResourceId, setDownloadingResourceId] = useState<string | null>(null);
  const [resources, setResources] = useState<ResourceDto[]>([]);
  const [loadingResources, setLoadingResources] = useState(true);

  useEffect(() => {
    if (lesson.id) {
      setLoadingResources(true);
      contentService.getLessonResources(lesson.id)
        .then((res) => {
          setResources(res.data?.data ?? []);
        })
        .catch((err) => {
          console.error("Error loading resources:", err);
        })
        .finally(() => {
          setLoadingResources(false);
        });
    }
  }, [lesson]);

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
        toast.error('فشل في تحميل الملف');
      }
    } catch (err) {
      console.error("Error signing download URL:", err);
      // Error is already toasted by apiClient interceptor
    } finally {
      setDownloadingResourceId(null);
    }
  };

  useEffect(() => {
    if (lesson.homework?.questions) {
      setShuffledQuestions(shuffleArray(lesson.homework.questions));
    }
  }, [lesson.homework]);

  const handleHomeworkSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!lesson.homework) return;
    setIsSubmitting(true);
    try {
      const answersList = Object.entries(homeworkAnswers).map(([qid, val]) => ({
        questionId: qid,
        providedAnswer: val
      }));
      await apiClient.post(`/homework/${lesson.homework.id}/submit`, answersList);
      setHomeworkSubmitted(true);
    } catch {
      toast.error('فشل في تسليم الواجب');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (lesson.isLocked) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-12 text-center shadow-sm sm:px-8 sm:py-20">
        <div className="mb-8 flex h-24 w-24 items-center justify-center rounded-full border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] text-[var(--admin-danger)] shadow-inner">
          <svg className="w-12 h-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
        </div>
        <h2 className="mb-5 text-3xl font-black tracking-tight text-[var(--admin-text)] sm:text-4xl">هذه الحصة مغلقة حالياً</h2>
        <p className="max-w-2xl text-base font-medium leading-8 text-[var(--admin-muted)] sm:text-xl sm:leading-relaxed">
          {lesson.lockedReason || "يجب النّجاح في الحصة السابقة واجتياز الامتحانات والواجبات المرتبطة بها لتتمكن من استكمال المنصة."}
        </p>
        <div className="mt-10 flex w-full max-w-2xl flex-col justify-center gap-3 sm:flex-row sm:flex-wrap sm:gap-4">
          <button 
            type="button"
            onClick={() => router.back()} 
            className="min-h-12 w-full rounded-[20px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-6 py-4 font-black text-[var(--admin-text)] opacity-80 shadow-sm transition-all hover:-translate-y-1 hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto sm:px-8"
          >
            العودة للمسار التسلسلي
          </button>
          
          {lesson.blockingExamId && (
            <button 
              type="button"
              onClick={() => router.push(`/student/exams/${lesson.blockingExamId}?packageId=${packageId}`)} 
              className="min-h-12 w-full rounded-[20px] border border-[var(--admin-primary)] bg-[var(--admin-primary)] px-6 py-4 font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary)]/40 transition-all hover:-translate-y-1 hover:brightness-110 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto sm:px-8"
            >
              اذهب للامتحان
            </button>
          )}

          {lesson.blockingHomeworkLessonId && packageId && (
            <button 
              type="button"
              onClick={() => router.push(`/student/packages/${packageId}/lessons/${lesson.blockingHomeworkLessonId}`)} 
              className="min-h-12 w-full rounded-[20px] border border-[var(--admin-primary)] bg-[var(--admin-primary)] px-6 py-4 font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary)]/40 transition-all hover:-translate-y-1 hover:brightness-110 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto sm:px-8"
            >
              اذهب لحل الواجب
            </button>
          )}
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
            />
          ) : (
            <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl p-12 text-center text-[var(--admin-muted)] font-medium">
              لا توجد فيديوهات متاحة لهذا الدرس حاليًا.
            </div>
          )}
        </div>

        <LessonCommentsSection lessonId={lesson.id} />

        <div className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
            <div className="mb-6 flex items-center gap-3">
              <FileText className="h-5 w-5 text-[var(--admin-primary)]" />
              <h3 className="text-xl font-black text-[var(--admin-text)]">المصادر والملفات</h3>
            </div>
            <ul className="space-y-4 text-sm">
              {loadingResources ? (
                <div className="space-y-2 py-4 animate-pulse">
                  <div className="h-12 w-full bg-[var(--admin-card-soft)] rounded-2xl"></div>
                  <div className="h-12 w-full bg-[var(--admin-card-soft)] rounded-2xl"></div>
                </div>
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

          {lesson.examId && (
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
              <div className="flex items-center gap-3">
                <FlaskConical className="h-5 w-5 text-[var(--admin-primary)]" />
                <h3 className="text-xl font-black text-[var(--admin-text)]">اختبار الدرس</h3>
                {lesson.examPassed && (
                  <span className="rounded-full bg-emerald-100 dark:bg-emerald-900/40 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-400">
                    تم الاجتياز
                  </span>
                )}
              </div>
              <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
                {lesson.examPassed
                  ? 'لقد اجتزت هذا الاختبار بنجاح. يمكنك مراجعة إجاباتك ونتائجك.'
                  : 'اختبر استيعابك لهذا الدرس قبل الانتقال إلى المرحلة التالية. الدرجات المسجلة تؤثر على ترتيبك في لوحة الشرف.'
                }
              </p>
              <button
                type="button"
                onClick={() => router.push(`/student/exams/${lesson.examId}?packageId=${packageId}`)}
                className={`mt-6 w-full rounded-2xl px-4 py-4 text-sm font-black transition-all hover:-translate-y-1 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] ${
                  lesson.examPassed
                    ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                    : 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)]'
                }`}
              >
                {lesson.examPassed ? 'راجع الامتحان' : 'ابدأ الاختبار الآن'}
              </button>
            </div>
          )}
        </div>
      </div>

      {lesson.homework && (
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-sm lg:p-10">
          <div className="mb-8 sm:mb-10">
            <div>
              <h2 className="text-2xl font-black tracking-tight text-[var(--admin-text)] sm:text-3xl">{lesson.homework.title}</h2>
              {lesson.homework.instructions && (
                <p className="mt-3 text-sm leading-relaxed text-[var(--admin-muted)] font-medium">{lesson.homework.instructions}</p>
              )}
            </div>
          </div>

          {homeworkSubmitted ? (
            <div className="rounded-[28px] border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] p-10 text-center">
              <svg className="mx-auto mb-6 h-16 w-16 text-[var(--admin-success)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <h3 className="text-3xl font-black text-[var(--admin-success)] tracking-tight">تم تسليم الواجب بنجاح</h3>
              <p className="mt-4 text-lg font-medium text-[var(--admin-muted)]">تم حفظ إجاباتك وإضافة النقاط إلى رصيدك.</p>
              <button 
                type="button"
                onClick={() => {
                   window.scrollTo({ top: 0, behavior: 'smooth' });
                }} 
                className="mt-8 rounded-[20px] bg-[var(--admin-success)] px-10 py-4 font-black text-[var(--admin-primary-contrast)] transition-all hover:-translate-y-1 hover:brightness-110"
              >
                العودة لأعلى
              </button>
            </div>
          ) : (
            <AnimatedStepper
              isSubmitting={isSubmitting}
              onComplete={() => handleHomeworkSubmit()}
              steps={shuffledQuestions.map((q, idx) => {
                const qType = q.questionType || 'Essay';
                const questionLabelId = `lesson-homework-answer-label-${q.id}`;
                return {
                  id: q.id,
                  content: (
                    <div className="bg-[var(--admin-bg)] border border-[var(--admin-border)] rounded-[28px] p-6 sm:p-8 shadow-sm mx-auto max-w-4xl">
                      <h4 id={questionLabelId} className="font-bold text-lg mb-8 text-[var(--admin-text)] leading-relaxed flex items-start gap-4">
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] font-black text-base shadow-inner">
                          {idx + 1}
                        </span> 
                        <span className="pt-2 text-xl font-bold">{q.text}</span>
                      </h4>
                      {qType === 'Essay' && (
                        <textarea 
                          className="w-full rounded-[24px] border border-[var(--admin-border)] p-6 min-h-[180px] bg-[var(--admin-card)] text-[var(--admin-text)] focus:border-[var(--admin-primary)] focus:ring-4 focus:ring-[var(--admin-primary)]/10 transition-all outline-none resize-y text-lg shadow-inner"
                          placeholder="اكتب إجابتك هنا بوضوح..."
                          required
                          value={homeworkAnswers[q.id] || ''}
                          onChange={e => setHomeworkAnswers(prev => ({...prev, [q.id]: e.target.value}))}
                          aria-labelledby={questionLabelId}
                        />
                      )}
                      {qType === 'MCQ' && (
                        <div className="space-y-3">
                          {q.possibleAnswers?.map((opt, optIdx) => {
                            const isSelected = homeworkAnswers[q.id] === opt;
                            return (
                              <label
                                key={optIdx}
                                className={`flex cursor-pointer items-center gap-4 rounded-2xl p-4 transition border ${
                                  isSelected
                                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 shadow-[0_0_0_1px_var(--admin-primary)]'
                                    : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:bg-[var(--admin-card-strong)]'
                                }`}
                              >
                                <input
                                  type="radio"
                                  name={`q-${q.id}`}
                                  value={opt}
                                  checked={isSelected}
                                  onChange={() => setHomeworkAnswers(prev => ({ ...prev, [q.id]: opt }))}
                                  className="sr-only"
                                />
                                <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border transition ${
                                  isSelected
                                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]'
                                    : 'border-[var(--admin-border)]'
                                }`}>
                                  {isSelected && (
                                    <span className="h-1.5 w-1.5 rounded-full bg-white" />
                                  )}
                                </span>
                                <span className="text-sm font-bold text-[var(--admin-text)]">{opt}</span>
                              </label>
                            );
                          })}
                        </div>
                      )}
                      {qType === 'FindTheMistake' && q.baseText && (
                        <FindTheMistakeInteract
                          baseText={q.baseText}
                          selectedText={homeworkAnswers[q.id] || ''}
                          onSelect={(txt) => setHomeworkAnswers(prev => ({ ...prev, [q.id]: txt }))}
                        />
                      )}
                    </div>
                  )
                };
              })}
            />
          )}
        </div>
      )}
    </div>
  );
}
