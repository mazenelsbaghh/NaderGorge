'use client';

import { useEffect, useState } from 'react';
import { isAxiosError } from 'axios';
import { LoaderCircle, Save } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { useAuthStore } from '@/stores/auth-store';
import {
  liveSupportAIService,
  type AIConfig,
  type AIPolicy,
  type SaveAIDraft,
  type AIStats,
  type AIStatsPeriod,
} from '@/services/live-support-ai-service';
import { ConversationInvestigation } from '@/components/live-support/admin/ConversationInvestigation';
import { liveSupportService, type LiveSupportConversationTimeline, type LiveSupportAdminConversation } from '@/services/live-support-service';
import { AIOverview } from '@/components/live-support/ai-admin/AIOverview';
import { AIDisableControl } from '@/components/live-support/ai-admin/AIDisableControl';
import { AIPolicyEditor } from '@/components/live-support/ai-admin/AIPolicyEditor';
import { AIKnowledgeManager } from '@/components/live-support/ai-admin/AIKnowledgeManager';
import { AIDataActionSelector } from '@/components/live-support/ai-admin/AIDataActionSelector';
import { AIVerificationPolicyEditor } from '@/components/live-support/ai-admin/AIVerificationPolicyEditor';
import { AIPreview } from '@/components/live-support/ai-admin/AIPreview';
import { AIActivityEvidence } from '@/components/live-support/ai-admin/AIActivityEvidence';

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
  const [activeTab, setActiveTab] = useState<'settings' | 'knowledge' | 'activity'>('settings');
  const [stats, setStats] = useState<AIStats>();
  const [statsPeriod, setStatsPeriod] = useState<AIStatsPeriod>('last-24h');
  const [loadingStats, setLoadingStats] = useState(false);
  const [activeConversations, setActiveConversations] = useState<LiveSupportAdminConversation[]>([]);
  const [loadingActiveConversations, setLoadingActiveConversations] = useState(false);
  const [timeline, setTimeline] = useState<LiveSupportConversationTimeline>();
  const isBuiltInAdmin = user?.roles.includes('Admin') === true;

  useEffect(() => {
    if (!isBuiltInAdmin) return;
    void liveSupportAIService.getConfig().then(nextConfig => {
      setConfig(nextConfig);
      const savedPolicy = nextConfig.draft ?? nextConfig.published;
      setDraft(savedPolicy ? draftFromPolicy(savedPolicy, nextConfig.draft?.version) : defaultDraft(nextConfig));
    }).catch(error => setNotice(apiErrorMessage(error, 'تعذر تحميل إعدادات المساعد الذكي.')));
  }, [isBuiltInAdmin]);

  async function loadStats(period: AIStatsPeriod) {
    setLoadingStats(true);
    try {
      const nextStats = await liveSupportAIService.getStats(period);
      setStats(nextStats);
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر تحميل الإحصائيات.'));
    } finally {
      setLoadingStats(false);
    }
  }

  async function loadActiveConversations() {
    setLoadingActiveConversations(true);
    try {
      const list = await liveSupportAIService.getActiveConversations();
      setActiveConversations(list);
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر تحميل المحادثات النشطة.'));
    } finally {
      setLoadingActiveConversations(false);
    }
  }

  useEffect(() => {
    if (activeTab === 'activity' && isBuiltInAdmin) {
      void loadStats(statsPeriod);
    }
  }, [activeTab, statsPeriod, isBuiltInAdmin]);

  useEffect(() => {
    if (activeTab === 'activity' && isBuiltInAdmin) {
      void loadActiveConversations();
      const interval = window.setInterval(() => {
        void liveSupportAIService.getActiveConversations().then(setActiveConversations).catch(() => undefined);
      }, 10_000);
      return () => window.clearInterval(interval);
    }
  }, [activeTab, isBuiltInAdmin]);

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
      if (!config?.published) return;
      await liveSupportAIService.disable(config.published.version);
      setConfig(current => current?.published ? { ...current, published: { ...current.published, isEnabled: false } } : current);
      setNotice('تم إيقاف المساعد وبدء تحويل المحادثات إلى الدعم البشري.');
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر إيقاف المساعد.'));
    } finally {
      setBusy(false);
    }
  }

  async function enable() {
    setBusy(true);
    setNotice('');
    try {
      if (!config?.published) return;
      const updatedPolicy = await liveSupportAIService.enable(config.published.version);
      setConfig(current => current ? { ...current, published: updatedPolicy } : current);
      setNotice('تم تشغيل وتفعيل المساعد الذكي بنجاح.');
    } catch (error) {
      setNotice(apiErrorMessage(error, 'تعذر تفعيل المساعد.'));
    } finally {
      setBusy(false);
    }
  }

  return <AdminShellChrome activePath="/admin/live-support/ai" sectionLabel="خدمة العملاء" pageTitle="المساعد الذكي للدعم" subtitle="حدّد ما يستطيع المساعد قراءته واقتراحه. كل إجراء مؤثر يحتاج إلى تأكيد صاحب المحادثة.">
    {!config ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin" aria-label="جارٍ التحميل" /></div> : <div dir="rtl" className="space-y-5">
      {notice && <div role="status" aria-live="polite" className="rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-800">{notice}</div>}
      
      <div className="flex overflow-x-auto border-b border-slate-200" role="tablist" aria-label="إدارة المساعد الذكي">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'settings'}
          onClick={() => setActiveTab('settings')}
          className={`px-6 py-3 font-semibold text-sm transition-colors border-b-2 -mb-px ${
            activeTab === 'settings'
              ? 'border-cyan-700 text-cyan-800'
              : 'border-transparent text-slate-500 hover:text-slate-900'
          }`}
        >
          الإعدادات وقاعدة القرار
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'knowledge'}
          onClick={() => setActiveTab('knowledge')}
          className={`px-6 py-3 font-semibold text-sm transition-colors border-b-2 -mb-px ${
            activeTab === 'knowledge'
              ? 'border-cyan-700 text-cyan-800'
              : 'border-transparent text-slate-500 hover:text-slate-900'
          }`}
        >
          المعرفة والمعاينة
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'activity'}
          onClick={() => setActiveTab('activity')}
          className={`px-6 py-3 font-semibold text-sm transition-colors border-b-2 -mb-px ${activeTab === 'activity' ? 'border-cyan-700 text-cyan-800' : 'border-transparent text-slate-500 hover:text-slate-900'}`}
        >
          الإحصائيات والأدلة
        </button>
      </div>

      {activeTab === 'settings' ? (
        <>
          <AIDisableControl policy={config.published} busy={busy} onDisable={() => void disable()} onEnable={() => void enable()} />
          <AIPolicyEditor draft={draft} onChange={setDraft} onRestore={() => setDraft(current => ({ ...current, systemInstructions: DEFAULT_SYSTEM_INSTRUCTIONS }))} />
          <AIDataActionSelector title="البيانات التي يمكن قراءتها" items={config.catalogs.readableData} selected={draft.readableDataKeys} onChange={keys => setDraft({ ...draft, readableDataKeys: keys })}/>
          <AIDataActionSelector title="الإجراءات التي يمكن اقتراحها" note="لا يُنفّذ أي إجراء إلا بعد تأكيد صريح." items={config.catalogs.actions} selected={draft.actionKeys} onChange={keys => setDraft({ ...draft, actionKeys: keys })}/>
          <AIVerificationPolicyEditor draft={draft} questions={config.catalogs.verificationQuestions} lookupKeys={config.catalogs.lookupKeys} onChange={setDraft}/>
          <section className="grid gap-4 rounded-3xl border border-slate-200 bg-white p-5 sm:grid-cols-2 lg:grid-cols-5">
            <NumberField label="الإجابات الصحيحة المطلوبة" value={draft.verificationRequiredCorrect} min={1} max={Math.max(1, draft.verificationQuestionKeys.length)} change={value => setDraft({ ...draft, verificationRequiredCorrect: value })}/>
            <NumberField label="الحد الأقصى لمحاولات التحقق" value={draft.verificationMaxAttempts} min={1} max={10} change={value => setDraft({ ...draft, verificationMaxAttempts: value })}/>
            <NumberField label="صلاحية تأكيد الإجراء بالثواني" value={draft.pendingActionExpirySeconds} min={30} max={900} change={value => setDraft({ ...draft, pendingActionExpirySeconds: value })}/>
            <NumberField label="مدة الخمول بالدقائق" value={draft.inactivityMinutes} min={5} max={1440} change={value => setDraft({ ...draft, inactivityMinutes: value })}/>
            <NumberField label="مهلة تنبيه الخمول بالثواني" value={draft.inactivityWarningGraceSeconds} min={30} max={600} change={value => setDraft({ ...draft, inactivityWarningGraceSeconds: value })}/>
          </section>
          <div className="sticky bottom-4 flex flex-wrap justify-end gap-3 rounded-2xl border border-slate-200 bg-white/95 p-4 shadow-lg"><button type="button" disabled={busy} onClick={() => void save()} className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold text-slate-900 disabled:opacity-50"><Save className="ml-2 inline size-4"/>{busy ? 'جارٍ التنفيذ...' : 'حفظ كمسودة'}</button><button type="button" disabled={busy} onClick={() => void saveAndPublish()} className="min-h-11 rounded-xl bg-slate-900 px-5 font-bold text-white disabled:opacity-40">{busy ? 'جارٍ التنفيذ...' : 'حفظ ونشر وتفعيل'}</button></div>
        </>
      ) : activeTab === 'knowledge' ? (
        <div className="space-y-5" role="tabpanel">
          <AIKnowledgeManager policyVersionId={config.published?.id}/>
          <AIPreview policyVersionId={config.published?.id}/>
        </div>
      ) : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <h2 className="text-lg font-bold text-slate-950">إحصائيات أداء المساعد الذكي</h2>
              <p className="text-sm text-slate-600 mt-1">تتبع مدى فاعلية المساعد الذكي ونسب حل المشاكل أو تحويلها.</p>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-slate-700">الفترة الزمنية:</span>
              <select
                value={statsPeriod}
                onChange={e => setStatsPeriod(e.target.value as AIStatsPeriod)}
                className="h-11 rounded-xl border border-slate-200 bg-white px-3 font-semibold text-slate-800 outline-none focus:border-cyan-700 focus:ring-2 focus:ring-cyan-700/20"
              >
                <option value="last-24h">آخر 24 ساعة</option>
                <option value="last-7d">آخر 7 أيام</option>
                <option value="last-30d">آخر 30 يوم</option>
                <option value="all">كل الأوقات</option>
              </select>
            </div>
          </div>

          <AIOverview policy={config.published} stats={stats} loading={loadingStats}/>
          <AIActivityEvidence period={statsPeriod}/>

          <section className="overflow-hidden rounded-3xl border border-slate-200 bg-white mt-5">
            <div className="border-b border-slate-100 p-5">
              <h3 className="font-bold text-slate-900">المحادثات النشطة مع المساعد الذكي حالياً</h3>
              <p className="mt-1 text-sm text-slate-500">تتحدث هذه القائمة تلقائياً كل 10 ثوانٍ. افتح أي محادثة لمتابعة الردود.</p>
            </div>
            {loadingActiveConversations ? (
              <div className="grid min-h-40 place-items-center"><LoaderCircle className="animate-spin text-cyan-750" /></div>
            ) : activeConversations.length === 0 ? (
              <div className="p-8 text-center text-sm text-slate-500">لا توجد محادثات نشطة مع المساعد الذكي حالياً.</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[800px] text-right text-sm">
                  <thead className="bg-slate-50 text-xs text-slate-500">
                    <tr>
                      <th className="p-3">الشخص</th>
                      <th className="p-3">الموضوع</th>
                      <th className="p-3">حالة الرد الآلي</th>
                      <th className="p-3">وقت البدء</th>
                      <th className="p-3">المتابعة</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activeConversations.map((item) => (
                      <tr key={item.id} className="border-t border-slate-100 hover:bg-cyan-50">
                        <td className="p-3 font-semibold text-slate-900">
                          {item.participantName}
                          <span className="mr-2 text-xs font-normal text-slate-500">
                            {item.participantType === 'Guest' ? 'زائر' : 'طالب'}
                          </span>
                        </td>
                        <td className="p-3 text-slate-700 max-w-xs truncate">{item.subject || '—'}</td>
                        <td className="p-3">{renderAiStatus(item.aiTurnStatus, item.aiTurnFailureCode)}</td>
                        <td className="p-3 text-slate-600">{new Date(item.createdAt).toLocaleString('ar-EG')}</td>
                        <td className="p-3">
                          <button
                            type="button"
                            onClick={() => void liveSupportService.getAdminTimeline(item.id).then(setTimeline)}
                            className="min-h-10 rounded-lg bg-slate-900 px-3 font-semibold text-white hover:bg-slate-800 transition-colors"
                          >
                            فتح المحادثة
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </div>}
    {timeline && <ConversationInvestigation timeline={timeline} close={() => setTimeline(undefined)}/>}
  </AdminShellChrome>;
}

function renderAiStatus(status?: string, failureCode?: string) {
  if (!status) return <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-50 px-2 py-1 text-xs font-semibold text-slate-600">نشط</span>;

  switch (status) {
    case 'Queued':
      return (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700 animate-pulse">
          <span className="h-1.5 w-1.5 rounded-full bg-blue-600"></span>
          في الطابور
        </span>
      );
    case 'Processing':
      return (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-cyan-50 px-2.5 py-1 text-xs font-semibold text-cyan-700 animate-pulse">
          <span className="h-1.5 w-1.5 rounded-full bg-cyan-600"></span>
          يكتب الآن...
        </span>
      );
    case 'Completed':
      return (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-600"></span>
          تم الرد بنجاح
        </span>
      );
    case 'Failed':
      return (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 px-2.5 py-1 text-xs font-semibold text-red-700" title={failureCode}>
          <span className="h-1.5 w-1.5 rounded-full bg-red-600"></span>
          فشل الرد {failureCode ? `(${failureCode})` : ''}
        </span>
      );
    default:
      return <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">{status}</span>;
  }
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

function NumberField({ label, value, min, max, change }: { label: string; value: number; min: number; max: number; change: (value: number) => void }) {
  return <label className="text-sm font-semibold text-slate-700">{label}<input type="number" value={value} min={min} max={max} onChange={event => change(Number(event.target.value))} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-700 focus:ring-2 focus:ring-cyan-700/20"/></label>;
}
