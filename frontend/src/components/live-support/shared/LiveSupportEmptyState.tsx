import { Inbox } from 'lucide-react';
import type { ReactNode } from 'react';

export function LiveSupportEmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <section className="grid min-h-56 place-items-center p-6 text-center"><div><span className="mx-auto grid size-12 place-items-center rounded-xl bg-slate-100 text-slate-600"><Inbox size={22}/></span><h3 className="mt-3 font-bold text-slate-900">{title}</h3><p className="mt-1 max-w-sm text-sm leading-6 text-slate-600">{description}</p>{action ? <div className="mt-4">{action}</div> : null}</div></section>;
}
