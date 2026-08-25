'use client';

import { AlertTriangle, Check, ShieldCheck, UserRoundCheck, X } from 'lucide-react';

import { cairoDateTimeLocalToUtcISOString } from '@/lib/cairo-time';
import { getEducationStageLabel, getGradeLevelLabel, getStudyTrackLabel } from '@/lib/academic-labels';
import { hasWhatsAppAcademicAudienceBase, validateWhatsAppAudienceFilters } from '@/lib/whatsapp-campaign';
import type {
  WhatsAppCampaignAudienceFilters,
  WhatsAppCampaignContactRole,
  WhatsAppCampaignFacetOption,
  WhatsAppCampaignFacets,
} from '@/services/live-support-service';

interface WhatsAppCampaignAudienceBuilderProps {
  filters: WhatsAppCampaignAudienceFilters;
  facets: WhatsAppCampaignFacets;
  onChange: (filters: WhatsAppCampaignAudienceFilters) => void;
}

const contactRoles: ReadonlyArray<{ value: WhatsAppCampaignContactRole; label: string }> = [
  { value: 'StudentPrimary', label: 'رقم الطالب الأساسي' },
  { value: 'StudentSecondary', label: 'رقم الطالب الإضافي' },
  { value: 'FatherPrimary', label: 'رقم الأب الأساسي' },
  { value: 'FatherSecondary', label: 'رقم الأب الإضافي' },
  { value: 'Mother', label: 'رقم الأم' },
];

