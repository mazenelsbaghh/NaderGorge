'use client';

import { useState, useEffect, type ReactNode } from 'react';
import { ChevronRight, Info, Users } from 'lucide-react';

export function StaffConversationLayout({ queue, workspace, context }: { queue: ReactNode; workspace: ReactNode; context?: ReactNode }) {
  const [mobileView, setMobileView] = useState<'queue' | 'workspace' | 'context'>('queue');

  useEffect(() => {
    if (!context) {
      setMobileView('queue');
    } else if (mobileView === 'queue') {
      setMobileView('workspace');
    }
  }, [context, mobileView]);

  return (
    <div className="h-[min(700px,calc(100dvh-12rem))] min-h-[500px] min-w-0 overflow-hidden rounded-2xl border border-slate-200 bg-white">
      {/* Mobile/Tablet Header */}
      <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 p-3 lg:hidden">
        {mobileView === 'queue' && (
          <span className="flex items-center gap-2 text-sm font-bold text-slate-800">
            <Users size={16} />
            المحادثات الواردة
          </span>
        )}
        {mobileView === 'workspace' && (
          <div className="flex w-full items-center justify-between gap-2">
            <button
              onClick={() => setMobileView('queue')}
              className="flex items-center gap-1 text-xs font-bold text-cyan-700"
            >
              <ChevronRight size={16} />
              قائمة المحادثات
            </button>
            {context && (
              <button
                onClick={() => setMobileView('context')}
                className="flex items-center gap-1 text-xs font-bold text-slate-700 bg-slate-200 px-2 py-1 rounded-lg"
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
            className="flex items-center gap-1 text-xs font-bold text-cyan-700"
          >
            <ChevronRight size={16} />
            العودة للمحادثة
          </button>
        )}
      </div>

      {/* Main Grid View */}
      <div className="grid h-[calc(100%_-_3.0625rem)] min-w-0 lg:h-full lg:grid-cols-[300px_minmax(0,680px)] xl:grid-cols-[300px_minmax(0,680px)_minmax(260px,320px)] xl:justify-start">
        {/* Queue Pane */}
        <div className={`h-full min-h-0 min-w-0 overflow-hidden ${mobileView === 'queue' ? 'block' : 'hidden lg:block'}`}>
          {queue}
        </div>

        {/* Workspace Pane */}
        <div className={`h-full min-h-0 min-w-0 overflow-hidden ${mobileView === 'workspace' ? 'block' : 'hidden lg:block'}`}>
          {workspace}
        </div>

        {/* Context Pane */}
        {context && (
          <div className={`h-full min-h-0 min-w-0 overflow-hidden xl:col-span-1 ${mobileView === 'context' ? 'block' : 'hidden xl:block'}`}>
            {context}
          </div>
        )}
      </div>
    </div>
  );
}
