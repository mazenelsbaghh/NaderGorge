'use client';

import { type KeyboardEvent, useId, useState } from 'react';
import { AssistantTaskBoard } from './AssistantTaskBoard';
import { AssistantOperationsTaskBoard } from './AssistantOperationsTaskBoard';
import { ArrowLeft, BriefcaseBusiness, CheckCircle2, GraduationCap, Sparkles } from 'lucide-react';

type WorkspaceId = 'academic' | 'operations';

export function AssistantDashboardTabs() {
  const [activeTab, setActiveTab] = useState<WorkspaceId>('academic');
  const tabListId = useId();
  const academicPanelId = `${tabListId}-academic-panel`;
  const operationsPanelId = `${tabListId}-operations-panel`;

  const workspaces = [
    {
      id: 'academic' as const,
      label: 'مهام الطلاب والمتابعة الأكاديمية',
      description: 'مراجعة الإجابات والحالات التعليمية التي تحتاج تدخلاً.',
      Icon: GraduationCap,
      panelId: academicPanelId,
    },
    {
      id: 'operations' as const,
      label: 'المهام التشغيلية اليومية',
      description: 'المهام المسندة من الإدارة ومتابعة مواعيدها وحالتها.',
      Icon: BriefcaseBusiness,
      panelId: operationsPanelId,
    },
  ];

  const moveFocus = (event: KeyboardEvent<HTMLButtonElement>, currentId: WorkspaceId) => {
    const currentIndex = workspaces.findIndex(({ id }) => id === currentId);
    const nextIndex = event.key === 'Home'
      ? 0
      : event.key === 'End'
        ? workspaces.length - 1
        : event.key === 'ArrowRight'
          ? (currentIndex - 1 + workspaces.length) % workspaces.length
          : event.key === 'ArrowLeft'
            ? (currentIndex + 1) % workspaces.length
            : currentIndex;

    if (nextIndex === currentIndex && !['Home', 'End'].includes(event.key)) return;

    event.preventDefault();
    const nextWorkspace = workspaces[nextIndex];
    setActiveTab(nextWorkspace.id);
    document.getElementById(`${tabListId}-${nextWorkspace.id}-tab`)?.focus();
  };

  return (
    <div className="mx-auto max-w-6xl space-y-7">
      <section aria-labelledby={`${tabListId}-heading`} className="overflow-hidden rounded-2xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]">
        <div className="grid gap-6 px-5 py-6 sm:px-7 sm:py-7 lg:grid-cols-[1fr_auto] lg:items-end">
          <div className="max-w-2xl">
            <div className="flex items-center gap-2 text-sm font-bold text-[var(--admin-primary-contrast)]/75">
              <Sparkles className="h-4 w-4" aria-hidden="true" />
              <span>مساحة عملك اليوم</span>
            </div>
            <h2 id={`${tabListId}-heading`} className="mt-3 text-2xl font-black leading-tight sm:text-3xl">
              ابدأ بالمهمة الأقرب إلى قرارك التالي
            </h2>
            <p className="mt-3 max-w-xl text-sm leading-7 text-[var(--admin-primary-contrast)]/80">
              اختر المتابعة الأكاديمية أو العمليات، ثم نفّذ الإجراء المطلوب من مكان واحد.
            </p>
          </div>
          <div className="flex items-center gap-3 border-t border-white/20 pt-4 lg:border-t-0 lg:border-r lg:pr-6 lg:pt-0">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-white/15">
              <CheckCircle2 className="h-5 w-5" aria-hidden="true" />
            </span>
            <p className="text-sm font-bold leading-6">اختر المساحة<br />واستكمل المهمة</p>
          </div>
        </div>
      </section>

      <div className="border-b border-[var(--admin-border)]">
        <div id={tabListId} role="tablist" aria-label="مساحات عمل المساعد" className="flex overflow-x-auto">
          {workspaces.map(({ id, label, description, Icon, panelId }) => {
            const isActive = activeTab === id;

            return (
              <button
                key={id}
                id={`${tabListId}-${id}-tab`}
                type="button"
                role="tab"
                aria-selected={isActive}
                aria-controls={panelId}
                tabIndex={isActive ? 0 : -1}
                onClick={() => setActiveTab(id)}
                onKeyDown={(event) => moveFocus(event, id)}
                className={`group relative flex min-w-[230px] flex-1 items-start gap-3 px-1 pb-4 text-right transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 ${
                  isActive ? 'text-[var(--admin-text)]' : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
                }`}
              >
                <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${isActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] group-hover:text-[var(--admin-primary)]'}`}>
                  <Icon className="h-5 w-5" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-3 text-sm font-black">
                    {label}
                    <ArrowLeft className={`h-4 w-4 shrink-0 transition-transform ${isActive ? '-translate-x-0.5' : ''}`} aria-hidden="true" />
                  </span>
                  <span className="mt-1 block text-xs font-medium leading-5">{description}</span>
                </span>
                <span className={`absolute bottom-0 right-0 h-0.5 transition-[width] duration-200 ${isActive ? 'w-full bg-[var(--admin-primary)]' : 'w-0 bg-transparent'}`} />
              </button>
            );
          })}
        </div>
      </div>

      <section
        id={activeTab === 'academic' ? academicPanelId : operationsPanelId}
        role="tabpanel"
        aria-labelledby={`${tabListId}-${activeTab}-tab`}
        tabIndex={0}
        className="focus-visible:outline-none"
      >
        {activeTab === 'academic' ? (
          <AssistantTaskBoard />
        ) : (
          <AssistantOperationsTaskBoard />
        )}
      </section>
    </div>
  );
}
