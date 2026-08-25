'use client';

import {
  AlertTriangle,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  FileLock2,
  History,
  LoaderCircle,
  Search,
  ShieldCheck,
  ShieldX,
  X,
} from 'lucide-react';
import { type FormEvent, type ReactNode, useEffect, useRef, useState } from 'react';

import { cairoDateTimeLocalToUtcISOString, formatCairoTimestamp } from '@/lib/cairo-time';
import { createClientId } from '@/lib/client-id';
import { maskWhatsAppDestination } from '@/lib/whatsapp-campaign';
import {
  getLiveSupportApiError,
  liveSupportService,
  type WhatsAppContactCandidate,
  type WhatsAppContactCandidatePage,
  type WhatsAppContactCategoryState,
  type WhatsAppContactPreference,
  type WhatsAppContactPreferencePage,
} from '@/services/live-support-service';

const evidenceSources = [
  ['web_consent_form', 'نموذج موافقة إلكتروني'],
  ['signed_document', 'مستند موافقة موقّع'],
  ['recorded_call', 'مكالمة مسجّلة'],
  ['inbound_request', 'طلب وارد موثّق'],
  ['legacy_import', 'دليل تاريخي مُراجع'],
] as const;

type PreferenceView = 'contacts' | 'audit';
type PreferenceCategory = WhatsAppContactPreference['category'];
type PreferenceState = WhatsAppContactPreference['state'];

