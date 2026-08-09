'use client';

import { useState, useEffect, type ReactNode } from 'react';
import { ChevronRight, Info, PanelLeftClose, PanelLeftOpen, PanelRightClose, PanelRightOpen, Users } from 'lucide-react';

export function StaffConversationLayout({ queue, workspace, context, workspaceFocusRequest }: { queue: ReactNode; workspace: ReactNode; context?: ReactNode; workspaceFocusRequest: number }) {
  const [mobileView, setMobileView] = useState<'queue' | 'workspace' | 'context'>('queue');
  const [queueVisible, setQueueVisible] = useState(true);
  const [contextVisible, setContextVisible] = useState(true);

  useEffect(() => {
    if (!context) {
      setMobileView('queue');
    } else if (mobileView === 'queue') {
      setMobileView('workspace');
    }
  }, [context, mobileView]);

  useEffect(() => {
    if (workspaceFocusRequest) setMobileView('workspace');
  }, [workspaceFocusRequest]);

  return (
    <div className="h-[calc(100dvh-2rem)] min-h-[560px] min-w-0 overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] sm:h-[calc(100dvh-7rem)]">
      {/* Mobile/Tablet Header */}
      <div className="flex items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 lg:hidden">
        {mobileView === 'queue' && (
          <span className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
            <Users size={16} />
            المحادثات الواردة
          </span>
        )}
        {mobileView === 'workspace' && (
          <div className="flex w-full items-center justify-between gap-2">
            <button
              onClick={() => setMobileView('queue')}
              className="flex min-h-11 items-center gap-1 text-xs font-bold text-[var(--admin-primary)]"
            >
              <ChevronRight size={16} />
              قائمة المحادثات
            </button>
            {context && (
              <button
                onClick={() => setMobileView('context')}
                className="flex min-h-11 items-center gap-1 rounded-lg bg-[var(--admin-card-strong)] px-2 py-1 text-xs font-bold text-[var(--admin-text)]"
              >
                <Info size={14} />
                ملف الطالب
              </button>
            )}
          </div>
        )}
        {mobileView === 'context' && (
          <button
            onClick={() => setMobileView('workspace')}
            className="flex min-h-11 items-center gap-1 text-xs font-bold text-[var(--admin-primary)]"
          >
            <ChevronRight size={16} />
            العودة للمحادثة
          </button>
        )}
      </div>

      <div className="hidden h-[3.25rem] items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 lg:flex">
        <button type="button" onClick={() => setQueueVisible((visible) => !visible)} className="inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]" aria-pressed={queueVisible}>
          {queueVisible ? <PanelRightClose size={17} /> : <PanelRightOpen size={17} />}
          {queueVisible ? 'إخفاء المحادثات' : 'إظهار المحادثات'}
        </button>
        {context ? <button type="button" onClick={() => setContextVisible((visible) => !visible)} className="hidden min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] xl:inline-flex" aria-pressed={contextVisible}>
          {contextVisible ? <PanelLeftClose size={17} /> : <PanelLeftOpen size={17} />}
          {contextVisible ? 'إخفاء بيانات الطالب' : 'إظهار بيانات الطالب'}
        </button> : null}
      </div>

      {/* Main Grid View */}
      <div className={`grid h-[calc(100%_-_3.0625rem)] min-w-0 lg:h-[calc(100%_-_3.25rem)] ${queueVisible ? contextVisible && context ? 'lg:grid-cols-[260px_minmax(480px,1fr)] xl:grid-cols-[260px_minmax(520px,1fr)_minmax(290px,340px)]' : 'lg:grid-cols-[260px_minmax(480px,1fr)]' : contextVisible && context ? 'lg:grid-cols-[minmax(480px,1fr)] xl:grid-cols-[minmax(520px,1fr)_minmax(290px,340px)]' : 'lg:grid-cols-[minmax(480px,1fr)]'}`}>
        {/* Queue Pane */}
        <div className={`h-full min-h-0 min-w-0 overflow-hidden ${mobileView === 'queue' ? 'block' : 'hidden'} ${queueVisible ? 'lg:block' : 'lg:hidden'}`}>
          {queue}
        </div>

        {/* Workspace Pane */}
        <div className={`h-full min-h-0 min-w-0 overflow-hidden ${mobileView === 'workspace' ? 'block' : 'hidden lg:block'}`}>
          {workspace}
        </div>

        {/* Context Pane */}
        {context && (
          <div className={`h-full min-h-0 min-w-0 overflow-hidden xl:col-span-1 ${mobileView === 'context' ? 'block' : 'hidden'} ${contextVisible ? 'xl:block' : 'xl:hidden'}`}>
            {context}
          </div>
        )}
      </div>
    </div>
  );
}
