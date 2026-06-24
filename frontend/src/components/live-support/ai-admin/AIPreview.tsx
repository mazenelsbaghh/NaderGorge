'use client';

import { useState } from 'react';
import { FlaskConical, LoaderCircle } from 'lucide-react';
import { getLiveSupportAIError, liveSupportAIService, type AIPreviewResult } from '@/services/live-support-ai-service';

export function AIPreview({ policyVersionId }: { policyVersionId?: string }) {
  const [message, setMessage] = useState('');
  const [preview, setPreview] = useState<AIPreviewResult>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function runPreview() {
    if (!message.trim()) return setError('اكتب رسالة اختبار أولًا.');
    setBusy(true); setError(''); setPreview(undefined);
    try { setPreview(await liveSupportAIService.preview(message.trim(), policyVersionId)); }
    catch (cause) { setError(getLiveSupportAIError(cause, 'تعذر تشغيل المعاينة.')); }
    finally { setBusy(false); }
  }
  return <section className="rounded-2xl border border-slate-200 bg-white p-5" aria-labelledby="preview-title"><h2 id="preview-title" className="flex items-center gap-2 font-bold"><FlaskConical className="size-5 text-cyan-700"/>معاينة بدون تغييرات إنتاجية</h2><p className="mt-1 text-sm text-slate-600">تستخدم نفس مزود وقواعد القرار، ولا تنشئ رسالة أو إجراء أو حسابًا.</p><label className="mt-4 block text-sm font-semibold">رسالة الاختبار<textarea value={message} onChange={event => setMessage(event.target.value)} maxLength={4000} className="mt-1 min-h-28 w-full rounded-xl border border-slate-200 p-3"/></label><button type="button" disabled={busy} onClick={() => void runPreview()} className="mt-3 min-h-11 rounded-xl bg-cyan-700 px-4 font-bold text-white disabled:opacity-50">{busy ? <><LoaderCircle className="ml-2 inline size-4 animate-spin"/>جارٍ المعاينة</> : 'تشغيل المعاينة'}</button>{error && <p role="alert" className="mt-3 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}{preview && <div role="status" className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-950"><strong className="block">لا توجد تغييرات إنتاجية</strong><p className="mt-1">نوع القرار: <bdi dir="ltr">{preview.decision.type}</bdi></p>{preview.decision.messageAr && <p className="mt-2 whitespace-pre-wrap break-words" dir="auto">{preview.decision.messageAr}</p>}<p className="mt-2 text-xs">{preview.provider} / {preview.model} — {preview.latencyMs.toLocaleString('ar-EG')}ms</p><code className="mt-1 block break-all text-[11px]" dir="ltr">{preview.decisionHash}</code></div>}</section>;
}
