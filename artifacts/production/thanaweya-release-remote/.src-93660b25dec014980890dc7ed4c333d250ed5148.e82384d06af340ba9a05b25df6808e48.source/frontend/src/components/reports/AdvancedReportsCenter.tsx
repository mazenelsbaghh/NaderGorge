'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  BarChart3, Check, ChevronLeft, ChevronRight, Copy, Download, FileSpreadsheet,
  Filter, LoaderCircle, Plus, RefreshCw, Save, Search, Trash2, X,
} from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminModal } from '@/components/admin/AdminModal';
import { Dropdown } from '@/components/ui/dropdown';
import {
  advancedReportService,
  type AdvancedReportColumn,
  type AdvancedReportFilter,
  type AdvancedReportFilterGroup,
  type AdvancedReportQuery,
  type AdvancedReportResult,
  type AdvancedReportCatalogDomain,
  type AdvancedReportFilterOption,
  type ReportAudience,
  type ReportFilterOperator,
  type SavedAdvancedReport,
} from '@/services/advanced-report-service';
import { reportCatalog, type ReportDomainDefinition, type ReportFieldDefinition } from './report-catalog';
import { teacherService, type TeacherDto } from '@/services/teacher-service';

const PAGE_SIZE = 20;
type QuickReportType = 'purchase' | 'attendance' | 'video' | 'exam' | 'homework';
type QuickReportValues = Record<QuickReportType, string[]>;

const quickReportTypes: Array<{
  id: QuickReportType;
  label: string;
  field: string;
  options: Array<{ value: string; label: string }>;
}> = [
  { id: 'purchase', label: 'الشراء', field: 'purchaseStatus', options: [{ value: 'purchased', label: 'اشترى' }, { value: 'notPurchased', label: 'لم يشترِ' }, { value: 'expired', label: 'اشتراك منتهي' }, { value: 'gift', label: 'هدية' }, { value: 'code', label: 'بكود' }, { value: 'balance', label: 'من الرصيد' }] },
  { id: 'attendance', label: 'الحضور', field: 'attendanceStatus', options: [{ value: 'present', label: 'حاضر' }, { value: 'absent', label: 'غائب' }] },
  { id: 'video', label: 'المشاهدة', field: 'videoStatus', options: [{ value: 'watched', label: 'شاهد فيديو' }, { value: 'notWatched', label: 'لم يشاهد' }] },
  { id: 'exam', label: 'الامتحانات', field: 'examStatus', options: [{ value: 'passed', label: 'ناجح' }, { value: 'failed', label: 'راسب' }, { value: 'notAttempted', label: 'لم يمتحن' }, { value: 'noExam', label: 'لا يوجد امتحان' }] },
  { id: 'homework', label: 'الواجبات', field: 'homeworkStatus', options: [{ value: 'submitted', label: 'سلّم الواجب' }, { value: 'notSubmitted', label: 'لم يسلّم' }, { value: 'noHomework', label: 'لا يوجد واجب' }] },
];

const initialQuickValues: QuickReportValues = {
  purchase: ['purchased'], attendance: ['present'], video: ['watched'], exam: ['passed'], homework: ['submitted'],
};
const quickReportValueLabels = new Map(quickReportTypes.flatMap((type) =>
  type.options.map((option) => [`${type.field}:${option.value}`, option.label] as const)));
const operators: Array<{ value: ReportFilterOperator; label: string }> = [
  { value: 'eq', label: 'يساوي' },
  { value: 'neq', label: 'لا يساوي' },
  { value: 'contains', label: 'يحتوي على' },
  { value: 'in', label: 'ضمن أي من' },
  { value: 'gt', label: 'أكبر من' },
  { value: 'gte', label: 'أكبر من أو يساوي' },
  { value: 'lt', label: 'أقل من' },
  { value: 'lte', label: 'أقل من أو يساوي' },
  { value: 'before', label: 'قبل' },
  { value: 'after', label: 'بعد' },
  { value: 'between', label: 'بين' },
  { value: 'is-empty', label: 'فارغ' },
  { value: 'not-empty', label: 'غير فارغ' },
];

const makeId = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
const newFilter = (field = ''): AdvancedReportFilter => ({ id: makeId(), field, operator: 'eq', values: [] });
const newGroup = (): AdvancedReportFilterGroup => ({ id: makeId(), logic: 'and', filters: [], groups: [] });
const hydrateGroup = (group?: Partial<AdvancedReportFilterGroup> | null): AdvancedReportFilterGroup => ({
  id: group?.id || makeId(),
  logic: group?.logic === 'or' ? 'or' : 'and',
  filters: (group?.filters ?? []).map((filter) => ({ ...filter, id: filter.id || makeId() })),
  groups: (group?.groups ?? []).map(hydrateGroup),
});

const getStatus = (error: unknown) => (error as { response?: { status?: number } })?.response?.status;
const valueText = (value: unknown) => value === null || value === undefined || value === '' ? '—' : String(value);
function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

