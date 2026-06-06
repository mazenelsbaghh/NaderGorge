'use client';

import { devConsole } from '@/utils/dev-console';

import { useEffect, useState, useCallback } from 'react';
import {
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
import { AddUserDrawer } from './components/AddUserDrawer';
import { AssistantProfileModal } from './components/AssistantProfileModal';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminSearchToolbar,
  AdminPageSkeleton,
  ConfirmDialog,
} from '@/components/admin';
import { formatRelativeDate, getInitials } from '@/components/admin/admin-utils';
import { AdminUserListDto, adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

type UserRole = 'Admin' | 'Assistant' | 'Student';
type RoleFilter = 'all' | UserRole;

const ROLE_FILTERS: Array<{ value: RoleFilter; label: string }> = [
  { value: 'all', label: 'الكل' },
  { value: 'Admin', label: 'المديرين' },
  { value: 'Assistant', label: 'المساعدين' },
  { value: 'Student', label: 'الطلاب' },
];

function normalizeRole(user: AdminUserListDto): UserRole {
  if (user.roles.includes('Admin')) return 'Admin';
  if (user.roles.includes('Student')) return 'Student';
  return 'Assistant';
}

function roleLabel(role: UserRole) {
  if (role === 'Admin') return 'مدير النظام';
  if (role === 'Assistant') return 'مساعد تعليمي';
  return 'طالب';
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

export default function AdminUsersPage() {
  const router = useRouter();
  const [users, setUsers] = useState<AdminUserListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState<RoleFilter>('all');
  const [educationStageFilter, setEducationStageFilter] = useState('');
  const [gradeLevelFilter, setGradeLevelFilter] = useState('');
  const [studyTrackFilter, setStudyTrackFilter] = useState('');
  const [genderFilter, setGenderFilter] = useState('');
  const [governorateFilter] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [confirmUser, setConfirmUser] = useState<AdminUserListDto | null>(null);
  const [showAddUser, setShowAddUser] = useState(false);
  const [selectedAssistant, setSelectedAssistant] = useState<AdminUserListDto | null>(null);
  const [exporting, setExporting] = useState(false);

  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true);
      setLoadError(false);
      const data = await adminService.listUsers(
        1,
        1000,
        search,
        educationStageFilter || undefined,
        gradeLevelFilter || undefined,
        studyTrackFilter || undefined,
        genderFilter || undefined,
        governorateFilter || undefined
      );
      setUsers(data.items);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [search, educationStageFilter, gradeLevelFilter, studyTrackFilter, genderFilter, governorateFilter]);

  useEffect(() => {
    let isMounted = true;
    fetchUsers().finally(() => { if (!isMounted) return; });
    return () => { isMounted = false; };
  }, [fetchUsers]);


  async function handleToggleStatus(user: AdminUserListDto) {
    const nextStatus = user.status === 'Active' ? 'Disabled' : 'Active';
    if (normalizeRole(user) === 'Admin' && nextStatus === 'Disabled') {
      toast.error('لا يمكن تعطيل حساب مدير النظام');
      setConfirmUser(null);
      return;
    }

    try {
      await adminService.updateUserStatus(user.id, nextStatus);
      setUsers((currentUsers) =>
        currentUsers.map((entry) =>
          entry.id === user.id ? { ...entry, status: nextStatus } : entry,
        ),
      );
      setConfirmUser(null);
      toast.success(nextStatus === 'Active' ? 'تم تنشيط المستخدم' : 'تم تعليق المستخدم');
    } catch (error) {
      devConsole.error(error);
      toast.error('حدث خطأ أثناء تحديث حالة المستخدم، أعد المحاولة.');
    }
  }

  const handleExport = async () => {
    if (exporting) return;

    setExporting(true);
    const toastId = toast.loading('جاري تجهيز وتصدير البيانات...');

    try {
      const data = await adminService.listUsers(
        1,
        100000,
        search,
        educationStageFilter || undefined,
        gradeLevelFilter || undefined,
        studyTrackFilter || undefined,
        genderFilter || undefined,
        governorateFilter || undefined
      );

      const itemsToExport = roleFilter === 'all'
        ? data.items
        : data.items.filter((user) => normalizeRole(user) === roleFilter);

      if (!itemsToExport || itemsToExport.length === 0) {
        toast.error('لا توجد بيانات لتصديرها', { id: toastId });
        return;
      }

      const mapGender = (g?: string) => {
        if (!g) return '—';
        const m: Record<string, string> = { Male: 'ذكر', Female: 'أنثى' };
        return m[g] || g;
      };

      const mapSchoolType = (t?: string) => {
        if (!t) return '—';
        const m: Record<string, string> = {
          Government: 'حكومية', Language: 'لغات', Experimental: 'تجريبية',
          Private: 'خاصة', Azhari: 'أزهرية', American: 'أمريكية',
        };
        return m[t] || t;
      };

      const mapEducationStage = (s?: string) => {
        if (!s) return '—';
        const m: Record<string, string> = { Secondary: 'ثانوية', Baccalaureate: 'بكالوريا' };
        return m[s] || s;
      };

      const mapGradeLevel = (g?: string) => {
        if (!g || g === 'N/A') return '—';
        const m: Record<string, string> = {
          FirstSecondary: 'أولى ثانوي', SecondSecondary: 'ثانية ثانوي',
          FirstBaccalaureate: 'أولى بكالوريا', SecondBaccalaureate: 'ثانية بكالوريا',
        };
        return m[g] || g;
      };

      const mapStudyTrack = (t?: string) => {
        if (!t || t === 'N/A') return '—';
        const m: Record<string, string> = {
          Science: 'علمي', Arts: 'أدبي',
          MedicineAndLifeSciences: 'الطب وعلوم الحياة',
          EngineeringAndComputerScience: 'الهندسة وعلوم الحاسب',
          Business: 'قطاع الأعمال', ArtsAndHumanities: 'الآداب والفنون',
        };
        return m[t] || t;
      };

      const headers = [
        'كود الطالب',
        'الاسم الكامل',
        'رقم الهاتف',
        'رقم الهاتف الإضافي',
        'هاتف الأب / ولي الأمر',
        'تاريخ ميلاد الأب',
        'هاتف الأم',
        'تاريخ ميلاد الأم',
        'هاتف ولي الأمر الإضافي',
        'المرحلة الدراسية',
        'الصف الدراسي',
        'الشعبة / التخصص',
        'الجنسية',
        'المحافظة',
        'المنطقة / الحي',
        'العنوان بالتفصيل',
        'تاريخ الميلاد',
        'النوع',
        'اسم المدرسة',
        'نوع المدرسة',
        'الأب على قيد الحياة',
        'الأم على قيد الحياة',
        'الرصيد الحالي (ج.م)',
        'سبب التعليق (إن وجد)',
        'الصلاحية',
        'الحالة',
        'تاريخ الانضمام'
      ];

      const csvRows = [headers.join(',')];

      for (const u of itemsToExport) {
        const role = normalizeRole(u);
        const rowData = [
          u.studentCode || '—',
          u.fullName,
          u.phoneNumber,
          u.secondaryPhone || '—',
          u.parentPhone || '—',
          u.fatherDateOfBirth ? new Date(u.fatherDateOfBirth).toLocaleDateString('ar-EG') : '—',
          u.motherPhone || '—',
          u.motherDateOfBirth ? new Date(u.motherDateOfBirth).toLocaleDateString('ar-EG') : '—',
          u.secondaryParentPhone || '—',
          mapEducationStage(u.educationStage),
          mapGradeLevel(u.grade),
          mapStudyTrack(u.track),
          u.nationality || '—',
          u.governorate || '—',
          u.district || '—',
          u.address || '—',
          u.dateOfBirth ? new Date(u.dateOfBirth).toLocaleDateString('ar-EG') : '—',
          mapGender(u.gender),
          u.schoolName || '—',
          mapSchoolType(u.schoolType),
          u.isFatherAlive === false ? 'لا (متوفى)' : 'نعم',
          u.isMotherAlive === false ? 'لا (متوفاة)' : 'نعم',
          u.currentBalance !== undefined ? `${u.currentBalance}` : '0',
          u.suspensionReason || '—',
          roleLabel(role),
          statusLabel(u.status),
          new Date(u.createdAt).toLocaleDateString('ar-EG')
        ];

        const escapedRow = rowData.map(val => {
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
      link.setAttribute('download', `قائمة_المستخدمين_${new Date().toISOString().split('T')[0]}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      toast.success('تم تصدير البيانات بنجاح', { id: toastId });
    } catch (error) {
      devConsole.error(error);
      toast.error('حدث خطأ أثناء تصدير البيانات، يرجى المحاولة لاحقاً', { id: toastId });
    } finally {
      setExporting(false);
    }
  };

  const filteredUsers =
    roleFilter === 'all'
      ? users
      : users.filter((user) => normalizeRole(user) === roleFilter);

  const activeCount = users.filter((user) => user.status === 'Active').length;
  const pendingCount = users.filter((user) => user.status !== 'Active').length;

  const columns: AdminColumn<AdminUserListDto>[] = [
    {
      key: 'user',
      label: 'المستخدم',
      render: (u) => (
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-primary-15)] font-bold text-[var(--admin-primary)] shadow-sm">
            {getInitials(u.fullName)}
          </div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{u.fullName}</div>
            <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono tracking-wider">{u.phoneNumber}</div>
          </div>
        </div>
      )
    },
    {
      key: 'role',
      label: 'الدور والمرحلة',
      render: (u) => (
        <div className="flex flex-col gap-1">
          <span className="text-sm font-bold text-[var(--admin-text)]">
            {roleLabel(normalizeRole(u))}
          </span>
          {normalizeRole(u) === 'Student' && (
            <span className="text-xs text-[var(--admin-muted)]">
              {u.grade} {u.track !== 'N/A' && `- ${u.track}`}
            </span>
          )}
        </div>
      )
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (u) => (
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${getStatusClasses(
            u.status,
          )}`}
        >
          <span className="h-1.5 w-1.5 rounded-full bg-current" />
          {statusLabel(u.status)}
        </span>
      )
    },
    {
      key: 'lastActivity',
      label: 'آخر نشاط',
      render: (u) => (
        <span className="text-sm text-[var(--admin-muted)] font-medium">
          {formatRelativeDate(u.createdAt)}
        </span>
      )
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (u) => {
        const isAdmin = normalizeRole(u) === 'Admin';
        const isDisableAction = u.status === 'Active';
        const disableStatusToggle = isAdmin && isDisableAction;

        return (
        <div className="flex items-center justify-end gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
          <NeumorphButton
            type="button"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              if (disableStatusToggle) {
                toast.error('لا يمكن تعطيل حساب مدير النظام');
                return;
              }
              setConfirmUser(u);
            }}
            intent={u.status === 'Active' ? 'danger' : 'primary'}
            size="icon"
            disabled={disableStatusToggle}
            title={
              disableStatusToggle
                ? 'لا يمكن تعطيل مدير النظام'
                : u.status === 'Active'
                  ? 'تعليق المستخدم'
                  : 'تنشيط المستخدم'
            }
          >
            {u.status === 'Active' ? (
              <UserX className="h-5 w-5" />
            ) : (
              <UserCheck className="h-5 w-5" />
            )}
          </NeumorphButton>
        </div>
        );
      }
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/users"
      sectionLabel="إدارة المستخدمين"
      pageTitle="قائمة المستخدمين"
      subtitle="إدارة وتدقيق الوصول إلى النظام والبيانات الأكاديمية."
      action={
        <NeumorphButton intent="primary" size="lg" pill onClick={() => setShowAddUser(true)}>
          <UserPlus className="h-4 w-4" />
          إضافة مستخدم
        </NeumorphButton>
      }
    >
      <AddUserDrawer
        open={showAddUser}
        onClose={() => setShowAddUser(false)}
        onSuccess={() => fetchUsers()}
      />
      <ConfirmDialog
        open={!!confirmUser}
        title={confirmUser?.status === 'Active' ? 'تعليق حساب مستخدم؟' : 'تنشيط حساب مستخدم؟'}
        description={confirmUser?.status === 'Active'
          ? `هل أنت متأكد من تعليق حساب "${confirmUser?.fullName}"؟ لن يتمكن المستخدم من تسجيل الدخول حتى يتم تنشيطه مرة أخرى.`
          : `سيتم إعادة تفعيل حساب "${confirmUser?.fullName}" وتمكينه من الوصول للنظام.`
        }
        confirmLabel={confirmUser?.status === 'Active' ? 'نعم، تعليق الحساب' : 'نعم، تنشيط الحساب'}
        cancelLabel="إلغاء"
        variant={confirmUser?.status === 'Active' ? 'danger' : 'primary'}
        onConfirm={() => confirmUser && handleToggleStatus(confirmUser)}
        onCancel={() => setConfirmUser(null)}
      />

      {loadError ? (
        <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-16 text-center gap-6 bg-[var(--admin-card-soft)]">
          <div className="rounded-full bg-red-100 p-6 text-red-500 dark:bg-red-900/20 shadow-sm">
            <RefreshCw className="h-10 w-10" />
          </div>
          <div className="space-y-2">
            <h4 className="text-xl font-black text-[var(--admin-text)] text-shadow-sm">تعذّر تحميل قائمة المستخدمين</h4>
            <p className="max-w-sm text-[var(--admin-muted)] leading-relaxed">توجد مشكلة في الاتصال بالخادم حالياً. يرجى التحقق من الإنترنت وإعادة المحاولة.</p>
          </div>
          <NeumorphButton onClick={() => window.location.reload()} intent="primary" size="lg" pill className="px-10">
            <RefreshCw className="h-4 w-4" /> إعادة المحاولة الآن
          </NeumorphButton>
        </div>
      ) : loading ? (
        <AdminPageSkeleton />
      ) : (
        <>
          <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
            <AdminStatCard
              variant="light"
              icon={Users}
              label="الإجمالي"
              value={users.length}
              subtitle="إجمالي المستخدمين المسجلين"
            />

            <AdminStatCard
              variant="accent"
              icon={Sparkles}
              label="اليوم"
              value={activeCount}
              subtitle="نشطون وموثقون"
            />

            <AdminStatCard
              variant="muted"
              icon={Shield}
              label="قيد الانتظار"
              value={pendingCount}
              subtitle="حسابات تحتاج للمراجعة"
            />
          </section>

          <div className="mb-8 flex justify-center">
            <div className="inline-flex flex-wrap gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
              {ROLE_FILTERS.map((filter) => (
                <button
                  key={filter.value}
                  onClick={() => setRoleFilter(filter.value)}
                  className={`rounded-full px-6 py-2.5 text-sm font-bold transition ${roleFilter === filter.value
                    ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                    : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
                    }`}
                >
                  {filter.label}
                </button>
              ))}
            </div>
          </div>

          <AdminSearchToolbar
            value={search}
            onChange={setSearch}
            placeholder="البحث بكود الطالب، الاسم، أو الهاتف..."
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
                  <Download className={`h-4 w-4 ${exporting ? 'animate-spin' : ''}`} />
                  {exporting ? 'جاري التصدير...' : 'تصدير البيانات'}
                </button>
              </>
            }
          />

          {showFilters && (
            <div className="mb-8 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">المرحلة الدراسية</label>
                  <select
                    value={educationStageFilter}
                    onChange={(e) => setEducationStageFilter(e.target.value)}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="Secondary">ثانوية</option>
                    <option value="Baccalaureate">بكالوريا</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">الصف الدراسي</label>
                  <select
                    value={gradeLevelFilter}
                    onChange={(e) => setGradeLevelFilter(e.target.value)}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="FirstSecondary">أولى ثانوي</option>
                    <option value="SecondSecondary">ثانية ثانوي</option>
                    <option value="FirstBaccalaureate">أولى بكالوريا</option>
                    <option value="SecondBaccalaureate">ثانية بكالوريا</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">الشعبة / التخصص</label>
                  <select
                    value={studyTrackFilter}
                    onChange={(e) => setStudyTrackFilter(e.target.value)}
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none"
                  >
                    <option value="">الكل</option>
                    <option value="Arts">أدبي</option>
                    <option value="Science">علمي</option>
                    <option value="MedicineAndLifeSciences">الطب وعلوم الحياة</option>
                    <option value="EngineeringAndComputerScience">الهندسة وعلوم الحاسب</option>
                    <option value="Business">قطاع الأعمال</option>
                    <option value="ArtsAndHumanities">الآداب والفنون</option>
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">النوع</label>
                  <select
                    value={genderFilter}
                    onChange={(e) => setGenderFilter(e.target.value)}
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

          <AdminDataTable
            data={filteredUsers}
            columns={columns}
            loading={loading}
            rowKey={(u) => u.id}
            emptyMessage="لا توجد نتائج مطابقة."
            onRowClick={(u) => {
              if (normalizeRole(u) === 'Student') {
                router.push(`/admin/users/${u.id}`);
              } else {
                setSelectedAssistant(u);
              }
            }}
          />



          <AssistantProfileModal
            open={!!selectedAssistant}
            onClose={() => setSelectedAssistant(null)}
            assistant={selectedAssistant}
          />
        </>
      )}
    </AdminShellChrome>
  );
}
