'use client';

import { useCallback, useEffect, useState } from 'react';
import { FileSearch, LoaderCircle } from 'lucide-react';
import { getLiveSupportAIError, liveSupportAIService, type AIEvidenceItem, type AIStatsPeriod } from '@/services/live-support-ai-service';
import { LiveSupportEmptyState } from '../shared/LiveSupportEmptyState';
import { formatCairoTimestamp } from '@/lib/cairo-time';

export function AIActivityEvidence({ period }: { period: AIStatsPeriod }) {
  const [items, setItems] = useState<AIEvidenceItem[]>([]);
  const [cursor, setCursor] = useState<string>();
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  const load = useCallback(async (nextCursor?: string) => {
    setState('loading');
    setError('');
    try { const page = await liveSupportAIService.getEvidence(period, nextCursor); setItems(current => nextCursor ? [...current, ...page.items] : page.items); setCursor(page.nextCursor); setState('ready'); }
    catch (cause) { setError(getLiveSupportAIError(cause, 'تعذر تحميل سجل الأدلة.')); setState('error'); }
  }, [period]);
  useEffect(() => { void load(); }, [load]);
  return <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]" aria-labelledby="evidence-title"><div className="border-b border-[var(--admin-border)] p-5"><h2 id="evidence-title" className="flex items-center gap-2 font-bold"><FileSearch className="size-5 text-[var(--admin-primary)]"/>أدلة نشاط المساعد</h2><p className="text-sm text-[var(--admin-muted)]">معرّفات وحالات آمنة فقط؛ لا تظهر prompts أو بيانات شخصية.</p></div>{state === 'error' && <p role="alert" className="m-4 rounded-xl bg-[var(--admin-danger-10)] p-3 text-sm text-[var(--admin-danger)]">{error}<button className="mr-2 font-bold underline" onClick={() => void load()}>إعادة المحاولة</button></p>}{state === 'loading' && items.length === 0 ? <div className="grid min-h-40 place-items-center"><LoaderCircle className="animate-spin"/></div> : items.length === 0 ? <LiveSupportEmptyState title="لا يوجد نشاط" description="لم تُسجل قرارات في الفترة المحددة."/> : <div className="overflow-x-auto"><table className="w-full min-w-[760px] text-right text-sm"><thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr><th className="p-3">الوقت</th><th className="p-3">الحالة</th><th className="p-3">القرار</th><th className="p-3">المزود</th><th className="p-3">المحاولات</th><th className="p-3">Turn ID</th></tr></thead><tbody>{items.map(entry => <tr key={entry.turnId} className="border-t border-[var(--admin-border)]"><td className="p-3"><time dateTime={entry.at}><bdi>{formatCairoTimestamp(entry.at)}</bdi></time></td><td className="p-3">{entry.status}</td><td className="p-3">{entry.decisionType || entry.failureCode || '—'}</td><td className="p-3"><bdi dir="ltr">{entry.provider || '—'} {entry.model || ''}</bdi></td><td className="p-3">{entry.callbackAttempts}</td><td className="p-3"><code className="break-all text-xs" dir="ltr">{entry.turnId}</code></td></tr>)}</tbody></table></div>}{cursor && <div className="border-t border-[var(--admin-border)] p-4 text-center"><button disabled={state === 'loading'} onClick={() => void load(cursor)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold disabled:opacity-50">تحميل المزيد</button></div>}</section>;
}
