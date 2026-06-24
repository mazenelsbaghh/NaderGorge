import { AlertCircle, CheckCircle2, Info } from 'lucide-react';
import type { ReactNode } from 'react';

export function LiveSupportStateNotice({ tone = 'info', title, children, retry }: { tone?: 'info' | 'success' | 'error'; title: string; children?: ReactNode; retry?: () => void }) {
  const Icon = tone === 'error' ? AlertCircle : tone === 'success' ? CheckCircle2 : Info;
  const colors = tone === 'error' ? 'border-red-200 bg-red-50 text-red-900' : tone === 'success' ? 'border-emerald-200 bg-emerald-50 text-emerald-950' : 'border-cyan-200 bg-cyan-50 text-slate-900';
  return <section role={tone === 'error' ? 'alert' : 'status'} className={`rounded-2xl border p-4 ${colors}`}><div className="flex items-start gap-3"><Icon className="mt-0.5 shrink-0" size={19}/><div className="min-w-0 flex-1"><h3 className="font-bold">{title}</h3>{children ? <div className="mt-1 text-sm leading-6">{children}</div> : null}</div>{retry ? <button type="button" onClick={retry} className="min-h-11 rounded-xl border border-current px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2">إعادة المحاولة</button> : null}</div></section>;
}