export function WhatsAppCampaignAudienceBuilder({
  filters,
  facets,
  onChange,
}: WhatsAppCampaignAudienceBuilderProps) {
  const errors = validateWhatsAppAudienceFilters(filters);
  const lessons = facets.lessons;
  const educationStages = relabelFacets(facets.educationStages, getEducationStageLabel);
  const gradeLevels = relabelFacets(facets.gradeLevels, getGradeLevelLabel);
  const studyTracks = relabelFacets(facets.studyTracks, getStudyTrackLabel);
  const crmStatuses = relabelFacets(facets.crmStatuses, crmStatusLabel);
  const hasAcademicBase = hasWhatsAppAcademicAudienceBase(filters);
  const hasAcademicScopeWithoutAccess = hasWhatsAppAcademicAudienceBase({
    ...filters,
    hasActiveAccess: null,
  });
  const hasPurchasePeriod = Boolean(filters.purchaseFromUtc && filters.purchaseToUtc);
  const hasWatchScope = filters.lessonIds.length > 0 && Boolean(filters.watchFromUtc && filters.watchToUtc);
  const hasExamScope = filters.examIds.length > 0 && Boolean(filters.examFromUtc && filters.examToUtc);
  const hasHomeworkScope = filters.homeworkIds.length > 0 && Boolean(filters.homeworkFromUtc && filters.homeworkToUtc);
  const canChooseNegativeWatch = Boolean(
    hasWatchScope && hasAcademicBase
  );
  const canChooseNegativeExam = Boolean(
    hasExamScope && hasAcademicBase
  );
  const canChooseNegativeHomework = Boolean(
    hasHomeworkScope && hasAcademicBase
  );

  function patch(change: Partial<WhatsAppCampaignAudienceFilters>) {
    onChange({ ...filters, ...change });
  }

  function setBooleanCondition(
    key: 'hasActiveAccess' | 'hasPaidPurchase' | 'hasWatched' | 'hasExamAttempt' | 'hasHomeworkSubmission',
    value: boolean | null,
  ) {
    patch({ [key]: value });
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(17rem,0.42fr)]">
        <div className="rounded-xl border border-[var(--admin-primary-15)] bg-[var(--admin-primary-15)] p-4">
          <div className="flex items-start gap-3">
            <ShieldCheck aria-hidden="true" size={20} className="mt-0.5 shrink-0 text-[var(--admin-primary)]" />
            <div className="min-w-0">
              <h3 className="font-black text-[var(--admin-text)]">قاعدة الجمهور المؤهل</h3>
              <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
                يبدأ الحساب من الطلاب النشطين أصحاب وجهة اتصال صالحة وموافقة صريحة على فئة القالب. إلغاء الموافقة يستبعد الوجهة دائمًا، ولا تُستنتج الموافقة من شراء أو محادثة سابقة.
              </p>
            </div>
          </div>
        </div>
        <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
          <p className="text-xs font-black text-[var(--admin-muted)]">منطق الفلاتر</p>
          <p className="mt-1 text-sm font-bold leading-6 text-[var(--admin-text)]">الفئات معًا AND، والاختيارات داخل الفئة OR.</p>
        </div>
      </div>

      <fieldset className="rounded-xl border border-[var(--admin-border)] p-4">
        <legend className="px-2 text-sm font-black text-[var(--admin-text)]">وجهة الرسالة</legend>
        <p className="mb-3 text-xs leading-5 text-[var(--admin-muted)]">الموافقة منفصلة لكل رقم ودور اتصال. لن يُستخدم رقم آخر بدلًا من الوجهة المحددة.</p>
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {contactRoles.map((role) => {
            const checked = filters.contactRoles.includes(role.value);
            return (
              <label key={role.value} className={`flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border px-3 text-sm font-bold transition-colors ${checked ? 'border-[var(--admin-accent)] bg-[var(--admin-accent-soft)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-card-soft)]'}`}>
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={() => {
                    const next = checked
                      ? filters.contactRoles.filter((candidate) => candidate !== role.value)
                      : [...filters.contactRoles, role.value];
                    patch({ contactRoles: next.length > 0 ? next : ['StudentPrimary'] });
                  }}
                  className="size-4 accent-[var(--admin-accent)]"
                />
                <span>{role.label}</span>
              </label>
            );
          })}
        </div>
      </fieldset>

      <section aria-labelledby="campaign-academic-filters">
        <div className="mb-3">
          <h3 id="campaign-academic-filters" className="font-black text-[var(--admin-text)]">النطاق الدراسي</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">اترك الفئة فارغة لعدم تقييدها، أو اختر أكثر من قيمة داخلها.</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <FacetPicker label="المرحلة" options={educationStages} values={filters.educationStages} onChange={(educationStages) => patch({ educationStages })} />
          <FacetPicker label="الصف" options={gradeLevels} values={filters.gradeLevels} onChange={(gradeLevels) => patch({ gradeLevels })} />
          <FacetPicker label="المسار" options={studyTracks} values={filters.studyTracks} onChange={(studyTracks) => patch({ studyTracks })} />
          <FacetPicker label="المدرس" options={facets.teachers} values={filters.teacherIds} onChange={(teacherIds) => patch({ teacherIds })} />
          <FacetPicker label="المادة" options={facets.subjects} values={filters.subjectIds} onChange={(subjectIds) => patch({ subjectIds })} />
          <FacetPicker label="الباقة" options={facets.packages} values={filters.packageIds} onChange={(packageIds) => patch({ packageIds })} />
          <FacetPicker label="حالة CRM" options={crmStatuses} values={filters.crmStatuses} onChange={(crmStatuses) => patch({ crmStatuses })} />
        </div>
      </section>

      <section aria-labelledby="campaign-commercial-filters" className="border-t border-[var(--admin-border)] pt-5">
        <div className="mb-4">
          <h3 id="campaign-commercial-filters" className="font-black text-[var(--admin-text)]">الوصول والشراء</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">الوصول النشط قد يأتي من كود أو هدية؛ الشراء المدفوع يعني عملية شراء مالية فعلية.</p>
        </div>
        <div className="grid gap-5 xl:grid-cols-2">
          <ConditionGroup
            label="صلاحية المحتوى"
            value={filters.hasActiveAccess ?? null}
            options={[
              [null, 'أي حالة'],
              [true, 'لديه وصول نشط'],
              [false, 'لا يملك وصولًا نشطًا'],
            ]}
            disabledValues={hasAcademicScopeWithoutAccess ? [] : [false]}
            disabledHint="اختر نطاقًا دراسيًا أو محتوى أولًا لتفعيل «لا يملك وصولًا نشطًا»."
            onChange={(value) => setBooleanCondition('hasActiveAccess', value)}
          />
          <ConditionGroup
            label="الشراء المالي"
            value={filters.hasPaidPurchase ?? null}
            options={[
              [null, 'أي حالة'],
              [true, 'اشترى ودفع'],
              [false, 'لم يشترِ ويدفع'],
            ]}
            disabledValues={[
              ...(!hasPurchasePeriod ? [true] as const : []),
              ...(!hasPurchasePeriod || !hasAcademicBase ? [false] as const : []),
            ]}
            disabledHint="حدد فترة للشراء؛ و«لم يشترِ ويدفع» يحتاج أيضًا نطاقًا دراسيًا أو محتوى."
            onChange={(value) => setBooleanCondition('hasPaidPurchase', value)}
          />
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <DateField
            label="بداية فترة الشراء (القاهرة)"
            value={filters.purchaseFromUtc}
            onChange={(date) => patch({ purchaseFromUtc: startOfCairoDate(date) })}
          />
          <DateField
            label="نهاية فترة الشراء (القاهرة)"
            value={filters.purchaseToUtc}
            exclusiveEnd
            onChange={(date) => patch({ purchaseToUtc: endOfCairoDate(date) })}
          />
        </div>
      </section>

      <section aria-labelledby="campaign-activity-filters" className="border-t border-[var(--admin-border)] pt-5">
        <div className="mb-4">
          <h3 id="campaign-activity-filters" className="font-black text-[var(--admin-text)]">نشاط المشاهدة</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">هذه مشاهدة فعلية داخل المنصة وليست حضورًا أو غيابًا للفصل.</p>
        </div>
        {lessons.length === 0 ? (
          <div className="flex items-start gap-2 rounded-xl bg-[var(--admin-warning-10)] p-4 text-sm leading-6 text-[var(--admin-warning)]">
            <AlertTriangle aria-hidden="true" size={18} className="mt-0.5 shrink-0" />
            فلتر المشاهدة غير متاح حتى يوفّر الخادم قائمة الحصص القابلة للاختيار. لن نستخدم مُعرّفًا مكتوبًا يدويًا أو فلتر «لم يشاهد» عامًا.
          </div>
        ) : (
          <div className="space-y-4">
            <div className="grid gap-3 lg:grid-cols-3">
              <FacetPicker label="الحصة" options={lessons} values={filters.lessonIds} onChange={(lessonIds) => patch({ lessonIds, hasWatched: lessonIds.length > 0 ? filters.hasWatched : null })} />
              <DateField
                label="من تاريخ (القاهرة)"
                value={filters.watchFromUtc}
                onChange={(date) => patch({ watchFromUtc: startOfCairoDate(date) })}
              />
              <DateField
                label="إلى تاريخ (القاهرة)"
                value={filters.watchToUtc}
                exclusiveEnd
                onChange={(date) => patch({ watchToUtc: endOfCairoDate(date) })}
              />
            </div>
            <ConditionGroup
              label="حالة المشاهدة"
              value={filters.hasWatched ?? null}
              options={[
                [null, 'لا أفلتر بالمشاهدة'],
                [true, 'شاهد الحصة'],
                [false, 'لم يشاهدها'],
              ]}
              disabledValues={[...(!hasWatchScope ? [true] as const : []), ...(!canChooseNegativeWatch ? [false] as const : [])]}
              disabledHint="الحالتان تحتاجان حصة وفترة؛ و«لم يشاهدها» يحتاج أيضًا نطاقًا دراسيًا أو محتوى."
              onChange={(value) => setBooleanCondition('hasWatched', value)}
            />
          </div>
        )}
      </section>

      <details className="group rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
        <summary className="flex min-h-12 cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 font-black text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-accent)]">
          فلاتر الامتحان والواجب المتقدمة
          <span aria-hidden="true" className="text-xl text-[var(--admin-muted)] transition-transform group-open:rotate-45">+</span>
        </summary>
        <div className="space-y-6 border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
          <div className="space-y-4">
            <div className="grid gap-3 lg:grid-cols-3">
              <FacetPicker label="الامتحان" options={facets.exams} values={filters.examIds} onChange={(examIds) => patch({ examIds, hasExamAttempt: examIds.length > 0 ? filters.hasExamAttempt : null })} />
              <DateField label="بداية فترة الامتحان" value={filters.examFromUtc} onChange={(date) => patch({ examFromUtc: startOfCairoDate(date) })} />
              <DateField label="نهاية فترة الامتحان" value={filters.examToUtc} exclusiveEnd onChange={(date) => patch({ examToUtc: endOfCairoDate(date) })} />
            </div>
            <ConditionGroup
              label="محاولة الامتحان"
              value={filters.hasExamAttempt ?? null}
              options={[[null, 'لا أفلتر بالامتحان'], [true, 'امتحن'], [false, 'لم يمتحن']]}
              disabledValues={[...(!hasExamScope ? [true] as const : []), ...(!canChooseNegativeExam ? [false] as const : [])]}
              disabledHint="الحالتان تحتاجان امتحانًا وفترة؛ و«لم يمتحن» يحتاج أيضًا نطاقًا دراسيًا أو محتوى."
              onChange={(value) => setBooleanCondition('hasExamAttempt', value)}
            />
          </div>
          <div className="space-y-4 border-t border-[var(--admin-border)] pt-5">
            <div className="grid gap-3 lg:grid-cols-3">
              <FacetPicker label="الواجب" options={facets.homeworks} values={filters.homeworkIds} onChange={(homeworkIds) => patch({ homeworkIds, hasHomeworkSubmission: homeworkIds.length > 0 ? filters.hasHomeworkSubmission : null })} />
              <DateField label="بداية فترة الواجب" value={filters.homeworkFromUtc} onChange={(date) => patch({ homeworkFromUtc: startOfCairoDate(date) })} />
              <DateField label="نهاية فترة الواجب" value={filters.homeworkToUtc} exclusiveEnd onChange={(date) => patch({ homeworkToUtc: endOfCairoDate(date) })} />
            </div>
            <ConditionGroup
              label="تسليم الواجب"
              value={filters.hasHomeworkSubmission ?? null}
              options={[[null, 'لا أفلتر بالواجب'], [true, 'سلّم الواجب'], [false, 'لم يسلّم الواجب']]}
              disabledValues={[...(!hasHomeworkScope ? [true] as const : []), ...(!canChooseNegativeHomework ? [false] as const : [])]}
              disabledHint="الحالتان تحتاجان واجبًا وفترة؛ و«لم يسلّم الواجب» يحتاج أيضًا نطاقًا دراسيًا أو محتوى."
              onChange={(value) => setBooleanCondition('hasHomeworkSubmission', value)}
            />
          </div>
        </div>
      </details>

      {errors.length > 0 ? (
        <ul role="alert" className="space-y-1 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] p-4 text-sm font-semibold text-[var(--admin-warning)]">
          {errors.map((error) => <li key={error}>• {error}</li>)}
        </ul>
      ) : (
        <p role="status" className="flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-success-10)] px-4 text-sm font-semibold text-[var(--admin-success)]">
          <UserRoundCheck aria-hidden="true" size={18} /> إعداد الجمهور صالح للمعاينة الآمنة.
        </p>
      )}
    </div>
  );
}

