'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useState, useCallback } from 'react';
import {
  Download,
  Shield,
  Sparkles,
  UserPlus,
  Users,
  UserX,
  UserCheck,
  RefreshCw,
  Briefcase,
} from 'lucide-react';
import { AddUserDrawer } from '../users/components/AddUserDrawer';
import { AssistantProfileModal } from '../users/components/AssistantProfileModal';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminSearchToolbar,
  AdminPageSkeleton,
  ConfirmDialog,
  EmployeeProfileDrawer,
} from '@/components/admin';
import {
  formatRelativeDate,
  getInitials,
} from '@/components/admin/admin-utils';
import { AdminUserListDto, adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

function normalizeRole(user: AdminUserListDto): 'Admin' | 'Assistant' | 'Student' {
  if (user.roles.includes('Admin')) return 'Admin';
  if (user.roles.includes('Student')) return 'Student';
  return 'Assistant';
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

export default function AdminAssistantsPage() {
  const [users, setUsers] = useState<AdminUserListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [search, setSearch] = useState('');
  const [confirmUser, setConfirmUser] = useState<AdminUserListDto | null>(null);
  const [showAddUser, setShowAddUser] = useState(false);
  const [selectedAssistant, setSelectedAssistant] = useState<AdminUserListDto | null>(null);
  const [profileUser, setProfileUser] = useState<AdminUserListDto | null>(null);
  const [exporting, setExporting] = useState(false);

  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true);
      setLoadError(false);
      const data = await adminService.listUsers(1, 1000, search);
      setUsers(data.items);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [search]);

  useEffect(() => {
    let isMounted = true;
    fetchUsers().finally(() => {
      if (!isMounted) return;
    });
    return () => {
      isMounted = false;
    };
  }, [fetchUsers]);

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
        nextStatus === 'Active' ? 'تم تنشيط المساعد' : 'تم تعليق المساعد'
      );
    } catch (error) {
      devConsole.error(error);
      toast.error('حدث خطأ أثناء تحديث حالة المساعد، أعد المحاولة.');
    }
  }

  const handleExport = async () => {
    if (exporting) return;

    setExporting(true);
    const toastId = toast.loading('جاري تصدير بيانات المساعدين...');

    try {
      const data = await adminService.listUsers(1, 100000, search);
      const itemsToExport = data.items.filter(
        (user) => normalizeRole(user) === 'Assistant'
      );

      if (!itemsToExport || itemsToExport.length === 0) {
        toast.error('لا توجد بيانات لتصديرها', { id: toastId });
        return;
      }

      const headers = [
        'الاسم الكامل',
        'رقم الهاتف',
        'الصلاحيات / الأدوار',
        'الحالة',
        'تاريخ الانضمام',
      ];

      const csvRows = [headers.join(',')];

      for (const u of itemsToExport) {
        const rowData = [
          u.fullName,
          u.phoneNumber,
          u.roles.join(' | '),
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
        `قائمة_المساعدين_${new Date().toISOString().split('T')[0]}.csv`
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

  const filteredAssistants = users.filter((user) => normalizeRole(user) === 'Assistant');
  const activeCount = filteredAssistants.filter((user) => user.status === 'Active').length;
  const pendingCount = filteredAssistants.filter((user) => user.status !== 'Active').length;

  const columns: AdminColumn<AdminUserListDto>[] = [
    {
      key: 'assistant',
      label: 'المساعد',
      render: (u) => (
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-primary-15)] font-bold text-[var(--admin-primary)] shadow-sm">
            {getInitials(u.fullName)}
          </div>
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
      key: 'roles',
      label: 'الأدوار والصلاحيات',
      render: (u) => (
        <span className="text-sm font-bold text-[var(--admin-text)]">
          {u.roles.filter(r => r !== 'Assistant').join(', ') || 'مساعد تعليمي عام'}
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
      label: 'آخر نشاط',
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
              setProfileUser(u);
            }}
            intent="primary"
            size="icon"
            title="إعدادات الملف الوظيفي"
          >
            <Briefcase className="h-5 w-5" />
          </NeumorphButton>
          <NeumorphButton
            type="button"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              setConfirmUser(u);
            }}
            intent={u.status === 'Active' ? 'danger' : 'primary'}
            size="icon"
            title={u.status === 'Active' ? 'تعليق المساعد' : 'تنشيط المساعد'}
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
    <AdminShellChrome
      activePath="/admin/assistants"
      sectionLabel="المساعدين"
      pageTitle="إدارة المساعدين"
      subtitle="إدارة الحسابات الوظيفية المخصصة للمساعدين، تعيين الأدوار والصلاحيات، ومراقبة الحالات."
      action={
        <NeumorphButton
          intent="primary"
          size="lg"
          pill
          onClick={() => setShowAddUser(true)}
        >
          <UserPlus className="h-4 w-4" />
          إضافة مساعد جديد
        </NeumorphButton>
      }
    >
      <AddUserDrawer
        open={showAddUser}
        onClose={() => setShowAddUser(false)}
        onSuccess={() => fetchUsers()}
        defaultRole="Assistant"
      />
      <EmployeeProfileDrawer
        open={!!profileUser}
        onClose={() => setProfileUser(null)}
        userId={profileUser?.id || ''}
        userName={profileUser?.fullName || ''}
        onSuccess={() => fetchUsers()}
      />
      <ConfirmDialog
        open={!!confirmUser}
        title={
          confirmUser?.status === 'Active'
            ? 'تعليق حساب مساعد؟'
            : 'تنشيط حساب مساعد؟'
        }
        description={`هل أنت متأكد من تغيير حالة حساب المساعد "${confirmUser?.fullName}"؟`}
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

      {loadError ? (
        <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-16 text-center gap-6 bg-[var(--admin-card-soft)]">
          <RefreshCw className="h-10 w-10 text-[var(--admin-muted)]" />
          <h4 className="text-xl font-black text-[var(--admin-text)]">تعذّر تحميل قائمة المساعدين</h4>
          <NeumorphButton onClick={() => fetchUsers()} intent="primary" size="lg" pill>
            إعادة المحاولة
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
              label="إجمالي المساعدين"
              value={filteredAssistants.length}
              subtitle="إجمالي المساعدين المسجلين"
            />
            <AdminStatCard
              variant="accent"
              icon={Sparkles}
              label="النشطون"
              value={activeCount}
              subtitle="حسابات نشطة ومفعلة"
            />
            <AdminStatCard
              variant="muted"
              icon={Shield}
              label="المعلقون"
              value={pendingCount}
              subtitle="حسابات معلقة أو غير مفعلة"
            />
          </section>

          <AdminSearchToolbar
            value={search}
            onChange={setSearch}
            placeholder="البحث بالاسم أو رقم الهاتف..."
            actions={
              <button
                onClick={handleExport}
                disabled={exporting}
                className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-bg)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-50"
              >
                <Download className={`h-4 w-4 ${exporting ? 'animate-spin' : ''}`} />
                {exporting ? 'جاري التصدير...' : 'تصدير البيانات'}
              </button>
            }
          />

          <AdminDataTable
            data={filteredAssistants}
            columns={columns}
            loading={loading}
            rowKey={(u) => u.id}
            emptyMessage="لا توجد نتائج مطابقة لفلترة المساعدين."
            onRowClick={(u) => {
              setSelectedAssistant(u);
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
