'use client';

import { devConsole } from '@/utils/dev-console';
import { type ReactNode, useEffect, useState, useCallback } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Download,
  Filter,
  Shield,
  Sparkles,
  UserPlus,
  Users,
  UserX,
  UserCheck,
  RefreshCw,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { AddUserDrawer } from '../users/components/AddUserDrawer';

import {
  AdminPage,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminSearchToolbar,
  AdminPageSkeleton,
  ConfirmDialog,
} from '@/components/admin';
import {
  formatRelativeDate,
} from '@/components/admin/admin-utils';
import { AdminUserListDto, adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { getEducationStageLabel, getGradeLevelLabel, getStudyTrackLabel } from '@/lib/academic-labels';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

const STUDENT_PAGE_SIZES = [25, 50] as const;

function isRequestCancellation(error: unknown) {
  if (error instanceof DOMException && error.name === 'AbortError') return true;
  return (
    typeof error === 'object' &&
    error !== null &&
    'code' in error &&
    error.code === 'ERR_CANCELED'
  );
}

function StudentManagementFrame({ staff, children }: { staff: boolean; children: ReactNode }) {
  const title = 'إدارة الطلاب';
  const subtitle = 'البحث عن الطلاب، فلترة المراحل، تفعيل أو تعليق الحسابات وتصدير البيانات الأكاديمية.';
  if (staff) return <AssistantShellChrome activePath="/assistant/students" sectionLabel="خدمة الطلاب" pageTitle={title} subtitle={subtitle}>{children}</AssistantShellChrome>;
  return <AdminPage activePath="/admin/students" sectionLabel="الطلاب" pageTitle={title} subtitle={subtitle}>{children}</AdminPage>;
}

function statusLabel(status: string) {
  return status === 'Active' ? 'نشط' : 'معلق';
}

function getStatusClasses(status: string) {
  if (status === 'Active') {
    return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400';
  }
  return 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]';
}

