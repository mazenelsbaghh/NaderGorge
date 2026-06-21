'use client';

import { useEffect, useState } from 'react';
import { Bot, LoaderCircle, Save, ShieldAlert } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { useAuthStore } from '@/stores/auth-store';
import { liveSupportAIService, type AICatalogItem, type AIConfig, type SaveAIDraft } from '@/services/live-support-ai-service';

const emptyDraft: SaveAIDraft = {
  systemInstructions: '', readableDataKeys: [], actionKeys: [], lookupKeys: [], verificationQuestionKeys: [],
  verificationRequiredCorrect: 1, verificationMaxAttempts: 3, pendingActionExpirySeconds: 300,
  inactivityMinutes: 30, inactivityWarningGraceSeconds: 120,
};

export default function AdminAISupportPageClient() {
  const user = useAuthStore(state => state.user);
  const [config, setConfig] = useState<AIConfig>();
  const [draft, setDraft] = useState<SaveAIDraft>(emptyDraft);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const isBuiltInAdmin = user?.roles.includes('Admin') === true;

  useEffect(() => {
    if (!isBuiltInAdmin) return;
    void liveSupportAIService.getConfig().then(next => {
      setConfig(next);
      const source = next.draft ?? next.published;
      if (source) setDraft({
        systemInstructions: source.systemInstructions, readableDataKeys: source.readableDataKeys, actionKeys: source.actionKeys,
        lookupKeys: source.lookupKeys, verificationQuestionKeys: source.verificationQuestionKeys,
        verificationRequiredCorrect: source.verificationRequiredCorrect, verificationMaxAttempts: source.verificationMaxAttempts,
        pendingActionExpirySeconds: source.pendingActionExpirySeconds, inactivityMinutes: source.inactivityMinutes,
        inactivityWarningGraceSeconds: source.inactivityWarningGraceSeconds, expectedVersion: next.draft?.version,
      });
    }).catch(() => setNotice('تعذر تحميل إعدادات المساعد الذكي.'));
  }, [isBuiltInAdmin]);

  if (!isBuiltInAdmin) return <div dir="rtl" role="alert" className="m-6 rounded-2xl border border-red-200 bg-red-50 p-5 text-red-900">هذه الإعدادات متاحة لدور Admin الأساسي فقط.</div>;

  async function save() {
    setBusy(true); setNotice('');
    try {
      const saved = await liveSupportAIService.saveDraft(draft);
      setDraft(current => ({ ...current, expectedVersion: saved.version }));
      setConfig(current => current ? { ...current, draft: saved } : current);
      setNotice('تم حفظ المسودة. لن تُفعّل قبل النشر.');
    } catch { setNotice('تعذر حفظ المسودة. راجع القيم أو أعد تحميل الصفحة.'); }
    finally { setBusy(false); }
  }

  async function publish() {
    if (!config?.draft) return;
    setBusy(true);
    try { const published = await liveSupportAIService.publish(config.draft.version); setConfig(current => current ? { ...current, draft: undefined, published } : current); setNotice('تم نشر الإعدادات وتفعيل المساعد الذكي.'); }
    catch { setNotice('تعذر النشر. احفظ أحدث مسودة أولًا.'); }
    finally { setBusy(false); }
  }

  async function disable() {
    if (!window.confirm('إيقاف المساعد وتحويل المحادثات النشطة للدعم البشري؟')) return;
    setBusy(true);
    try { await liveSupportAIService.disable(); setConfig(current => current?.published ? { ...current, published: { ...current.published, isEnabled: false } } : current); setNotice('تم إيقاف المساعد وبدء تحويل المحادثات.'); }
    catch { setNotice('تعذر إيقاف المساعد.'); }
    finally { setBusy(false); }
  }

  return <AdminShellChrome activePath="/admin/live-support/ai" sectionLabel="خدمة العملاء" pageTitle="المساعد الذكي للدعم" subtitle="حدد بدقة ما يستطيع المساعد قراءته واقتراحه. كل إجراء مؤثر يحتاج تأكيد صاحب المحادثة.">
    {!config ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin" aria-label="جارٍ التحميل" /></div> : <div dir="rtl" className="space-y-5">
      {notice && <div role="status" className="rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-800">{notice}</div>}
      <section className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border border-slate-200 bg-white p-5">
        <div className="flex items-center gap-3"><span className="grid size-12 place-items-center rounded-2xl bg-cyan-50 text-cyan-800"><Bot /></span><div><h2 className="font-bold text-slate-950">حالة المساعد</h2><p className="text-sm text-slate-500">{config.published?.isEnabled ? `مفعّل — الإصدار ${config.published.versionNumber}` : 'متوقف'}</p></div></div>
        {config.published?.isEnabled && <button type="button" disabled={busy} onClick={() => void disable()} className="min-h-11 rounded-xl border border-red-200 px-4 font-bold text-red-700 disabled:opacity-50"><ShieldAlert className="ml-2 inline size-4"/>إيقاف وتحويل للبشر</button>}
      </section>
      <section className="rounded-3xl border border-slate-200 bg-white p-5"><h2 className="font-bold text-slate-950">تعليمات المساعد وقاعدة القرار</h2><p className="mt-1 text-sm text-slate-500">لا تضع كلمات مرور أو مفاتيح أو بيانات سرية هنا.</p><textarea value={draft.systemInstructions} maxLength={20000} onChange={event => setDraft({ ...draft, systemInstructions: event.target.value })} className="mt-4 min-h-44 w-full rounded-2xl border border-slate-200 p-4 leading-7 outline-none focus:border-cyan-700" placeholder="اكتب أسلوب الرد، حدود المساعدة، ومتى يجب التحويل لموظف..." /></section>
      <CatalogSection title="البيانات التي يمكن قراءتها" items={config.catalogs.readableData} selected={draft.readableDataKeys} change={keys => setDraft({ ...draft, readableDataKeys: keys })}/>
      <CatalogSection title="الإجراءات التي يمكن اقتراحها" note="لا يُنفذ أي إجراء إلا بعد عرض أثره والحصول على تأكيد صريح." items={config.catalogs.actions} selected={draft.actionKeys} change={keys => setDraft({ ...draft, actionKeys: keys })}/>
      <CatalogSection title="مفاتيح البحث الآمنة" note="تُقبل القيمة الكاملة فقط ولا تظهر نتائج أو تلميحات." items={config.catalogs.lookupKeys} selected={draft.lookupKeys} change={keys => setDraft({ ...draft, lookupKeys: keys })}/>
      <CatalogSection title="أسئلة التحقق" note="التحقق للمحادثة الحالية فقط ولا تُحفظ الإجابات الخام." items={config.catalogs.verificationQuestions} selected={draft.verificationQuestionKeys} change={keys => setDraft({ ...draft, verificationQuestionKeys: keys })}/>
      <section className="grid gap-4 rounded-3xl border border-slate-200 bg-white p-5 sm:grid-cols-2 lg:grid-cols-5">
        <NumberField label="الإجابات الصحيحة" value={draft.verificationRequiredCorrect} min={1} max={Math.max(1, draft.verificationQuestionKeys.length)} change={value => setDraft({ ...draft, verificationRequiredCorrect: value })}/>
        <NumberField label="محاولات التحقق" value={draft.verificationMaxAttempts} min={1} max={10} change={value => setDraft({ ...draft, verificationMaxAttempts: value })}/>
        <NumberField label="انتهاء التأكيد (ث)" value={draft.pendingActionExpirySeconds} min={30} max={900} change={value => setDraft({ ...draft, pendingActionExpirySeconds: value })}/>
        <NumberField label="الخمول (دقيقة)" value={draft.inactivityMinutes} min={5} max={1440} change={value => setDraft({ ...draft, inactivityMinutes: value })}/>
        <NumberField label="مهلة التنبيه (ث)" value={draft.inactivityWarningGraceSeconds} min={30} max={600} change={value => setDraft({ ...draft, inactivityWarningGraceSeconds: value })}/>
      </section>
      <div className="sticky bottom-4 flex flex-wrap justify-end gap-3 rounded-2xl border border-slate-200 bg-white/95 p-4 shadow-lg"><button type="button" disabled={busy} onClick={() => void save()} className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold disabled:opacity-50"><Save className="ml-2 inline size-4"/>حفظ مسودة</button><button type="button" disabled={busy || !config.draft} onClick={() => void publish()} className="min-h-11 rounded-xl bg-slate-900 px-5 font-bold text-white disabled:opacity-40">نشر وتفعيل</button></div>
    </div>}
  </AdminShellChrome>;
}

function CatalogSection({ title, note, items, selected, change }: { title: string; note?: string; items: AICatalogItem[]; selected: string[]; change: (keys: string[]) => void }) {
  return <section className="rounded-3xl border border-slate-200 bg-white p-5"><h2 className="font-bold text-slate-950">{title}</h2>{note && <p className="mt-1 text-sm text-slate-500">{note}</p>}<div className="mt-4 grid gap-2 md:grid-cols-2 xl:grid-cols-3">{items.map(item => <label key={item.key} className="flex min-h-12 cursor-pointer items-center gap-3 rounded-xl border border-slate-200 px-3 text-sm"><input type="checkbox" className="size-5 accent-cyan-700" checked={selected.includes(item.key)} onChange={event => change(event.target.checked ? [...selected, item.key] : selected.filter(key => key !== item.key))}/><span>{item.label}</span></label>)}</div></section>;
}

function NumberField({ label, value, min, max, change }: { label: string; value: number; min: number; max: number; change: (value: number) => void }) {
  return <label className="text-sm font-semibold text-slate-700">{label}<input type="number" value={value} min={min} max={max} onChange={event => change(Number(event.target.value))} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3"/></label>;
}
