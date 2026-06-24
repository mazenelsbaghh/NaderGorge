'use client';

import { useEffect, useState } from 'react';
import { FileSearch, LoaderCircle } from 'lucide-react';
import { getLiveSupportAIError, liveSupportAIService, type AIEvidenceItem, type AIStatsPeriod } from '@/services/live-support-ai-service';
import { LiveSupportEmptyState } from '../shared/LiveSupportEmptyState';

export function AIActivityEvidence({ period }: { period: AIStatsPeriod }) {
  const [items, setItems] = useState<AIEvidenceItem[]>([]);
  const [cursor, setCursor] = useState<string>();
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  async function load(nextCursor?: string) {
    setState('loading');
    try { const page = await liveSupportAIService.getEvidence(period, nextCursor); setItems(current => nextCursor ? [...current, ...page.items] : page.items); setCursor(page.nextCursor); setState('ready'); }
    catch (cause) { setError(getLiveSupportAIError(cause, 'تعذر تحميل سجل الأدلة.')); setState('error'); }
  }
  useEffect(() => { void load(); }, [period]);
  return <section className="rounded-2xl border border-slate-200 bg-white" aria-labelledby="evidence-title"><div className="border-b border-slate-200 p-5"><h2 id="evidence-title" className="flex items-center gap-2 font-bold"><FileSearch className="size-5 text-cyan-700"/>أدلة نشاط المساعد</h2><p className="text-sm text-slate-600">معرّفات وحالات آمنة فقط؛ لا تظهر prompts أو بيانات شخصية.</p></div>{state === 'error' && <p role="alert" className="m-4 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}<button className="mr-2 font-bold underline" onClick={() => void load()}>إعادة المحاولة</button></p>}{state === 'loading' && items.length === 0 ? <div className="grid min-h-40 place-items-center"><LoaderCircle className="animate-spin"/></div> : items.length === 0 ? <LiveSupportEmptyState title="لا يوجد نشاط" description="لم تُسجل قرارات في الفترة المحددة."/> : <div className="overflow-x-auto"><table className="w-full min-w-[760px] text-right text-sm"><thead className="bg-slate-50 text-xs text-slate-600"><tr><th className="p-3">الوقت</th><th className="p-3">الحالة</th><th className="p-3">القرار</th><th className="p-3">المزود</th><th className="p-3">المحاولات</th><th className="p-3">Turn ID</th></tr></thead><tbody>{items.map(entry => <tr key={entry.turnId} className="border-t border-slate-100"><td className="p-3"><bdi>{new Date(entry.at).toLocaleString('ar-EG')}</bdi></td><td className="p-3">{entry.status}</td><td className="p-3">{entry.decisionType || entry.failureCode || '—'}</td><td className="p-3"><bdi dir="ltr">{entry.provider || '—'} {entry.model || ''}</bdi></td><td className="p-3">{entry.callbackAttempts}</td><td className="p-3"><code className="break-all text-xs" dir="ltr">{entry.turnId}</code></td></tr>)}</tbody></table></div>}{cursor && <div className="border-t border-slate-100 p-4 text-center"><button disabled={state === 'loading'} onClick={() => void load(cursor)} className="min-h-11 rounded-xl border border-slate-300 px-4 font-bold disabled:opacity-50">تحميل المزيد</button></div>}</section>;
}
