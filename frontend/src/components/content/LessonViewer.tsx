"use client";

import { useState } from "react";
import { BookOpenCheck, FileText, FlaskConical, PenTool } from "lucide-react";
import { useRouter } from "next/navigation";
import apiClient from "@/services/api-client";
import toast from 'react-hot-toast';

import type { LessonDetailDto } from "@/services/content-service";

import SecureVideoPlayer from "../video/SecureVideoPlayer";

export function LessonViewer({
  lesson,
  packageId,
}: {
  lesson: LessonDetailDto;
  packageId?: string;
}) {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<'content' | 'homework'>('content');
  const [homeworkAnswers, setHomeworkAnswers] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [homeworkSubmitted, setHomeworkSubmitted] = useState(false);

  const handleHomeworkSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lesson.homework) return;
    setIsSubmitting(true);
    try {
      const answersList = Object.entries(homeworkAnswers).map(([qid, val]) => ({
        questionId: qid,
        providedAnswer: val
      }));
      await apiClient.post(`/homework/${lesson.homework.id}/submit`, answersList);
      setHomeworkSubmitted(true);
      // Optional: gamification update could be triggered globally if using context, but E2E might just click and look.
    } catch {
      toast.error('فشل في تسليم الواجب');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 sm:space-y-8">
      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl p-4 sm:rounded-[28px] sm:p-6">
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <BookOpenCheck className="h-6 w-6" />
          </div>
          <div className="min-w-0 flex-1">
            <h1 className="text-2xl font-black text-[var(--admin-text)] sm:text-3xl">
              {lesson.title}
            </h1>
            <p className="mt-3 text-sm leading-7 text-[var(--admin-muted)] sm:text-base">
              {lesson.summary}
            </p>
          </div>
        </div>
      </div>

      {lesson.homework && (
        <div className="flex gap-2 border-b border-[var(--admin-border)] pb-2 mb-6">
          <button
            onClick={() => setActiveTab('content')}
            className={`px-4 py-2 font-bold rounded-xl transition ${activeTab === 'content' ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)]'}`}
          >
            المحتوى التعليمي
          </button>
          <button
            onClick={() => setActiveTab('homework')}
            className={`px-4 py-2 font-bold rounded-xl transition ${activeTab === 'homework' ? 'bg-[var(--admin-primary)] text-white' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)]'}`}
          >
            الواجب
          </button>
        </div>
      )}

      {activeTab === 'content' && (
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="space-y-6 lg:col-span-2">
            {lesson.videos.map((video) => (
              <div key={video.id} className="space-y-3">
                <h3 className="text-lg font-black text-[var(--admin-text)] sm:text-xl">
                  {video.title}
                </h3>
                <SecureVideoPlayer 
                  lessonVideoId={video.id} 
                  className="w-full rounded-2xl shadow-xl border border-pharaoh-gold/20"
                />
              </div>
            ))}
            {lesson.videos.length === 0 && (
              <div className="rounded-[24px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl p-8 text-center text-[var(--admin-muted)]">
                لا توجد فيديوهات متاحة لهذا الدرس حاليًا.
              </div>
            )}
          </div>

          <div className="space-y-6">
            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl p-5 sm:p-6">
              <div className="mb-4 flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
                  <FileText className="h-5 w-5" />
                </div>
                <h3 className="text-lg font-black text-[var(--admin-text)]">المصادر</h3>
              </div>
              <ul className="space-y-3">
                {lesson.resources.map((res) => (
                  <li key={res.id}>
                    <a
                      href={res.fileUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="flex items-center gap-3 rounded-2xl bg-[var(--admin-card-soft)] px-4 py-3 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)]"
                    >
                      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                      </svg>
                      {res.title}
                    </a>
                  </li>
                ))}
                {lesson.resources.length === 0 && (
                  <p className="text-sm text-[var(--admin-muted)]">لا توجد ملفات مرفقة.</p>
                )}
              </ul>
            </div>

            {lesson.examId && (
              <div className="overflow-hidden rounded-[24px] bg-[linear-gradient(135deg,#7f5427,#9a6933)] p-5 text-white shadow-lg sm:p-6">
                <div className="flex items-center gap-3">
                  <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/14">
                    <FlaskConical className="h-5 w-5" />
                  </div>
                  <h3 className="text-lg font-black">اختبار الدرس</h3>
                </div>
                <p className="mt-3 text-sm leading-7 text-white/80">
                  اختبر فهمك لهذا الدرس قبل الانتقال إلى المرحلة التالية.
                </p>
                <button
                  onClick={() => router.push(`/student/exams/${lesson.examId}?packageId=${packageId}`)}
                  className="mt-5 w-full rounded-2xl bg-[var(--admin-card)] px-4 py-3 text-sm font-extrabold text-[var(--admin-primary)] shadow-md transition-colors hover:bg-[var(--admin-card-strong)]"
                >
                  ابدأ الاختبار
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {activeTab === 'homework' && lesson.homework && (
        <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl p-6 lg:p-8">
          <div className="flex items-center gap-3 mb-6">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
              <PenTool className="h-6 w-6" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-[var(--admin-text)]">{lesson.homework.title}</h2>
              <p className="text-sm text-[var(--admin-muted)]">{lesson.homework.instructions}</p>
            </div>
          </div>

          {homeworkSubmitted ? (
            <div className="rounded-2xl bg-[var(--admin-success-10)] border border-[var(--admin-success-20)] p-8 text-center">
              <svg className="w-16 h-16 mx-auto mb-4 text-[var(--admin-success)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <h3 className="text-2xl font-bold text-[var(--admin-success)]">تم تسليم الواجب بنجاح</h3>
              <p className="mt-2 text-[var(--admin-muted)]">تم حفظ إجاباتك وإضافة النقاط إلى رصيدك.</p>
              <button onClick={() => setActiveTab('content')} className="mt-6 px-6 py-2 bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] rounded-xl font-bold hover:brightness-110 transition">العودة للدرس</button>
            </div>
          ) : (
            <form onSubmit={handleHomeworkSubmit} className="space-y-6">
              {lesson.homework.questions.map((q, idx) => {
                const qType = q.questionType || 'Essay';
                return (
                  <div key={q.id} className="bg-[var(--admin-card-soft)] border border-[var(--admin-border)] rounded-2xl p-5">
                    <h4 className="font-bold text-lg mb-4 text-[var(--admin-text)]">{idx + 1}. {q.text}</h4>
                    {qType === 'Essay' ? (
                      <textarea 
                        className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 min-h-[120px] bg-[var(--admin-card-soft)] text-[var(--admin-text)]"
                        placeholder="اكتب إجابتك هنا..."
                        required
                        value={homeworkAnswers[q.id] || ''}
                        onChange={e => setHomeworkAnswers(prev => ({...prev, [q.id]: e.target.value}))}
                      />
                    ) : (
                      <div className="text-sm text-[var(--admin-muted)]">Multiple choice unsupported in this component.</div>
                    )}
                  </div>
                );
              })}
              
              <div className="flex justify-end pt-4">
                <button 
                  type="submit" 
                  disabled={isSubmitting}
                  className="px-8 py-3 rounded-2xl bg-gradient-to-l from-[var(--admin-primary)] to-[var(--admin-primary-strong)] font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 disabled:opacity-50"
                >
                  {isSubmitting ? 'جاري التسليم...' : 'تسليم الواجب'}
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </div>
  );
}
