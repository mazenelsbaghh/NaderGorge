'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ContentSectionDto, LessonSummaryDto, contentService } from '@/services/content-service';

export function SectionList({ sections }: { sections: ContentSectionDto[] }) {
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
      } catch (err) {
        console.error('Failed to load lessons');
      }
    }
  };

  return (
    <div className="space-y-4">
      {sections.map((section) => (
        <div key={section.id} className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm transition-all dark:border-gray-800 dark:bg-gray-900">
          <button
            onClick={() => toggleSection(section.id)}
            className="flex w-full items-center justify-between px-6 py-4 text-left hover:bg-gray-50 dark:hover:bg-gray-800/50"
          >
            <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{section.title}</h3>
            <svg
              className={`h-5 w-5 text-gray-500 transition-transform ${expandedSection === section.id ? 'rotate-180' : ''}`}
              fill="none" viewBox="0 0 24 24" stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </button>

          <AnimatePresence>
            {expandedSection === section.id && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-gray-100 bg-gray-50 px-6 py-4 dark:border-gray-800 dark:bg-gray-900/50"
              >
                {lessons[section.id] ? (
                  <ul className="space-y-3">
                    {lessons[section.id].map((lesson) => (
                      <li key={lesson.id} className="flex items-center justify-between rounded-xl bg-white p-4 shadow-sm dark:bg-gray-800">
                        <div>
                          <p className="font-medium text-gray-900 dark:text-gray-100">{lesson.title}</p>
                          <p className="text-sm text-gray-500 dark:text-gray-400">{lesson.summary}</p>
                        </div>
                        {lesson.hasAccess ? (
                          <div className="flex items-center gap-3">
                            {lesson.isCompleted && <span className="text-green-500">✅ Completed</span>}
                            <button className="rounded-lg bg-blue-100 px-3 py-1.5 text-sm font-semibold text-blue-700 hover:bg-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:hover:bg-blue-900/60">
                              View
                            </button>
                          </div>
                        ) : (
                          <span className="text-sm font-medium text-red-500 flex items-center gap-1">
                            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                               <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                            </svg>
                            Locked
                          </span>
                        )}
                      </li>
                    ))}
                    {lessons[section.id].length === 0 && (
                      <p className="text-sm text-gray-500 text-center py-4">No lessons in this section.</p>
                    )}
                  </ul>
                ) : (
                  <div className="animate-pulse space-y-3">
                    <div className="h-16 rounded-xl bg-gray-200 dark:bg-gray-800"></div>
                    <div className="h-16 rounded-xl bg-gray-200 dark:bg-gray-800"></div>
                  </div>
                )}
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      ))}
      
      {sections.length === 0 && (
        <div className="rounded-2xl border border-dashed border-gray-300 p-12 text-center dark:border-gray-700">
          <p className="text-gray-500 dark:text-gray-400">No sections available.</p>
        </div>
      )}
    </div>
  );
}
