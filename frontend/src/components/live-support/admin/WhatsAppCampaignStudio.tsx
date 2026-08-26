'use client';

import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Check,
  CheckCircle2,
  Clock3,
  FileCheck2,
  History,
  LoaderCircle,
  LockKeyhole,
  Megaphone,
  RefreshCw,
  ShieldAlert,
  Sparkles,
  UsersRound,
} from 'lucide-react';
import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { formatCairoTimestamp } from '@/lib/cairo-time';
import { createClientId } from '@/lib/client-id';
import {
  createEmptyWhatsAppAudienceFilters,
  inspectCampaignTemplate,
  isWhatsAppCampaignPreviewCurrent,
  maskWhatsAppDestination,
  validateWhatsAppAudienceFilters,
  validateWhatsAppVariableMappings,
} from '@/lib/whatsapp-campaign';
import {
  getLiveSupportApiError,
  getLiveSupportApiErrorCode,
  liveSupportService,
  type LiveSupportWhatsAppTemplate,
  type WhatsAppCampaignAudienceFilters,
  type WhatsAppCampaignAudiencePreview,
  type WhatsAppCampaignBootstrap,
  type WhatsAppCampaignDraft,
  type WhatsAppCampaignFacets,
  type WhatsAppCampaignPage,
  type WhatsAppCampaignSummary,
  type WhatsAppCampaignVariableMapping,
} from '@/services/live-support-service';

import { WhatsAppCampaignAudienceBuilder } from './WhatsAppCampaignAudienceBuilder';
import { WhatsAppContactPreferencesPanel } from './WhatsAppContactPreferencesPanel';
import { WhatsAppCampaignHistory } from './WhatsAppCampaignHistory';
import { WhatsAppCampaignTemplateEditor } from './WhatsAppCampaignTemplateEditor';

interface WhatsAppCampaignStudioProps {
  templates: LiveSupportWhatsAppTemplate[];
  syncingTemplates: boolean;
  templateSyncFeedback: string;
  canManage: boolean;
  onSyncTemplates: () => void;
}

type ComposerStep = 1 | 2 | 3 | 4;
type StudioTab = 'composer' | 'history';

const emptyFacets: WhatsAppCampaignFacets = {
  educationStages: [],
  gradeLevels: [],
  studyTracks: [],
  crmStatuses: [],
  teachers: [],
  subjects: [],
  packages: [],
  lessons: [],
  exams: [],
  homeworks: [],
};

const steps: ReadonlyArray<{ value: ComposerStep; label: string; hint: string }> = [
  { value: 1, label: 'القالب والمتغيرات', hint: 'قالب نصي معتمد' },
  { value: 2, label: 'الجمهور', hint: 'نطاق واضح وموافقة' },
  { value: 3, label: 'المعاينة', hint: 'عدد وعينات محجوبة' },
  { value: 4, label: 'التثبيت والإرسال', hint: 'لقطة ثابتة وتأكيد قوي' },
];

