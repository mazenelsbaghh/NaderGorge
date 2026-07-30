'use client';

import { useEffect, useState } from 'react';
import { BookOpen, LoaderCircle, Plus } from 'lucide-react';
import { getLiveSupportAIError, liveSupportAIService, type AIKnowledgeRevision } from '@/services/live-support-ai-service';
import { LiveSupportEmptyState } from '../shared/LiveSupportEmptyState';

export function AIKnowledgeManager({ policyVersionId }: { policyVersionId?: string }) {
  const [revisions, setRevisions] = useState<AIKnowledgeRevision[]>([]);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'saving' | 'error'>('loading');
  const [notice, setNotice] = useState('');

  async function load() {
    setState('loading');
    try { setRevisions(await liveSupportAIService.getKnowledge()); setState('ready'); }
    catch (error) { setNotice(getLiveSupportAIError(error, 'تعذر تحميل قاعدة المعرفة.')); setState('error'); }
  }
  useEffect(() => { void load(); }, []);

  async function createRevision() {
    if (!title.trim() || !content.trim()) return setNotice('العنوان والمحتوى مطلوبان.');
    setState('saving');
    try {
      const revision = await liveSupportAIService.saveKnowledgeRevision({ title: title.trim(), content: content.trim(), publish: true });
      setRevisions(current => [revision, ...current]); setTitle(''); setContent(''); setNotice('تم نشر مراجعة معرفة جديدة.'); setState('ready');
    } catch (error) { setNotice(getLiveSupportAIError(error, 'تعذر نشر مصدر المعرفة.')); setState('error'); }
  }

  async function linkSelected() {
    if (!policyVersionId) return setNotice('انشر سياسة أولًا قبل ربط المعرفة.');
    setState('saving');
    try { await liveSupportAIService.linkKnowledge(policyVersionId, selected); setNotice('تم ربط المصادر بالسياسة المنشورة.'); setState('ready'); }
    catch (error) { setNotice(getLiveSupportAIError(error, 'تعذر ربط مصادر المعرفة.')); setState('error'); }
  }

  return <section className="space-y-4" aria-labelledby="knowledge-title"><div><h2 id="knowledge-title" className="flex items-center gap-2 font-bold text-slate-950"><BookOpen className="size-5 text-cyan-700"/>قاعدة المعرفة</h2><p className="text-sm text-slate-600">كل تعديل ينشئ مراجعة مستقلة قابلة للتدقيق.</p></div>{notice && <p role="status" className={`rounded-xl p-3 text-sm ${state === 'error' ? 'bg-red-50 text-red-700' : 'bg-cyan-50 text-cyan-900'}`}>{notice}</p>}<div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(280px,420px)]"><div className="rounded-2xl border border-slate-200 bg-white p-4">{state === 'loading' ? <LoaderCircle className="animate-spin" aria-label="جارٍ التحميل"/> : revisions.length === 0 ? <LiveSupportEmptyState title="قاعدة المعرفة فارغة" description="أضف أول مصدر منشور للمساعد."/> : <div className="space-y-2">{revisions.map(revision => <label key={revision.revisionId} className="flex gap-3 rounded-xl border border-slate-200 p-3"><input type="checkbox" checked={selected.includes(revision.revisionId)} onChange={event => setSelected(current => event.target.checked ? [...current, revision.revisionId] : current.filter(id => id !== revision.revisionId))}/><span><strong className="block text-sm">{revision.title}</strong><span className="text-xs text-slate-500">مراجعة {revision.revisionNumber} — {revision.isPublished ? 'منشورة' : 'مسودة'}</span></span></label>)}</div>}<button type="button" disabled={state === 'saving' || selected.length === 0} onClick={() => void linkSelected()} className="mt-4 min-h-11 rounded-xl bg-slate-900 px-4 font-bold text-white disabled:opacity-40">ربط المحدد بالسياسة</button></div><div className="rounded-2xl border border-slate-200 bg-white p-4"><h3 className="font-bold">مراجعة جديدة</h3><label className="mt-3 block text-sm font-semibold">العنوان<input value={title} onChange={event => setTitle(event.target.value)} className="mt-1 h-11 w-full rounded-xl border border-slate-200 px-3"/></label><label className="mt-3 block text-sm font-semibold">المحتوى<textarea value={content} onChange={event => setContent(event.target.value)} className="mt-1 min-h-40 w-full rounded-xl border border-slate-200 p-3"/></label><button type="button" disabled={state === 'saving'} onClick={() => void createRevision()} className="mt-3 min-h-11 rounded-xl bg-cyan-700 px-4 font-bold text-white disabled:opacity-50"><Plus className="ml-2 inline size-4"/>نشر المراجعة</button></div></div></section>;
}
