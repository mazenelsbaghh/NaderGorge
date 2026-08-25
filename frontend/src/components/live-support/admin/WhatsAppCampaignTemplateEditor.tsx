'use client';

import { AlertTriangle, CheckCircle2, FileText, RefreshCw, Search } from 'lucide-react';
import { useMemo, useState } from 'react';

import { formatCairoTimestamp } from '@/lib/cairo-time';
import {
  componentLabel,
  inspectCampaignTemplate,
  validateWhatsAppVariableMappings,
} from '@/lib/whatsapp-campaign';
import type {
  LiveSupportWhatsAppTemplate,
  WhatsAppCampaignAudienceFilters,
  WhatsAppCampaignFacets,
  WhatsAppCampaignVariableMapping,
  WhatsAppCampaignVariableSource,
} from '@/services/live-support-service';

interface WhatsAppCampaignTemplateEditorProps {
  templates: LiveSupportWhatsAppTemplate[];
  facets: WhatsAppCampaignFacets;
  selectedTemplateId: string;
  mappings: WhatsAppCampaignVariableMapping[];
  syncing: boolean;
  syncFeedback: string;
  canUsePurchaseDate: boolean;
  audienceFilters: WhatsAppCampaignAudienceFilters;
  onTemplateChange: (templateId: string) => void;
  onMappingsChange: (mappings: WhatsAppCampaignVariableMapping[]) => void;
  onSync: () => void;
}

const variableSources: ReadonlyArray<{
  value: WhatsAppCampaignVariableSource;
  label: string;
  referenceFacet?: 'teachers' | 'subjects' | 'packages' | 'lessons';
}> = [
  { value: 'StudentFirstName', label: 'اسم الطالب الأول' },
  { value: 'StudentFullName', label: 'اسم الطالب كاملًا' },
  { value: 'EducationStage', label: 'المرحلة التعليمية' },
  { value: 'GradeLevel', label: 'الصف الدراسي' },
  { value: 'StudyTrack', label: 'المسار الدراسي' },
  { value: 'Governorate', label: 'المحافظة' },
  { value: 'SchoolName', label: 'المدرسة' },
  { value: 'TeacherName', label: 'اسم المدرس', referenceFacet: 'teachers' },
  { value: 'SubjectName', label: 'اسم المادة', referenceFacet: 'subjects' },
  { value: 'PackageName', label: 'اسم الباقة', referenceFacet: 'packages' },
  { value: 'LessonName', label: 'اسم الحصة', referenceFacet: 'lessons' },
  { value: 'PurchaseDate', label: 'تاريخ الشراء' },
  { value: 'Literal', label: 'نص ثابت' },
];