export function WhatsAppCampaignStudio({
  templates,
  syncingTemplates,
  templateSyncFeedback,
  canManage,
  onSyncTemplates,
}: WhatsAppCampaignStudioProps) {
  const [tab, setTab] = useState<StudioTab>('composer');
  const [step, setStep] = useState<ComposerStep>(1);
  const [maxStep, setMaxStep] = useState<ComposerStep>(1);
  const [bootstrap, setBootstrap] = useState<WhatsAppCampaignBootstrap>();
  const [bootstrapLoading, setBootstrapLoading] = useState(false);
  const [bootstrapError, setBootstrapError] = useState('');
  const [campaignPage, setCampaignPage] = useState<WhatsAppCampaignPage>();
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState('');
  const [changingCampaignId, setChangingCampaignId] = useState<string>();

  const [campaignName, setCampaignName] = useState('');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [mappings, setMappings] = useState<WhatsAppCampaignVariableMapping[]>([]);
  const [filters, setFilters] = useState<WhatsAppCampaignAudienceFilters>(() => createEmptyWhatsAppAudienceFilters());
  const [preview, setPreview] = useState<WhatsAppCampaignAudiencePreview>();
  const [previewTemplateId, setPreviewTemplateId] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState('');
  const [, setPreviewExpiryTick] = useState(0);
  const [frozenDraft, setFrozenDraft] = useState<WhatsAppCampaignDraft>();
  const [freezing, setFreezing] = useState(false);
  const [confirmationPhrase, setConfirmationPhrase] = useState('');
  const [launching, setLaunching] = useState(false);
  const [launchFeedback, setLaunchFeedback] = useState('');
  const previewAbortRef = useRef<AbortController | undefined>(undefined);
  const draftIdempotencyRef = useRef(createClientId());
  const launchIdempotencyRef = useRef(createClientId());

  const availableTemplates = useMemo(
    () => mergeTemplates(templates, bootstrap?.templates ?? []),
    [bootstrap?.templates, templates],
  );
  const facets = bootstrap?.facets ?? emptyFacets;
  const selectedTemplate = availableTemplates.find((template) => template.id === selectedTemplateId);
  const templateSupport = selectedTemplate ? inspectCampaignTemplate(selectedTemplate) : undefined;
  const mappingErrors = templateSupport?.supported
    ? validateWhatsAppVariableMappings(templateSupport.parameters, mappings, filters, selectedTemplate?.category)
    : ['اختر قالبًا نصيًا معتمدًا.'];
  const audienceErrors = validateWhatsAppAudienceFilters(filters);
  const templateStepValid = Boolean(selectedTemplate && templateSupport?.supported && mappingErrors.length === 0);
  const audienceStepValid = filters.contactRoles.length > 0 && audienceErrors.length === 0;
  const previewCurrent = Boolean(
    preview && isWhatsAppCampaignPreviewCurrent(
      preview,
      previewTemplateId,
      selectedTemplate,
    )
  );
  const canUsePurchaseDate = filters.hasPaidPurchase === true &&
    filters.packageIds.length === 1 &&
    Boolean(filters.purchaseFromUtc && filters.purchaseToUtc);

  const loadBootstrap = useCallback(async (signal?: AbortSignal) => {
    if (!canManage) return;
    setBootstrapLoading(true);
    setBootstrapError('');
    try {
      const next = await liveSupportService.getWhatsAppCampaignBootstrap(signal);
      setBootstrap(next);
      setCampaignPage(next.campaigns);
    } catch (cause) {
      if ((cause as { code?: string })?.code === 'ERR_CANCELED') return;
      setBootstrapError(getLiveSupportApiError(cause, 'تعذر تحميل مركز حملات واتساب. أعد المحاولة.'));
    } finally {
      setBootstrapLoading(false);
    }
  }, [canManage]);

  useEffect(() => {
    const controller = new AbortController();
    void loadBootstrap(controller.signal);
    return () => controller.abort();
  }, [loadBootstrap]);

  useEffect(() => {
    if (step < 3 || !templateStepValid || !audienceStepValid || !selectedTemplate) return;
    let requestController: AbortController | undefined;
    const timer = window.setTimeout(() => {
      previewAbortRef.current?.abort();
      const controller = new AbortController();
      requestController = controller;
      previewAbortRef.current = controller;
      setPreviewLoading(true);
      setPreviewError('');
      setPreview(undefined);
      setFrozenDraft(undefined);
      setConfirmationPhrase('');
      void liveSupportService.previewWhatsAppCampaignAudience({
        templateId: selectedTemplate.id,
        filters,
        variableMappings: mappings,
      }, controller.signal).then((nextPreview) => {
        if (previewAbortRef.current !== controller) return;
        setPreview(nextPreview);
        setPreviewTemplateId(selectedTemplate.id);
      }).catch((cause) => {
        if (previewAbortRef.current !== controller) return;
        if ((cause as { code?: string })?.code === 'ERR_CANCELED') return;
        setPreviewError(getLiveSupportApiError(cause, 'تعذر حساب الجمهور الآمن. راجع الفلاتر ثم أعد المحاولة.'));
      }).finally(() => {
        if (previewAbortRef.current === controller) setPreviewLoading(false);
      });
    }, 650);
    return () => {
      window.clearTimeout(timer);
      requestController?.abort();
      if (previewAbortRef.current === requestController) previewAbortRef.current = undefined;
    };
  }, [audienceStepValid, filters, mappings, selectedTemplate, step, templateStepValid]);

  useEffect(() => () => previewAbortRef.current?.abort(), []);

  useEffect(() => {
    if (!preview?.expiresAt) return;
    const remaining = new Date(preview.expiresAt).getTime() - Date.now();
    if (!Number.isFinite(remaining) || remaining <= 0) {
      setPreviewExpiryTick((current) => current + 1);
      return;
    }
    const timer = window.setTimeout(
      () => setPreviewExpiryTick((current) => current + 1),
      Math.min(remaining + 25, 2_147_000_000),
    );
    return () => window.clearTimeout(timer);
  }, [preview?.expiresAt]);

  function invalidateReview() {
    previewAbortRef.current?.abort();
    previewAbortRef.current = undefined;
    draftIdempotencyRef.current = createClientId();
    launchIdempotencyRef.current = createClientId();
    setPreview(undefined);
    setPreviewError('');
    setFrozenDraft(undefined);
    setConfirmationPhrase('');
    setLaunchFeedback('');
    if (step > 2) setStep(2);
    if (maxStep > 2) setMaxStep(2);
  }

  function selectTemplate(templateId: string) {
    setSelectedTemplateId(templateId);
    setMappings([]);
    invalidateReview();
  }

  function updateMappings(nextMappings: WhatsAppCampaignVariableMapping[]) {
    setMappings(nextMappings);
    invalidateReview();
  }

  function updateFilters(nextFilters: WhatsAppCampaignAudienceFilters) {
    invalidateReview();
    const nextPurchasePackageId = nextFilters.hasPaidPurchase === true &&
      nextFilters.packageIds.length === 1 &&
      nextFilters.purchaseFromUtc &&
      nextFilters.purchaseToUtc
      ? nextFilters.packageIds[0]
      : undefined;
    if (nextPurchasePackageId) {
      setMappings((current) => current.map((mapping) => mapping.source === 'PurchaseDate'
        ? { ...mapping, referenceId: nextPurchasePackageId, format: 'dd/MM/yyyy' }
        : mapping));
    }
    setFilters(nextFilters);
  }

  function goNext() {
    if (step === 1 && !templateStepValid) return;
    if (step === 2 && (!templateStepValid || !audienceStepValid)) return;
    if (step === 3 && (!previewCurrent || !preview || preview.eligibleCount < 1)) return;
    const next = Math.min(4, step + 1) as ComposerStep;
    setStep(next);
    setMaxStep((current) => Math.max(current, next) as ComposerStep);
  }

  async function freezeDraft() {
    if (!preview || !previewCurrent || !selectedTemplate || freezing) return;
    const trimmedName = campaignName.trim();
    if (!trimmedName) {
      setLaunchFeedback('اكتب اسمًا واضحًا للحملة قبل تثبيت المراجعة.');
      return;
    }
    setFreezing(true);
    setLaunchFeedback('');
    try {
      const nextDraft = await liveSupportService.createWhatsAppCampaignDraft({
        name: trimmedName,
        templateId: selectedTemplate.id,
        audienceFingerprint: preview.audienceFingerprint,
        filters,
        variableMappings: mappings,
      }, draftIdempotencyRef.current);
      setFrozenDraft(nextDraft);
      setConfirmationPhrase('');
    } catch (cause) {
      handleReviewFailure(cause, 'تعذر تثبيت لقطة الحملة. حدّث المعاينة وأعد المحاولة.');
    } finally {
      setFreezing(false);
    }
  }

  async function launchCampaign() {
    if (!frozenDraft || !preview || launching) return;
    if (!previewCurrent) {
      setLaunchFeedback('انتهت صلاحية المعاينة أو تغيّر القالب. أنشئ معاينة ومراجعة جديدتين.');
      return;
    }
    if (confirmationPhrase !== frozenDraft.confirmationPhrase) {
      setLaunchFeedback('اكتب عبارة التأكيد كما تظهر تمامًا.');
      return;
    }
    setLaunching(true);
    setLaunchFeedback('');
    try {
      await liveSupportService.launchWhatsAppCampaign(frozenDraft.campaignId, {
        expectedVersion: frozenDraft.version,
        audienceFingerprint: preview.audienceFingerprint,
        reviewToken: frozenDraft.reviewToken,
        confirmationPhrase,
        idempotencyKey: launchIdempotencyRef.current,
      });
      setLaunchFeedback(`بدأ إرسال الحملة إلى ${formatNumber(frozenDraft.recipientCount)} وجهة مؤهلة.`);
      resetComposer();
      setTab('history');
      await loadCampaigns(1);
    } catch (cause) {
      handleReviewFailure(cause, 'تعذر بدء الحملة. لم نعتبرها مرسلة؛ راجع الحالة وأعد المحاولة.');
    } finally {
      setLaunching(false);
    }
  }

  function handleReviewFailure(cause: unknown, fallback: string) {
    const code = getLiveSupportApiErrorCode(cause);
    const message = getLiveSupportApiError(cause, fallback);
    setLaunchFeedback(message);
    if (code === 'WHATSAPP_CAMPAIGN_TEMPLATE_CHANGED' || code === 'WHATSAPP_CAMPAIGN_AUDIENCE_CHANGED') {
      draftIdempotencyRef.current = createClientId();
      launchIdempotencyRef.current = createClientId();
      setPreview(undefined);
      setFrozenDraft(undefined);
      setConfirmationPhrase('');
      setStep(3);
      setMaxStep(3);
      setPreviewError('تغيّر القالب أو الجمهور بعد المعاينة. أنشئ معاينة جديدة قبل الإرسال.');
    }
  }

  function resetComposer() {
    previewAbortRef.current?.abort();
    previewAbortRef.current = undefined;
    draftIdempotencyRef.current = createClientId();
    launchIdempotencyRef.current = createClientId();
    setCampaignName('');
    setSelectedTemplateId('');
    setMappings([]);
    setFilters(createEmptyWhatsAppAudienceFilters());
    setPreview(undefined);
    setFrozenDraft(undefined);
    setConfirmationPhrase('');
    setStep(1);
    setMaxStep(1);
  }

  async function loadCampaigns(page = campaignPage?.page ?? 1) {
    setHistoryLoading(true);
    setHistoryError('');
    try {
      setCampaignPage(await liveSupportService.getWhatsAppCampaigns({ page, pageSize: 20 }));
    } catch (cause) {
      setHistoryError(getLiveSupportApiError(cause, 'تعذر تحديث سجل الحملات. أعد المحاولة.'));
    } finally {
      setHistoryLoading(false);
    }
  }

  async function changeCampaignStatus(
    campaign: WhatsAppCampaignSummary,
    operation: 'pause' | 'resume' | 'cancel',
    reason?: string,
  ) {
    if (changingCampaignId) return;
    setChangingCampaignId(campaign.id);
    setHistoryError('');
    try {
      await liveSupportService.changeWhatsAppCampaignStatus(
        campaign.id,
        operation,
        campaign.version,
        reason,
      );
      await loadCampaigns(campaignPage?.page ?? 1);
    } catch (cause) {
      setHistoryError(getLiveSupportApiError(cause, 'تعذر تغيير حالة الحملة. حدّث السجل وحاول مرة أخرى.'));
    } finally {
      setChangingCampaignId(undefined);
    }
  }

  if (!canManage) {
    return (
      <section aria-labelledby="whatsapp-campaigns-heading" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)]">
        <div className="flex items-start gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"><LockKeyhole aria-hidden="true" size={19} /></span>
          <div>
            <h2 id="whatsapp-campaigns-heading" className="font-black text-[var(--admin-text)]">حملات وقوالب واتساب</h2>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">تحتاج صلاحية «إدارة حملات واتساب» لإنشاء جماهير أو إرسال قوالب جماعية.</p>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section aria-labelledby="whatsapp-campaigns-heading" className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)]">
      <header className="relative overflow-hidden border-b border-[var(--admin-border)] bg-[var(--admin-primary)] px-4 py-5 text-[var(--admin-primary-contrast)] sm:px-6">
        <div aria-hidden="true" className="absolute inset-y-0 start-0 w-1/3 bg-[linear-gradient(90deg,color-mix(in_srgb,var(--admin-accent)_30%,transparent),transparent)]" />
        <div className="relative flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[color-mix(in_srgb,var(--admin-primary-contrast)_12%,transparent)]"><Megaphone aria-hidden="true" size={20} /></span>
            <div className="min-w-0">
              <h2 id="whatsapp-campaigns-heading" className="text-lg font-black">حملات وقوالب واتساب</h2>
              <p className="mt-1 max-w-3xl text-sm leading-6 opacity-80">كوّن جمهورًا دقيقًا، عاين الاستبعادات، وثبّت نسخة لا تتغير قبل أي إرسال.</p>
            </div>
          </div>
          <div role="tablist" aria-label="أقسام مركز حملات واتساب" className="grid min-h-11 grid-cols-2 gap-1 rounded-xl bg-[color-mix(in_srgb,var(--admin-primary-contrast)_10%,transparent)] p-1">
            <TabButton selected={tab === 'composer'} onClick={() => setTab('composer')} icon={Sparkles}>حملة جديدة</TabButton>
            <TabButton selected={tab === 'history'} onClick={() => { setTab('history'); void loadCampaigns(); }} icon={History}>سجل الحملات</TabButton>
          </div>
        </div>
      </header>

      {bootstrapError && !bootstrap ? (
        <div role="alert" className="m-4 rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-[var(--admin-danger)] sm:m-5">
          <p className="font-bold">{bootstrapError}</p>
          <button type="button" onClick={() => void loadBootstrap()} disabled={bootstrapLoading} className="mt-3 inline-flex min-h-11 items-center gap-2 rounded-xl border border-current px-4 text-sm font-black focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current disabled:opacity-60">
            <RefreshCw aria-hidden="true" size={16} className={bootstrapLoading ? 'animate-spin' : ''} /> إعادة المحاولة
          </button>
        </div>
      ) : tab === 'history' ? (
        <div className="p-4 sm:p-5">
          <WhatsAppCampaignHistory
            page={campaignPage}
            loading={historyLoading || bootstrapLoading}
            error={historyError}
            changingCampaignId={changingCampaignId}
            onReload={() => void loadCampaigns()}
            onPageChange={(page) => void loadCampaigns(page)}
            onOperation={(campaign, operation, reason) => void changeCampaignStatus(campaign, operation, reason)}
          />
        </div>
      ) : (
        <div className="min-w-0">
          <nav aria-label="خطوات إنشاء حملة واتساب" className="border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-3 sm:px-5">
            <ol className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
              {steps.map((item) => {
                const selected = step === item.value;
                const complete = item.value < step || item.value < maxStep;
                const enabled = item.value <= maxStep;
                return (
                  <li key={item.value}>
                    <button type="button" disabled={!enabled} aria-current={selected ? 'step' : undefined} onClick={() => setStep(item.value)} className={`flex min-h-14 w-full items-center gap-3 rounded-xl border px-3 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-50 ${selected ? 'border-[var(--admin-accent)] bg-[var(--admin-card)] text-[var(--admin-primary)]' : 'border-transparent text-[var(--admin-muted)] hover:bg-[var(--admin-card)]'}`}>
                      <span className={`grid size-8 shrink-0 place-items-center rounded-full text-xs font-black ${selected || complete ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-strong)]'}`}>{complete ? <Check aria-hidden="true" size={15} /> : item.value}</span>
                      <span className="min-w-0"><strong className="block truncate text-sm">{item.label}</strong><small className="mt-0.5 block truncate font-medium">{item.hint}</small></span>
                    </button>
                  </li>
                );
              })}
            </ol>
          </nav>

          <div className="p-4 sm:p-6">
            {bootstrapLoading && !bootstrap ? <ComposerSkeleton /> : null}
            {bootstrap ? (
              <>
                {step === 1 ? (
                  <WhatsAppCampaignTemplateEditor
                    templates={availableTemplates}
                    facets={facets}
                    selectedTemplateId={selectedTemplateId}
                    mappings={mappings}
                    syncing={syncingTemplates}
                    syncFeedback={templateSyncFeedback}
                    canUsePurchaseDate={canUsePurchaseDate}
                    audienceFilters={filters}
                    onTemplateChange={selectTemplate}
                    onMappingsChange={updateMappings}
                    onSync={onSyncTemplates}
                  />
                ) : null}
                {step === 2 ? <>
                  {!templateStepValid ? <div role="alert" className="mb-4 flex flex-col gap-3 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] p-4 text-sm text-[var(--admin-warning)] sm:flex-row sm:items-center sm:justify-between"><p className="font-semibold">تغيّر نطاق الجمهور بما يجعل أحد متغيرات القالب غير صالح، مثل تاريخ الشراء. راجع الربط قبل المعاينة.</p><button type="button" onClick={() => setStep(1)} className="inline-flex min-h-11 shrink-0 items-center justify-center rounded-xl border border-current px-4 font-black focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current">مراجعة المتغيرات</button></div> : null}
                  <WhatsAppCampaignAudienceBuilder filters={filters} facets={facets} onChange={updateFilters} />
                </> : null}
                {step === 3 ? <CampaignPreview preview={preview} loading={previewLoading} error={previewError} current={previewCurrent} onRetry={() => { setStep(2); window.setTimeout(() => setStep(3), 0); }} /> : null}
                {step === 4 && preview ? (
                  <CampaignReview
                    campaignName={campaignName}
                    onCampaignNameChange={(name) => {
                      setCampaignName(name);
                      setFrozenDraft(undefined);
                      setConfirmationPhrase('');
                      setLaunchFeedback('');
                      draftIdempotencyRef.current = createClientId();
                      launchIdempotencyRef.current = createClientId();
                    }}
                    template={selectedTemplate}
                    preview={preview}
                    current={previewCurrent}
                    draft={frozenDraft}
                    confirmationPhrase={confirmationPhrase}
                    freezing={freezing}
                    launching={launching}
                    feedback={launchFeedback}
                    onFreeze={() => void freezeDraft()}
                    onConfirmationPhraseChange={setConfirmationPhrase}
                    onLaunch={() => void launchCampaign()}
                  />
                ) : null}

                <div className="mt-7 flex flex-col-reverse gap-2 border-t border-[var(--admin-border)] pt-4 sm:flex-row sm:items-center sm:justify-between">
                  <button type="button" disabled={step === 1 || freezing || launching} onClick={() => setStep(Math.max(1, step - 1) as ComposerStep)} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-40">
                    <ArrowRight aria-hidden="true" size={17} /> السابق
                  </button>
                  {step < 4 ? (
                    <button type="button" disabled={(step === 1 && !templateStepValid) || (step === 2 && (!templateStepValid || !audienceStepValid)) || (step === 3 && (!previewCurrent || !preview || preview.eligibleCount < 1))} onClick={goNext} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-50">
                      التالي <ArrowLeft aria-hidden="true" size={17} />
                    </button>
                  ) : null}
                </div>
              </>
            ) : null}
          </div>
        </div>
      )}
      <WhatsAppContactPreferencesPanel />
    </section>
  );
}

