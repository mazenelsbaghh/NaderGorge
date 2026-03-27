"use client";

import { useState } from "react";
import { ChevronDown, CalendarDays } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";

import { SectionList } from "@/components/content/SectionList";
import { contentService, type ContentSectionDto, type TermDto } from "@/services/content-service";

export function TermAccordion({ terms, packageId }: { terms: TermDto[]; packageId: string }) {
  const [expandedTerm, setExpandedTerm] = useState<string | null>(null);
  const [termSections, setTermSections] = useState<Record<string, ContentSectionDto[]>>({});

  const toggleTerm = async (termId: string) => {
    if (expandedTerm === termId) {
      setExpandedTerm(null);
      return;
    }

    setExpandedTerm(termId);
    if (!termSections[termId]) {
      try {
        const res = await contentService.getSections(termId);
        setTermSections((prev) => ({ ...prev, [termId]: res.data.data }));
      } catch (err) {
        console.error("Failed to load sections for term", err);
      }
    }
  };

  return (
    <div className="space-y-4">
      {terms.map((term) => (
        <div key={term.id} className="overflow-hidden rounded-[26px] border-[2px] border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-sm">
          <button
            onClick={() => toggleTerm(term.id)}
            className="group flex w-full items-center justify-between gap-4 px-5 py-5 text-right transition-colors hover:bg-[var(--admin-card)] sm:px-6"
          >
            <div className="flex items-center gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-[14px] bg-[var(--admin-primary-15)] text-[var(--admin-primary)] transition-transform group-hover:scale-110">
                <CalendarDays className="h-6 w-6" />
              </div>
              <h3 className="text-lg font-black text-[var(--admin-text)] sm:text-xl">
                {term.title}
              </h3>
            </div>
            <ChevronDown
              className={`h-6 w-6 shrink-0 text-[var(--admin-muted)] transition-transform duration-300 ${
                expandedTerm === term.id ? "rotate-180" : ""
              }`}
            />
          </button>

          <AnimatePresence>
            {expandedTerm === term.id && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                transition={{ duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
                className="overflow-hidden border-t-2 border-[var(--admin-border)] bg-[var(--admin-card)]"
              >
                <div className="px-4 py-6 sm:px-6">
                  {termSections[term.id] ? (
                    <SectionList sections={termSections[term.id]} packageId={packageId} />
                  ) : (
                    <div className="animate-pulse space-y-4">
                      <div className="h-16 w-full rounded-2xl bg-[var(--admin-card-strong)]" />
                      <div className="h-16 w-full rounded-2xl bg-[var(--admin-card-strong)]" />
                    </div>
                  )}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      ))}

      {terms.length === 0 && (
        <div className="rounded-[28px] border-2 border-dashed border-[var(--admin-border)] p-12 text-center">
          <p className="font-bold text-[var(--admin-muted)]">لا توجد أترام متاحة في هذه الباقة.</p>
        </div>
      )}
    </div>
  );
}
