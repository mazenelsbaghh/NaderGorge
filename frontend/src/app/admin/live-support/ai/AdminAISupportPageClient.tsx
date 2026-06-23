'use client';

import { useEffect, useState } from 'react';
import { isAxiosError } from 'axios';
import { Bot, LoaderCircle, Save, ShieldAlert } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { useAuthStore } from '@/stores/auth-store';
import {
  liveSupportAIService,
  type AICatalogItem,
  type AIConfig,
  type AIPolicy,
  type SaveAIDraft,
} from '@/services/live-support-ai-service';

export const DEFAULT_SYSTEM_INSTRUCTIONS = `أنت مساعد الدعم الذكي لمنصة مسار، وهي منصة تعليمية عربية تساعد الطلاب على مشاهدة الدروس، حل الامتحانات والواجبات، متابعة التقدم، وإدارة الباقات وطلبات المشاهدة.

قواعد الرد:
1. عرّف نفسك بوضوح كمساعد آلي، وتحدث بالعربية المصرية المهذبة والواضحة، واستخدم جملًا قصيرة ومباشرة.
2. اعتمد فقط على التعليمات المنشورة، وقاعدة المعرفة، والبيانات التي سمح مدير النظام بقراءتها. لا تخمّن معلومة ولا تعد الطالب بشيء غير مؤكد.
3. اشرح للطالب وضع حسابه أو دراسته بلغة بسيطة، واذكر الخطوة التالية التي يستطيع تنفيذها.
4. لا تطلب أو تعرض كلمات المرور، رموز الدخول، مفاتيح الخدمة، بيانات الدفع السرية، أو أي بيانات غير لازمة لحل المشكلة.
5. لا تكشف بيانات حساب أو تؤكد وجوده قبل إتمام التحقق المطلوب. استخدم البحث بالقيمة الكاملة فقط، ولا تعرض نتائج جزئية أو تلميحات.
6. قبل أي إجراء مؤثر، اذكر اسم الإجراء والهدف والأثر والنتيجة المتوقعة، ثم اطلب تأكيدًا صريحًا من صاحب المحادثة. لا تعتبر السكوت أو الرد المبهم موافقة.
7. نفّذ فقط الإجراءات المسموح بها في الإعدادات وعلى حساب الطالب المرتبط بالمحادثة. لا تغيّر إعدادات المنصة أو الصلاحيات العامة ولا تتعامل مع حساب آخر.
8. إذا لم تتوفر معلومة موثوقة، أو لم توجد صلاحية كافية، أو فشل التحقق، أو طلب المستخدم موظفًا، فاشرح السبب باختصار وحوّل المحادثة إلى الدعم البشري دون متابعة الرد الآلي.
9. عند حدوث خطأ تقني، لا تدّعي نجاح العملية. اذكر أن التنفيذ لم يكتمل وحوّل للدعم عند الحاجة.
10. اختم كل رد بخطوة عملية واضحة أو سؤال واحد ضروري لاستكمال الحل، وتجنب الرسائل الطويلة والتكرار.`;

