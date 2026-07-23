'use client';

import { type KeyboardEvent, useId, useState } from 'react';
import { AssistantTaskBoard } from './AssistantTaskBoard';
import { AssistantOperationsTaskBoard } from './AssistantOperationsTaskBoard';
import { ArrowLeft, Briefcase, GraduationCap } from 'lucide-react';

export function AssistantDashboardTabs() {
  const [activeTab, setActiveTab] = useState<'academic' | 'operations'>('academic');
  const tabListId = useId();
  const academicPanelId = `${tabListId}-academic-panel`;
  const operationsPanelId = `${tabListId}-operations-panel`;

  const workspaces = [
    {
      id: 'academic' as const,
      label: 'مهام الطلاب والمتابعة الأكاديمية',
      description: 'ابدأ هنا لمراجعة إجابات الطلاب، ومتابعة الحالات التعليمية التي تحتاج تدخلاً.',
      Icon: GraduationCap,
      panelId: academicPanelId,
    },
    {
      id: 'operations' as const,
      label: 'المهام التشغيلية اليومية',
      description: 'انتقل هنا للمهام المسندة إليك من الإدارة ومواعيدها وحالتها.',
      Icon: Briefcase,
      panelId: operationsPanelId,
    },
  ];

  const moveFocus = (event: KeyboardEvent<HTMLButtonElement>, currentId: 'academic' | 'operations') => {
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
    <div className="mx-auto max-w-6xl space-y-6">
      <section
        aria-labelledby={`${tabListId}-heading`}
        className="border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6"
      >
        <div className="flex flex-col gap-3 border-b border-[var(--admin-border)] pb-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-sm font-bold text-[var(--admin-primary)]">ابدأ من هنا</p>
            <h2 id={`${tabListId}-heading`} className="mt-1 text-xl font-bold text-[var(--admin-text)]">
              اختر مساحة العمل التي تحتاجها الآن
            </h2>
          </div>
          <p className="max-w-xl text-sm leading-6 text-[var(--admin-muted)]">
            رتّب يومك بين متابعة الطلاب والمهام التشغيلية، ثم افتح المهمة التالية مباشرة من القائمة.
          </p>
        </div>

        <div
          id={tabListId}
          role="tablist"
          aria-label="مساحات عمل المساعد"
          className="mt-4 grid gap-3 sm:grid-cols-2"
        >
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
                className={`group flex min-h-28 items-start gap-3 border p-4 text-right transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 ${
                  isActive
                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-10)] text-[var(--admin-text)]'
                    : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)] hover:text-[var(--admin-text)]'
                }`}
              >
                <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center ${isActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card)] text-[var(--admin-primary)]'}`}>
                  <Icon className="h-5 w-5" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-3 text-base font-bold">
                    {label}
                    <ArrowLeft className={`h-4 w-4 shrink-0 transition-transform ${isActive ? '-translate-x-0.5' : ''}`} aria-hidden="true" />
                  </span>
                  <span className="mt-1 block text-sm font-medium leading-6">{description}</span>
                </span>
              </button>
            );
          })}
        </div>
      </section>

      <section
        id={activeTab === 'academic' ? academicPanelId : operationsPanelId}
        role="tabpanel"
        aria-labelledby={`${tabListId}-${activeTab}-tab`}
        tabIndex={0}
        className="focus-visible:outline-none"
      >
        {activeTab === 'academic' ? <AssistantTaskBoard /> : <AssistantOperationsTaskBoard />}
      </section>
    </div>
  );
}