export function WhatsAppContactPreferencesPanel() {
  const [view, setView] = useState<PreferenceView>('contacts');
  const [search, setSearch] = useState('');
  const [candidatePage, setCandidatePage] = useState<WhatsAppContactCandidatePage>();
  const [auditPage, setAuditPage] = useState<WhatsAppContactPreferencePage>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [panelFeedback, setPanelFeedback] = useState('');
  const [selected, setSelected] = useState<WhatsAppContactCandidate>();
  const [state, setState] = useState<PreferenceState>('OptedOut');
  const [category, setCategory] = useState<PreferenceCategory>('All');
  const [source, setSource] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [effectiveAtLocal, setEffectiveAtLocal] = useState('');
  const [saving, setSaving] = useState(false);
  const [formFeedback, setFormFeedback] = useState('');
  const requestAbortRef = useRef<AbortController | undefined>(undefined);
  const idempotencyKeyRef = useRef(createClientId());

  useEffect(() => () => requestAbortRef.current?.abort(), []);

  async function loadResults(nextPage = 1, targetView: PreferenceView = view) {
    const normalized = search.trim();
    if (normalized.length > 0 && normalized.length < 2) {
      setError('اكتب حرفين على الأقل للبحث.');
      return false;
    }
    if (targetView === 'contacts' && normalized.length < 2) {
      setError('ابحث باسم الطالب أو كوده؛ لا يمكن فتح كل جهات الاتصال دفعة واحدة.');
      return false;
    }
    if (targetView === 'contacts' && looksLikePhoneNumber(normalized)) {
      setError('لخصوصية البيانات ابحث باسم الطالب أو كوده، وليس برقم هاتف كامل.');
      return false;
    }

    requestAbortRef.current?.abort();
    const controller = new AbortController();
    requestAbortRef.current = controller;
    setLoading(true);
    setError('');
    setPanelFeedback('');
    try {
      if (targetView === 'contacts') {
        setCandidatePage(await liveSupportService.searchWhatsAppContactCandidates(
          { search: normalized, page: nextPage, pageSize: 10 },
          controller.signal,
        ));
      } else {
        setAuditPage(await liveSupportService.getWhatsAppContactPreferences(
          { search: normalized, page: nextPage, pageSize: 12 },
          controller.signal,
        ));
      }
      return true;
    } catch (cause) {
      if ((cause as { code?: string })?.code === 'ERR_CANCELED') return false;
      setError(getLiveSupportApiError(
        cause,
        targetView === 'contacts'
          ? 'تعذر البحث عن جهات الاتصال. أعد المحاولة.'
          : 'تعذر تحميل سجل تدقيق التواصل. أعد المحاولة.',
      ));
      return false;
    } finally {
      if (requestAbortRef.current === controller) setLoading(false);
    }
  }

  function switchView(nextView: PreferenceView) {
    requestAbortRef.current?.abort();
    requestAbortRef.current = undefined;
    setView(nextView);
    setLoading(false);
    setError('');
    setPanelFeedback('');
    setSelected(undefined);
  }

  function startRecording(
    candidate: WhatsAppContactCandidate,
    nextCategory: PreferenceCategory,
    nextState: PreferenceState,
  ) {
    setSelected(candidate);
    setState(nextState);
    setCategory(nextState === 'OptedIn' && nextCategory === 'All' ? 'Marketing' : nextCategory);
    setSource('');
    setEvidenceReference('');
    setEffectiveAtLocal(toCairoDateTimeLocal(new Date()));
    setFormFeedback('');
    setPanelFeedback('');
    idempotencyKeyRef.current = createClientId();
  }

  function invalidatePreferenceRequest() {
    idempotencyKeyRef.current = createClientId();
    setFormFeedback('');
  }

  async function submitPreference(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || saving) return;
    if (!source || !evidenceReference.trim() || !effectiveAtLocal) {
      setFormFeedback('مصدر الدليل ومرجعه ووقت الموافقة أو الرفض مطلوبة.');
      return;
    }
    if (state === 'OptedIn' && category === 'All') {
      setFormFeedback('الموافقة يجب أن تكون لفئة Marketing أو Utility؛ «كل الرسائل» متاحة للإلغاء فقط.');
      return;
    }

    setSaving(true);
    setFormFeedback('');
    try {
      await liveSupportService.recordWhatsAppContactPreference({
        studentUserId: selected.studentUserId,
        contactRole: selected.contactRole,
        category,
        state,
        source,
        evidenceReference: evidenceReference.trim(),
        effectiveAt: cairoDateTimeLocalToUtcISOString(effectiveAtLocal),
        expectedLatestPreferenceId: latestPreferenceId(selected, category),
        expectedLatestGlobalPreferenceId: state === 'OptedIn' && category !== 'All'
          ? selected.global.latestPreferenceId ?? null
          : null,
      }, idempotencyKeyRef.current);
      setSelected(undefined);
      idempotencyKeyRef.current = createClientId();
      await loadResults(candidatePage?.page ?? 1, 'contacts');
      setPanelFeedback('تم تسجيل التفضيل مع الدليل في سجل التدقيق.');
    } catch (cause) {
      const message = getLiveSupportApiError(
        cause,
        'تعذر تسجيل التفضيل. حدّث نتائج البحث وراجع الدليل ثم أعد المحاولة.',
      );
      const status = (cause as { response?: { status?: number } } | null)?.response?.status;
      if (status === 409) {
        setSelected(undefined);
        idempotencyKeyRef.current = createClientId();
        const refreshed = await loadResults(candidatePage?.page ?? 1, 'contacts');
        setError(refreshed
          ? `${message} تم تحديث حالة الوجهة؛ راجعها قبل إنشاء قرار جديد.`
          : `${message} تعذر التحديث التلقائي؛ ابحث عن الطالب مرة أخرى قبل أي قرار.`);
      } else {
        setFormFeedback(message);
      }
    } finally {
      setSaving(false);
    }
  }

  const totalAuditPages = auditPage
    ? Math.max(1, Math.ceil(auditPage.total / auditPage.pageSize))
    : 1;

  return (
    <details className="group border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
      <summary className="flex min-h-14 cursor-pointer list-none items-center justify-between gap-4 px-4 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-accent)] sm:px-6">
        <span className="flex min-w-0 items-center gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[var(--admin-primary-15)] text-[var(--admin-primary)]"><FileLock2 aria-hidden="true" size={17} /></span>
          <span className="min-w-0">
            <strong className="block text-sm font-black text-[var(--admin-text)]">إدارة تفضيلات التواصل الموثقة</strong>
            <small className="mt-0.5 block truncate text-xs font-semibold text-[var(--admin-muted)]">قرار منفصل لكل وجهة وفئة، وليس اختيارًا وقت الإرسال</small>
          </span>
        </span>
        <span aria-hidden="true" className="text-xl text-[var(--admin-muted)] transition-transform group-open:rotate-45">+</span>
      </summary>

      <div className="border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6">
        <div className="flex items-start gap-2 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] p-4 text-sm leading-6 text-[var(--admin-warning)]">
          <AlertTriangle aria-hidden="true" size={18} className="mt-0.5 shrink-0" />
          لا تسجل «موافق» إلا من دليل صريح قابل للمراجعة. الشراء أو بدء محادثة دعم لا يعنيان موافقة تسويقية، ويمكن تسجيل الرفض دائمًا.
        </div>

        <div role="tablist" aria-label="أقسام تفضيلات التواصل" className="mt-4 grid min-h-11 grid-cols-2 gap-1 rounded-xl bg-[var(--admin-card-soft)] p-1 sm:max-w-md">
          <PreferenceTab selected={view === 'contacts'} onClick={() => switchView('contacts')} icon={<Search aria-hidden="true" size={15} />}>الجهات والموافقات</PreferenceTab>
          <PreferenceTab selected={view === 'audit'} onClick={() => switchView('audit')} icon={<History aria-hidden="true" size={15} />}>سجل التدقيق</PreferenceTab>
        </div>

        <form className="mt-4 flex flex-col gap-2 sm:flex-row" onSubmit={(event) => { event.preventDefault(); void loadResults(1); }}>
          <label className="min-w-0 flex-1">
            <span className="sr-only">{view === 'contacts' ? 'بحث جهات الاتصال' : 'بحث سجل التدقيق'}</span>
            <span className="flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-3 focus-within:border-[var(--admin-accent)] focus-within:ring-2 focus-within:ring-[var(--admin-accent-soft)]">
              <Search aria-hidden="true" size={16} className="shrink-0 text-[var(--admin-muted)]" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value.slice(0, 100))}
                placeholder={view === 'contacts' ? 'اسم الطالب أو كود الطالب' : 'اسم الطالب أو آخر أرقام الوجهة — اختياري'}
                dir="auto"
                className="min-w-0 flex-1 bg-transparent text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)]"
              />
            </span>
          </label>
          <button type="submit" disabled={loading} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-60">
            {loading ? <LoaderCircle aria-hidden="true" size={16} className="animate-spin" /> : <Search aria-hidden="true" size={16} />}
            {view === 'contacts' ? 'ابحث بأمان' : 'اعرض السجل'}
          </button>
        </form>

        {error ? <p role="alert" className="mt-3 rounded-xl bg-[var(--admin-danger-10)] p-3 text-sm font-semibold text-[var(--admin-danger)]">{error}</p> : null}
        {panelFeedback ? <p role="status" className="mt-3 rounded-xl bg-[var(--admin-success-10)] p-3 text-sm font-semibold text-[var(--admin-success)]">{panelFeedback}</p> : null}

        {view === 'contacts' ? (
          <CandidateResults page={candidatePage} loading={loading} onPageChange={(nextPage) => void loadResults(nextPage, 'contacts')} onRecord={startRecording} />
        ) : (
          <AuditResults page={auditPage} loading={loading} totalPages={totalAuditPages} onPageChange={(nextPage) => void loadResults(nextPage, 'audit')} />
        )}

        {selected ? (
          <form onSubmit={(event) => void submitPreference(event)} className="mt-5 rounded-xl border border-[var(--admin-accent)] bg-[var(--admin-accent-soft)] p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <h3 className="truncate font-black text-[var(--admin-text)]" title={selected.studentName}>تسجيل قرار موثق — {selected.studentName}</h3>
                <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]"><bdi dir="ltr">{maskWhatsAppDestination(selected.maskedDestination)}</bdi> · {contactRoleLabel(selected.contactRole)}</p>
              </div>
              <button type="button" aria-label="إغلاق نموذج القرار" onClick={() => setSelected(undefined)} className="grid size-11 place-items-center rounded-xl text-[var(--admin-muted)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"><X aria-hidden="true" size={18} /></button>
            </div>
            <p className="mt-3 rounded-lg bg-[var(--admin-card)] px-3 py-2 text-xs leading-5 text-[var(--admin-muted)]">هذا تسجيل جديد غير قابل للمحو، وليس مفتاح تشغيل فوريًا. الحالة الفعلية تُحسب من أحدث دليل للفئة وأي إلغاء شامل.</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <SelectField label="الحالة" value={state} onChange={(value) => { invalidatePreferenceRequest(); const next = value as PreferenceState; setState(next); if (next === 'OptedIn' && category === 'All') setCategory('Marketing'); }} options={[["OptedIn", 'موافقة صريحة'], ["OptedOut", 'إلغاء / رفض']]} />
              <SelectField label="فئة التواصل" value={category} onChange={(value) => { invalidatePreferenceRequest(); setCategory(value as PreferenceCategory); }} options={state === 'OptedOut' ? [['All', 'كل الرسائل — إلغاء شامل'], ['Marketing', 'Marketing'], ['Utility', 'Utility']] : [['Marketing', 'Marketing'], ['Utility', 'Utility']]} />
              <SelectField label="مصدر الدليل" value={source} onChange={(value) => { invalidatePreferenceRequest(); setSource(value); }} options={[['', 'اختر مصدرًا موثقًا'], ...evidenceSources]} />
              <label className="sm:col-span-2">
                <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">مرجع الدليل</span>
                <input value={evidenceReference} onChange={(event) => { invalidatePreferenceRequest(); setEvidenceReference(event.target.value.slice(0, 500)); }} maxLength={500} required placeholder="رقم النموذج أو التسجيل أو المستند ومكان الرجوع إليه" className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]" />
              </label>
              <label>
                <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">وقت الموافقة أو الرفض (القاهرة)</span>
                <input type="datetime-local" value={effectiveAtLocal} max={toCairoDateTimeLocal(new Date())} onChange={(event) => { invalidatePreferenceRequest(); setEffectiveAtLocal(event.target.value); }} required className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]" />
              </label>
            </div>
            {formFeedback ? <p role="alert" className="mt-3 text-sm font-semibold text-[var(--admin-danger)]">{formFeedback}</p> : null}
            <button type="submit" disabled={saving || !source || !evidenceReference.trim() || !effectiveAtLocal} className={`mt-4 inline-flex min-h-11 items-center gap-2 rounded-xl px-5 text-sm font-black text-white focus-visible:outline-none focus-visible:ring-2 disabled:cursor-not-allowed disabled:opacity-50 ${state === 'OptedOut' ? 'bg-[var(--admin-danger)] focus-visible:ring-[var(--admin-danger)]' : 'bg-[var(--admin-primary)] focus-visible:ring-[var(--admin-accent)]'}`}>
              {saving ? <LoaderCircle aria-hidden="true" size={16} className="animate-spin" /> : state === 'OptedOut' ? <ShieldX aria-hidden="true" size={16} /> : <CheckCircle2 aria-hidden="true" size={16} />}
              {saving ? 'جارٍ التسجيل…' : 'تسجيل التفضيل مع الدليل'}
            </button>
          </form>
        ) : null}
      </div>
    </details>
  );
}