const emptyDraft: SaveAIDraft = {
  systemInstructions: DEFAULT_SYSTEM_INSTRUCTIONS,
  readableDataKeys: [],
  actionKeys: [],
  lookupKeys: [],
  verificationQuestionKeys: [],
  verificationRequiredCorrect: 2,
  verificationMaxAttempts: 3,
  pendingActionExpirySeconds: 300,
  inactivityMinutes: 30,
  inactivityWarningGraceSeconds: 120,
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
    void liveSupportAIService.getConfig().then(nextConfig => {
      setConfig(nextConfig);
      const savedPolicy = nextConfig.draft ?? nextConfig.published;
      setDraft(savedPolicy ? draftFromPolicy(savedPolicy, nextConfig.draft?.version) : defaultDraft(nextConfig));
    }).catch(error => setNotice(apiErrorMessage(error, 'تعذر تحميل إعدادات المساعد الذكي.')));
  }, [isBuiltInAdmin]);

  if (!isBuiltInAdmin) {
    return <div dir="rtl" role="alert" className="m-6 rounded-2xl border border-red-200 bg-red-50 p-5 text-red-900">هذه الإعدادات متاحة لمدير النظام الأساسي فقط.</div>;
  }

  function validateCurrentDraft() {
    if (!draft.systemInstructions.trim()) return 'تعليمات المساعد مطلوبة قبل الحفظ.';
    if (draft.verificationQuestionKeys.length === 0) return 'اختر سؤال تحقق واحدًا على الأقل.';
    if (draft.verificationRequiredCorrect > draft.verificationQuestionKeys.length) return 'عدد الإجابات الصحيحة أكبر من عدد أسئلة التحقق المختارة.';
    return '';
  }

  async function save() {
    const validationMessage = validateCurrentDraft();
    if (validationMessage) return setNotice(validationMessage);
    setBusy(true);
    setNotice('');
    try {
      const savedDraft = await liveSupportAIService.saveDraft(draft);
      setDraft(current => ({ ...current, expectedVersion: savedDraft.version }));
      setConfig(current => current ? { ...current, draft: savedDraft } : current);
      setNotice('تم حفظ المسودة. لن يعمل المساعد بهذه التعديلات قبل النشر.');
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر حفظ المسودة. راجع القيم ثم حاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  }

  async function saveAndPublish() {
    const validationMessage = validateCurrentDraft();
    if (validationMessage) return setNotice(validationMessage);
    setBusy(true);
    setNotice('');
    try {
      const savedDraft = await liveSupportAIService.saveDraft(draft);
      setDraft(current => ({ ...current, expectedVersion: savedDraft.version }));
      setConfig(current => current ? { ...current, draft: savedDraft } : current);
      const publishedPolicy = await liveSupportAIService.publish(savedDraft.version);
      setDraft(draftFromPolicy(publishedPolicy));
      setConfig(current => current ? { ...current, draft: undefined, published: publishedPolicy } : current);
      setNotice('تم حفظ الإعدادات ونشرها، والمساعد الذكي مفعّل الآن.');
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر النشر. أعد تحميل الصفحة ثم حاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  }

  async function disable() {
    if (!window.confirm('هل تريد إيقاف المساعد وتحويل المحادثات النشطة إلى الدعم البشري؟')) return;
    setBusy(true);
    try {
      await liveSupportAIService.disable();
      setConfig(current => current?.published ? { ...current, published: { ...current.published, isEnabled: false } } : current);
      setNotice('تم إيقاف المساعد وبدء تحويل المحادثات إلى الدعم البشري.');
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر إيقاف المساعد.'));
    } finally {
      setBusy(false);
    }
  }

  return <AdminShellChrome activePath="/admin/live-support/ai" sectionLabel="خدمة العملاء" pageTitle="المساعد الذكي للدعم" subtitle="حدّد ما يستطيع المساعد قراءته واقتراحه. كل إجراء مؤثر يحتاج إلى تأكيد صاحب المحادثة.">
    {!config ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin" aria-label="جارٍ التحميل" /></div> : <div dir="rtl" className="space-y-5">
      {notice && <div role="status" aria-live="polite" className="rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-800">{notice}</div>}
      <section className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border border-slate-200 bg-white p-5">
        <div className="flex items-center gap-3"><span className="grid size-12 place-items-center rounded-2xl bg-cyan-50 text-cyan-800"><Bot /></span><div><h2 className="font-bold text-slate-950">حالة المساعد</h2><p className="text-sm text-slate-600">{config.published?.isEnabled ? `مفعّل، الإصدار ${config.published.versionNumber}` : 'متوقف'}</p></div></div>
        {config.published?.isEnabled && <button type="button" disabled={busy} onClick={() => void disable()} className="min-h-11 rounded-xl border border-red-200 px-4 font-bold text-red-700 disabled:opacity-50"><ShieldAlert className="ml-2 inline size-4"/>إيقاف وتحويل للدعم البشري</button>}
      </section>
      <section className="rounded-3xl border border-slate-200 bg-white p-5"><div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="font-bold text-slate-950">تعليمات المساعد وقاعدة القرار</h2><p className="mt-1 text-sm text-slate-600">أضفنا تعليمات افتراضية لمنصة مسار. يمكنك تعديلها، ولا تضع هنا كلمات مرور أو بيانات سرية.</p></div><button type="button" onClick={() => setDraft(current => ({ ...current, systemInstructions: DEFAULT_SYSTEM_INSTRUCTIONS }))} className="min-h-11 rounded-xl border border-cyan-700 px-4 text-sm font-bold text-cyan-800 hover:bg-cyan-50 focus:outline-none focus:ring-2 focus:ring-cyan-700/30">استعادة التعليمات الافتراضية</button></div><textarea value={draft.systemInstructions} maxLength={20000} onChange={event => setDraft({ ...draft, systemInstructions: event.target.value })} className="mt-4 min-h-80 w-full rounded-2xl border border-slate-200 p-4 leading-7 text-slate-900 outline-none focus:border-cyan-700 focus:ring-2 focus:ring-cyan-700/20" placeholder="اكتب أسلوب الرد، حدود المساعدة، ومتى يجب التحويل إلى موظف..."/><p className="mt-2 text-left text-xs text-slate-600">{draft.systemInstructions.length.toLocaleString('ar-EG')} من ٢٠٬٠٠٠ حرف</p></section>
      <CatalogSection title="البيانات التي يمكن قراءتها" items={config.catalogs.readableData} selected={draft.readableDataKeys} change={keys => setDraft({ ...draft, readableDataKeys: keys })}/>
      <CatalogSection title="الإجراءات التي يمكن اقتراحها" note="لا يُنفّذ أي إجراء إلا بعد عرض أثره والحصول على تأكيد صريح من صاحب المحادثة." items={config.catalogs.actions} selected={draft.actionKeys} change={keys => setDraft({ ...draft, actionKeys: keys })}/>
      <CatalogSection title="طرق البحث الآمنة" note="تُقبل القيمة الكاملة فقط، ولا تظهر نتائج جزئية أو تلميحات." items={config.catalogs.lookupKeys} selected={draft.lookupKeys} change={keys => setDraft({ ...draft, lookupKeys: keys })}/>
      <CatalogSection title="أسئلة التحقق من الحساب" note="التحقق للمحادثة الحالية فقط، ولا تُحفظ الإجابات الخام." items={config.catalogs.verificationQuestions} selected={draft.verificationQuestionKeys} change={keys => setDraft({ ...draft, verificationQuestionKeys: keys })}/>
      <section className="grid gap-4 rounded-3xl border border-slate-200 bg-white p-5 sm:grid-cols-2 lg:grid-cols-5">
        <NumberField label="الإجابات الصحيحة المطلوبة" value={draft.verificationRequiredCorrect} min={1} max={Math.max(1, draft.verificationQuestionKeys.length)} change={value => setDraft({ ...draft, verificationRequiredCorrect: value })}/>
        <NumberField label="الحد الأقصى لمحاولات التحقق" value={draft.verificationMaxAttempts} min={1} max={10} change={value => setDraft({ ...draft, verificationMaxAttempts: value })}/>
        <NumberField label="صلاحية تأكيد الإجراء بالثواني" value={draft.pendingActionExpirySeconds} min={30} max={900} change={value => setDraft({ ...draft, pendingActionExpirySeconds: value })}/>
        <NumberField label="مدة الخمول بالدقائق" value={draft.inactivityMinutes} min={5} max={1440} change={value => setDraft({ ...draft, inactivityMinutes: value })}/>
        <NumberField label="مهلة تنبيه الخمول بالثواني" value={draft.inactivityWarningGraceSeconds} min={30} max={600} change={value => setDraft({ ...draft, inactivityWarningGraceSeconds: value })}/>
      </section>
      <div className="sticky bottom-4 flex flex-wrap justify-end gap-3 rounded-2xl border border-slate-200 bg-white/95 p-4 shadow-lg"><button type="button" disabled={busy} onClick={() => void save()} className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold text-slate-900 disabled:opacity-50"><Save className="ml-2 inline size-4"/>{busy ? 'جارٍ التنفيذ...' : 'حفظ كمسودة'}</button><button type="button" disabled={busy} onClick={() => void saveAndPublish()} className="min-h-11 rounded-xl bg-slate-900 px-5 font-bold text-white disabled:opacity-40">{busy ? 'جارٍ التنفيذ...' : 'حفظ ونشر وتفعيل'}</button></div>
    </div>}
  </AdminShellChrome>;
}

function defaultDraft(config: AIConfig): SaveAIDraft {
  return {
    ...emptyDraft,
    readableDataKeys: config.catalogs.readableData.map(catalogItem => catalogItem.key),
    actionKeys: config.catalogs.actions.map(catalogItem => catalogItem.key),
    lookupKeys: config.catalogs.lookupKeys.map(catalogItem => catalogItem.key),
    verificationQuestionKeys: config.catalogs.verificationQuestions.map(catalogItem => catalogItem.key),
  };
}

function draftFromPolicy(policy: AIPolicy, expectedVersion?: number): SaveAIDraft {
  return {
    systemInstructions: policy.systemInstructions,
    readableDataKeys: policy.readableDataKeys,
    actionKeys: policy.actionKeys,
    lookupKeys: policy.lookupKeys,
    verificationQuestionKeys: policy.verificationQuestionKeys,
    verificationRequiredCorrect: policy.verificationRequiredCorrect,
    verificationMaxAttempts: policy.verificationMaxAttempts,
    pendingActionExpirySeconds: policy.pendingActionExpirySeconds,
    inactivityMinutes: policy.inactivityMinutes,
    inactivityWarningGraceSeconds: policy.inactivityWarningGraceSeconds,
    expectedVersion,
  };
}

function apiErrorMessage(error: unknown, fallback: string) {
  return isAxiosError<{ message?: string }>(error) ? error.response?.data?.message ?? fallback : fallback;
}

function CatalogSection({ title, note, items, selected, change }: { title: string; note?: string; items: AICatalogItem[]; selected: string[]; change: (keys: string[]) => void }) {
  return <section className="rounded-3xl border border-slate-200 bg-white p-5"><h2 className="font-bold text-slate-950">{title}</h2>{note && <p className="mt-1 text-sm text-slate-600">{note}</p>}<div className="mt-4 grid gap-2 md:grid-cols-2 xl:grid-cols-3">{items.map(catalogItem => <label key={catalogItem.key} className="flex min-h-16 cursor-pointer items-start gap-3 rounded-xl border border-slate-200 px-3 py-3 text-sm transition-colors hover:bg-slate-50 focus-within:border-cyan-700 focus-within:ring-2 focus-within:ring-cyan-700/20"><input type="checkbox" className="mt-0.5 size-5 shrink-0 accent-cyan-700" checked={selected.includes(catalogItem.key)} onChange={event => change(event.target.checked ? [...selected, catalogItem.key] : selected.filter(selectedKey => selectedKey !== catalogItem.key))}/><span><span className="block font-semibold text-slate-900">{catalogItem.label}</span><span className="mt-1 block text-xs leading-5 text-slate-600">{catalogItem.description}</span></span></label>)}</div></section>;
}

function NumberField({ label, value, min, max, change }: { label: string; value: number; min: number; max: number; change: (value: number) => void }) {
  return <label className="text-sm font-semibold text-slate-700">{label}<input type="number" value={value} min={min} max={max} onChange={event => change(Number(event.target.value))} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-700 focus:ring-2 focus:ring-cyan-700/20"/></label>;
}
