'use client';

import { devConsole } from '@/utils/dev-console';
import { FormEvent, useEffect, useId, useState } from 'react';
import { motion } from 'framer-motion';
import { LockOpen, RefreshCcw, ShieldAlert } from 'lucide-react';

import { 
  AdminShellChrome,
  AdminStatCard 
} from '@/components/admin';
import { adminService } from '@/services/admin-service';
import { ContentSectionDto, LessonSummaryDto, PackageDto, VideoDto, contentService } from '@/services/content-service';
import NeumorphButton from '@/components/ui/neumorph-button';

interface UserItem {
  id: string;
  fullName: string;
  phoneNumber: string;
}

interface SearchOption {
  id: string;
  label: string;
}

function SearchablePicker({
  label,
  placeholder,
  value,
  options,
  onChange,
  disabled = false,
  loading = false,
}: {
  label: string;
  placeholder: string;
  value: string;
  options: SearchOption[];
  onChange: (value: string) => void;
  disabled?: boolean;
  loading?: boolean;
}) {
  const listId = useId();
  const [query, setQuery] = useState('');
  const selected = options.find((option) => option.id === value);

  useEffect(() => {
    setQuery(selected?.label ?? '');
  }, [selected?.label]);

  function handleChange(nextQuery: string) {
    setQuery(nextQuery);
    onChange(options.find((option) => option.label === nextQuery)?.id ?? '');
  }

  return (
    <div className="space-y-1.5">
      <label className="block text-sm font-semibold text-[var(--admin-text)]">{label}</label>
      <input
        list={listId}
        value={query}
        onChange={(event) => handleChange(event.target.value)}
        disabled={disabled || loading}
        placeholder={loading ? 'جارٍ التحميل…' : placeholder}
        className="admin-input min-h-11 disabled:cursor-not-allowed disabled:opacity-55"
        aria-label={label}
      />
      <datalist id={listId}>
        {options.map((option) => <option key={option.id} value={option.label} />)}
      </datalist>
      {!disabled && !loading && query && !value && (
        <p className="text-xs text-amber-700">اختر نتيجة من القائمة لإكمال الاختيار.</p>
      )}
      {!disabled && !loading && !options.length && (
        <p className="text-xs text-[var(--admin-muted)]">لا توجد نتائج متاحة الآن.</p>
      )}
    </div>
  );
}

function getErrorMessage(error: unknown, fallback: string) {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as { response?: unknown }).response === 'object' &&
    (error as { response?: { data?: unknown } }).response !== null
  ) {
    const response = (error as { response?: { data?: { message?: unknown } } }).response;
    if (typeof response?.data?.message === 'string') {
      return response.data.message;
    }
  }

  return fallback;
}

