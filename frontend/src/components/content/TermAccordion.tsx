"use client";

import { useState, useEffect } from "react";
import { ChevronDown, CalendarDays, RefreshCcw, TriangleAlert } from "lucide-react";

import { SectionList } from "@/components/content/SectionList";
import { contentService, type ContentSectionDto, type TermDto } from "@/services/content-service";

export function TermAccordion({ terms, packageId, isPackageEnrolled }: { terms: TermDto[]; packageId: string; isPackageEnrolled?: boolean }) {
  const [expandedTerm, setExpandedTerm] = useState<string | null>(null);
  const [termSections, setTermSections] = useState<Record<string, ContentSectionDto[]>>({});
  const [loadingTerms, setLoadingTerms] = useState<Record<string, boolean>>({});
  const [termErrors, setTermErrors] = useState<Record<string, string>>({});

  const loadSections = async (termId: string) => {
    setLoadingTerms((prev) => ({ ...prev, [termId]: true }));
    setTermErrors((prev) => {
      const next = { ...prev };
      delete next[termId];
      return next;
    });

    try {
      const res = await contentService.getSections(termId);
      setTermSections((prev) => ({ ...prev, [termId]: res.data.data }));
    } catch {
      setTermErrors((prev) => ({ ...prev, [termId]: "تعذر تحميل محتوى هذا الترم الآن. أعد المحاولة." }));
    } finally {
      setLoadingTerms((prev) => ({ ...prev, [termId]: false }));
    }
  };

  const toggleTerm = async (termId: string) => {
    if (expandedTerm === termId) {
      setExpandedTerm(null);
      return;
    }

    setExpandedTerm(termId);
    if (!termSections[termId] && !loadingTerms[termId]) {
      await loadSections(termId);
    }
  };

  // Auto-expand if there's only one term
  useEffect(() => {
    if (terms.length === 1 && expandedTerm === null) {
      void toggleTerm(terms[0].id);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [terms]);

  return (
    <div className="space-y-4">
      {terms.map((term) => (
        <div key={term.id} className="overflow-hidden rounded-[26px] border-[2px] border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-sm">
          <button
            type="button"
            onClick={() => toggleTerm(term.id)}
            aria-expanded={expandedTerm === term.id}
            className="group flex w-full items-center justify-between gap-4 px-5 py-5 text-right transition-colors hover:bg-[var(--admin-card)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-inset sm:px-6"
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

          <div
            className={`grid overflow-hidden transition-[grid-template-rows,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] ${
              expandedTerm === term.id ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
            }`}
            aria-hidden={expandedTerm !== term.id}
            inert={expandedTerm !== term.id}
          >
            <div className="min-h-0 border-t-2 border-[var(--admin-border)] bg-[var(--admin-card)]">
              <div className="px-4 py-6 sm:px-6">
                {termSections[term.id] ? (
                  <SectionList sections={termSections[term.id]} packageId={packageId} isPackageEnrolled={isPackageEnrolled} />
                ) : termErrors[term.id] ? (
                  <div className="rounded-[22px] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-5 text-center">
                    <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-[var(--admin-card)] text-[var(--admin-danger)]">
                      <TriangleAlert className="h-5 w-5" />
                    </div>
                    <p className="mt-4 text-sm font-bold leading-7 text-[var(--admin-danger)]">
                      {termErrors[term.id]}
                    </p>
                    <button
                      type="button"
                      onClick={() => void loadSections(term.id)}
                      className="mt-4 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-[var(--admin-card)] px-5 py-2.5 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-danger-10)]"
                    >
                      <RefreshCcw className="h-4 w-4" />
                      إعادة المحاولة
                    </button>
                  </div>
                ) : loadingTerms[term.id] ? (
                  <div className="animate-pulse space-y-4" role="status" aria-live="polite">
                    <div className="h-16 w-full rounded-2xl bg-[var(--admin-card-strong)]" />
                    <div className="h-16 w-full rounded-2xl bg-[var(--admin-card-strong)]" />
                    <span className="sr-only">جارٍ تحميل أقسام الترم</span>
                  </div>
                ) : (
                  <div className="rounded-[22px] border border-dashed border-[var(--admin-border)] px-4 py-6 text-center text-sm font-bold text-[var(--admin-muted)]">
                    افتح الترم لعرض الأقسام المرتبطة به.
                  </div>
                )}
              </div>
            </div>
          </div>
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