function CampaignPreview({
  preview,
  loading,
  error,
  current,
  onRetry,
}: {
  preview?: WhatsAppCampaignAudiencePreview;
  loading: boolean;
  error: string;
  current: boolean;
  onRetry: () => void;
}) {
  if (loading) {
    return (
      <div className="grid min-h-72 place-items-center rounded-xl bg-[var(--admin-card-soft)] text-center" aria-busy="true">
        <div><LoaderCircle aria-hidden="true" size={30} className="mx-auto animate-spin text-[var(--admin-accent)]" /><p className="mt-3 font-black text-[var(--admin-text)]">جارٍ حساب الجمهور والموافقات…</p><p className="mt-1 text-sm text-[var(--admin-muted)]">لن تظهر أرقام كاملة في المعاينة.</p></div>
      </div>
    );
  }
  if (error) {
    return <div role="alert" className="rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-5 text-[var(--admin-danger)]"><p className="font-bold">{error}</p><button type="button" onClick={onRetry} className="mt-3 inline-flex min-h-11 items-center gap-2 rounded-xl border border-current px-4 text-sm font-black focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"><RefreshCw aria-hidden="true" size={16} /> إعادة الحساب</button></div>;
  }
  if (!preview) return <p className="rounded-xl border border-dashed border-[var(--admin-border)] p-8 text-center text-sm text-[var(--admin-muted)]">تبدأ المعاينة تلقائيًا بعد اكتمال القالب والجمهور.</p>;

  const exclusions = Object.entries(preview.excludedByReason).filter(([, count]) => count > 0);
  return (
    <div className="space-y-5">
      {!current ? <p role="alert" className="flex items-start gap-2 rounded-xl bg-[var(--admin-warning-10)] p-4 text-sm font-semibold text-[var(--admin-warning)]"><ShieldAlert aria-hidden="true" size={18} className="mt-0.5 shrink-0" /> انتهت صلاحية المعاينة أو تغيّر القالب. أعد الحساب قبل المتابعة.</p> : null}
      <div className="grid gap-3 sm:grid-cols-3">
        <PreviewMetric label="مطابق للفلاتر" value={preview.eligibleCount + preview.excludedCount} icon={UsersRound} />
        <PreviewMetric label="مؤهل بموافقة صريحة" value={preview.eligibleCount} icon={CheckCircle2} success />
        <PreviewMetric label="مستبعد بأمان" value={preview.excludedCount} icon={ShieldAlert} warning />
      </div>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,0.75fr)_minmax(0,1.25fr)]">
        <section aria-labelledby="campaign-exclusions-heading">
          <h3 id="campaign-exclusions-heading" className="font-black text-[var(--admin-text)]">أسباب الاستبعاد</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">لن يُرسل النظام إلى أي وجهة غير مؤهلة.</p>
          {exclusions.length === 0 ? <p className="mt-3 rounded-xl bg-[var(--admin-success-10)] p-4 text-sm font-bold text-[var(--admin-success)]">لا توجد استبعادات في هذه المعاينة.</p> : (
            <dl className="mt-3 divide-y divide-[var(--admin-border)] rounded-xl border border-[var(--admin-border)]">
              {exclusions.map(([reason, count]) => <div key={reason} className="flex items-center justify-between gap-4 px-3 py-3 text-sm"><dt className="min-w-0 font-semibold text-[var(--admin-text)]">{exclusionReasonLabel(reason)}</dt><dd className="shrink-0 font-black text-[var(--admin-warning)]">{formatNumber(count)}</dd></div>)}
            </dl>
          )}
          {Number(preview.excludedByReason.duplicate_or_ambiguous_phone ?? 0) > 0 ? (
            <p className="mt-3 flex items-start gap-2 rounded-xl bg-[var(--admin-warning-10)] p-3 text-xs leading-5 text-[var(--admin-warning)]"><AlertTriangle aria-hidden="true" size={16} className="mt-0.5 shrink-0" /> الرقم نفسه مرتبط بأكثر من طالب، لذلك لم يُرسل له شيء، خصوصًا مع الرسائل الشخصية. لا نعرض أسماء الحسابات المشتركة.</p>
          ) : null}
        </section>

        <section aria-labelledby="campaign-samples-heading" className="min-w-0">
          <div className="flex flex-wrap items-end justify-between gap-2"><div><h3 id="campaign-samples-heading" className="font-black text-[var(--admin-text)]">عينات آمنة من الرسائل</h3><p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">معاينة آمنة؛ البيانات الشخصية مستبدلة بعناوين محجوبة، والنص الثابت وأسماء المحتوى فقط كما ستظهر.</p></div><span className="text-xs font-bold text-[var(--admin-muted)]">حتى {preview.samples.length} عينات</span></div>
          {preview.samples.length === 0 ? <p className="mt-3 rounded-xl border border-dashed border-[var(--admin-border)] p-5 text-sm text-[var(--admin-muted)]">لا توجد عينات مؤهلة للعرض.</p> : (
            <div className="mt-3 space-y-2">
              {preview.samples.map((sample, index) => <article key={`${sample.maskedPhone}-${sample.contactRole}-${index}`} className="min-w-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3"><div className="flex flex-wrap items-center justify-between gap-2 text-xs"><strong className="text-[var(--admin-text)]">{sample.maskedName || 'اسم محجوب'}</strong><span className="font-bold text-[var(--admin-muted)]"><bdi dir="ltr">{maskWhatsAppDestination(sample.maskedPhone)}</bdi> · {contactRoleLabel(sample.contactRole)}</span></div><p className="mt-2 max-h-36 overflow-y-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--admin-card)] p-3 text-sm leading-6 text-[var(--admin-text)]" dir="auto">{sample.renderedPreview}</p></article>)}
            </div>
          )}
        </section>
      </div>
      <p className="flex items-center gap-2 text-xs font-semibold text-[var(--admin-muted)]"><Clock3 aria-hidden="true" size={14} /> صلاحية المعاينة حتى <time dateTime={preview.expiresAt}>{formatCairoTimestamp(preview.expiresAt)}</time>.</p>
    </div>
  );
}