const domainFieldOptions: Record<string, Record<string, Array<{ value: string; label: string }>>> = {
  'student-journey': Object.fromEntries(quickReportTypes.map((type) => [type.field, type.options])),
  students: {
    stage: [
      { value: 'Primary', label: 'ابتدائي' },
      { value: 'Preparatory', label: 'إعدادي' },
      { value: 'Secondary', label: 'ثانوي' },
      { value: 'Baccalaureate', label: 'بكالوريا' },
      { value: 'Azhari', label: 'أزهري' },
      { value: 'American', label: 'أمريكي' },
    ],
  },
  purchases: {
    purchaseStatus: [
      { value: 'purchased', label: 'اشترى' },
      { value: 'notPurchased', label: 'لم يشترِ' },
      { value: 'expired', label: 'انتهت الصلاحية' },
      { value: 'gift', label: 'هدية' },
    ],
    source: [
      { value: 'direct', label: 'شراء مباشر' },
      { value: 'code', label: 'كود وصول' },
      { value: 'gift', label: 'هدية' },
      { value: 'balance', label: 'الرصيد' },
    ],
    grantType: [
      { value: 'Package', label: 'باقة' },
      { value: 'Term', label: 'ترم' },
      { value: 'Month', label: 'قسم محتوى' },
      { value: 'Lesson', label: 'حصة' },
      { value: 'Video', label: 'فيديو' },
      { value: 'Exam', label: 'اختبار' },
      { value: 'Balance', label: 'رصيد' },
    ],
  },
  attendance: {
    attendanceStatus: [
      { value: 'present', label: 'حاضر' },
      { value: 'absent', label: 'غائب' },
    ],
  },
  content: {
    contentType: [
      { value: 'package', label: 'كورس / باقة' },
      { value: 'video', label: 'فيديو' },
    ],
  },
  codes: {
    codeType: [
      { value: 'Package', label: 'باقة' },
      { value: 'Term', label: 'ترم' },
      { value: 'Month', label: 'قسم محتوى' },
      { value: 'Lesson', label: 'حصة' },
      { value: 'Video', label: 'فيديو' },
      { value: 'Exam', label: 'اختبار' },
      { value: 'Balance', label: 'رصيد' },
    ],
  },
  'balance-recharge': {
    recordType: [
      { value: 'recharge', label: 'طلب شحن' },
      { value: 'balance', label: 'حركة رصيد' },
    ],
  },
  assessments: {
    assessmentType: [
      { value: 'exam', label: 'اختبار' },
      { value: 'homework', label: 'واجب' },
    ],
  },
  support: {
    status: [
      { value: 'Waiting', label: 'في الانتظار' },
      { value: 'Assigned', label: 'تم التعيين' },
      { value: 'Active', label: 'نشطة' },
      { value: 'Closed', label: 'مغلقة' },
      { value: 'Abandoned', label: 'متروكة' },
    ],
    channel: [
      { value: 'Student', label: 'طالب' },
      { value: 'Guest', label: 'زائر' },
    ],
  },
  'comments-community': {
    recordType: [
      { value: 'lesson-comment', label: 'تعليق على حصة' },
      { value: 'community-post', label: 'منشور مجتمع' },
    ],
    status: [
      { value: 'Pending', label: 'قيد المراجعة' },
      { value: 'Approved', label: 'مقبول' },
      { value: 'Rejected', label: 'مرفوض' },
    ],
  },
};

const numberOptions = [0, 1, 5, 10, 25, 50, 100, 250, 500, 1000]
  .map((value) => ({ value: String(value), label: value.toLocaleString('ar-EG') }));

