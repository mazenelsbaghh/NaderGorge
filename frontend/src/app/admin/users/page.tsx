'use client';

import { useEffect, useState } from 'react';
import {
  Download,
  Eye,
  Filter,
  Monitor,
  PencilLine,
  Shield,
  Sparkles,
  Trash2,
  UserPlus,
  Users,
  UserX,
  UserCheck,
  RefreshCw,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
  AdminSearchToolbar,
  AdminPageSkeleton,
  ConfirmDialog,
} from '@/components/admin';
import { formatRelativeDate, getInitials } from '@/components/admin/admin-utils';
import { AdminUserListDto, DeviceDto, adminService } from '@/services/admin-service';
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
  if (user.roles.includes('Assistant')) return 'Assistant';
  return 'Student';
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
  const [deviceModalUser, setDeviceModalUser] = useState<AdminUserListDto | null>(null);
  const [devices, setDevices] = useState<DeviceDto[]>([]);

  const [educationStageFilter, setEducationStageFilter] = useState('');
  const [gradeLevelFilter, setGradeLevelFilter] = useState('');
  const [studyTrackFilter, setStudyTrackFilter] = useState('');
  const [genderFilter, setGenderFilter] = useState('');
  const [governorateFilter, setGovernorateFilter] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [confirmUser, setConfirmUser] = useState<AdminUserListDto | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function fetchUsers() {
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
        if (!isMounted) return;

        setUsers(data.items);
      } catch (error) {
        setLoadError(true);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    fetchUsers();

    return () => {
      isMounted = false;
    };
  }, [search, educationStageFilter, gradeLevelFilter, studyTrackFilter, genderFilter, governorateFilter]);

  async function handleViewDevices(user: AdminUserListDto) {
    setDeviceModalUser(user);

    try {
      const data = await adminService.getUserDevices(user.id);
      setDevices(data);
    } catch (error) {
      console.error(error);
      toast.error('حدث خطأ أثناء تحميل بيانات الأجهزة، يرجى المحاولة.');
    }
  }

  async function handleToggleStatus(user: AdminUserListDto) {
    const nextStatus = user.status === 'Active' ? 'Disabled' : 'Active';

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
      console.error(error);
      toast.error('حدث خطأ أثناء تحديث حالة المستخدم، أعد المحاولة.');
    }
  }

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
      render: (u) => (
        <div className="flex items-center justify-end gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
          <NeumorphButton
            type="button"
            onClick={() => {
              if (normalizeRole(u) === 'Student') {
                router.push(`/admin/users/${u.id}`);
              } else {
                handleViewDevices(u);
              }
            }}
            intent="icon"
            size="icon"
            title="عرض التفاصيل"
          >
            <Eye className="h-5 w-5" />
          </NeumorphButton>

          <NeumorphButton
            type="button"
            onClick={() => setConfirmUser(u)}
            intent={u.status === 'Active' ? 'danger' : 'primary'}
            size="icon"
            title={u.status === 'Active' ? 'تعليق المستخدم' : 'تنشيط المستخدم'}
          >
            {u.status === 'Active' ? (
              <UserX className="h-5 w-5" />
            ) : (
              <UserCheck className="h-5 w-5" />
            )}
          </NeumorphButton>
        </div>
      )
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/users"
      sectionLabel="إدارة المستخدمين"
      pageTitle="قائمة المستخدمين"
      subtitle="إدارة وتدقيق الوصول إلى النظام والبيانات الأكاديمية."
      action={
        <NeumorphButton intent="primary" size="lg" pill>
          <UserPlus className="h-4 w-4" />
          دعوة عضو جديد
        </NeumorphButton>
      }
    >
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
                <button className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-bg)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]">
                  <Download className="h-4 w-4" />
                  تصدير البيانات
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
            expandedRowRender={(u) => (
              <div className="grid gap-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 md:grid-cols-2 lg:grid-cols-4 text-right">
                <div>
                  <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">المنطقة / الحي</p>
                  <p className="text-sm font-black text-[var(--admin-text)]">{u.district || 'غير متوفر'}</p>
                  {u.studentCode && (
                    <p className="text-xs text-[var(--admin-muted)] mt-1">كود الطالب: {u.studentCode}</p>
                  )}
                </div>
                <div>
                  <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">تاريخ الميلاد</p>
                  <p className="text-sm font-bold text-[var(--admin-text)]">
                    {u.dateOfBirth ? new Date(u.dateOfBirth).toLocaleDateString('en-GB') : 'غير متوفر'}
                  </p>
                </div>
                <div>
                  <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">حالة الوالدين</p>
                  <div className="flex flex-col gap-1 mt-1 text-sm font-bold">
                    <span className={u.isFatherAlive === false ? 'text-red-500' : 'text-emerald-500'}>
                      الأب: {u.isFatherAlive === false ? 'متوفى' : 'على قيد الحياة'}
                    </span>
                    <span className={u.isMotherAlive === false ? 'text-red-500' : 'text-emerald-500'}>
                      الأم: {u.isMotherAlive === false ? 'متوفاة' : 'على قيد الحياة'}
                    </span>
                  </div>
                </div>
                <div>
                  <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">العنوان</p>
                  <p className="text-sm font-bold text-[var(--admin-text)]">
                    {u.governorate && u.governorate !== 'N/A' ? `${u.governorate}` : ''}
                    {u.district ? ` - ${u.district}` : ''}
                    {u.address ? ` - ${u.address}` : ' - غير متوفر'}
                  </p>
                  {(u.secondaryPhone || u.secondaryParentPhone) && (
                    <div className="mt-2 text-xs text-[var(--admin-muted)]">
                      {u.secondaryPhone && <p>هاتف إضافي: {u.secondaryPhone}</p>}
                      {u.secondaryParentPhone && <p>هاتف ولي أمر إضافي: {u.secondaryParentPhone}</p>}
                    </div>
                  )}
                </div>
              </div>
            )}
          />

          <AdminModal
            open={!!deviceModalUser}
            onClose={() => { setDeviceModalUser(null); setDevices([]); }}
            title="الأجهزة المسجلة"
            subtitle={deviceModalUser?.fullName}
          >
            {devices.length === 0 ? (
              <div className="rounded-[1.5rem] bg-[var(--admin-card-soft)] p-8 text-center text-[var(--admin-muted)] border border-[var(--admin-border)] mt-4">
                لا توجد أجهزة مسجلة لهذا المستخدم.
              </div>
            ) : (
              <div className="space-y-3 mt-4">
                {devices.map((device) => (
                  <div
                    key={device.id}
                    className="flex flex-col gap-4 rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:flex-row sm:items-center sm:justify-between shadow-sm"
                  >
                    <div className="flex items-center gap-4">
                      <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
                        <Monitor className="h-5 w-5" />
                      </div>
                      <div>
                        <div className="font-bold text-[var(--admin-text)]">
                          {device.browser} <span className="text-[var(--admin-muted)] font-normal px-1">•</span> {device.os}
                        </div>
                        <div className="text-sm text-[var(--admin-muted)] mt-1 font-mono">{device.fingerprint}</div>
                      </div>
                    </div>
                    <span className={`rounded-full px-3 py-1 text-xs font-bold ${device.isActive ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'}`}>
                      {device.isActive ? 'مسجل الدخول' : 'يُمكن توثيقه'}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </AdminModal>
        </>
      )}
    </AdminShellChrome>
  );
}
