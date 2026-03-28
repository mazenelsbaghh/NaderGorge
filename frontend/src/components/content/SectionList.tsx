"use client";

import { useState } from "react";
import { ChevronDown, Lock } from "lucide-react";
import { useRouter } from "next/navigation";
import { AnimatePresence, motion } from "framer-motion";

import {
  contentService,
  type ContentSectionDto,
  type LessonSummaryDto,
} from "@/services/content-service";

export function SectionList({ sections, packageId }: { sections: ContentSectionDto[]; packageId: string }) {
  const router = useRouter();
  const [expandedSection, setExpandedSection] = useState<string | null>(null);
  const [lessons, setLessons] = useState<Record<string, LessonSummaryDto[]>>({});

  const toggleSection = async (sectionId: string) => {
    if (expandedSection === sectionId) {
      setExpandedSection(null);
      return;
    }

    setExpandedSection(sectionId);
    if (!lessons[sectionId]) {
      try {
        const res = await contentService.getLessons(sectionId);
        setLessons((prev) => ({ ...prev, [sectionId]: res.data.data }));
      } catch {
        console.error("Failed to load lessons");
      }
    }
  };

  return (
    <div className="space-y-4">
      {sections.map((section) => (
        <div key={section.id} className="overflow-hidden rounded-[22px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
          <button
            onClick={() => toggleSection(section.id)}
            className="flex w-full items-center justify-between gap-4 px-4 py-4 text-right hover:bg-[var(--admin-card)] sm:px-6"
          >
            <ChevronDown
              className={`h-5 w-5 shrink-0 text-[var(--admin-muted)] transition-transform ${expandedSection === section.id ? "rotate-180" : ""}`}
            />
            <h3 className="min-w-0 text-base font-bold text-[var(--admin-text)] sm:text-lg">
              {section.title}
            </h3>
          </button>

          <AnimatePresence>
            {expandedSection === section.id && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-4 sm:px-6"
              >
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
                            {lesson.summary}
                          </p>
                        </div>
                        {lesson.hasAccess ? (
                          <div className="flex flex-wrap items-center gap-3">
                            {lesson.isCompleted && (
                              <span className="text-sm font-bold text-green-600">مكتمل</span>
                            )}
                            <button
                              onClick={() => router.push(`/student/packages/${packageId}/lessons/${lesson.id}`)}
                              className="rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-bold text-[var(--admin-primary-contrast)] transition-all hover:bg-[var(--admin-primary-strong)]"
                            >
                              مشاهدة
                            </button>
                          </div>
                        ) : (
                          <span className="flex items-center gap-1 text-sm font-bold text-red-500">
                            <Lock className="h-4 w-4" />
                            مقفول
                          </span>
                        )}
                      </li>
                    ))}
                    {lessons[section.id].length === 0 && (
                      <p className="py-4 text-center text-sm text-[var(--admin-muted)]">
                        لا توجد دروس في هذا القسم.
                      </p>
                    )}
                  </ul>
                ) : (
                  <div className="animate-pulse space-y-3">
                    <div className="h-16 rounded-xl bg-[var(--admin-card-strong)]"></div>
                    <div className="h-16 rounded-xl bg-[var(--admin-card-strong)]"></div>
                  </div>
                )}
              </motion.div>
            )}
          </AnimatePresence>
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