export function WhatsAppCampaignTemplateEditor({
  templates,
  facets,
  selectedTemplateId,
  mappings,
  syncing,
  syncFeedback,
  canUsePurchaseDate,
  audienceFilters,
  onTemplateChange,
  onMappingsChange,
  onSync,
}: WhatsAppCampaignTemplateEditorProps) {
  const [search, setSearch] = useState('');
  const selectedTemplate = templates.find((template) => template.id === selectedTemplateId);
  const selectedSupport = selectedTemplate ? inspectCampaignTemplate(selectedTemplate) : undefined;
  const mappingErrors = selectedSupport?.supported
    ? validateWhatsAppVariableMappings(selectedSupport.parameters, mappings, audienceFilters)
    : [];
  const templateSummary = useMemo(() => summarizeTemplates(templates), [templates]);

  const visibleTemplates = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase('ar-EG');
    return templates
      .filter((template) => template.status.toUpperCase() === 'APPROVED')
      .filter((template) => !normalizedSearch || [template.name, template.language, template.category]
        .join(' ')
        .toLocaleLowerCase('ar-EG')
        .includes(normalizedSearch));
  }, [search, templates]);

  function updateMapping(
    componentType: 'HEADER' | 'BODY',
    position: number,
    change: Partial<WhatsAppCampaignVariableMapping>,
  ) {
    const existing = mappings.find((mapping) =>
      mapping.componentType === componentType && mapping.position === position
    );
    const next: WhatsAppCampaignVariableMapping = {
      componentType,
      position,
      source: existing?.source ?? 'StudentFirstName',
      literalValue: existing?.literalValue ?? null,
      referenceId: existing?.referenceId ?? null,
      format: existing?.format ?? null,
      ...change,
    };
    onMappingsChange([
      ...mappings.filter((mapping) =>
        mapping.componentType !== componentType || mapping.position !== position
      ),
      next,
    ]);
  }

  return (
    <div className="grid min-w-0 gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(19rem,0.72fr)]">
      <div className="min-w-0 space-y-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <label className="min-w-0 flex-1">
            <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">ابحث في القوالب المعتمدة</span>
            <span className="flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 focus-within:border-[var(--admin-accent)] focus-within:ring-2 focus-within:ring-[var(--admin-accent-soft)]">
              <Search aria-hidden="true" size={17} className="shrink-0 text-[var(--admin-muted)]" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="اسم القالب أو اللغة أو الفئة"
                className="min-w-0 flex-1 bg-transparent text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)]"
              />
            </span>
          </label>
          <button
            type="button"
            onClick={onSync}
            disabled={syncing}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <RefreshCw aria-hidden="true" size={16} className={syncing ? 'animate-spin' : ''} />
            {syncing ? 'جارٍ مزامنة كل القوالب…' : 'مزامنة كل القوالب'}
          </button>
        </div>

        <div className="flex flex-wrap items-center gap-2 rounded-xl bg-[var(--admin-card-soft)] p-3 text-xs font-bold text-[var(--admin-muted)]">
          <TemplateStatus label="معتمد" count={templateSummary.approved} tone="success" />
          <TemplateStatus label="قيد المراجعة" count={templateSummary.pending} tone="warning" />
          <TemplateStatus label="مرفوض" count={templateSummary.rejected} tone="danger" />
          <TemplateStatus label="قديم / STALE" count={templateSummary.stale} />
          <span className="basis-full sm:ms-auto sm:basis-auto">آخر مزامنة: {templateSummary.lastSyncedAt ? formatCairoTimestamp(templateSummary.lastSyncedAt) : 'لا توجد بعد'}</span>
        </div>

        {syncFeedback ? (
          <p
            role={syncFeedback.startsWith('تعذر') ? 'alert' : 'status'}
            className={`text-sm font-semibold ${syncFeedback.startsWith('تعذر') ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-success)]'}`}
          >
            {syncFeedback}
          </p>
        ) : null}

        <fieldset>
          <legend className="mb-2 text-sm font-bold text-[var(--admin-text)]">اختر القالب</legend>
          {visibleTemplates.length === 0 ? (
            <div className="rounded-xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 text-sm leading-6 text-[var(--admin-muted)]">
              لا توجد قوالب معتمدة مطابقة. امسح البحث أو نفّذ المزامنة لجلب أحدث قوالب Meta.
            </div>
          ) : (
            <div className="grid gap-2 sm:grid-cols-2">
              {visibleTemplates.map((template) => {
                const support = inspectCampaignTemplate(template);
                const selected = template.id === selectedTemplateId;
                return (
                  <label
                    key={template.id}
                    className={`relative flex min-h-20 min-w-0 gap-3 rounded-xl border p-3 transition-colors ${support.supported ? 'cursor-pointer' : 'cursor-not-allowed opacity-65'} ${selected ? 'border-[var(--admin-accent)] bg-[var(--admin-accent-soft)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] hover:bg-[var(--admin-card-soft)]'}`}
                  >
                    <input
                      type="radio"
                      name="whatsapp-campaign-template"
                      value={template.id}
                      checked={selected}
                      disabled={!support.supported}
                      onChange={() => onTemplateChange(template.id)}
                      className="mt-1 size-4 shrink-0 accent-[var(--admin-accent)]"
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-black text-[var(--admin-text)]" title={template.name} dir="auto">
                        {template.name}
                      </span>
                      <span className="mt-1 flex flex-wrap gap-1.5 text-xs text-[var(--admin-muted)]">
                        <span>{template.language}</span>
                        <span aria-hidden="true">·</span>
                        <span>{template.category}</span>
                      </span>
                      {!support.supported ? (
                        <span className="mt-1.5 block text-xs leading-5 text-[var(--admin-warning)]">{support.reason}</span>
                      ) : null}
                    </span>
                  </label>
                );
              })}
            </div>
          )}
        </fieldset>

        {selectedTemplate && selectedSupport?.supported ? (
          <section aria-labelledby="campaign-variables-heading">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 id="campaign-variables-heading" className="text-sm font-black text-[var(--admin-text)]">ربط متغيرات القالب</h3>
                <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">كل متغير مربوط بمكوّنه ومكانه؛ لن تُرسل أي رسالة ينقصها متغير.</p>
              </div>
              <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">
                {selectedSupport.parameters.length} متغير
              </span>
            </div>

            {selectedSupport.parameters.length === 0 ? (
              <p className="flex min-h-12 items-center gap-2 rounded-xl bg-[var(--admin-success-10)] px-4 text-sm font-semibold text-[var(--admin-success)]">
                <CheckCircle2 aria-hidden="true" size={17} /> القالب لا يحتاج متغيرات.
              </p>
            ) : (
              <div className="space-y-3">
                {selectedSupport.parameters.map((parameter) => {
                  const mapping = mappings.find((candidate) =>
                    candidate.componentType === parameter.componentType &&
                    candidate.position === parameter.parameterIndex
                  );
                  const sourceDefinition = variableSources.find((source) => source.value === mapping?.source);
                  const referenceOptions = sourceDefinition?.referenceFacet ? facets[sourceDefinition.referenceFacet] : [];
                  return (
                    <div key={parameter.key} className="grid min-w-0 gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 lg:grid-cols-[minmax(11rem,0.7fr)_minmax(13rem,1fr)]">
                      <div className="min-w-0">
                        <p className="text-xs font-black text-[var(--admin-primary)]">
                          {componentLabel(parameter.componentType)} · متغير {parameter.parameterIndex}
                        </p>
                        <p className="mt-1 line-clamp-2 [overflow-wrap:anywhere] text-xs leading-5 text-[var(--admin-muted)]" dir="auto" title={parameter.surroundingText}>
                          {parameter.surroundingText}
                        </p>
                      </div>
                      <div className="grid min-w-0 gap-2 sm:grid-cols-2">
                        <label className="min-w-0">
                          <span className="sr-only">مصدر متغير {parameter.parameterIndex}</span>
                          <select
                            value={mapping?.source ?? ''}
                            onChange={(event) => {
                              const source = event.target.value as WhatsAppCampaignVariableSource;
                              updateMapping(parameter.componentType, parameter.parameterIndex, {
                                source,
                                literalValue: null,
                                referenceId: source === 'PurchaseDate' && canUsePurchaseDate
                                  ? audienceFilters.packageIds[0]
                                  : null,
                                format: source === 'PurchaseDate' ? 'dd/MM/yyyy' : null,
                              });
                            }}
                            className="min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
                          >
                            <option value="">اختر مصدر القيمة</option>
                            {variableSources.map((source) => (
                              <option key={source.value} value={source.value} disabled={source.value === 'PurchaseDate' && !canUsePurchaseDate}>
                                {source.label}{source.value === 'PurchaseDate' && !canUsePurchaseDate ? ' — اختر شراءً مدفوعًا وباقة واحدة' : ''}
                              </option>
                            ))}
                          </select>
                        </label>
                        {mapping?.source === 'Literal' ? (
                          <label className="min-w-0">
                            <span className="sr-only">النص الثابت للمتغير {parameter.parameterIndex}</span>
                            <input
                              value={mapping.literalValue ?? ''}
                              onChange={(event) => updateMapping(parameter.componentType, parameter.parameterIndex, { literalValue: event.target.value.slice(0, 1024) })}
                              maxLength={1024}
                              placeholder="اكتب النص الثابت"
                              dir="auto"
                              className="min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
                            />
                          </label>
                        ) : sourceDefinition?.referenceFacet ? (
                          <label className="min-w-0">
                            <span className="sr-only">القيمة المرجعية للمتغير {parameter.parameterIndex}</span>
                            <select
                              value={mapping?.referenceId ?? ''}
                              onChange={(event) => updateMapping(parameter.componentType, parameter.parameterIndex, { referenceId: event.target.value || null })}
                              className="min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
                            >
                              <option value="">اختر {sourceDefinition.label}</option>
                              {referenceOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                            </select>
                          </label>
                        ) : (
                          <p className="flex min-h-11 items-center rounded-lg px-3 text-xs leading-5 text-[var(--admin-muted)]">
                            تُحل القيمة لكل طالب عند تثبيت الجمهور.
                          </p>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
            {mappingErrors.length > 0 ? (
              <ul role="alert" className="mt-3 space-y-1 rounded-xl bg-[var(--admin-warning-10)] p-3 text-xs font-semibold text-[var(--admin-warning)]">
                {mappingErrors.map((error) => <li key={error}>• {error}</li>)}
              </ul>
            ) : null}
          </section>
        ) : null}
      </div>

      <aside className="min-w-0 self-start rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] xl:sticky xl:top-24">
        <div className="flex items-center gap-2">
          <FileText aria-hidden="true" size={18} />
          <h3 className="font-black">معاينة القالب</h3>
        </div>
        {!selectedTemplate ? (
          <p className="mt-4 text-sm leading-6 opacity-75">اختر قالبًا معتمدًا لرؤية نص الرسالة ومتغيراتها هنا.</p>
        ) : !selectedSupport?.supported ? (
          <p className="mt-4 flex gap-2 text-sm leading-6 text-amber-200">
            <AlertTriangle aria-hidden="true" size={18} className="mt-0.5 shrink-0" />
            {selectedSupport?.reason}
          </p>
        ) : (
          <div className="mt-4 space-y-3">
            <div className="flex flex-wrap gap-2 text-xs font-bold opacity-75">
              <span>{selectedTemplate.language}</span>
              <span aria-hidden="true">·</span>
              <span>{selectedTemplate.category}</span>
            </div>
            <p className="max-h-80 overflow-y-auto whitespace-pre-wrap [overflow-wrap:anywhere] rounded-xl bg-[color-mix(in_srgb,var(--admin-primary-contrast)_10%,transparent)] p-4 text-sm leading-7" dir="auto">
              {renderLocalPreview(selectedTemplate, mappings)}
            </p>
          </div>
        )}
      </aside>
    </div>
  );
}

function renderLocalPreview(
  template: LiveSupportWhatsAppTemplate,
  mappings: WhatsAppCampaignVariableMapping[],
) {
  return template.components
    .filter((component) => ['HEADER', 'BODY', 'FOOTER'].includes((component.type ?? '').toUpperCase()))
    .map((component) => {
      const componentType = (component.type ?? '').toUpperCase();
      return (component.text ?? '').replace(/\{\{\s*(\d+)\s*\}\}/g, (_, rawPosition: string) => {
        if (componentType !== 'HEADER' && componentType !== 'BODY') return '…';
        const position = Number(rawPosition);
        const mapping = mappings.find((candidate) =>
          candidate.componentType === componentType && candidate.position === position
        );
        if (!mapping) return `{{${position}}}`;
        if (mapping.source === 'Literal') return mapping.literalValue?.trim() || `{{${position}}}`;
        return `‹${variableSources.find((source) => source.value === mapping.source)?.label ?? 'قيمة الطالب'}›`;
      });
    })
    .filter(Boolean)
    .join('\n\n') || 'القالب لا يحتوي نصًا قابلًا للمعاينة.';
}

function summarizeTemplates(templates: LiveSupportWhatsAppTemplate[]) {
  const counts = { approved: 0, pending: 0, rejected: 0, stale: 0 };
  let lastSyncedAt = '';
  for (const template of templates) {
    const status = template.status.toUpperCase();
    if (status === 'APPROVED') counts.approved += 1;
    else if (status === 'PENDING' || status === 'IN_APPEAL') counts.pending += 1;
    else if (status === 'REJECTED' || status === 'DISABLED') counts.rejected += 1;
    else if (status === 'STALE' || status === 'PAUSED') counts.stale += 1;
    if (template.lastSyncedAt && (!lastSyncedAt || template.lastSyncedAt > lastSyncedAt)) {
      lastSyncedAt = template.lastSyncedAt;
    }
  }
  return { ...counts, lastSyncedAt };
}

function TemplateStatus({ label, count, tone }: { label: string; count: number; tone?: 'success' | 'warning' | 'danger' }) {
  const color = tone === 'success'
    ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]'
    : tone === 'warning'
      ? 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]'
      : tone === 'danger'
        ? 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
        : 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]';
  return <span className={`rounded-full px-2.5 py-1 ${color}`}>{label}: {new Intl.NumberFormat('ar-EG').format(count)}</span>;
}