function FacetPicker({
  label,
  options,
  values,
  onChange,
}: {
  label: string;
  options: WhatsAppCampaignFacetOption[];
  values: string[];
  onChange: (values: string[]) => void;
}) {
  const available = options.filter((option) => !values.includes(option.value));
  return (
    <div className="min-w-0">
      <label>
        <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">{label}</span>
        <select
          value=""
          disabled={available.length === 0}
          onChange={(event) => {
            if (event.target.value) onChange([...values, event.target.value]);
          }}
          className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)] disabled:cursor-not-allowed disabled:opacity-60"
        >
          <option value="">{options.length === 0 ? `لا توجد بيانات ${label}` : `إضافة ${label}`}</option>
          {available.map((option) => (
            <option key={option.value} value={option.value}>{option.label} ({option.count})</option>
          ))}
        </select>
      </label>
      {values.length > 0 ? (
        <div className="mt-2 flex flex-wrap gap-2" aria-label={`اختيارات ${label}`}>
          {values.map((value) => {
            const option = options.find((candidate) => candidate.value === value);
            return (
              <span key={value} className="inline-flex min-h-9 max-w-full items-center gap-1 rounded-full bg-[var(--admin-accent-soft)] ps-3 pe-1 text-xs font-bold text-[var(--admin-primary)]">
                <span className="max-w-48 truncate" title={option?.label ?? value}>{option?.label ?? value}</span>
                <button
                  type="button"
                  onClick={() => onChange(values.filter((candidate) => candidate !== value))}
                  aria-label={`إزالة ${option?.label ?? value}`}
                  className="grid size-8 shrink-0 place-items-center rounded-full transition-colors hover:bg-[var(--admin-primary-15)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"
                >
                  <X aria-hidden="true" size={14} />
                </button>
              </span>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}

function ConditionGroup({
  label,
  value,
  options,
  disabledValues = [],
  disabledHint,
  onChange,
}: {
  label: string;
  value: boolean | null;
  options: ReadonlyArray<readonly [boolean | null, string]>;
  disabledValues?: Array<boolean | null>;
  disabledHint?: string;
  onChange: (value: boolean | null) => void;
}) {
  return (
    <fieldset>
      <legend className="mb-2 text-sm font-bold text-[var(--admin-text)]">{label}</legend>
      <div className="grid gap-2 sm:grid-cols-3">
        {options.map(([optionValue, optionLabel]) => {
          const selected = value === optionValue;
          const disabled = disabledValues.includes(optionValue);
          return (
            <button
              key={`${optionValue}`}
              type="button"
              disabled={disabled}
              aria-pressed={selected}
              onClick={() => onChange(optionValue)}
              className={`relative min-h-11 rounded-xl border px-3 text-sm font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-45 ${selected ? 'border-[var(--admin-accent)] bg-[var(--admin-accent-soft)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-card-soft)]'}`}
            >
              {selected ? <Check aria-hidden="true" size={14} className="absolute end-2 top-2" /> : null}
              {optionLabel}
            </button>
          );
        })}
      </div>
      {disabledHint && disabledValues.length > 0 ? <p className="mt-2 text-xs leading-5 text-[var(--admin-muted)]">{disabledHint}</p> : null}
    </fieldset>
  );
}

function DateField({ label, value, exclusiveEnd = false, onChange }: { label: string; value?: string | null; exclusiveEnd?: boolean; onChange: (value: string) => void }) {
  let dateValue = value ? new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Cairo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(value)) : '';
  if (dateValue && exclusiveEnd) {
    const displayedDay = new Date(`${dateValue}T00:00:00Z`);
    displayedDay.setUTCDate(displayedDay.getUTCDate() - 1);
    dateValue = displayedDay.toISOString().slice(0, 10);
  }
  return (
    <label>
      <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">{label}</span>
      <input
        type="date"
        value={dateValue}
        onChange={(event) => onChange(event.target.value)}
        className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
      />
    </label>
  );
}

function startOfCairoDate(date: string) {
  return date ? cairoDateTimeLocalToUtcISOString(`${date}T00:00`) : null;
}

function endOfCairoDate(date: string) {
  if (!date) return null;
  const nextDay = new Date(`${date}T00:00:00Z`);
  nextDay.setUTCDate(nextDay.getUTCDate() + 1);
  return cairoDateTimeLocalToUtcISOString(`${nextDay.toISOString().slice(0, 10)}T00:00`);
}

function relabelFacets(
  options: WhatsAppCampaignFacetOption[],
  getLabel: (value?: string | null) => string,
) {
  return options.map((option) => ({ ...option, label: getLabel(option.value) }));
}

function crmStatusLabel(value?: string | null) {
  return ({
    Unassigned: 'غير مسند',
    Assigned: 'مسند للمتابعة',
    InProgress: 'قيد المتابعة والاتصال',
    Cold: 'بارد / لم يستجب',
    Closed: 'مغلق / منتهي',
  } as Record<string, string>)[value ?? ''] ?? value ?? 'غير محدد';
}
