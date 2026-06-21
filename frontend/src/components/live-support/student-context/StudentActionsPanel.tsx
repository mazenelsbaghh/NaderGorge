'use client';

import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, LoaderCircle, Play, X } from 'lucide-react';
import { liveSupportService, type LiveSupportActionDefinition } from '@/services/live-support-service';
import { studentActionFields } from './student-action-definitions';

type FieldValue = string | number | boolean;

export function StudentActionsPanel({ conversationId, hasStudent, onCompleted }: { conversationId: string; hasStudent: boolean; onCompleted: () => void }) {
  const [catalog, setCatalog] = useState<LiveSupportActionDefinition[]>([]);
  const [selected, setSelected] = useState<LiveSupportActionDefinition>();
  const [values, setValues] = useState<Record<string, FieldValue>>({});
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState('');
  const available = useMemo(() => catalog.filter((item) => hasStudent ? item.key !== 'student.create-and-link' : item.key === 'student.create-and-link'), [catalog, hasStudent]);
  useEffect(() => { void liveSupportService.getActionCatalog(conversationId).then(setCatalog); }, [conversationId]);

  function choose(action: LiveSupportActionDefinition) { setSelected(action); setConfirming(false); setResult(''); setValues(Object.fromEntries((studentActionFields[action.key] ?? []).map((field) => [field.key, field.type === 'checkbox' ? false : '']))); }
  async function execute() {
    if (!selected) return;
    setBusy(true); setResult('');
    try {
      const payload = Object.fromEntries(Object.entries(values).filter(([, value]) => value !== '').map(([key, value]) => [key, studentActionFields[selected.key]?.find((field) => field.key === key)?.type === 'number' ? Number(value) : value]));
      const response = await liveSupportService.executeStudentAction<Record<string, unknown>, { message: string }>(conversationId, selected.key, crypto.randomUUID(), selected.confirmationVersion, payload);
      setResult(response.message); setConfirming(false); onCompleted();
    } catch (cause) { setResult((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تنفيذ الإجراء.'); }
    finally { setBusy(false); }
  }

  return <section className="rounded-2xl border border-slate-200 bg-white p-3"><h3 className="font-bold text-slate-900">إجراءات الطالب</h3><p className="mt-1 text-xs text-slate-500">كل إجراء يحتاج تأكيدًا ويُسجل باسمك ووقت تنفيذه.</p><div className="mt-3 grid grid-cols-2 gap-2">{available.map((action) => <button key={action.key} onClick={() => choose(action)} className={`rounded-xl border p-2 text-right text-xs font-semibold ${action.danger === 'financial' ? 'border-amber-300 bg-amber-50 text-amber-900' : action.danger === 'high' ? 'border-red-200 bg-red-50 text-red-800' : 'border-slate-200 text-slate-700 hover:border-cyan-600'}`}>{action.labelAr}</button>)}</div>
    {selected && <div className="fixed inset-0 z-[120] grid place-items-center bg-slate-950/60 p-4" onClick={() => setSelected(undefined)}><div role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()} className="max-h-[90dvh] w-full max-w-lg overflow-y-auto rounded-3xl bg-white p-5" dir="rtl"><div className="flex items-center justify-between"><div><h3 className="font-bold">{selected.labelAr}</h3><p className="mt-1 text-xs text-slate-500">{selected.key}</p></div><button onClick={() => setSelected(undefined)} aria-label="إغلاق" className="grid size-10 place-items-center rounded-full hover:bg-slate-100"><X size={18}/></button></div>{!confirming ? <form onSubmit={(event) => { event.preventDefault(); setConfirming(true); }} className="mt-5 space-y-3">{(studentActionFields[selected.key] ?? []).map((field) => <label key={field.key} className={field.type === 'checkbox' ? 'flex items-center gap-2 text-sm' : 'block text-sm'}>{field.type === 'checkbox' ? <><input type="checkbox" checked={Boolean(values[field.key])} onChange={(event) => setValues({ ...values, [field.key]: event.target.checked })} className="size-5"/>{field.label}</> : <>{field.label}{field.type === 'select' ? <select required={field.required} value={String(values[field.key] ?? '')} onChange={(event) => setValues({ ...values, [field.key]: event.target.value })} className="mt-1 h-11 w-full rounded-xl border px-3"><option value="">اختر</option>{field.options?.map((option) => <option key={option}>{option}</option>)}</select> : <input type={field.type === 'datetime' ? 'datetime-local' : field.type} required={field.required} value={String(values[field.key] ?? '')} onChange={(event) => setValues({ ...values, [field.key]: event.target.value })} className="mt-1 h-11 w-full rounded-xl border px-3"/>}</>}</label>)}<button className="h-11 w-full rounded-xl bg-slate-900 font-semibold text-white">مراجعة وتأكيد</button></form> : <div className="mt-5"><div className="rounded-2xl border border-amber-200 bg-amber-50 p-4"><AlertTriangle className="mb-2 text-amber-700"/><h4 className="font-bold">تأكيد التنفيذ</h4><p className="mt-2 text-sm">سيتم تنفيذ «{selected.labelAr}» على الطالب المرتبط وتسجيل العملية كاملة.</p></div><div className="mt-4 flex gap-2"><button onClick={() => setConfirming(false)} className="h-11 flex-1 rounded-xl border">رجوع</button><button disabled={busy} onClick={() => void execute()} className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-red-700 font-semibold text-white">{busy ? <LoaderCircle className="animate-spin" size={17}/> : <Play size={17}/>}تنفيذ</button></div></div>}{result && <p role="status" className="mt-3 rounded-xl bg-slate-100 p-3 text-sm">{result}</p>}</div></div>}
  </section>;
}