export default function AdminStudentsPageClient({ staff = false }: { staff?: boolean }) {
  const router = useRouter();
  const [users, setUsers] = useState<AdminUserListDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] =
    useState<(typeof STUDENT_PAGE_SIZES)[number]>(25);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [reloadToken, setReloadToken] = useState(0);
  const [educationStageFilter, setEducationStageFilter] = useState('');
  const [gradeLevelFilter, setGradeLevelFilter] = useState('');
  const [studyTrackFilter, setStudyTrackFilter] = useState('');
  const [genderFilter, setGenderFilter] = useState('');
  const [governorateFilter] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [confirmUser, setConfirmUser] = useState<AdminUserListDto | null>(null);
  const [showAddUser, setShowAddUser] = useState(false);
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 300);

    return () => window.clearTimeout(timeoutId);
  }, [search]);

  const fetchUsers = useCallback(async (signal: AbortSignal) => {
    try {
      setLoading(true);
      setLoadError(false);
      const data = await adminService.listUsers(
        page,
        pageSize,
        debouncedSearch,
        educationStageFilter || undefined,
        gradeLevelFilter || undefined,
        studyTrackFilter || undefined,
        genderFilter || undefined,
        governorateFilter || undefined,
        'Student',
        signal
      );
      if (!data || signal.aborted) return;

      const lastPage = Math.max(1, Math.ceil(data.totalCount / pageSize));
      setTotalCount(data.totalCount);
      if (page > lastPage) {
        setPage(lastPage);
        return;
      }
      setUsers(data.items);
    } catch (error) {
      if (isRequestCancellation(error)) return;
      setLoadError(true);
    } finally {
      if (!signal.aborted) setLoading(false);
    }
  }, [
    page,
    pageSize,
    debouncedSearch,
    educationStageFilter,
    gradeLevelFilter,
    studyTrackFilter,
    genderFilter,
    governorateFilter,
  ]);

  useEffect(() => {
    const controller = new AbortController();
    void fetchUsers(controller.signal);
    return () => controller.abort();
  }, [fetchUsers, reloadToken]);

  const refreshUsers = useCallback(() => {
    setReloadToken((current) => current + 1);
  }, []);

  async function handleToggleStatus(user: AdminUserListDto) {
    const nextStatus = user.status === 'Active' ? 'Disabled' : 'Active';
    try {
      await adminService.updateUserStatus(user.id, nextStatus);
      setUsers((currentUsers) =>
        currentUsers.map((entry) =>
          entry.id === user.id ? { ...entry, status: nextStatus } : entry
        )
      );
      setConfirmUser(null);
      toast.success(
        nextStatus === 'Active' ? 'تم تنشيط الطالب' : 'تم تعليق الطالب'
      );
    } catch (error) {
      devConsole.error(error);
      toast.error('حدث خطأ أثناء تحديث حالة الطالب، أعد المحاولة.');
    }
  }

  const handleExport = async () => {
    if (exporting) return;

    setExporting(true);
    const toastId = toast.loading('جاري تصدير بيانات الطلاب...');

    try {
      const itemsToExport = await adminService.exportUsers({
        search: search.trim() || undefined,
        educationStage: educationStageFilter || undefined,
        gradeLevel: gradeLevelFilter || undefined,
        studyTrack: studyTrackFilter || undefined,
        gender: genderFilter || undefined,
        governorate: governorateFilter || undefined,
        role: 'Student',
      });

      if (!itemsToExport || itemsToExport.length === 0) {
        toast.error('لا توجد بيانات لتصديرها', { id: toastId });
        return;
      }

      const mapGender = (g?: string) => {
        if (!g) return '—';
        const m: Record<string, string> = { Male: 'ذكر', Female: 'أنثى' };
        return m[g] || g;
      };

      const mapEducationStage = (s?: string) => {
        if (!s) return '—';
        return getEducationStageLabel(s);
      };

      const mapGradeLevel = (g?: string) => {
        if (!g || g === 'N/A') return '—';
        return getGradeLevelLabel(g);
      };

      const mapStudyTrack = (t?: string) => {
        if (!t || t === 'N/A') return '—';
        return getStudyTrackLabel(t);
      };

      const headers = [
        'رقم متابعة ولي الأمر',
        'الاسم الكامل',
        'رقم الهاتف',
        'رقم الهاتف الإضافي',
        'هاتف الأب / ولي الأمر',
        'المرحلة الدراسية',
        'الصف الدراسي',
        'الشعبة / التخصص',
        'المحافظة',
        'النوع',
        'الحالة',
        'تاريخ الانضمام',
      ];

      const csvRows = [headers.join(',')];

      for (const u of itemsToExport) {
        const rowData = [
          u.parentTrackingCode || '—',
          u.fullName,
          u.phoneNumber,
          u.secondaryPhone || '—',
          u.parentPhone || '—',
          mapEducationStage(u.educationStage),
          mapGradeLevel(u.grade),
          mapStudyTrack(u.track),
          u.governorate || '—',
          mapGender(u.gender),
          statusLabel(u.status),
          new Date(u.createdAt).toLocaleDateString('ar-EG'),
        ];

        const escapedRow = rowData.map((val) => {
          const stringVal = String(val).replace(/"/g, '""');
          return `"${stringVal}"`;
        });

        csvRows.push(escapedRow.join(','));
      }

      const csvContent = '\uFEFF' + csvRows.join('\n');
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.setAttribute('href', url);
      link.setAttribute(
        'download',
        `قائمة_الطلاب_${new Date().toISOString().split('T')[0]}.csv`
      );
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      toast.success('تم تصدير البيانات بنجاح', { id: toastId });
    } catch (error) {
      devConsole.error(error);
      toast.error('حدث خطأ أثناء تصدير البيانات، يرجى المحاولة لاحقاً', {
        id: toastId,
      });
    } finally {
      setExporting(false);
    }
  };

  const activeStudents = users.filter((user) => user.status === 'Active').length;
  const pendingStudents = users.filter((user) => user.status !== 'Active').length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const firstVisibleItem = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastVisibleItem = Math.min(page * pageSize, totalCount);

  const columns: AdminColumn<AdminUserListDto>[] = [
    {
      key: 'student',
      label: 'الطالب',
      render: (u) => (
        <div className="flex items-center gap-4">
          <UserAvatar
            avatarSlug={u.avatarSlug}
            fullName={u.fullName}
            size={48}
            className="shadow-sm"
          />
          <div>
            <div className="font-bold text-[var(--admin-text)]">
              {u.fullName}
            </div>
            <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono tracking-wider">
              {u.phoneNumber}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: 'grade',
      label: 'المرحلة والصف',
      render: (u) => (
        <div className="flex flex-col gap-1">
          <span className="text-sm font-bold text-[var(--admin-text)]">
            {u.grade && u.grade !== 'N/A' ? getGradeLevelLabel(u.grade) : '—'}
          </span>
          {(u.educationStage && u.educationStage !== 'N/A') || (u.track && u.track !== 'N/A') ? (
            <span className="text-xs text-[var(--admin-muted)]">
              {u.educationStage && u.educationStage !== 'N/A' && getEducationStageLabel(u.educationStage)}
              {u.track && u.track !== 'N/A' && `${u.educationStage && u.educationStage !== 'N/A' ? ' - ' : ''}${getStudyTrackLabel(u.track)}`}
            </span>
          ) : null}
        </div>
      ),
    },
    {
      key: 'parentTrackingCode',
      label: 'رقم متابعة ولي الأمر',
      render: (u) => (
        <span className="font-mono text-sm font-semibold text-[var(--admin-primary)]">
          {u.parentTrackingCode || '—'}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (u) => (
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${getStatusClasses(
            u.status
          )}`}
        >
          <span className="h-1.5 w-1.5 rounded-full bg-current" />
          {statusLabel(u.status)}
        </span>
      ),
    },
    {
      key: 'lastActivity',
      label: 'تاريخ التسجيل',
      render: (u) => (
        <span className="text-sm text-[var(--admin-muted)] font-medium">
          {formatRelativeDate(u.createdAt)}
        </span>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (u) => (
        <div className="flex items-center justify-end gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
          <NeumorphButton
            type="button"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              setConfirmUser(u);
            }}
            intent={u.status === 'Active' ? 'danger' : 'primary'}
            size="icon"
            title={u.status === 'Active' ? 'تعليق حساب الطالب' : 'تنشيط حساب الطالب'}
          >
            {u.status === 'Active' ? (
              <UserX className="h-5 w-5" />
            ) : (
              <UserCheck className="h-5 w-5" />
            )}
          </NeumorphButton>
        </div>
      ),
    },
  ];

  return (
    <StudentManagementFrame staff={staff}>
      <div className="flex justify-end">
        <NeumorphButton intent="primary" size="lg" pill onClick={() => setShowAddUser(true)}>
          <UserPlus className="h-4 w-4" />إضافة طالب جديد
        </NeumorphButton>
      </div>
      <AddUserDrawer
        open={showAddUser}
        onClose={() => setShowAddUser(false)}
        onSuccess={refreshUsers}
        defaultRole="Student"
      />
      <ConfirmDialog
        open={!!confirmUser}
        title={
          confirmUser?.status === 'Active'
            ? 'تعليق حساب طالب؟'
            : 'تنشيط حساب طالب؟'
        }
        description={
          confirmUser?.status === 'Active'
            ? `هل أنت متأكد من تعليق حساب الطالب "${confirmUser?.fullName}"؟ لن يتمكن من تسجيل الدخول للمنصة حتى تفعيله مجدداً.`
            : `سيتم إعادة تفعيل حساب الطالب "${confirmUser?.fullName}" وتمكينه من الدخول ومشاهدة الدروس.`
        }
        confirmLabel={
          confirmUser?.status === 'Active'
            ? 'نعم، تعليق الحساب'
            : 'نعم، تنشيط الحساب'
        }
        cancelLabel="إلغاء"
        variant={confirmUser?.status === 'Active' ? 'danger' : 'primary'}
        onConfirm={() => confirmUser && handleToggleStatus(confirmUser)}
        onCancel={() => setConfirmUser(null)}
      />

      {loadError && users.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-16 text-center gap-6 bg-[var(--admin-card-soft)]">
          <div className="rounded-full bg-red-100 p-6 text-red-500 dark:bg-red-900/20 shadow-sm">
            <RefreshCw className="h-10 w-10" />
          </div>
          <div className="space-y-2">
            <h4 className="text-xl font-black text-[var(--admin-text)] text-shadow-sm">
              تعذّر تحميل قائمة الطلاب
            </h4>
            <p className="max-w-sm text-[var(--admin-muted)] leading-relaxed">
              توجد مشكلة في الاتصال بالخادم حالياً. يرجى التحقق من الاتصال وإعادة المحاولة.
            </p>
          </div>
          <NeumorphButton
            onClick={refreshUsers}
            intent="primary"
            size="lg"
            pill
            className="px-10"
          >
            <RefreshCw className="h-4 w-4" /> إعادة المحاولة الآن
          </NeumorphButton>
        </div>
      ) : loading && users.length === 0 ? (
        <AdminPageSkeleton />
      ) : (
        <>
          <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
            <AdminStatCard
              variant="light"
              icon={Users}
              label="إجمالي الطلاب"
              value={totalCount}
              subtitle="وفق البحث والفلاتر الحالية"
            />

            <AdminStatCard
              variant="accent"
              icon={Sparkles}
              label="النشطون في الصفحة"
              value={activeStudents}
              subtitle={`من أصل ${users.length} طالب ظاهر`}
            />

            <AdminStatCard
              variant="muted"
              icon={Shield}
              label="المعلقون في الصفحة"
              value={pendingStudents}
              subtitle={`من أصل ${users.length} طالب ظاهر`}
            />
          </section>

          <AdminSearchToolbar
            value={search}
            onChange={setSearch}
            placeholder="البحث برقم متابعة ولي الأمر، الاسم، أو رقم الهاتف..."
            actions={
              <>
                <button
                  onClick={() => setShowFilters(!showFilters)}
                  className={`inline-flex items-center gap-2 rounded-full border px-6 py-3 text-sm font-bold transition ${showFilters ? 'bg-[var(--admin-primary-15)] border-[var(--admin-primary)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}
                >
                  <Filter className="h-4 w-4" />
                  تصفية
                </button>
                <button
                  onClick={handleExport}
                  disabled={exporting}
                  className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-bg)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-50"
                >
                  <Download
                    className={`h-4 w-4 ${exporting ? 'animate-spin' : ''}`}
                  />
                  {exporting ? 'جاري التصدير...' : 'تصدير البيانات'}
                </button>
              </>
            }
          />

          {showFilters && (
            <div className="mb-8 rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 animate-in fade-in slide-in-from-top-3 duration-200">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">
                    المرحلة الدراسية
                  </label>
                  <select
                    value={educationStageFilter}
                    onChange={(e) => {
                      setEducationStageFilter(e.target.value);
                      setPage(1);
                    }}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="Secondary">{getEducationStageLabel('Secondary')}</option>
                    <option value="Baccalaureate">{getEducationStageLabel('Baccalaureate')}</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">
                    الصف الدراسي
                  </label>
                  <select
                    value={gradeLevelFilter}
                    onChange={(e) => {
                      setGradeLevelFilter(e.target.value);
                      setPage(1);
                    }}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="FirstSecondary">{getGradeLevelLabel('FirstSecondary')}</option>
                    <option value="SecondSecondary">{getGradeLevelLabel('SecondSecondary')}</option>
                    <option value="FirstBaccalaureate">{getGradeLevelLabel('FirstBaccalaureate')}</option>
                    <option value="SecondBaccalaureate">{getGradeLevelLabel('SecondBaccalaureate')}</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">
                    الشعبة / التخصص
                  </label>
                  <select
                    value={studyTrackFilter}
                    onChange={(e) => {
                      setStudyTrackFilter(e.target.value);
                      setPage(1);
                    }}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="Arts">{getStudyTrackLabel('Arts')}</option>
                    <option value="Science">{getStudyTrackLabel('Science')}</option>
                    <option value="MedicineAndLifeSciences">
                      {getStudyTrackLabel('MedicineAndLifeSciences')}
                    </option>
                    <option value="EngineeringAndComputerScience">
                      {getStudyTrackLabel('EngineeringAndComputerScience')}
                    </option>
                    <option value="Business">{getStudyTrackLabel('Business')}</option>
                    <option value="ArtsAndHumanities">{getStudyTrackLabel('ArtsAndHumanities')}</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">
                    النوع
                  </label>
                  <select
                    value={genderFilter}
                    onChange={(e) => {
                      setGenderFilter(e.target.value);
                      setPage(1);
                    }}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="Male">ذكر</option>
                    <option value="Female">أنثى</option>
                  </select>
                </div>
              </div>
            </div>
          )}

          {loadError ? (
            <div
              role="alert"
              className="mb-4 flex items-center justify-between gap-3 rounded-xl bg-[var(--admin-danger-10)] px-4 py-3 text-sm font-bold text-[var(--admin-danger)]"
            >
              <span>تعذر تحديث النتائج. ما زالت آخر بيانات ناجحة معروضة.</span>
              <button
                type="button"
                onClick={refreshUsers}
                className="min-h-11 rounded-full px-4 underline underline-offset-4"
              >
                إعادة المحاولة
              </button>
            </div>
          ) : null}

          <div
            className={`content-visibility-auto transition-opacity ${
              loading ? 'opacity-70' : 'opacity-100'
            }`}
            aria-busy={loading}
          >
            <AdminDataTable
              data={users}
              columns={columns}
              loading={false}
              pagination={false}
              rowKey={(u) => u.id}
              emptyMessage="لا توجد نتائج مطابقة لفلترة الطلاب."
              onRowClick={(u) => {
                router.push(staff ? `/assistant/students/${u.id}` : `/admin/users/${u.id}`);
              }}
            />
            <div className="flex flex-wrap items-center justify-between gap-4 border-x border-b border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:px-6">
              <div className="flex flex-wrap items-center gap-4">
                <span className="text-sm font-bold text-[var(--admin-muted)]">
                  عرض {firstVisibleItem.toLocaleString('ar-EG')}–
                  {lastVisibleItem.toLocaleString('ar-EG')} من{' '}
                  {totalCount.toLocaleString('ar-EG')} طالب
                </span>
                <label className="flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)]">
                  عدد الصفوف
                  <select
                    aria-label="عدد الطلاب في الصفحة"
                    value={pageSize}
                    onChange={(event) => {
                      setPageSize(
                        Number(event.target.value) as (typeof STUDENT_PAGE_SIZES)[number]
                      );
                      setPage(1);
                    }}
                    className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-[var(--admin-text)]"
                  >
                    {STUDENT_PAGE_SIZES.map((size) => (
                      <option key={size} value={size}>
                        {size.toLocaleString('ar-EG')}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <nav
                className="flex items-center gap-2"
                aria-label="صفحات قائمة الطلاب"
              >
                <button
                  type="button"
                  disabled={page <= 1 || loading}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  className="flex min-h-11 min-w-11 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] disabled:cursor-not-allowed disabled:opacity-40"
                  aria-label="الصفحة السابقة"
                >
                  <ChevronRight className="h-5 w-5" />
                </button>
                <span
                  className="min-w-24 text-center text-sm font-black text-[var(--admin-primary)]"
                  aria-current="page"
                >
                  {page.toLocaleString('ar-EG')} /{' '}
                  {totalPages.toLocaleString('ar-EG')}
                </span>
                <button
                  type="button"
                  disabled={page >= totalPages || loading}
                  onClick={() =>
                    setPage((current) => Math.min(totalPages, current + 1))
                  }
                  className="flex min-h-11 min-w-11 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] disabled:cursor-not-allowed disabled:opacity-40"
                  aria-label="الصفحة التالية"
                >
                  <ChevronLeft className="h-5 w-5" />
                </button>
              </nav>
            </div>
          </div>
        </>
      )}
    </StudentManagementFrame>
  );
}