export default function AdminOverridesPageClient() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [students, setStudents] = useState<UserItem[]>([]);
  const [sharedLoading, setSharedLoading] = useState(true);
  const [sharedError, setSharedError] = useState<string | null>(null);

  const [wPkgId, setWPkgId] = useState('');
  const [wSecId, setWSecId] = useState('');
  const [wLesId, setWLesId] = useState('');
  const [wVideoId, setWVideoId] = useState('');
  const [wStudentId, setWStudentId] = useState('');
  const [wSections, setWSections] = useState<ContentSectionDto[]>([]);
  const [wLessons, setWLessons] = useState<LessonSummaryDto[]>([]);
  const [wVideos, setWVideos] = useState<VideoDto[]>([]);
  const [wLoading, setWLoading] = useState(false);
  const [wOptionsLoading, setWOptionsLoading] = useState(false);
  const [wResult, setWResult] = useState<string | null>(null);

  const [uPkgId, setUPkgId] = useState('');
  const [uSecId, setUSecId] = useState('');
  const [uLessonId, setULessonId] = useState('');
  const [uStudentId, setUStudentId] = useState('');
  const [uSections, setUSections] = useState<ContentSectionDto[]>([]);
  const [uLessons, setULessons] = useState<LessonSummaryDto[]>([]);
  const [uLoading, setULoading] = useState(false);
  const [uOptionsLoading, setUOptionsLoading] = useState(false);
  const [uResult, setUResult] = useState<string | null>(null);

  useEffect(() => {
    void loadSharedData();
  }, []);

  async function loadSharedData() {
    setSharedLoading(true);
    setSharedError(null);
    try {
      const [packagesResponse, usersResponse] = await Promise.all([
        contentService.getPackages(),
        adminService.listUsers(1, 1000, ''),
      ]);
      setPackages((packagesResponse.data?.data || []) as PackageDto[]);
      setStudents((usersResponse?.items || []) as UserItem[]);
    } catch (error) {
      devConsole.error(error);
      setSharedError(getErrorMessage(error, 'تعذر تحميل الباقات أو قائمة الطلاب.'));
    } finally {
      setSharedLoading(false);
    }
  }

  async function handleWPkgChange(pkgId: string) {
    setWPkgId(pkgId);
    setWSecId('');
    setWLesId('');
    setWVideoId('');
    setWLessons([]);
    setWVideos([]);
    if (!pkgId) return setWSections([]);

    setWOptionsLoading(true);
    setWResult(null);
    try {
      const response = await contentService.getSections(pkgId);
      setWSections((response.data?.data || []) as ContentSectionDto[]);
    } catch (error) {
      setWSections([]);
      setWResult(getErrorMessage(error, 'تعذر تحميل أقسام الباقة.'));
    } finally {
      setWOptionsLoading(false);
    }
  }

  async function handleWSecChange(secId: string) {
    setWSecId(secId);
    setWLesId('');
    setWVideoId('');
    setWVideos([]);
    if (!secId) return setWLessons([]);

    setWOptionsLoading(true);
    setWResult(null);
    try {
      const response = await contentService.getLessons(secId);
      setWLessons((response.data?.data || []) as LessonSummaryDto[]);
    } catch (error) {
      setWLessons([]);
      setWResult(getErrorMessage(error, 'تعذر تحميل دروس القسم.'));
    } finally {
      setWOptionsLoading(false);
    }
  }

  async function handleWLesChange(lessonId: string) {
    setWLesId(lessonId);
    setWVideoId('');
    if (!lessonId) return setWVideos([]);

    setWOptionsLoading(true);
    setWResult(null);
    try {
      const response = await contentService.getLessonDetail(lessonId);
      setWVideos((response.data?.data?.videos || []) as VideoDto[]);
    } catch (error) {
      setWVideos([]);
      setWResult(getErrorMessage(error, 'تعذر تحميل فيديوهات الدرس.'));
    } finally {
      setWOptionsLoading(false);
    }
  }

  async function handleUPkgChange(pkgId: string) {
    setUPkgId(pkgId);
    setUSecId('');
    setULessonId('');
    setULessons([]);
    if (!pkgId) return setUSections([]);

    setUOptionsLoading(true);
    setUResult(null);
    try {
      const response = await contentService.getSections(pkgId);
      setUSections((response.data?.data || []) as ContentSectionDto[]);
    } catch (error) {
      setUSections([]);
      setUResult(getErrorMessage(error, 'تعذر تحميل أقسام الباقة.'));
    } finally {
      setUOptionsLoading(false);
    }
  }

  async function handleUSecChange(secId: string) {
    setUSecId(secId);
    setULessonId('');
    if (!secId) return setULessons([]);

    setUOptionsLoading(true);
    setUResult(null);
    try {
      const response = await contentService.getLessons(secId);
      setULessons((response.data?.data || []) as LessonSummaryDto[]);
    } catch (error) {
      setULessons([]);
      setUResult(getErrorMessage(error, 'تعذر تحميل دروس القسم.'));
    } finally {
      setUOptionsLoading(false);
    }
  }

  async function handleResetWatch(event: FormEvent) {
    event.preventDefault();
    setWLoading(true);
    setWResult(null);

    try {
      const response = await adminService.resetWatchLimit(wVideoId, wStudentId);
      setWResult(response.message || 'تم تصفير حد المشاهدة');
    } catch (error) {
      setWResult(getErrorMessage(error, 'تعذر تنفيذ العملية'));
    } finally {
      setWLoading(false);
    }
  }

  async function handleUnlockLesson(event: FormEvent) {
    event.preventDefault();
    setULoading(true);
    setUResult(null);

    try {
      const response = await adminService.manualUnlockLesson(uLessonId, uStudentId);
      setUResult(response.message || 'تم فتح الدرس');
    } catch (error) {
      setUResult(getErrorMessage(error, 'تعذر تنفيذ العملية'));
    } finally {
      setULoading(false);
    }
  }

  return (
    <AdminShellChrome
      activePath="/admin/overrides"
      sectionLabel="التعديلات اليدوية"
      pageTitle="أدوات الدعم الإداري"
      subtitle="تصفير المشاهدة وفتح الدروس يدويًا مع الحفاظ على سجل واضح."
    >
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={RefreshCcw}
          label="المشاهدات"
          value={wVideos.length}
          subtitle="فيديوهات متاحة لإعادة الضبط"
        />
        <AdminStatCard
          variant="accent"
          icon={LockOpen}
          label="الدروس المغلقة"
          value={uLessons.length}
          subtitle="دروس قابلة للفتح اليدوي"
        />
        <AdminStatCard
          variant="muted"
          icon={ShieldAlert}
          label="الطلاب"
          value={students.length}
          subtitle="متاح تطبيق التعديلات عليهم"
        >
          <div className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-transparent via-rose-500/20 to-transparent" />
        </AdminStatCard>
      </section>

      {sharedError && (
        <div role="alert" className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
          <span>{sharedError}</span>
          <button type="button" onClick={() => void loadSharedData()} className="rounded-lg border border-rose-300 px-3 py-2 font-bold hover:bg-rose-100">
            إعادة تحميل البيانات
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <motion.section initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="admin-panel">
          <div className="mb-6 flex items-center gap-3">
            <div className="admin-badge admin-badge--pill">
              <RefreshCcw className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">تصفير حد مشاهدة الفيديو</h2>
              <p className="text-sm text-[var(--admin-muted)]">إزالة سجل المشاهدة لطالب وفيديو محدد.</p>
            </div>
          </div>

          <form onSubmit={handleResetWatch} className="space-y-4">
            <SearchablePicker label="الباقة" placeholder="ابحث باسم الباقة" value={wPkgId} onChange={(value) => void handleWPkgChange(value)} options={packages.map((pkg) => ({ id: pkg.id, label: pkg.name }))} loading={sharedLoading} />
            <SearchablePicker label="القسم" placeholder="ابحث باسم القسم" value={wSecId} onChange={(value) => void handleWSecChange(value)} options={wSections.map((section) => ({ id: section.id, label: `${section.order}. ${section.title}` }))} disabled={!wPkgId} loading={wOptionsLoading} />
            <SearchablePicker label="الدرس" placeholder="ابحث باسم الدرس" value={wLesId} onChange={(value) => void handleWLesChange(value)} options={wLessons.map((lesson) => ({ id: lesson.id, label: `${lesson.order}. ${lesson.title}` }))} disabled={!wSecId} loading={wOptionsLoading} />
            <SearchablePicker label="الفيديو" placeholder="ابحث باسم الفيديو" value={wVideoId} onChange={setWVideoId} options={wVideos.map((video) => ({ id: video.id, label: video.title }))} disabled={!wLesId} loading={wOptionsLoading} />
            <SearchablePicker label="الطالب" placeholder="ابحث بالاسم أو رقم الهاتف" value={wStudentId} onChange={setWStudentId} options={students.map((student) => ({ id: student.id, label: `${student.fullName} (${student.phoneNumber})` }))} loading={sharedLoading} />

            <div className="flex items-center justify-between gap-3 pt-4 border-t border-[var(--admin-border)]">
              <NeumorphButton type="submit" disabled={wLoading || !wVideoId || !wStudentId} loading={wLoading} intent="primary" size="lg" pill>
                تصفير الحد
              </NeumorphButton>
              {wResult ? <span className="text-sm font-semibold text-[var(--admin-primary)]">{wResult}</span> : null}
            </div>
          </form>
        </motion.section>

        <motion.section initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }} className="admin-panel">
          <div className="mb-6 flex items-center gap-3">
            <div className="admin-badge admin-badge--pill">
              <LockOpen className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">فتح درس يدويًا</h2>
              <p className="text-sm text-[var(--admin-muted)]">تجاوز البوابات وفتح درس محدد لطالب.</p>
            </div>
          </div>

          <form onSubmit={handleUnlockLesson} className="space-y-4">
            <SearchablePicker label="الباقة" placeholder="ابحث باسم الباقة" value={uPkgId} onChange={(value) => void handleUPkgChange(value)} options={packages.map((pkg) => ({ id: pkg.id, label: pkg.name }))} loading={sharedLoading} />
            <SearchablePicker label="القسم" placeholder="ابحث باسم القسم" value={uSecId} onChange={(value) => void handleUSecChange(value)} options={uSections.map((section) => ({ id: section.id, label: `${section.order}. ${section.title}` }))} disabled={!uPkgId} loading={uOptionsLoading} />
            <SearchablePicker label="الدرس" placeholder="ابحث باسم الدرس" value={uLessonId} onChange={setULessonId} options={uLessons.map((lesson) => ({ id: lesson.id, label: `${lesson.order}. ${lesson.title}` }))} disabled={!uSecId} loading={uOptionsLoading} />
            <SearchablePicker label="الطالب" placeholder="ابحث بالاسم أو رقم الهاتف" value={uStudentId} onChange={setUStudentId} options={students.map((student) => ({ id: student.id, label: `${student.fullName} (${student.phoneNumber})` }))} loading={sharedLoading} />

            <div className="flex items-center justify-between gap-3 pt-4 border-t border-[var(--admin-border)]">
              <NeumorphButton type="submit" disabled={uLoading || !uLessonId || !uStudentId} loading={uLoading} intent="primary" size="lg" pill>
                فتح الدرس
              </NeumorphButton>
              {uResult ? <span className="text-sm font-semibold text-[var(--admin-primary)]">{uResult}</span> : null}
            </div>
          </form>
        </motion.section>
      </div>

      <div className="mt-6 rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-sm text-[var(--admin-muted)] shadow-sm">
        كل عمليات التعديل اليدوي يتم تسجيلها داخل سجل المراجعة مع الطابع الزمني وهوية المسؤول.
      </div>
    </AdminShellChrome>
  );
}
