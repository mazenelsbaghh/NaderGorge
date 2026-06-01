"use client";

import { useState } from "react";
import { ChevronDown, Lock, RefreshCcw, TriangleAlert } from "lucide-react";
import { useRouter } from "next/navigation";

import {
  contentService,
  type ContentSectionDto,
  type LessonSummaryDto,
} from "@/services/content-service";

export function SectionList({ sections, packageId, isPackageEnrolled }: { sections: ContentSectionDto[]; packageId: string; isPackageEnrolled?: boolean }) {
  const router = useRouter();
  const [expandedSection, setExpandedSection] = useState<string | null>(null);
  const [lessons, setLessons] = useState<Record<string, LessonSummaryDto[]>>({});
  const [loadingSections, setLoadingSections] = useState<Record<string, boolean>>({});
  const [sectionErrors, setSectionErrors] = useState<Record<string, string>>({});

  const handleLessonAction = (lesson: LessonSummaryDto) => {
    if (lesson.isLocked) {
      if (lesson.blockingHomeworkLessonId) {
        router.push(`/student/packages/${packageId}/lessons/${lesson.blockingHomeworkLessonId}`);
        return;
      }

      if (lesson.blockingExamId) {
        router.push(`/student/exams/${lesson.blockingExamId}?packageId=${packageId}`);
        return;
      }
    }

    router.push(`/student/packages/${packageId}/lessons/${lesson.id}`);
  };

  const loadLessons = async (sectionId: string) => {
    setLoadingSections((prev) => ({ ...prev, [sectionId]: true }));
    setSectionErrors((prev) => {
      const next = { ...prev };
      delete next[sectionId];
      return next;
    });

    try {
      const res = await contentService.getLessons(sectionId);
      setLessons((prev) => ({ ...prev, [sectionId]: res.data.data }));
    } catch {
      setSectionErrors((prev) => ({ ...prev, [sectionId]: "تعذر تحميل دروس هذا القسم الآن. أعد المحاولة." }));
    } finally {
      setLoadingSections((prev) => ({ ...prev, [sectionId]: false }));
    }
  };

  const toggleSection = async (sectionId: string) => {
    if (expandedSection === sectionId) {
      setExpandedSection(null);
      return;
    }

    setExpandedSection(sectionId);
    if (!lessons[sectionId] && !loadingSections[sectionId]) {
      await loadLessons(sectionId);
    }
  };

  return (
    <div className="space-y-4">
      {sections.map((section) => (
        <div key={section.id} className="overflow-hidden rounded-[22px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
          <button
            type="button"
            onClick={() => toggleSection(section.id)}
            aria-expanded={expandedSection === section.id}
            className="flex w-full items-center justify-between gap-4 px-4 py-4 text-right hover:bg-[var(--admin-card)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-inset sm:px-6"
          >
            <ChevronDown
              className={`h-5 w-5 shrink-0 text-[var(--admin-muted)] transition-transform ${expandedSection === section.id ? "rotate-180" : ""}`}
            />
            <h3 className="min-w-0 text-base font-bold text-[var(--admin-text)] sm:text-lg">
              {section.title}
            </h3>
          </button>

          <div
            className={`grid overflow-hidden transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] ${
              expandedSection === section.id ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
            }`}
            aria-hidden={expandedSection !== section.id}
            inert={expandedSection !== section.id}
          >
            <div className="min-h-0 border-t border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-4 sm:px-6">
                {lessons[section.id] ? (
                  <ul className="space-y-3">
                    {lessons[section.id].map((lesson) => (
                      <li
                        key={lesson.id}
                        className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 sm:flex-row sm:items-center sm:justify-between"
                      >
                        <div className="min-w-0">
                          <p className="font-bold text-[var(--admin-text)]">{lesson.title}</p>
                          <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
                            {lesson.isLocked && lesson.lockedReason ? lesson.lockedReason : lesson.summary}
                          </p>
                        </div>
                        {lesson.hasAccess ? (
                          <div className="flex flex-wrap items-center gap-3">
                            {lesson.isCompleted && (
                              <span className="text-sm font-bold text-[var(--admin-success)]">مكتمل</span>
                            )}
                            {lesson.isLocked && (
                              <span className="flex items-center gap-1 text-sm font-bold text-[var(--admin-warning)]">
                                <Lock className="h-4 w-4" />
                                مقفول
                              </span>
                            )}
                            <button
                              type="button"
                              onClick={() => handleLessonAction(lesson)}
                              className="rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-bold text-[var(--admin-primary-contrast)] transition-all hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                            >
                              {lesson.isLocked
                                ? lesson.blockingHomeworkLessonId
                                  ? "اذهب لحل الواجب"
                                  : "اذهب للامتحان"
                                : "مشاهدة"}
                            </button>
                          </div>
                        ) : (
                          // No access to lesson
                          isPackageEnrolled === false ? (
                            <button
                              type="button"
                              onClick={() => router.push(`/student/packages/${packageId}`)}
                              className="flex shrink-0 items-center gap-1.5 rounded-xl border border-[var(--admin-primary-20)] bg-[var(--admin-primary-15)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-20)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
                            >
                              شراء الباقة
                            </button>
                          ) : (
                            <span className="flex shrink-0 items-center gap-1 text-sm font-bold text-[var(--admin-danger)]">
                              <Lock className="h-4 w-4" />
                              مقفول
                            </span>
                          )
                        )}
                      </li>
                    ))}
                    {lessons[section.id].length === 0 && (
                      <li className="py-4 text-center text-sm text-[var(--admin-muted)]">
                        لا توجد دروس في هذا القسم.
                      </li>
                    )}
                  </ul>
                ) : sectionErrors[section.id] ? (
                  <div className="rounded-[20px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-5 text-center">
                    <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-full bg-[var(--admin-card)] text-[var(--admin-danger)]">
                      <TriangleAlert className="h-5 w-5" />
                    </div>
                    <p className="mt-3 text-sm font-bold leading-7 text-[var(--admin-danger)]">
                      {sectionErrors[section.id]}
                    </p>
                    <button
                      type="button"
                      onClick={() => void loadLessons(section.id)}
                      className="mt-4 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-[var(--admin-card)] px-5 py-2.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-danger-10)]"
                    >
                      <RefreshCcw className="h-4 w-4" />
                      إعادة المحاولة
                    </button>
                  </div>
                ) : loadingSections[section.id] ? (
                  <div className="animate-pulse space-y-3" role="status" aria-live="polite">
                    <div className="h-16 rounded-xl bg-[var(--admin-card-strong)]"></div>
                    <div className="h-16 rounded-xl bg-[var(--admin-card-strong)]"></div>
                    <span className="sr-only">جارٍ تحميل دروس القسم</span>
                  </div>
                ) : (
                  <div className="rounded-[20px] border border-dashed border-[var(--admin-border)] px-4 py-6 text-center text-sm font-bold text-[var(--admin-muted)]">
                    افتح القسم لعرض الدروس المتاحة.
                  </div>
                )}
            </div>
          </div>
        </div>
      ))}
      
      {sections.length === 0 && (
        <div className="rounded-[28px] border-2 border-dashed border-[var(--admin-border)] p-12 text-center">
          <p className="text-[var(--admin-muted)]">لا توجد أقسام متاحة.</p>
        </div>
      )}
    </div>
  );
}