function CandidateResults({ page, loading, onPageChange, onRecord }: {
  page?: WhatsAppContactCandidatePage;
  loading: boolean;
  onPageChange: (page: number) => void;
  onRecord: (candidate: WhatsAppContactCandidate, category: PreferenceCategory, state: PreferenceState) => void;
}) {
  if (!page) return <EmptyState>ابحث باسم الطالب أو كوده، ثم اختر وجهة الاتصال المقنّعة. يمكن تسجيل أول موافقة أو إعادة الموافقة بعد STOP بدليل جديد.</EmptyState>;
  return (
    <div className="mt-4 space-y-3">
      {page.items.length === 0 ? <EmptyState>لا توجد جهة اتصال مطابقة. جرّب اسمًا أدق أو كود الطالب، ولا تكتب رقمًا جديدًا يدويًا.</EmptyState> : page.items.map((candidate) => {
        const globalStopActive = candidate.marketing.overriddenByGlobalOptOut
          && candidate.utility.overriddenByGlobalOptOut;
        return (
          <article key={`${candidate.studentUserId}:${candidate.contactRole}`} className="min-w-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 sm:p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <h4 className="truncate text-sm font-black text-[var(--admin-text)]" title={candidate.studentName}>{candidate.studentName}</h4>
                <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]"><bdi dir="ltr">{maskWhatsAppDestination(candidate.maskedDestination)}</bdi> · {contactRoleLabel(candidate.contactRole)}</p>
              </div>
              <button type="button" disabled={globalStopActive} onClick={() => onRecord(candidate, 'All', 'OptedOut')} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-danger-20)] px-3 text-xs font-black text-[var(--admin-danger)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)] disabled:cursor-not-allowed disabled:opacity-50"><ShieldX aria-hidden="true" size={15} />{globalStopActive ? 'الإيقاف الشامل فعّال' : 'تسجيل إيقاف شامل'}</button>
            </div>
            <div className="mt-3 grid gap-2 lg:grid-cols-2">
              <CategoryStateCard label="Marketing" category="Marketing" state={candidate.marketing} candidate={candidate} onRecord={onRecord} />
              <CategoryStateCard label="Utility" category="Utility" state={candidate.utility} candidate={candidate} onRecord={onRecord} />
            </div>
          </article>
        );
      })}
      {page.items.length > 0 ? <nav aria-label="صفحات جهات الاتصال" className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-3"><p className="text-xs font-semibold text-[var(--admin-muted)]">صفحة {page.page}</p><PageControls page={page.page} previousDisabled={loading || page.page <= 1} nextDisabled={loading || !page.hasMore} onPageChange={onPageChange} /></nav> : null}
    </div>
  );
}

