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
  }, [context]);

  return (
    <div className="min-h-[620px] min-w-0 overflow-hidden rounded-3xl border border-slate-200 bg-white">
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
      <div className="grid min-h-[620px] min-w-0 lg:grid-cols-[280px_minmax(0,1fr)] xl:grid-cols-[280px_minmax(360px,1fr)_320px]">
        {/* Queue Pane */}
        <div className={`min-w-0 ${mobileView === 'queue' ? 'block' : 'hidden lg:block'}`}>
          {queue}
        </div>

        {/* Workspace Pane */}
        <div className={`min-w-0 ${mobileView === 'workspace' ? 'block' : 'hidden lg:block'}`}>
          {workspace}
        </div>

        {/* Context Pane */}
        {context && (
          <div className={`min-w-0 lg:col-span-2 xl:col-span-1 ${mobileView === 'context' ? 'block' : 'hidden xl:block'}`}>
            {context}
          </div>
        )}
      </div>
    </div>
  );
}