function CampaignReview({
  campaignName,
  onCampaignNameChange,
  template,
  preview,
  current,
  draft,
  confirmationPhrase,
  freezing,
  launching,
  feedback,
  onFreeze,
  onConfirmationPhraseChange,
  onLaunch,
}: {
  campaignName: string;
  onCampaignNameChange: (name: string) => void;
  template?: LiveSupportWhatsAppTemplate;
  preview: WhatsAppCampaignAudiencePreview;
  current: boolean;
  draft?: WhatsAppCampaignDraft;
  confirmationPhrase: string;
  freezing: boolean;
  launching: boolean;
  feedback: string;
  onFreeze: () => void;
  onConfirmationPhraseChange: (value: string) => void;
  onLaunch: () => void;
}) {
  const phraseMatches = Boolean(current && draft && confirmationPhrase === draft.confirmationPhrase);
  return (
    <div className="grid min-w-0 gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(18rem,0.52fr)]">
      <div className="min-w-0 space-y-5">
        {!current ? <p role="alert" className="flex items-start gap-2 rounded-xl bg-[var(--admin-warning-10)] p-4 text-sm font-semibold text-[var(--admin-warning)]"><ShieldAlert aria-hidden="true" size={18} className="mt-0.5 shrink-0" />انتهت صلاحية المعاينة أو تغيّر القالب. ارجع للمعاينة وثبّت مراجعة جديدة.</p> : null}
        <label>
          <span className="mb-1.5 block text-sm font-black text-[var(--admin-text)]">اسم الحملة في السجل</span>
          <input value={campaignName} onChange={(event) => onCampaignNameChange(event.target.value.slice(0, 120))} maxLength={120} placeholder="مثال: تذكير حصة الكيمياء — سبتمبر" className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]" />
        </label>

        <div className="rounded-xl border border-[var(--admin-border)]">
          <dl className="grid gap-px bg-[var(--admin-border)] sm:grid-cols-2">
            <ReviewItem label="القالب" value={template?.name ?? 'غير متاح'} />
            <ReviewItem label="الفئة واللغة" value={`${template?.category ?? '—'} · ${template?.language ?? '—'}`} />
            <ReviewItem label="الوجهات المؤهلة" value={formatNumber(preview.eligibleCount)} />
            <ReviewItem label="المستبعدة" value={formatNumber(preview.excludedCount)} />
          </dl>
        </div>

        {!draft ? (
          <div className="rounded-xl border border-[var(--admin-primary-15)] bg-[var(--admin-primary-15)] p-4">
            <h3 className="flex items-center gap-2 font-black text-[var(--admin-text)]"><FileCheck2 aria-hidden="true" size={18} /> ثبّت لقطة المراجعة أولًا</h3>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">سيعيد الخادم فحص القالب، المتغيرات، الجمهور والموافقات، ثم يصدر عبارة تأكيد مرتبطة بهذه النسخة فقط.</p>
            <button type="button" disabled={!current || freezing || !campaignName.trim()} onClick={onFreeze} className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-50">{freezing ? <LoaderCircle aria-hidden="true" size={17} className="animate-spin" /> : <LockKeyhole aria-hidden="true" size={17} />}{freezing ? 'جارٍ تثبيت اللقطة…' : 'تثبيت المراجعة'}</button>
          </div>
        ) : (
          <div className="space-y-4 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] p-4">
            <div><h3 className="font-black text-[var(--admin-text)]">تأكيد الإرسال الفعلي</h3><p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">اكتب العبارة التالية حرفيًا. لا يمكن سحب رسالة قبلتها Meta، والإلغاء اللاحق يوقف غير المحجوز فقط.</p></div>
            <div className="rounded-lg border border-dashed border-[var(--admin-warning)] bg-[var(--admin-card)] p-3 text-center font-black text-[var(--admin-warning)] [overflow-wrap:anywhere]" dir="auto">{draft.confirmationPhrase}</div>
            <label><span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">عبارة التأكيد</span><input value={confirmationPhrase} onChange={(event) => onConfirmationPhraseChange(event.target.value)} autoComplete="off" spellCheck={false} dir="auto" aria-invalid={confirmationPhrase.length > 0 && !phraseMatches} className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]" /></label>
            <p className="text-xs font-semibold text-[var(--admin-muted)]">تنتهي صلاحية هذه المراجعة <time dateTime={draft.reviewExpiresAt}>{formatCairoTimestamp(draft.reviewExpiresAt)}</time>.</p>
            <button type="button" disabled={!phraseMatches || launching} onClick={onLaunch} className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-danger)] px-5 text-sm font-black text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)] disabled:cursor-not-allowed disabled:opacity-50">{launching ? <LoaderCircle aria-hidden="true" size={17} className="animate-spin" /> : <Megaphone aria-hidden="true" size={17} />}{launching ? 'جارٍ بدء الإرسال…' : `بدء إرسال ${formatNumber(draft.recipientCount)} رسالة`}</button>
          </div>
        )}
        {feedback ? <p role={feedback.startsWith('بدأ') ? 'status' : 'alert'} className={`rounded-xl p-3 text-sm font-semibold ${feedback.startsWith('بدأ') ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'}`}>{feedback}</p> : null}
      </div>

      <aside className="self-start rounded-xl bg-[var(--admin-card-soft)] p-4 xl:sticky xl:top-24">
        <h3 className="flex items-center gap-2 font-black text-[var(--admin-text)]"><ShieldAlert aria-hidden="true" size={18} /> ضمانات قبل الإرسال</h3>
        <ul className="mt-3 space-y-3 text-sm leading-6 text-[var(--admin-muted)]">
          <li className="flex gap-2"><Check aria-hidden="true" size={16} className="mt-1 shrink-0 text-[var(--admin-success)]" /> موافقة صريحة لكل وجهة وفئة قالب.</li>
          <li className="flex gap-2"><Check aria-hidden="true" size={16} className="mt-1 shrink-0 text-[var(--admin-success)]" /> لا أرقام كاملة في المعاينة أو السجل.</li>
          <li className="flex gap-2"><Check aria-hidden="true" size={16} className="mt-1 shrink-0 text-[var(--admin-success)]" /> أي تغيّر في القالب أو الجمهور يلغي المراجعة.</li>
          <li className="flex gap-2"><Check aria-hidden="true" size={16} className="mt-1 shrink-0 text-[var(--admin-success)]" /> الرقم المشترك أو الملتبس يُستبعد، ولا نختار طالبًا بالنيابة عنك.</li>
        </ul>
      </aside>
    </div>
  );
}