function CategoryStateCard({ label, category, state, candidate, onRecord }: {
  label: string;
  category: Exclude<PreferenceCategory, 'All'>;
  state: WhatsAppContactCategoryState;
  candidate: WhatsAppContactCandidate;
  onRecord: (candidate: WhatsAppContactCandidate, category: PreferenceCategory, state: PreferenceState) => void;
}) {
  const optedIn = state.effectiveState === 'OptedIn';
  return (
    <div className="flex min-w-0 flex-col gap-3 rounded-xl bg-[var(--admin-card-soft)] p-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2"><strong className="text-sm text-[var(--admin-text)]">{label}</strong><EffectiveState state={state.effectiveState} /></div>
        {state.overriddenByGlobalOptOut ? <p className="mt-1 text-xs font-semibold text-[var(--admin-danger)]">موقوفة بإلغاء شامل أحدث؛ إعادة الموافقة تحتاج دليلًا جديدًا.</p> : state.latestEffectiveAt ? <p className="mt-1 text-xs text-[var(--admin-muted)]">آخر دليل: <time dateTime={state.latestEffectiveAt}>{formatCairoTimestamp(state.latestEffectiveAt)}</time></p> : <p className="mt-1 text-xs text-[var(--admin-muted)]">لا يوجد قرار موثق لهذه الفئة.</p>}
      </div>
      <button type="button" onClick={() => onRecord(candidate, category, optedIn ? 'OptedOut' : 'OptedIn')} className={`inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl border px-3 text-xs font-black focus-visible:outline-none focus-visible:ring-2 ${optedIn ? 'border-[var(--admin-danger-20)] text-[var(--admin-danger)] focus-visible:ring-[var(--admin-danger)]' : 'border-[var(--admin-border)] text-[var(--admin-primary)] focus-visible:ring-[var(--admin-accent)]'}`}>
        {optedIn ? <ShieldX aria-hidden="true" size={15} /> : <ShieldCheck aria-hidden="true" size={15} />}
        {optedIn ? 'تسجيل إلغاء الفئة' : state.overriddenByGlobalOptOut ? 'إعادة موافقة موثقة' : 'تسجيل موافقة'}
      </button>
    </div>
  );
}

