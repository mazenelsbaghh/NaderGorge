import { Bot, ShieldAlert } from 'lucide-react';
import type { AIPolicy } from '@/services/live-support-ai-service';

export function AIDisableControl({ policy, busy, onDisable, onEnable }: { policy?: AIPolicy; busy: boolean; onDisable: () => void; onEnable: () => void }) {
  return <section className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-5" aria-labelledby="ai-state-title">
    <div><h2 id="ai-state-title" className="font-bold text-slate-950">حالة المساعد</h2><p className="mt-1 text-sm text-slate-600">{policy ? `${policy.isEnabled ? 'مفعّل' : 'متوقف'} — الإصدار ${policy.versionNumber}` : 'لا توجد سياسة منشورة بعد'}</p></div>
    {policy?.isEnabled ? <button type="button" disabled={busy} onClick={onDisable} className="min-h-11 rounded-xl border border-red-200 px-4 font-bold text-red-700 focus-visible:outline-2 focus-visible:outline-red-700 disabled:opacity-50"><ShieldAlert className="ml-2 inline size-4"/>إيقاف وتحويل للدعم البشري</button> : policy ? <button type="button" disabled={busy} onClick={onEnable} className="min-h-11 rounded-xl bg-cyan-700 px-5 font-bold text-white focus-visible:outline-2 focus-visible:outline-cyan-800 disabled:opacity-50"><Bot className="ml-2 inline size-4"/>تشغيل وتفعيل المساعد</button> : null}
  </section>;
}