function dateOptions() {
  const now = new Date();
  const asDate = (date: Date) => [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-');
  const shiftDays = (days: number) => {
    const date = new Date(now);
    date.setDate(date.getDate() - days);
    return date;
  };
  return [
    { value: asDate(now), label: 'اليوم' },
    { value: asDate(shiftDays(1)), label: 'أمس' },
    { value: asDate(shiftDays(7)), label: 'منذ 7 أيام' },
    { value: asDate(shiftDays(30)), label: 'منذ 30 يومًا' },
    { value: asDate(new Date(now.getFullYear(), now.getMonth(), 1)), label: 'بداية الشهر الحالي' },
    { value: asDate(new Date(now.getFullYear(), 0, 1)), label: 'بداية السنة الحالية' },
  ];
}

function mapCatalogDomain(serverDomain: AdvancedReportCatalogDomain, filterOptions: AdvancedReportFilterOption[] = []): ReportDomainDefinition {
  const local = reportCatalog.find((item) => item.id === serverDomain.key) ?? reportCatalog[0];
  return {
    id: serverDomain.key,
    label: serverDomain.label,
    description: serverDomain.description,
    icon: local.icon,
    defaultColumns: serverDomain.defaultColumns,
    fields: serverDomain.fields.map((field) => {
      const presetOptions = domainFieldOptions[serverDomain.key]?.[field.key];
      const lookupOptions = filterOptions
        .filter((option) => option.field === field.key)
        .map(({ value, label }) => ({ value, label }));
      return {
        key: field.key,
        label: field.label,
        kind: field.type === 'boolean' || presetOptions || lookupOptions.length ? 'select' : field.type,
        valueType: field.type,
        options: field.type === 'boolean'
          ? [{ value: 'true', label: 'نعم' }, { value: 'false', label: 'لا' }]
          : presetOptions ?? lookupOptions,
        operators: field.operators,
      };
    }),
  };
}

function formatCell(value: unknown, column: AdvancedReportColumn) {
  if (value === null || value === undefined || value === '') return '—';
  const translated = quickReportValueLabels.get(`${column.key}:${String(value)}`);
  if (translated) return translated;
  if (column.type === 'date' || column.type === 'datetime') {
    const date = new Date(String(value));
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' });
  }
  if (column.type === 'currency' && typeof value === 'number') {
    return new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', maximumFractionDigits: 2 }).format(value);
  }
  if (column.type === 'percentage' && typeof value === 'number') return `${value.toLocaleString('ar-EG')}٪`;
  if (typeof value === 'number') return value.toLocaleString('ar-EG');
  if (typeof value === 'boolean') return value ? 'نعم' : 'لا';
  return String(value);
}

function FilterRow({ filter, fields, onChange, onRemove }: {
  filter: AdvancedReportFilter;
  fields: ReportFieldDefinition[];
  onChange: (next: AdvancedReportFilter) => void;
  onRemove: () => void;
}) {
  const definition = fields.find((field) => field.key === filter.field);
  const availableOperators = definition?.operators?.length
    ? operators.filter((operator) => definition.operators?.includes(operator.value))
    : operators;
  const noValue = filter.operator === 'is-empty' || filter.operator === 'not-empty';
  const valueCount = filter.operator === 'between' ? 2 : 1;
  const choiceOptions = definition?.options?.length
    ? definition.options
    : definition?.valueType === 'number' || definition?.kind === 'number'
      ? numberOptions
      : definition?.valueType === 'date' || definition?.kind === 'date'
        ? dateOptions()
        : [];
  const isBooleanChoice = definition?.valueType === 'boolean' || (
    choiceOptions.length === 2 && choiceOptions.every((option) => option.value === 'true' || option.value === 'false')
  );
  const updateValue = (index: number, value: string) => {
    const values = [...filter.values];
    values[index] = definition?.valueType === 'number' || definition?.kind === 'number'
      ? Number(value)
      : isBooleanChoice
        ? value === 'true'
        : value;
    onChange({ ...filter, values });
  };

  return (
    <div className="grid gap-2 rounded-2xl bg-[var(--admin-bg)] p-3 md:grid-cols-[minmax(150px,1.2fr)_minmax(125px,.8fr)_minmax(180px,1.4fr)_40px]">
      <select aria-label="حقل الفلتر" value={filter.field} onChange={(event) => { const nextField = fields.find((field) => field.key === event.target.value); onChange({ ...filter, field: event.target.value, operator: (nextField?.operators?.[0] as ReportFilterOperator | undefined) ?? 'eq', values: [] }); }} className="h-10 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-bold outline-none focus:border-[var(--admin-primary)]">
        <option value="">اختر الحقل</option>
        {fields.map((field) => <option key={field.key} value={field.key}>{field.label}</option>)}
      </select>
      <select aria-label="نوع المقارنة" value={filter.operator} onChange={(event) => onChange({ ...filter, operator: event.target.value as ReportFilterOperator, values: [] })} className="h-10 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm outline-none focus:border-[var(--admin-primary)]">
        {availableOperators.map((operator) => <option key={operator.value} value={operator.value}>{operator.label}</option>)}
      </select>
      <div className={`grid gap-2 ${valueCount === 2 ? 'grid-cols-2' : ''}`}>
        {!noValue && Array.from({ length: valueCount }).map((_, index) => (
          <select key={index} aria-label={index ? 'القيمة الثانية' : 'قيمة الفلتر'} value={String(filter.values[index] ?? '')} onChange={(event) => updateValue(index, event.target.value)} disabled={!definition || !choiceOptions.length} className="h-11 min-w-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-medium outline-none focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20 disabled:cursor-not-allowed disabled:opacity-60">
            <option value="">{!definition ? 'اختر الحقل أولًا' : choiceOptions.length ? `اختر ${index ? 'نهاية المدة' : definition.label}` : 'لا توجد اختيارات متاحة'}</option>
            {choiceOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        ))}
        {noValue ? <span className="flex h-10 items-center text-xs font-bold text-[var(--admin-muted)]">لا يحتاج قيمة</span> : null}
      </div>
      <button type="button" onClick={onRemove} aria-label="حذف الفلتر" className="flex h-10 w-10 items-center justify-center rounded-xl text-[var(--admin-danger)] transition hover:bg-[var(--admin-danger)]/10"><X className="h-4 w-4" /></button>
    </div>
  );
}

function FilterGroupEditor({ group, fields, nested = false, onChange, onRemove }: {
  group: AdvancedReportFilterGroup;
  fields: ReportFieldDefinition[];
  nested?: boolean;
  onChange: (next: AdvancedReportFilterGroup) => void;
  onRemove?: () => void;
}) {
  const updateFilter = (index: number, filter: AdvancedReportFilter) => {
    const filters = [...group.filters];
    filters[index] = filter;
    onChange({ ...group, filters });
  };
  return (
    <div className={`${nested ? 'border-r-2 border-[var(--admin-primary)]/30 pr-3' : ''} space-y-3`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-xs font-black text-[var(--admin-muted)]">
          <span>{nested ? 'مجموعة فرعية' : 'تطبيق الشروط'}</span>
          <button type="button" onClick={() => onChange({ ...group, logic: group.logic === 'and' ? 'or' : 'and' })} className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-[var(--admin-primary)]">{group.logic === 'and' ? 'كلها (و)' : 'أي منها (أو)'}</button>
        </div>
        {onRemove ? <button type="button" onClick={onRemove} className="text-xs font-bold text-[var(--admin-danger)]">حذف المجموعة</button> : null}
      </div>
      {group.filters.map((filter, index) => (
        <FilterRow key={filter.id} filter={filter} fields={fields} onChange={(next) => updateFilter(index, next)} onRemove={() => onChange({ ...group, filters: group.filters.filter((item) => item.id !== filter.id) })} />
      ))}
      {group.groups.map((child, index) => (
        <FilterGroupEditor key={child.id} nested group={child} fields={fields} onChange={(next) => { const groups = [...group.groups]; groups[index] = next; onChange({ ...group, groups }); }} onRemove={() => onChange({ ...group, groups: group.groups.filter((item) => item.id !== child.id) })} />
      ))}
      {!group.filters.length && !group.groups.length ? <p className="rounded-xl bg-[var(--admin-bg)] px-3 py-3 text-sm font-medium text-[var(--admin-muted)]">اختَر ما تريد عرضه أولًا، مثل مدرس أو محتوى. يمكنك إنشاء التقرير بدون شروط أيضًا.</p> : null}
      <div className="flex flex-wrap gap-2">
        <button type="button" onClick={() => onChange({ ...group, filters: [...group.filters, newFilter()] })} className="inline-flex items-center gap-1 rounded-xl bg-[var(--admin-primary)] px-3 py-2 text-xs font-black text-[var(--admin-primary-contrast)]"><Plus className="h-3.5 w-3.5" /> إضافة شرط</button>
        {!nested ? <button type="button" onClick={() => onChange({ ...group, groups: [...group.groups, newGroup()] })} className="inline-flex items-center gap-1 rounded-xl border border-[var(--admin-border)] px-3 py-2 text-xs font-black hover:bg-[var(--admin-hover)]"><Filter className="h-3.5 w-3.5" /> شروط متقدمة (و/أو)</button> : null}
      </div>
    </div>
  );
}

function ResultsChart({ result }: { result: AdvancedReportResult }) {
  const points = result.chart?.points ?? [];
  const max = Math.max(...points.map((point) => point.value), 1);
  return (
    <section aria-labelledby="report-chart-title" className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 lg:p-6">
      <div className="mb-6 flex items-center justify-between gap-3">
        <div><p className="text-xs font-black text-[var(--admin-primary)]">الرسم البياني</p><h2 id="report-chart-title" className="mt-1 text-lg font-extrabold">{result.chart?.label || result.chart?.title || 'توزيع النتائج'}</h2></div>
        <BarChart3 className="h-5 w-5 text-[var(--admin-muted)]" />
      </div>
      {points.length ? (
        <div role="img" aria-label={`${result.chart.label || result.chart.title}: ${points.map((point) => `${point.label} ${point.value}`).join('، ')}`} className="flex h-64 items-end gap-2 overflow-x-auto border-b border-[var(--admin-border)] px-1 pt-4">
          {points.map((point) => (
            <div key={point.label} className="flex h-full min-w-14 flex-1 flex-col items-center justify-end gap-2">
              <span className="text-[11px] font-black text-[var(--admin-text)]">{point.value.toLocaleString('ar-EG')}</span>
              <div className="w-full max-w-16 rounded-t-xl bg-[var(--admin-primary)] transition-[height] duration-500 motion-reduce:transition-none" style={{ height: `${Math.max(4, (point.value / max) * 78)}%` }} />
              <span className="h-9 max-w-20 truncate text-[10px] font-bold text-[var(--admin-muted)]" title={point.label}>{point.label}</span>
            </div>
          ))}
        </div>
      ) : <p className="py-20 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد نقاط كافية للرسم.</p>}
    </section>
  );
}

export function AdvancedReportsCenter({ audience }: { audience: ReportAudience }) {
  const fallbackDomains = useMemo(() => reportCatalog.filter((domain) => audience === 'admin' || !domain.adminOnly), [audience]);
  const [domains, setDomains] = useState(fallbackDomains);
  const [domainId, setDomainId] = useState(fallbackDomains[0].id);
  const [filterGroup, setFilterGroup] = useState<AdvancedReportFilterGroup>(newGroup);
  const [result, setResult] = useState<AdvancedReportResult | null>(null);
  const [saved, setSaved] = useState<SavedAdvancedReport[]>([]);
  const [loading, setLoading] = useState(false);
  const [savedLoading, setSavedLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [search, setSearch] = useState('');
  const [reportName, setReportName] = useState('');
  const [isSaveModalOpen, setIsSaveModalOpen] = useState(false);
  const [isSavingDefinition, setIsSavingDefinition] = useState(false);
  const [activeDefinitionId, setActiveDefinitionId] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<AdvancedReportQuery['sort']>();
  const [selectedColumns, setSelectedColumns] = useState<string[]>([]);
  const [quickCourses, setQuickCourses] = useState<string[]>([]);
  const [quickTypes, setQuickTypes] = useState<QuickReportType[]>(['purchase']);
  const [quickValues, setQuickValues] = useState<QuickReportValues>(initialQuickValues);
  const [ledgerTeachers, setLedgerTeachers] = useState<TeacherDto[]>([]);
  const [ledgerTeacherId, setLedgerTeacherId] = useState('');
  const [ledgerLoading, setLedgerLoading] = useState(false);
  const domain = domains.find((item) => item.id === domainId) ?? domains[0];

  const query = useMemo<AdvancedReportQuery>(() => ({
    domain: domainId, filterGroup, columns: selectedColumns, sort, page, pageSize: PAGE_SIZE,
  }), [domainId, filterGroup, page, selectedColumns, sort]);

  const loadSaved = useCallback(async () => {
    setSavedLoading(true);
    try { setSaved(await advancedReportService.listSaved(audience)); }
    catch (requestError) { if (getStatus(requestError) === 403) setForbidden(true); }
    finally { setSavedLoading(false); }
  }, [audience]);

  useEffect(() => {
    void loadSaved();
    void Promise.all([advancedReportService.getCatalog(audience), advancedReportService.getFilterOptions(audience).catch(() => ({ options: [] }))])
      .then(([catalog, lookups]) => {
        const options = lookups?.options ?? [];
        const fromServer = (catalog?.domains ?? [])
          .filter((item) => item.isAvailable)
          .map((item) => mapCatalogDomain(item, options));
        if (fromServer.length) {
          setDomains(fromServer);
          setDomainId((current) => fromServer.some((item) => item.id === current) ? current : fromServer[0].id);
        }
      }).catch(() => {
        // The local catalog keeps the builder usable while the endpoint is temporarily unavailable.
      });
  }, [audience, loadSaved]);

  useEffect(() => {
    if (audience !== 'admin') return;
    void teacherService.getTeachers().then((response) => setLedgerTeachers(response.data ?? [])).catch(() => setLedgerTeachers([]));
  }, [audience]);

  const runReport = useCallback(async (nextQuery = query) => {
    setLoading(true); setError(null); setForbidden(false);
    try { setResult(await advancedReportService.run(audience, nextQuery)); }
    catch (requestError) {
      if (getStatus(requestError) === 403) setForbidden(true);
      else setError('تعذر إنشاء التقرير الآن. راجع الفلاتر ثم أعد المحاولة.');
    } finally { setLoading(false); }
  }, [audience, query]);

  const courseOptions = useMemo(() => domains.find((item) => item.id === 'student-journey')?.fields
    .find((field) => field.key === 'packageName')?.options ?? [], [domains]);

  const toggleQuickType = (type: QuickReportType) => {
    setQuickTypes((current) => current.includes(type) ? current.filter((item) => item !== type) : [...current, type]);
  };

  const toggleQuickValue = (type: QuickReportType, value: string) => {
    setQuickValues((current) => ({
      ...current,
      [type]: current[type].includes(value) ? current[type].filter((item) => item !== value) : [...current[type], value],
    }));
  };

  const runQuickReport = () => {
    if (!quickCourses.length) { toast.error('اختر كورسًا واحدًا على الأقل.'); return; }
    if (!quickTypes.length) { toast.error('اختر نوع بيانات واحدًا على الأقل.'); return; }
    if (quickTypes.some((type) => quickValues[type].length === 0)) { toast.error('اختر حالة واحدة على الأقل لكل نوع.'); return; }
    const nextDomain = 'student-journey';
    const nextGroup = newGroup();
    const courseFilter = newFilter('packageName');
    courseFilter.operator = 'in';
    courseFilter.values = quickCourses;
    nextGroup.filters = [courseFilter, ...quickTypes.map((type) => {
      const definition = quickReportTypes.find((item) => item.id === type)!;
      const filter = newFilter(definition.field);
      filter.operator = 'in';
      filter.values = quickValues[type];
      return filter;
    })];
    const nextDefinition = domains.find((item) => item.id === nextDomain);
    const nextQuery: AdvancedReportQuery = { domain: nextDomain, filterGroup: nextGroup, columns: nextDefinition?.defaultColumns ?? [], page: 1, pageSize: PAGE_SIZE };
    setDomainId(nextDomain); setFilterGroup(nextGroup); setSelectedColumns(nextDefinition?.defaultColumns ?? []); setPage(1); setSort(undefined); setResult(null); setActiveDefinitionId(null);
    setReportName(`رحلة الطالب - ${quickTypes.map((type) => quickReportTypes.find((item) => item.id === type)?.label).join(' + ')} - ${quickCourses.length === 1 ? courseOptions.find((option) => option.value === quickCourses[0])?.label ?? quickCourses[0] : `${quickCourses.length} كورسات`}`);
    void runReport(nextQuery);
  };

  const exportStudentLedger = async () => {
    if (audience === 'admin' && !ledgerTeacherId) { toast.error('اختر المدرس أولًا.'); return; }
    setLedgerLoading(true);
    try {
      const blob = await advancedReportService.exportStudentLedger(audience, audience === 'admin' ? ledgerTeacherId : undefined);
      const teacherName = ledgerTeachers.find((teacher) => teacher.id === ledgerTeacherId)?.fullName;
      downloadBlob(blob, `سجل-الطلاب${teacherName ? `-${teacherName}` : ''}.xlsx`);
      toast.success('تم تجهيز سجل الطلاب');
    } catch { toast.error('تعذر تجهيز سجل الطلاب.'); }
    finally { setLedgerLoading(false); }
  };

  const selectDomain = (next: ReportDomainDefinition) => {
    setDomainId(next.id); setFilterGroup(newGroup()); setSelectedColumns(next.defaultColumns ?? []); setPage(1); setSort(undefined); setResult(null); setActiveDefinitionId(null); setReportName('');
  };

  const saveDefinition = async () => {
    const automaticName = reportName.trim() || `${domain.label} - ${new Date().toLocaleDateString('ar-EG')}`;
    setIsSavingDefinition(true);
    try {
      const payload = { name: automaticName, configuration: query };
      if (activeDefinitionId) await advancedReportService.update(audience, activeDefinitionId, { ...payload, version: saved.find((item) => item.id === activeDefinitionId)?.version });
      else {
        const created = await advancedReportService.save(audience, payload);
        setActiveDefinitionId(created.id);
      }
      setReportName(automaticName);
      toast.success('تم حفظ تعريف التقرير'); await loadSaved();
      setIsSaveModalOpen(false);
    } catch { toast.error('تعذر حفظ التقرير.'); }
    finally { setIsSavingDefinition(false); }
  };

  const loadDefinition = (definition: SavedAdvancedReport) => {
    setDomainId(definition.domain); setFilterGroup(hydrateGroup(definition.configuration.filterGroup)); setSelectedColumns(definition.configuration.columns ?? []); setPage(1); setSort(definition.configuration.sort); setReportName(definition.name); setActiveDefinitionId(definition.id); setResult(null);
  };

  const duplicateDefinition = async (definition: SavedAdvancedReport) => {
    try { await advancedReportService.save(audience, { name: `${definition.name} - نسخة`, configuration: definition.configuration }); toast.success('تم نسخ التقرير'); await loadSaved(); }
    catch { toast.error('تعذر نسخ التقرير.'); }
  };

  const removeDefinition = async (definition: SavedAdvancedReport) => {
    try { await advancedReportService.remove(audience, definition.id); setSaved((items) => items.filter((item) => item.id !== definition.id)); if (activeDefinitionId === definition.id) setActiveDefinitionId(null); toast.success('تم حذف التقرير'); }
    catch { toast.error('تعذر حذف التقرير.'); }
  };

  const exportReport = async (format: 'xlsx' | 'pdf') => {
    if (!result?.rows.length) return;
    try {
      const blob = await advancedReportService.exportFile(audience, format, { ...query, page: 1 });
      downloadBlob(blob, `${reportName || domain.label}.${format}`);
    } catch { toast.error(`تعذر تجهيز ملف ${format === 'xlsx' ? 'Excel' : 'PDF'}.`); }
  };

  const searchedRows = useMemo(() => {
    if (!result) return [];
    const term = search.trim().toLocaleLowerCase('ar');
    if (!term) return result.rows;
    return result.rows.filter((row) => result.columns.some((column) => formatCell(row[column.key], column).toLocaleLowerCase('ar').includes(term)));
  }, [result, search]);

  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / PAGE_SIZE));

  if (forbidden) return <div role="alert" className="rounded-[28px] border border-[var(--admin-danger)]/30 bg-[var(--admin-card)] px-6 py-16 text-center"><ShieldMessage title="غير مصرح بعرض التقارير" body="اطلب صلاحية التقارير من مدير المنصة." /></div>;

  return (
    <div className="space-y-6" dir="rtl">
      <section aria-labelledby="report-picker-title" className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
        <div className="border-b border-[var(--admin-border)] px-4 py-4 sm:px-5">
          <h2 id="report-picker-title" className="text-base font-extrabold">تقرير الطلاب السريع</h2>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">اختار أكتر من نوع وحالة وكورس، ثم اعرض الطلاب مرة واحدة.</p>
        </div>

        <div className="space-y-5 p-4 sm:p-5">
          <fieldset>
            <legend className="mb-2 text-xs font-black text-[var(--admin-muted)]"><span className="ml-1 text-[var(--admin-primary)]">١</span> نوع البيانات <span className="font-bold">(يمكن اختيار أكثر من نوع)</span></legend>
            <div className="flex flex-wrap gap-2">
              {quickReportTypes.map((type) => {
                const checked = quickTypes.includes(type.id);
                return <label key={type.id} className={`inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border px-4 text-sm font-black transition-colors ${checked ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-text)] hover:border-[var(--admin-primary)]'}`}>
                  <input type="checkbox" className="sr-only" checked={checked} onChange={() => toggleQuickType(type.id)} />
                  <span className={`grid h-5 w-5 place-items-center rounded-md border ${checked ? 'border-current bg-[var(--admin-primary-contrast)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)]'}`}>{checked ? <Check className="h-3.5 w-3.5" /> : null}</span>
                  {type.label}
                </label>;
              })}
            </div>
          </fieldset>

          {quickTypes.length ? <div className="grid gap-3 lg:grid-cols-2">
            {quickReportTypes.filter((type) => quickTypes.includes(type.id)).map((type) => (
              <fieldset key={type.id} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3">
                <legend className="px-1 text-xs font-black text-[var(--admin-text)]">حالة {type.label}</legend>
                <div className="flex flex-wrap gap-2">
                  {type.options.map((option) => {
                    const checked = quickValues[type.id].includes(option.value);
                    return <label key={option.value} className={`cursor-pointer rounded-lg border px-3 py-2 text-xs font-black transition-colors ${checked ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}>
                      <input type="checkbox" className="sr-only" checked={checked} onChange={() => toggleQuickValue(type.id, option.value)} />
                      {option.label}
                    </label>;
                  })}
                </div>
              </fieldset>
            ))}
          </div> : <p role="status" className="rounded-xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)]">اختار نوع بيانات واحدًا على الأقل.</p>}

          <fieldset className="rounded-xl border border-[var(--admin-border)] p-3">
            <legend className="px-1 text-xs font-black text-[var(--admin-muted)]"><span className="ml-1 text-[var(--admin-primary)]">٢</span> الكورسات <span className="font-bold">(يمكن اختيار أكثر من كورس)</span></legend>
            <div className="mb-3 flex flex-wrap items-center gap-2">
              <button type="button" onClick={() => setQuickCourses(courseOptions.map((option) => option.value))} className="rounded-lg border border-[var(--admin-border)] px-3 py-2 text-xs font-black text-[var(--admin-primary)]">تحديد الكل</button>
              {quickCourses.length ? <button type="button" onClick={() => setQuickCourses([])} className="rounded-lg px-3 py-2 text-xs font-black text-[var(--admin-danger)]">مسح الاختيار</button> : null}
              <span className="text-xs font-bold text-[var(--admin-muted)]">{quickCourses.length ? `${quickCourses.length.toLocaleString('ar-EG')} محدد` : 'لم يتم اختيار كورس'}</span>
            </div>
            <div className="grid max-h-52 gap-2 overflow-y-auto pl-1 sm:grid-cols-2 xl:grid-cols-3">
              {courseOptions.map((option) => {
                const checked = quickCourses.includes(option.value);
                return <label key={option.value} className={`flex min-h-10 cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-xs font-bold ${checked ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-text)]'}`}>
                  <input type="checkbox" checked={checked} onChange={() => setQuickCourses((current) => checked ? current.filter((value) => value !== option.value) : [...current, option.value])} className="h-4 w-4 accent-[var(--admin-primary)]" />
                  <span className="min-w-0 truncate" title={option.label}>{option.label}</span>
                </label>;
              })}
              {!courseOptions.length ? <p className="col-span-full py-3 text-center text-xs font-bold text-[var(--admin-muted)]">جاري تحميل الكورسات…</p> : null}
            </div>
          </fieldset>

          <button type="button" onClick={runQuickReport} disabled={!quickCourses.length || !quickTypes.length || loading} className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto sm:min-w-48">
            {loading ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <BarChart3 className="h-4 w-4" />} عرض الطلاب
          </button>

          <details className="border-t border-[var(--admin-border)] pt-4">
            <summary className="cursor-pointer text-xs font-black text-[var(--admin-muted)]">تقرير مختلف: الطلاب أو الأكواد أو الرصيد أو المحتوى…</summary>
            <div className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
              <select aria-label="نوع التقرير المختلف" value={domainId === 'student-journey' ? '' : domainId} onChange={(event) => { const selectedDomain = domains.find((option) => option.id === event.target.value); if (selectedDomain) selectDomain(selectedDomain); }} className="h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20">
                <option value="">اختر التقرير المختلف</option>
                {domains.filter((option) => option.id !== 'student-journey').map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
              </select>
              <button type="button" onClick={() => void runReport()} disabled={loading || domainId === 'student-journey'} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary)] disabled:opacity-50">عرض التقرير المختلف</button>
            </div>
          </details>
        </div>
      </section>

      <section aria-labelledby="student-ledger-title" className="relative rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:flex sm:items-end sm:gap-4">
        <div className="min-w-0 flex-1"><p className="text-xs font-black text-[var(--admin-primary)]">سجل حياة الطالب</p><h2 id="student-ledger-title" className="mt-1 text-base font-extrabold">تحميل سجل الطلاب في شيت واحد</h2><p className="mt-1 text-xs font-medium text-[var(--admin-muted)]">صف لكل طالب، وكل باقات المدرس والحضور والواجبات وامتحانات الحصة والفيديو في أعمدة متجاورة.</p>{audience === 'admin' ? <Dropdown value={ledgerTeacherId} onChange={(value) => setLedgerTeacherId(Array.isArray(value) ? value[0] ?? '' : value)} options={ledgerTeachers.map((teacher) => ({ value: teacher.id, label: teacher.fullName }))} placeholder="اختر المدرس" searchable searchPlaceholder="ابحث باسم المدرس" className="mt-3" /> : <p className="mt-3 rounded-xl bg-[var(--admin-primary-15)] px-3 py-2 text-xs font-bold text-[var(--admin-primary)]">سيتم تحميل طلابك وكل باقاتك تلقائيًا.</p>}</div>
        <button type="button" onClick={() => void exportStudentLedger()} disabled={ledgerLoading} className="mt-3 inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-60 sm:mt-0">{ledgerLoading ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <FileSpreadsheet className="h-4 w-4" />} تحميل سجل الطلاب</button>
      </section>

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_290px]">
        <div className="space-y-6">
          <section className="overflow-hidden rounded-[30px] border border-[var(--admin-border)] bg-[var(--admin-card)]">
            <div className="border-b border-[var(--admin-border)] p-5 lg:p-6">
              <div><p className="text-xs font-black text-[var(--admin-primary)]">منشئ التقرير</p><h2 className="mt-1 text-xl font-extrabold">{domain.label}</h2><p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">{domain.description}</p></div>
            </div>
            <div className="space-y-5 p-5 lg:p-6">
              <details className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                <summary className="cursor-pointer text-sm font-black">خيارات متقدمة <span className="mr-1 text-xs text-[var(--admin-muted)]">لإضافة شروط أو تغيير أعمدة الجدول</span></summary>
                <div className="mt-4 space-y-5">
                  <FilterGroupEditor group={filterGroup} fields={domain.fields} onChange={(next) => { setFilterGroup(next); setPage(1); }} />
                  <div className="border-t border-[var(--admin-border)] pt-4">
                    <p className="mb-3 text-sm font-black">أعمدة الجدول <span className="mr-1 text-xs text-[var(--admin-muted)]">({selectedColumns.length ? `${selectedColumns.length} محددة` : 'الافتراضية'})</span></p>
                    <div className="flex flex-wrap gap-2">
                      {domain.fields.map((field) => {
                        const checked = selectedColumns.includes(field.key);
                        return <label key={field.key} className={`cursor-pointer rounded-full border px-3 py-2 text-xs font-black transition ${checked ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}><input type="checkbox" className="sr-only" checked={checked} onChange={() => setSelectedColumns((current) => checked ? current.filter((key) => key !== field.key) : [...current, field.key])} />{field.label}</label>;
                      })}
                      {selectedColumns.length ? <button type="button" onClick={() => setSelectedColumns([])} className="px-2 text-xs font-black text-[var(--admin-danger)]">استخدام الافتراضي</button> : null}
                    </div>
                  </div>
                </div>
              </details>
              <div className="flex flex-col gap-2 border-t border-[var(--admin-border)] pt-5 sm:flex-row">
                <div className="flex min-h-11 min-w-0 flex-1 items-center rounded-xl bg-[var(--admin-bg)] px-4 text-sm font-bold text-[var(--admin-muted)]">{reportName || `سيُحفظ تلقائيًا باسم: ${domain.label} - تاريخ اليوم`}</div>
                <button type="button" onClick={() => setIsSaveModalOpen(true)} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary)]"><Save className="h-4 w-4" />{activeDefinitionId ? 'تحديث الحفظ' : 'حفظ التقرير'}</button>
              </div>
            </div>
          </section>

          {loading ? <LoadingState /> : error ? <ErrorState message={error} onRetry={() => void runReport()} /> : result ? (
            <>
              {result.isTruncated && result.notice ? (
                <div role="alert" className="rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm font-bold text-amber-900">
                  {result.notice}
                </div>
              ) : null}
              <section aria-label="مؤشرات التقرير" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {(result.summary ?? []).map((kpi) => <div key={kpi.key} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4"><p className="text-xs font-bold text-[var(--admin-muted)]">{kpi.label}</p><p className="mt-2 text-2xl font-black tabular-nums">{valueText(kpi.value)}</p>{typeof kpi.change === 'number' ? <p className={`mt-1 text-xs font-black ${kpi.change >= 0 ? 'text-emerald-600' : 'text-[var(--admin-danger)]'}`}>{kpi.change >= 0 ? '+' : ''}{kpi.change}٪ عن الفترة السابقة</p> : null}</div>)}
              </section>
              <ResultsChart result={result} />
              <section className="overflow-hidden rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)]">
                <div className="flex flex-col gap-3 border-b border-[var(--admin-border)] p-4 md:flex-row md:items-center md:justify-between">
                  <div><h2 className="text-lg font-extrabold">الجدول التفصيلي</h2><p className="text-xs font-bold text-[var(--admin-muted)]">{result.totalCount.toLocaleString('ar-EG')} نتيجة</p></div>
                  <div className="flex flex-wrap items-start gap-2"><label className="relative min-w-52 flex-1"><Search className="absolute right-3 top-3 h-4 w-4 text-[var(--admin-muted)]" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="بحث في النتائج الظاهرة" className="h-10 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] pr-9 pl-3 text-sm outline-none" /><span className="mt-1 block px-1 text-[10px] font-bold text-[var(--admin-muted)]">البحث داخل الصفحة الحالية فقط</span></label><button type="button" onClick={() => void exportReport('xlsx')} className="inline-flex h-10 items-center gap-1.5 rounded-xl border border-[var(--admin-border)] px-3 text-xs font-black"><FileSpreadsheet className="h-4 w-4" />Excel</button><button type="button" onClick={() => void exportReport('pdf')} className="inline-flex h-10 items-center gap-1.5 rounded-xl border border-[var(--admin-border)] px-3 text-xs font-black"><Download className="h-4 w-4" />PDF</button></div>
                </div>
                {searchedRows.length ? <div className="overflow-x-auto"><table className="w-full min-w-[760px] border-collapse text-right text-sm"><thead><tr className="bg-[var(--admin-bg)]">{result.columns.map((column) => <th key={column.key} scope="col" className="whitespace-nowrap px-4 py-3 text-xs font-black text-[var(--admin-muted)]"><button type="button" onClick={() => { const direction = sort?.field === column.key && sort.direction === 'asc' ? 'desc' : 'asc'; setSort({ field: column.key, direction }); setPage(1); void runReport({ ...query, sort: { field: column.key, direction }, page: 1 }); }} className="hover:text-[var(--admin-primary)]">{column.label}{sort?.field === column.key ? sort.direction === 'asc' ? ' ↑' : ' ↓' : ''}</button></th>)}</tr></thead><tbody>{searchedRows.map((row, rowIndex) => <tr key={String(row.id ?? rowIndex)} className="border-t border-[var(--admin-border)] hover:bg-[var(--admin-hover)]">{result.columns.map((column) => <td key={column.key} className="max-w-72 whitespace-nowrap px-4 py-3 font-medium" title={formatCell(row[column.key], column)}>{formatCell(row[column.key], column)}</td>)}</tr>)}</tbody></table></div> : <EmptyState />}
                <div className="flex items-center justify-between border-t border-[var(--admin-border)] px-4 py-3"><span className="text-xs font-bold text-[var(--admin-muted)]">صفحة {page.toLocaleString('ar-EG')} من {totalPages.toLocaleString('ar-EG')}</span><div className="flex gap-2"><button type="button" disabled={page <= 1} onClick={() => { const next = page - 1; setPage(next); void runReport({ ...query, page: next }); }} aria-label="الصفحة السابقة" className="rounded-lg border border-[var(--admin-border)] p-2 disabled:opacity-40"><ChevronRight className="h-4 w-4" /></button><button type="button" disabled={page >= totalPages} onClick={() => { const next = page + 1; setPage(next); void runReport({ ...query, page: next }); }} aria-label="الصفحة التالية" className="rounded-lg border border-[var(--admin-border)] p-2 disabled:opacity-40"><ChevronLeft className="h-4 w-4" /></button></div></div>
              </section>
            </>
          ) : <StartState />}
        </div>

        <aside className="rounded-[26px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 xl:sticky xl:top-6">
          <div className="mb-4 flex items-center justify-between"><div><p className="text-xs font-black text-[var(--admin-primary)]">المحفوظة</p><h2 className="mt-1 font-extrabold">تقاريري</h2></div><RefreshCw className={`h-4 w-4 text-[var(--admin-muted)] ${savedLoading ? 'animate-spin' : ''}`} /></div>
          {savedLoading ? <div className="space-y-2">{[1, 2, 3].map((item) => <div key={item} className="h-16 animate-pulse rounded-xl bg-[var(--admin-hover)]" />)}</div> : saved.length ? <div className="max-h-[520px] space-y-2 overflow-y-auto">{saved.map((definition) => <div key={definition.id} className={`rounded-2xl border p-3 ${activeDefinitionId === definition.id ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)]' : 'border-[var(--admin-border)]'}`}><button type="button" onClick={() => loadDefinition(definition)} className="w-full text-right"><span className="block truncate text-sm font-black">{definition.name}</span><span className="mt-1 block text-[11px] font-bold text-[var(--admin-muted)]">{domains.find((item) => item.id === definition.domain)?.label ?? definition.domain}</span></button><div className="mt-2 flex justify-end gap-1"><button type="button" onClick={() => void duplicateDefinition(definition)} aria-label={`نسخ ${definition.name}`} className="rounded-lg p-1.5 text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]"><Copy className="h-3.5 w-3.5" /></button><button type="button" onClick={() => void removeDefinition(definition)} aria-label={`حذف ${definition.name}`} className="rounded-lg p-1.5 text-[var(--admin-danger)] hover:bg-[var(--admin-danger)]/10"><Trash2 className="h-3.5 w-3.5" /></button></div></div>)}</div> : <p className="rounded-2xl bg-[var(--admin-bg)] p-5 text-center text-xs font-bold leading-6 text-[var(--admin-muted)]">احفظ تركيبة الفلاتر لتشغيلها مرة أخرى بضغطة واحدة.</p>}
        </aside>
      </div>

      <AdminModal
        open={isSaveModalOpen}
        onClose={() => !isSavingDefinition && setIsSaveModalOpen(false)}
        title={activeDefinitionId ? 'تحديث التقرير المحفوظ' : 'حفظ التقرير'}
        subtitle="اختر اسمًا واضحًا لتتمكن من تشغيل التقرير نفسه لاحقًا."
        maxWidth="max-w-lg"
      >
        <form
          className="space-y-5"
          onSubmit={(event) => {
            event.preventDefault();
            void saveDefinition();
          }}
        >
          <label className="block text-sm font-bold text-[var(--admin-text)]" htmlFor="advanced-report-name">
            اسم التقرير
            <input
              id="advanced-report-name"
              autoFocus
              value={reportName}
              onChange={(event) => setReportName(event.target.value)}
              placeholder={`${domain.label} - ${new Date().toLocaleDateString('ar-EG')}`}
              className="mt-2 h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-medium text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
            />
          </label>
          <p className="text-xs font-medium leading-6 text-[var(--admin-muted)]">اترك الحقل فارغًا لاستخدام الاسم المقترح تلقائيًا.</p>
          <div className="flex flex-col-reverse gap-2 border-t border-[var(--admin-border)] pt-4 sm:flex-row sm:justify-end">
            <button type="button" onClick={() => setIsSaveModalOpen(false)} disabled={isSavingDefinition} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50">إلغاء</button>
            <button type="submit" disabled={isSavingDefinition} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50">
              {isSavingDefinition ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
              {isSavingDefinition ? 'جارٍ الحفظ…' : activeDefinitionId ? 'حفظ التحديث' : 'حفظ التقرير'}
            </button>
          </div>
        </form>
      </AdminModal>
    </div>
  );
}

function ShieldMessage({ title, body }: { title: string; body: string }) { return <><h2 className="text-xl font-black">{title}</h2><p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">{body}</p></>; }
function LoadingState() { return <div aria-live="polite" className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] py-20 text-center"><LoaderCircle className="mx-auto h-8 w-8 animate-spin text-[var(--admin-primary)]" /><p className="mt-3 text-sm font-black">نجمع البيانات ونبني التقرير…</p></div>; }
function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) { return <div role="alert" className="rounded-[28px] border border-[var(--admin-danger)]/30 bg-[var(--admin-card)] py-16 text-center"><ShieldMessage title="لم يكتمل التقرير" body={message} /><button type="button" onClick={onRetry} className="mt-5 rounded-xl bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-black text-[var(--admin-primary-contrast)]">إعادة المحاولة</button></div>; }
function EmptyState() { return <div className="py-16 text-center"><Search className="mx-auto h-7 w-7 text-[var(--admin-muted)]" /><p className="mt-3 text-sm font-black">لا توجد نتائج مطابقة</p><p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">غيّر البحث أو وسّع الفلاتر.</p></div>; }
function StartState() { return <div className="rounded-[28px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] py-20 text-center"><BarChart3 className="mx-auto h-9 w-9 text-[var(--admin-primary)]" /><h2 className="mt-4 text-lg font-black">ابدأ من المجال والفلاتر</h2><p className="mx-auto mt-2 max-w-md text-sm font-bold leading-6 text-[var(--admin-muted)]">يمكنك تشغيل تقرير شامل بلا فلاتر، أو دمج عدة شروط ومجموعات و/أو للوصول إلى الشريحة المطلوبة.</p></div>; }