function TabButton({ selected, onClick, icon: Icon, children }: { selected: boolean; onClick: () => void; icon: typeof Megaphone; children: ReactNode }) {
  return <button type="button" role="tab" aria-selected={selected} onClick={onClick} className={`inline-flex min-h-9 items-center justify-center gap-2 rounded-lg px-3 text-sm font-black transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white ${selected ? 'bg-[var(--admin-card)] text-[var(--admin-primary)]' : 'text-[var(--admin-primary-contrast)] hover:bg-[color-mix(in_srgb,var(--admin-primary-contrast)_8%,transparent)]'}`}><Icon aria-hidden="true" size={16} />{children}</button>;
}

function PreviewMetric({ label, value, icon: Icon, success = false, warning = false }: { label: string; value: number; icon: typeof UsersRound; success?: boolean; warning?: boolean }) {
  return <div className={`rounded-xl border p-4 ${success ? 'border-[var(--admin-success-20)] bg-[var(--admin-success-10)]' : warning ? 'border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)]' : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)]'}`}><div className="flex items-center justify-between gap-3"><p className={`text-xs font-black ${success ? 'text-[var(--admin-success)]' : warning ? 'text-[var(--admin-warning)]' : 'text-[var(--admin-muted)]'}`}>{label}</p><Icon aria-hidden="true" size={17} className={success ? 'text-[var(--admin-success)]' : warning ? 'text-[var(--admin-warning)]' : 'text-[var(--admin-muted)]'} /></div><p className="mt-2 text-2xl font-black text-[var(--admin-text)]">{formatNumber(value)}</p></div>;
}

