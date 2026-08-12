'use client';

import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import {
  ArrowRight,
  CalendarDays,
  ChevronLeft,
  Gift,
  PackageCheck,
  RefreshCw,
  Search,
  ShoppingBag,
  UsersRound,
} from 'lucide-react';
import {
  CONTENT_CACHE_KEYS,
  contentService,
  type ContentPackageSummaryDto,
  type ContentSummaryDto,
} from '@/services/content-service';
import { cairoCurrentDate, cairoCurrentMonthPeriod, cairoDateAfterDays, cairoDateTimeLocalToUtcISOString } from '@/lib/cairo-time';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type Period = 'all' | 'today' | 'month' | 'custom';

export interface ContentSummaryTeacherOption {
  id: string;
  fullName: string;
  profileImageUrl?: string;
  subjectNames: string[];
  packagesCount: number;
}

type ContentSummaryPanelProps =
  | { scope: 'teacher' }
  | {
      scope: 'admin';
      teacherOptions: ContentSummaryTeacherOption[];
      selectedTeacherId: string | null;
      onSelectTeacher: (teacherId: string) => void;
      onClearTeacher: () => void;
    };

const number = new Intl.NumberFormat('ar-EG-u-nu-latn');
const time = new Intl.DateTimeFormat('ar-EG-u-nu-latn', {
  hour: 'numeric',
  minute: '2-digit',
  second: '2-digit',
  timeZone: 'Africa/Cairo',
});
const EMPTY_TEACHER_OPTIONS: ContentSummaryTeacherOption[] = [];

function buildRange(period: Period, from: string, to: string) {
  if (period === 'all') return {};
  if (period === 'today') {
    const today = cairoCurrentDate();
    const tomorrow = cairoDateAfterDays(1, new Date(`${today}T00:00:00Z`));
    return {
      fromUtc: cairoDateTimeLocalToUtcISOString(`${today}T00:00`),
      toUtc: cairoDateTimeLocalToUtcISOString(`${tomorrow}T00:00`),
    };
  }
  if (period === 'month') {
    const { first, last } = cairoCurrentMonthPeriod();
    const firstOfNextMonth = cairoDateAfterDays(1, new Date(`${last}T00:00:00Z`));
    return {
      fromUtc: cairoDateTimeLocalToUtcISOString(`${first}T00:00`),
      toUtc: cairoDateTimeLocalToUtcISOString(`${firstOfNextMonth}T00:00`),
    };
  }
  if (!from && !to) return {};

  const endDate = to ? cairoDateAfterDays(1, new Date(`${to}T00:00:00Z`)) : undefined;
  return {
    fromUtc: from ? cairoDateTimeLocalToUtcISOString(`${from}T00:00`) : undefined,
    toUtc: endDate ? cairoDateTimeLocalToUtcISOString(`${endDate}T00:00`) : undefined,
  };
}

function AcquisitionRow({ label, values }: { label: string; values: ContentPackageSummaryDto['package'] }) {
  return (
    <div className="grid grid-cols-2 items-center gap-x-3 gap-y-2 border-b border-[var(--admin-border)]/60 py-3 last:border-b-0 sm:grid-cols-[1fr_auto_auto]">
      <span className="col-span-2 text-sm font-bold text-[var(--admin-text)] sm:col-span-1">{label}</span>
      <span className="min-w-20 text-center text-sm text-[var(--admin-muted)]">
        <strong className="font-black tabular-nums text-[var(--admin-text)]">{number.format(values.purchased)}</strong> مشتري
      </span>
      <span className="min-w-20 text-center text-sm text-[var(--admin-muted)]">
        <strong className="font-black tabular-nums text-[var(--admin-primary)]">{number.format(values.gifts)}</strong> هدية فقط
      </span>
    </div>
  );
}

