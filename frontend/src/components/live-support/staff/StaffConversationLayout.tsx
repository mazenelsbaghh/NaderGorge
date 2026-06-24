import type { ReactNode } from 'react';

export function StaffConversationLayout({ queue, workspace, context }: { queue: ReactNode; workspace: ReactNode; context?: ReactNode }) {
  return <div className="grid min-h-[620px] min-w-0 overflow-hidden rounded-3xl border border-slate-200 bg-white lg:grid-cols-[280px_minmax(0,1fr)] xl:grid-cols-[280px_minmax(360px,1fr)_320px]">
    {queue}<div className="min-w-0">{workspace}</div>{context ? <div className="min-w-0 lg:col-span-2 xl:col-span-1">{context}</div> : null}
  </div>;
}