function AuditResults({ page, loading, totalPages, onPageChange }: {
  page?: WhatsAppContactPreferencePage;
  loading: boolean;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  if (!page) return <EmptyState>اعرض السجل لمراجعة كل قرار ودليله. هذه أحداث تاريخية؛ الحالة الفعلية تظهر في تبويب الجهات.</EmptyState>;
  return (
    <div className="mt-4 space-y-3">
      {page.items.length === 0 ? <EmptyState>لا توجد أحداث تدقيق مطابقة.</EmptyState> : page.items.map((preference) => (
        <article key={preference.id} className="grid min-w-0 gap-3 rounded-xl border border-[var(--admin-border)] p-3 lg:grid-cols-[minmax(12rem,1fr)_auto] lg:items-center">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2"><h4 className="truncate text-sm font-black text-[var(--admin-text)]" title={preference.studentName ?? 'وجهة خارجية'}>{preference.studentName ?? 'وجهة خارجية'}</h4><PreferenceState state={preference.state} /></div>
            <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]"><bdi dir="ltr">{maskWhatsAppDestination(preference.maskedDestination)}</bdi> · {contactRoleLabel(preference.contactRole)} · {categoryLabel(preference.category)}</p>
            <p className="mt-1 break-words text-xs text-[var(--admin-muted)]">{sourceLabel(preference.source)} · {preference.evidenceReference}</p>
          </div>
          <p className="flex items-center gap-1.5 text-xs text-[var(--admin-muted)]"><Clock3 aria-hidden="true" size={14} /><time dateTime={preference.effectiveAt}>{formatCairoTimestamp(preference.effectiveAt)}</time></p>
        </article>
      ))}
      {page.total > 0 ? <nav aria-label="صفحات سجل تدقيق التواصل" className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-3"><p className="text-xs font-semibold text-[var(--admin-muted)]">صفحة {page.page} من {totalPages} · {page.total} سجل</p><PageControls page={page.page} previousDisabled={loading || page.page <= 1} nextDisabled={loading || page.page >= totalPages} onPageChange={onPageChange} /></nav> : null}
    </div>
  );
}

function EmptyState({ children }: { children: ReactNode }) {
  return <div className="mt-4 rounded-xl border border-dashed border-[var(--admin-border)] p-5 text-center text-sm leading-6 text-[var(--admin-muted)]"><ShieldCheck aria-hidden="true" size={22} className="mx-auto mb-2" />{children}</div>;
}

function EffectiveState({ state }: { state: WhatsAppContactCategoryState['effectiveState'] }) {
  const presentation = state === 'OptedIn'
    ? ['موافق فعليًا', 'bg-[var(--admin-success-10)] text-[var(--admin-success)]']
    : state === 'OptedOut'
      ? ['موقوف فعليًا', 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]']
      : ['بلا موافقة', 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]'];
  return <span className={`rounded-full px-2.5 py-1 text-xs font-black ${presentation[1]}`}>{presentation[0]}</span>;
}

function PreferenceState({ state }: { state: WhatsAppContactPreference['state'] }) {
  return <span className={`rounded-full px-2.5 py-1 text-xs font-black ${state === 'OptedIn' ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'}`}>{state === 'OptedIn' ? 'حدث موافقة' : 'حدث إلغاء'}</span>;
}

function PreferenceTab({ selected, onClick, icon, children }: { selected: boolean; onClick: () => void; icon: ReactNode; children: ReactNode }) {
  return <button type="button" role="tab" aria-selected={selected} onClick={onClick} className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-3 text-xs font-black focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] ${selected ? 'bg-[var(--admin-card)] text-[var(--admin-primary)] shadow-sm' : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'}`}>{icon}{children}</button>;
}

function PageControls({ page, previousDisabled, nextDisabled, onPageChange }: { page: number; previousDisabled: boolean; nextDisabled: boolean; onPageChange: (page: number) => void }) {
  return <div className="flex gap-2" dir="ltr"><button type="button" aria-label="الصفحة السابقة" disabled={previousDisabled} onClick={() => onPageChange(page - 1)} className="grid size-11 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-40"><ChevronLeft aria-hidden="true" size={18} /></button><button type="button" aria-label="الصفحة التالية" disabled={nextDisabled} onClick={() => onPageChange(page + 1)} className="grid size-11 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-40"><ChevronRight aria-hidden="true" size={18} /></button></div>;
}

function SelectField({ label, value, options, onChange }: { label: string; value: string; options: ReadonlyArray<readonly [string, string]>; onChange: (value: string) => void }) {
  return <label><span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">{label}</span><select value={value} onChange={(event) => onChange(event.target.value)} required className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]">{options.map(([optionValue, optionLabel]) => <option key={optionValue || 'empty'} value={optionValue}>{optionLabel}</option>)}</select></label>;
}

function latestPreferenceId(candidate: WhatsAppContactCandidate, category: PreferenceCategory) {
  if (category === 'Marketing') return candidate.marketing.latestPreferenceId ?? null;
  if (category === 'Utility') return candidate.utility.latestPreferenceId ?? null;
  return candidate.global.latestPreferenceId ?? null;
}

function contactRoleLabel(role: string) {
  return ({ StudentPrimary: 'الطالب الأساسي', StudentSecondary: 'الطالب الإضافي', FatherPrimary: 'الأب الأساسي', FatherSecondary: 'الأب الإضافي', Mother: 'الأم', External: 'رقم خارجي' } as Record<string, string>)[role] ?? 'جهة اتصال';
}

function categoryLabel(category: WhatsAppContactPreference['category']) {
  return category === 'All' ? 'كل الرسائل' : category;
}

function sourceLabel(source: string) {
  return evidenceSources.find(([value]) => value === source)?.[1] ?? source;
}

function looksLikePhoneNumber(value: string) {
  return (value.match(/[0-9\u0660-\u0669]/g) ?? []).length >= 10;
}

function toCairoDateTimeLocal(date: Date) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(date);
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
}