function PackageSummary({ packageSummary }: { packageSummary: ContentPackageSummaryDto }) {
  return (
    <article className="overflow-hidden rounded-2xl bg-[var(--admin-card)] shadow-[0_2px_8px_rgba(10,29,61,0.08)]">
      <header className="flex flex-wrap items-start justify-between gap-4 bg-[var(--admin-primary-15)] px-5 py-4">
        <div className="min-w-0">
          <h3 className="text-balance text-lg font-black text-[var(--admin-text)]">{packageSummary.packageName}</h3>
          {packageSummary.teacherName && <p className="mt-1 text-xs font-medium text-[var(--admin-muted)]">{packageSummary.teacherName}</p>}
        </div>
        <div className="flex shrink-0 items-center gap-2 rounded-full bg-[var(--admin-card)] px-3 py-2 text-sm font-black text-[var(--admin-primary)]">
          <UsersRound className="h-4 w-4" aria-hidden="true" />
          <span className="tabular-nums">{number.format(packageSummary.totalStudents)} طالب</span>
        </div>
      </header>

      <div className="px-5">
        <AcquisitionRow label="الباقة كاملة" values={packageSummary.package} />
        <AcquisitionRow label="الترم" values={packageSummary.term} />
        <AcquisitionRow label="الكورس / القسم" values={packageSummary.section} />
        <AcquisitionRow label="الحصة" values={packageSummary.lesson} />
      </div>

      <footer className="grid grid-cols-3 divide-x divide-x-reverse divide-[var(--admin-border)] border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-center">
        <div className="px-2 py-3"><ShoppingBag className="mx-auto mb-1 h-4 w-4 text-[var(--admin-secondary)]" aria-hidden="true" /><b className="tabular-nums">{number.format(packageSummary.purchasedStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">مشتري</span></div>
        <div className="px-2 py-3"><Gift className="mx-auto mb-1 h-4 w-4 text-[var(--admin-primary)]" aria-hidden="true" /><b className="tabular-nums">{number.format(packageSummary.giftStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">هدية فقط</span></div>
        <div className="px-2 py-3"><UsersRound className="mx-auto mb-1 h-4 w-4 text-[var(--admin-text)]" aria-hidden="true" /><b className="tabular-nums">{number.format(packageSummary.totalStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">الإجمالي</span></div>
      </footer>
    </article>
  );
}

export function ContentSummaryPanel(props: ContentSummaryPanelProps) {
  const [period, setPeriod] = useState<Period>('all');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [summary, setSummary] = useState<ContentSummaryDto | null>(null);
  const [loading, setLoading] = useState(props.scope === 'teacher' || Boolean(props.selectedTeacherId));
  const [networkError, setNetworkError] = useState('');
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const [teacherSearch, setTeacherSearch] = useState('');
  const teacherSearchId = useId();
  const rangeErrorId = useId();
  const teacherSearchRef = useRef<HTMLInputElement>(null);
  const summaryHeadingRef = useRef<HTMLHeadingElement>(null);
  const previousSelectedTeacherIdRef = useRef<string | null | undefined>(undefined);
  const range = useMemo(() => buildRange(period, from, to), [period, from, to]);
  const rangeError = period === 'custom' && from && to && from > to
    ? 'تاريخ البداية يجب أن يسبق تاريخ النهاية.'
    : '';

  const teacherOptions = props.scope === 'admin' ? props.teacherOptions : EMPTY_TEACHER_OPTIONS;
  const selectedTeacherId = props.scope === 'admin' ? props.selectedTeacherId : null;
  const selectedTeacher = useMemo(
    () => teacherOptions.find((teacher) => teacher.id === selectedTeacherId),
    [selectedTeacherId, teacherOptions],
  );
  const filteredTeachers = useMemo(() => {
    const query = teacherSearch.trim().toLocaleLowerCase('ar-EG');
    return [...teacherOptions]
      .filter((teacher) => {
        if (!query) return true;
        return teacher.fullName.toLocaleLowerCase('ar-EG').includes(query)
          || teacher.subjectNames.some((subject) => subject.toLocaleLowerCase('ar-EG').includes(query));
      })
      .sort((left, right) => left.fullName.localeCompare(right.fullName, 'ar'));
  }, [teacherOptions, teacherSearch]);

  const canLoadSummary = props.scope === 'teacher' || Boolean(selectedTeacher);
  const requestRefresh = useCallback(() => {
    setRetryKey((currentRetryKey) => currentRetryKey + 1);
  }, []);

  useEffect(() => {
    setLastUpdatedAt(null);
  }, [props.scope, range.fromUtc, range.toUtc, selectedTeacher?.id]);

  useEffect(() => {
    if (props.scope !== 'admin') return;

    const previousSelectedTeacherId = previousSelectedTeacherIdRef.current;
    previousSelectedTeacherIdRef.current = selectedTeacherId;
    if (previousSelectedTeacherId === selectedTeacherId) return;
    if (!selectedTeacherId && previousSelectedTeacherId === undefined) return;

    const focusFrame = window.requestAnimationFrame(() => {
      if (selectedTeacherId) summaryHeadingRef.current?.focus();
      else teacherSearchRef.current?.focus();
    });

    return () => window.cancelAnimationFrame(focusFrame);
  }, [props.scope, selectedTeacherId]);

  useEffect(() => {
    if (!canLoadSummary || rangeError) return;

    const handleWindowFocus = () => requestRefresh();
    // Content and access-grant events are both normalized to this key.
    const cleanupCacheStore = registerCacheStore(
      CONTENT_CACHE_KEYS.packages,
      () => {},
      requestRefresh,
    );
    window.addEventListener('focus', handleWindowFocus);

    return () => {
      cleanupCacheStore();
      window.removeEventListener('focus', handleWindowFocus);
    };
  }, [canLoadSummary, rangeError, requestRefresh]);

  useEffect(() => {
    if (!canLoadSummary) {
      setSummary(null);
      setLoading(false);
      setNetworkError('');
      return;
    }

    if (rangeError) {
      setSummary(null);
      setLoading(false);
      setNetworkError('');
      return;
    }

    const controller = new AbortController();
    let active = true;

    setLoading(true);
    setNetworkError('');
    setSummary(null);

    void (async () => {
      try {
        const response = await contentService.getContentSummary(props.scope, {
          teacherId: props.scope === 'admin' ? selectedTeacher?.id : undefined,
          fromUtc: range.fromUtc,
          toUtc: range.toUtc,
          signal: controller.signal,
        });

        if (!active || controller.signal.aborted) return;
        const summaryData = response.data.data;
        if (!summaryData) throw new Error('Missing content summary data');
        setSummary(summaryData);
        setLastUpdatedAt(new Date());
      } catch {
        if (!active || controller.signal.aborted) return;
        setNetworkError('تعذر تحميل ملخص المحتوى. حاول مرة أخرى.');
      } finally {
        if (active && !controller.signal.aborted) setLoading(false);
      }
    })();

    return () => {
      active = false;
      controller.abort();
    };
  }, [canLoadSummary, props.scope, range.fromUtc, range.toUtc, rangeError, retryKey, selectedTeacher?.id]);

  if (props.scope === 'admin' && !selectedTeacherId) {
    return (
      <section className="space-y-5" aria-labelledby="content-summary-teacher-picker-title">
        <div>
          <h2 id="content-summary-teacher-picker-title" className="text-xl font-black text-[var(--admin-text)]">اختر المدرس لعرض الملخص</h2>
          <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">ابحث بالاسم أو المادة، ثم افتح ملخص الشراء والهدايا الخاص بالمدرس.</p>
        </div>

        {teacherOptions.length > 0 ? (
          <div className="relative">
            <label htmlFor={teacherSearchId} className="sr-only">البحث عن مدرس بالاسم أو المادة</label>
            <Search className="pointer-events-none absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" aria-hidden="true" />
            <input
              ref={teacherSearchRef}
              id={teacherSearchId}
              type="search"
              value={teacherSearch}
              onChange={(event) => setTeacherSearch(event.target.value)}
              placeholder="ابحث عن مدرس بالاسم أو المادة..."
              className="admin-input pr-11"
            />
            <span className="sr-only" role="status" aria-live="polite">{number.format(filteredTeachers.length)} مدرس مطابق</span>
          </div>
        ) : null}

        {!teacherOptions.length ? (
          <div className="rounded-2xl bg-[var(--admin-card)] px-6 py-12 text-center" role="status">
            <UsersRound className="mx-auto mb-3 h-8 w-8 text-[var(--admin-secondary)]" aria-hidden="true" />
            <p className="font-black text-[var(--admin-text)]">لا يوجد مدرسون مضافون.</p>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">أضف مدرسًا أولًا ليظهر ملخص المحتوى الخاص به هنا.</p>
          </div>
        ) : filteredTeachers.length === 0 ? (
          <div className="rounded-2xl bg-[var(--admin-card)] px-6 py-10 text-center" role="status">
            <p className="font-black text-[var(--admin-text)]">لا توجد نتائج مطابقة.</p>
            <button type="button" onClick={() => setTeacherSearch('')} className="admin-btn-ghost mt-3 min-h-11 px-4">مسح البحث</button>
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {filteredTeachers.map((teacher) => {
              const subjects = teacher.subjectNames.length ? teacher.subjectNames.join('، ') : 'لا توجد مواد محددة';
              return (
                <button
                  key={teacher.id}
                  type="button"
                  onClick={() => props.onSelectTeacher(teacher.id)}
                  className="group flex min-h-28 w-full items-center gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-right transition-[color,background-color,border-color,transform,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-[var(--admin-primary)] hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
                >
                  {teacher.profileImageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={resolveMediaUrl(teacher.profileImageUrl)} alt="" className="h-12 w-12 shrink-0 rounded-xl border border-[var(--admin-border)] object-cover" />
                  ) : (
                    <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-lg font-black text-[var(--admin-primary)]" aria-hidden="true">
                      {teacher.fullName.trim().charAt(0) || 'م'}
                    </span>
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-black text-[var(--admin-text)] group-hover:text-[var(--admin-primary)]">{teacher.fullName}</span>
                    <span className="mt-1 block truncate text-xs font-medium text-[var(--admin-muted)]" title={subjects}>{subjects}</span>
                    <span className="mt-2 block text-xs font-bold text-[var(--admin-primary)]">{number.format(teacher.packagesCount)} باقة</span>
                  </span>
                  <ChevronLeft className="h-5 w-5 shrink-0 text-[var(--admin-primary)] transition-transform duration-200 group-hover:-translate-x-1" aria-hidden="true" />
                </button>
              );
            })}
          </div>
        )}
      </section>
    );
  }

  if (props.scope === 'admin' && !selectedTeacher) {
    return (
      <section className="rounded-2xl bg-[var(--admin-card)] px-6 py-10 text-center" aria-label="خطأ في اختيار المدرس">
        <div role="alert">
          <p className="font-black text-[var(--admin-danger)]">المدرس المحدد غير موجود أو لم يعد متاحًا.</p>
        </div>
        <button type="button" onClick={props.onClearTeacher} className="admin-btn-ghost mt-4 inline-flex min-h-11 items-center gap-2 px-4">
          <ArrowRight className="h-4 w-4" aria-hidden="true" />
          العودة لقائمة المدرسين
        </button>
      </section>
    );
  }

  return (
    <section className="space-y-6" aria-label="ملخص اقتناء المحتوى" aria-busy={loading}>
      {props.scope === 'admin' && selectedTeacher ? (
        <div className="flex flex-col gap-4 border-b border-[var(--admin-border)]/60 pb-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-center gap-3">
            {selectedTeacher.profileImageUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={resolveMediaUrl(selectedTeacher.profileImageUrl)} alt="" className="h-11 w-11 shrink-0 rounded-xl border border-[var(--admin-border)] object-cover" />
            ) : (
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] font-black text-[var(--admin-primary)]" aria-hidden="true">
                {selectedTeacher.fullName.trim().charAt(0) || 'م'}
              </span>
            )}
            <div className="min-w-0">
              <h2 ref={summaryHeadingRef} tabIndex={-1} className="truncate text-lg font-black text-[var(--admin-text)] focus:outline-none">ملخص {selectedTeacher.fullName}</h2>
              <p className="mt-0.5 text-sm text-[var(--admin-muted)]">الشراء والهدايا حسب مستوى المحتوى والفترة المختارة.</p>
            </div>
          </div>
          <button type="button" onClick={props.onClearTeacher} className="admin-btn-ghost inline-flex min-h-11 items-center justify-center gap-2 px-4 sm:w-fit">
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
            كل المدرسين
          </button>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-3" aria-live="polite">
        <p className="text-xs font-medium text-[var(--admin-muted)]">
          {lastUpdatedAt ? `آخر تحديث: ${time.format(lastUpdatedAt)}` : 'لم يتم التحديث بعد'}
        </p>
        <button
          type="button"
          onClick={requestRefresh}
          disabled={loading || Boolean(rangeError)}
          className="admin-btn-ghost inline-flex min-h-11 items-center gap-2 px-4 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin motion-reduce:animate-none' : ''}`} aria-hidden="true" />
          تحديث البيانات
        </button>
      </div>

      <div className="flex flex-col gap-4 rounded-2xl bg-[var(--admin-card-soft)] p-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
          <CalendarDays className="h-5 w-5 text-[var(--admin-secondary)]" aria-hidden="true" />
          فترة الملخص
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="اختيار فترة الملخص">
          {([['all', 'كل الوقت'], ['today', 'اليوم'], ['month', 'هذا الشهر'], ['custom', 'فترة مخصصة']] as const).map(([value, label]) => (
            <button key={value} type="button" onClick={() => setPeriod(value)} aria-pressed={period === value} className={`min-h-11 rounded-full px-4 text-sm font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${period === value ? 'bg-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>
              {label}
            </button>
          ))}
        </div>
      </div>

      <p className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-xs font-medium leading-6 text-[var(--admin-muted)]">
        كل سطر يحسب الطلاب المختلفين داخل مستواه. إجمالي البطاقة يحسب الطالب مرة واحدة فقط، وإذا كان له شراء وهدية داخل نفس الباقة يُصنَّف كمشترٍ لا كهدية فقط. الأرقام تاريخية للفترة المختارة؛ تشمل الصلاحية المنتهية وتستبعد الملغاة.
      </p>

      {period === 'custom' && (
        <div className="rounded-xl bg-[var(--admin-card)] p-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <label className="flex-1 text-sm font-bold text-[var(--admin-text)]">
              من
              <input
                className="admin-input mt-2"
                type="date"
                value={from}
                max={to || undefined}
                aria-invalid={Boolean(rangeError)}
                aria-describedby={rangeError ? rangeErrorId : undefined}
                onChange={(event) => setFrom(event.target.value)}
              />
            </label>
            <label className="flex-1 text-sm font-bold text-[var(--admin-text)]">
              إلى
              <input
                className="admin-input mt-2"
                type="date"
                value={to}
                min={from || undefined}
                aria-invalid={Boolean(rangeError)}
                aria-describedby={rangeError ? rangeErrorId : undefined}
                onChange={(event) => setTo(event.target.value)}
              />
            </label>
          </div>
          {rangeError ? <p id={rangeErrorId} className="mt-3 text-sm font-bold text-[var(--admin-danger)]" role="alert">{rangeError}</p> : null}
        </div>
      )}

      {rangeError ? null : loading ? (
        <div className="grid gap-5 lg:grid-cols-2" role="status" aria-live="polite">
          <span className="sr-only">جاري تحميل ملخص المحتوى</span>
          {[0, 1].map((skeletonIndex) => <div key={skeletonIndex} className="h-80 animate-pulse rounded-2xl bg-[var(--admin-card-soft)] motion-reduce:animate-none" aria-hidden="true" />)}
        </div>
      ) : networkError ? (
        <div className="flex flex-col items-center gap-3 rounded-2xl bg-[var(--admin-card)] px-6 py-10 text-center" role="alert">
          <p className="font-bold text-[var(--admin-danger)]">{networkError}</p>
          <button type="button" onClick={requestRefresh} className="admin-btn-ghost inline-flex min-h-11 items-center gap-2 px-4"><RefreshCw className="h-4 w-4" aria-hidden="true" />إعادة المحاولة</button>
        </div>
      ) : !summary?.packages.length ? (
        <div className="rounded-2xl bg-[var(--admin-card)] px-6 py-12 text-center" role="status">
          <PackageCheck className="mx-auto mb-3 h-8 w-8 text-[var(--admin-secondary)]" aria-hidden="true" />
          <p className="font-black text-[var(--admin-text)]">لا توجد باقات لهذا المدرس في هذا النطاق.</p>
        </div>
      ) : (
        <>
          <div className="grid gap-5 lg:grid-cols-2">
            {summary.packages.map((packageSummary) => <PackageSummary key={packageSummary.packageId} packageSummary={packageSummary} />)}
          </div>

          <section className="rounded-2xl bg-[var(--admin-card)] p-5 shadow-[0_2px_8px_rgba(10,29,61,0.08)]">
            <h2 className="text-lg font-black text-[var(--admin-text)]">الباقات التي اشتراها الطلاب معًا</h2>
            {summary.packageCombinations.length ? (
              <div className="mt-4 divide-y divide-[var(--admin-border)]">
                {summary.packageCombinations.map((combination) => (
                  <div key={combination.packageIds.join('-')} className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between">
                    <p className="font-bold text-[var(--admin-text)] [overflow-wrap:anywhere]">{combination.packageNames.join(' + ')}</p>
                    <span className="w-fit shrink-0 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-sm font-black tabular-nums text-[var(--admin-primary)]">{number.format(combination.studentsCount)} طالب</span>
                  </div>
                ))}
              </div>
            ) : <p className="mt-3 text-sm text-[var(--admin-muted)]">لا يوجد طلاب اشتروا أكثر من باقة خلال الفترة المختارة.</p>}
          </section>
        </>
      )}
    </section>
  );
}