function ReviewItem({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0 bg-[var(--admin-card)] p-3"><dt className="text-xs font-bold text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 truncate text-sm font-black text-[var(--admin-text)]" title={value} dir="auto">{value}</dd></div>;
}

function ComposerSkeleton() {
  return <div aria-label="جارٍ تحميل مركز حملات واتساب" aria-busy="true" className="space-y-4"><div className="h-12 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" /><div className="grid gap-3 sm:grid-cols-2">{Array.from({ length: 4 }, (_, index) => <div key={index} className="h-24 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" />)}</div></div>;
}

function mergeTemplates(primary: LiveSupportWhatsAppTemplate[], secondary: LiveSupportWhatsAppTemplate[]) {
  const merged = new Map<string, LiveSupportWhatsAppTemplate>();
  for (const template of secondary) merged.set(template.id, template);
  for (const template of primary) {
    const existing = merged.get(template.id);
    merged.set(template.id, {
      ...existing,
      ...template,
      fingerprint: template.fingerprint || existing?.fingerprint || '',
    });
  }
  return [...merged.values()].sort((left, right) => left.name.localeCompare(right.name, 'ar'));
}

function exclusionReasonLabel(reason: string) {
  const normalized = reason.replace(/[_\s-]/g, '').toLocaleLowerCase('en');
  return ({
    nophone: 'لا توجد وجهة اتصال',
    invalidphone: 'وجهة الاتصال غير صالحة',
    noconsent: 'لا توجد موافقة صريحة',
    optedout: 'ألغى الموافقة',
    missingvariable: 'متغيرات الرسالة ناقصة',
    missingvariables: 'متغيرات الرسالة ناقصة',
    duplicateorambiguousphone: 'رقم مشترك أو ملتبس',
    duplicatephone: 'رقم مكرر',
  } as Record<string, string>)[normalized] ?? 'استبعاد أمان آخر';
}

function contactRoleLabel(role: string) {
  return ({ StudentPrimary: 'الطالب الأساسي', StudentSecondary: 'الطالب الإضافي', FatherPrimary: 'الأب الأساسي', FatherSecondary: 'الأب الإضافي', Mother: 'الأم' } as Record<string, string>)[role] ?? 'جهة اتصال';
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('ar-EG').format(value);
}
