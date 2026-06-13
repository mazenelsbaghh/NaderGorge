'use client';

import { devConsole } from '@/utils/dev-console';
import { useCallback, useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard, AdminModal, AdminDataTable, AdminPageSkeleton } from '@/components/admin';
import { adminService, type AdminUserListDto, type UserAuditLogDto } from '@/services/admin-service';
import {
  hrService,
  type EmployeeDto,
  type AdminAttendanceLogDto,
  type AdminVacationDto,
} from '@/services/hr-service';
import {
  Users,
  CheckCircle2,
  Clock,
  FileCheck,
  Shield,
  PenLine,
  KeyRound,
  Power,
  Briefcase,
  CalendarClock,
  FileText,
  ListTodo,
  BookOpenCheck,
  ShieldAlert,
  Building2,
  DollarSign,
  CheckCircle,
  XCircle,
  Loader2,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { formatRelativeDate } from '@/components/admin/admin-utils';

// ── Types for new endpoints (wrapped in try/catch) ──────────────────────
interface AssistantStatsDto {
  completedTasksCount: number;
  pendingTasksCount: number;
  homeworkReviewsCount: number;
  warningsResolvedCount: number;
}

interface AssistantTaskDto {
  id: string;
  taskType: string;
  studentName: string;
  status: string;
  createdAt: string;
  completedAt?: string | null;
}

interface HomeworkReviewDto {
  id: string;
  studentName: string;
  lessonTitle: string;
  overallScore: number;
  assistantNotes?: string;
  status: string;
  gradedAt?: string | null;
}

interface WarningResolvedDto {
  id: string;
  studentName: string;
  warningType: string;
  resolutionNotes?: string;
  createdAt: string;
  resolvedAt?: string | null;
}

type TabKey = 'overview' | 'tasks' | 'grading' | 'warnings' | 'hr' | 'audit';
type ModalKey = 'none' | 'editProfile' | 'password' | 'status' | 'hrSettings';

function translateAction(action: string): string {
  const map: Record<string, string> = {
    AdjustBalance: 'تعديل رصيد الطالب',
    OverrideVideoLimit: 'تجاوز حد مشاهدة الفيديو',
    ToggleStudentSystemAccess: 'تعديل صلاحية وصول الطالب',
    TOGGLE_ACCESS: 'تعديل صلاحية وصول الطالب',
    ResetWatchLimit: 'إعادة تعيين حد مشاهدة الفيديو',
    AdjustGamificationPoints: 'تعديل نقاط الطالب',
    AdjustGamification: 'تعديل نقاط الطالب',
    ApproveWatchRequest: 'الموافقة على طلب مشاهدة إضافية',
    AddStudentNote: 'إضافة ملاحظة للطالب',
    DeleteStudentNote: 'حذف ملاحظة الطالب',
    UpdateStudentProfile: 'تحديث الملف الشخصي للطالب',
    DisconnectStudentDevice: 'فصل جهاز الطالب',
    DisconnectDevice: 'فصل جهاز الطالب',
    DisconnectAllDevices: 'فصل جميع الأجهزة للطالب',
    RemoveDevice: 'حذف جهاز مسجل',
    CreateUser: 'إنشاء مستخدم جديد',
    UpdateUserStatus: 'تحديث حالة المستخدم',
    UpdateUserRoles: 'تحديث أدوار المستخدم',
    SetWatchCount: 'تعديل عدد المشاهدات للفيديو',
    RejectWatchRequest: 'رفض طلب مشاهدة إضافية',
    CreateEmployeeProfile: 'إنشاء ملف موظف',
    UpdateEmployeeProfile: 'تحديث ملف الموظف',
  };
  return map[action] || action;
}

function translateTaskType(t: string): string {
  const map: Record<string, string> = {
    GradeEssay: 'تصحيح إجابة',
    FollowUpAtRisk: 'متابعة طالب معرض',
    ResolvePaymentIssue: 'حل مشكلة مالية',
  };
  return map[t] || t;
}

function translateTaskStatus(s: string): string {
  const map: Record<string, string> = {
    Open: 'مفتوحة',
    InReview: 'قيد المراجعة',
    Done: 'مكتملة',
  };
  return map[s] || s;
}

function taskStatusBadge(status: string) {
  const styles: Record<string, string> = {
    Open: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400',
    InReview: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400',
    Done: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400',
  };
  return styles[status] || 'bg-zinc-100 text-zinc-700 dark:bg-zinc-950/40 dark:text-zinc-400';
}

const renderChangedValues = (oldVal?: string, newVal?: string) => {
  try {
    if (!oldVal && !newVal) return null;
    const oldObj = oldVal ? JSON.parse(oldVal) : null;
    const newObj = newVal ? JSON.parse(newVal) : null;

    const keys = Array.from(new Set([...Object.keys(oldObj || {}), ...Object.keys(newObj || {})]));
    const changes = keys.filter(key => {
      const o = oldObj ? oldObj[key] : undefined;
      const n = newObj ? newObj[key] : undefined;
      return o !== n;
    });

    if (changes.length === 0) return null;

    return (
      <div className="mt-2 space-y-1 text-xs text-[var(--admin-muted)] bg-[var(--admin-hover)]/30 px-3 py-2 rounded-xl border border-[var(--admin-border)]/5 outline-none">
        {changes.map(key => {
          const o = oldObj ? oldObj[key] : undefined;
          const n = newObj ? newObj[key] : undefined;
          return (
            <div key={key} className="flex flex-wrap items-center gap-1.5">
              <span className="font-bold text-[var(--admin-text)]">{key}:</span>
              {o !== undefined && <span className="line-through text-red-500/80">{String(o)}</span>}
              {o !== undefined && <span className="opacity-40">←</span>}
              {n !== undefined && <span className="text-emerald-500 font-bold">{String(n)}</span>}
            </div>
          );
        })}
      </div>
    );
  } catch {
    const d = newVal || oldVal;
    if (!d) return null;
    return (
      <div className="mt-2 text-xs text-[var(--admin-muted)] bg-[var(--admin-hover)]/30 px-3 py-2 rounded-xl border border-[var(--admin-border)]/5">
        {d}
      </div>
    );
  }
};


export default function AssistantProfilePageClient() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState<ModalKey>('none');

  // ── Data states ──
  const [assistant, setAssistant] = useState<AdminUserListDto | null>(null);
  const [employeeProfile, setEmployeeProfile] = useState<EmployeeDto | null>(null);
  const [attendance, setAttendance] = useState<AdminAttendanceLogDto[]>([]);
  const [vacations, setVacations] = useState<AdminVacationDto[]>([]);
  const [auditLogs, setAuditLogs] = useState<UserAuditLogDto[]>([]);

  // New endpoint data (graceful fallback)
  const [stats, setStats] = useState<AssistantStatsDto>({ completedTasksCount: 0, pendingTasksCount: 0, homeworkReviewsCount: 0, warningsResolvedCount: 0 });
  const [tasks, setTasks] = useState<AssistantTaskDto[]>([]);
  const [homeworkReviews, setHomeworkReviews] = useState<HomeworkReviewDto[]>([]);
  const [warningsResolved, setWarningsResolved] = useState<WarningResolvedDto[]>([]);

  // ── Form states ──
  const [editFields, setEditFields] = useState<Record<string, string>>({});
  const [passwordInput, setPasswordInput] = useState('');
  const [hrFields, setHrFields] = useState({ salary: '0', startTime: '09:00:00', dailyHours: '8' });
  const [taskFilter, setTaskFilter] = useState<string>('all');

  const TABS: AdminTab<TabKey>[] = [
    { key: 'overview', label: 'نظرة عامة' },
    { key: 'tasks', label: 'المهام' },
    { key: 'grading', label: 'التصحيح' },
    { key: 'warnings', label: 'التحذيرات' },
    { key: 'hr', label: 'الموارد البشرية' },
    { key: 'audit', label: 'سجل النشاط' },
  ];

  // ── Data fetching ──────────────────────────────────────────────────────
  const fetchAssistant = useCallback(async () => {
    try {
      setLoading(true);
      // 1. Load assistant from users list
      const usersData = await adminService.listUsers(1, 1000, '');
      const user = usersData.items.find((u) => u.id === id);
      if (!user) {
        toast.error('المساعد غير موجود');
        router.push('/admin/assistants');
        return;
      }
      setAssistant(user);

      // 2. Load employee profile
      const employees = await hrService.listEmployees(user.phoneNumber);
      const emp = employees.find((e) => e.userId === id);
      setEmployeeProfile(emp || null);
      if (emp?.employeeProfile) {
        setHrFields({
          salary: emp.employeeProfile.basicSalary.toString(),
          startTime: emp.employeeProfile.standardStartTime || '09:00:00',
          dailyHours: emp.employeeProfile.targetDailyHours.toString(),
        });
      }

      // 3. Load attendance
      const attData = await hrService.getAttendance(user.phoneNumber);
      setAttendance(attData.filter((a) => a.employeePhone === user.phoneNumber));

      // 4. Load vacations
      const vacData = await hrService.getVacations(user.phoneNumber);
      setVacations(vacData.filter((v) => v.employeePhone === user.phoneNumber));

      // 5. Load audit logs
      const logs = await adminService.getUserAuditLogs(id);
      setAuditLogs(logs || []);

      // 6. New endpoints — try/catch for each so they fail gracefully
      try {
        const res = await adminService.getAssistantStats(id);
        if (res) setStats(res);
      } catch { /* endpoint doesn't exist yet */ }

      try {
        const res = await adminService.getAssistantTasks(id);
        if (res) setTasks(res);
      } catch { /* endpoint doesn't exist yet */ }

      try {
        const res = await adminService.getAssistantHomeworkReviews(id);
        if (res) setHomeworkReviews(res);
      } catch { /* endpoint doesn't exist yet */ }

      try {
        const res = await adminService.getAssistantWarnings(id);
        if (res) setWarningsResolved(res);
      } catch { /* endpoint doesn't exist yet */ }

    } catch (err) {
      devConsole.error('Failed to load assistant', err);
      toast.error('فشل في تحميل بيانات الملف التعريفي');
    } finally {
      setLoading(false);
    }
  }, [id, router]);

  useEffect(() => {
    fetchAssistant();
  }, [fetchAssistant]);

  // ── Handlers ───────────────────────────────────────────────────────────
  const handleStatusToggle = async () => {
    if (!assistant || submitting) return;
    const nextStatus = assistant.status === 'Active' ? 'Disabled' : 'Active';
    setSubmitting(true);
    try {
      await adminService.updateUserStatus(assistant.id, nextStatus);
      setAssistant({ ...assistant, status: nextStatus });
      setModalOpen('none');
      toast.success(nextStatus === 'Active' ? 'تم تفعيل الحساب بنجاح' : 'تم إيقاف الحساب بنجاح');
    } catch {
      toast.error('فشل تغيير الحالة');
    } finally {
      setSubmitting(false);
    }
  };

  const handleEditProfileSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting || !assistant) return;
    setSubmitting(true);
    try {
      // Use the same update endpoint — we update the user profile
      await adminService.updateStudentProfile(id, editFields);
      toast.success('تم تحديث البيانات');
      setModalOpen('none');
      fetchAssistant();
    } catch {
      toast.error('فشل التحديث');
    } finally {
      setSubmitting(false);
    }
  };

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (passwordInput.length < 4) {
      toast.error('كلمة المرور يجب أن تكون 4 أحرف على الأقل');
      return;
    }
    if (submitting) return;
    setSubmitting(true);
    try {
      await adminService.adminResetPassword(id, passwordInput);
      toast.success('تم تغيير كلمة المرور');
      setModalOpen('none');
      setPasswordInput('');
    } catch {
      toast.error('فشل تغيير كلمة المرور');
    } finally {
      setSubmitting(false);
    }
  };

  const handleHrSettingsSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assistant || submitting) return;
    setSubmitting(true);
    try {
      const res = await hrService.saveEmployeeProfile({
        userId: assistant.id,
        basicSalary: Number(hrFields.salary) || 0,
        standardStartTime: hrFields.startTime,
        targetDailyHours: Number(hrFields.dailyHours) || 8,
      });
      if (res.success) {
        toast.success('تم حفظ إعدادات الموظف بنجاح');
        setModalOpen('none');
        // Reload employee profile
        const employees = await hrService.listEmployees(assistant.phoneNumber);
        const emp = employees.find((e) => e.userId === id);
        setEmployeeProfile(emp || null);
      } else {
        toast.error(res.message || 'فشل في حفظ الإعدادات');
      }
    } catch {
      toast.error('حدث خطأ أثناء حفظ الإعدادات');
    } finally {
      setSubmitting(false);
    }
  };

  const handleApproveVacation = async (vacId: string) => {
    try {
      const res = await hrService.approveVacation(vacId);
      if (res.success) {
        toast.success('تمت الموافقة على الإجازة');
        if (assistant) {
          const vacData = await hrService.getVacations(assistant.phoneNumber);
          setVacations(vacData.filter((v) => v.employeePhone === assistant.phoneNumber));
        }
      }
    } catch {
      toast.error('فشل في الموافقة على الإجازة');
    }
  };

  const handleRejectVacation = async (vacId: string) => {
    try {
      const res = await hrService.rejectVacation(vacId);
      if (res.success) {
        toast.success('تم رفض الإجازة');
        if (assistant) {
          const vacData = await hrService.getVacations(assistant.phoneNumber);
          setVacations(vacData.filter((v) => v.employeePhone === assistant.phoneNumber));
        }
      }
    } catch {
      toast.error('فشل في رفض الإجازة');
    }
  };

  // ── Helpers ────────────────────────────────────────────────────────────
  const formatDate = (d?: string | null) => {
    if (!d) return 'غير متوفر';
    return new Date(d).toLocaleDateString('en-GB');
  };

  const mapRole = (roles: string[]) => {
    const filtered = roles.filter(r => r !== 'Assistant');
    if (filtered.length === 0) return 'مساعد تعليمي عام';
    const map: Record<string, string> = {
      Admin: 'مدير النظام',
      Teacher: 'مدرس',
      Moderator: 'مشرف',
    };
    return filtered.map(r => map[r] || r).join('، ');
  };

  const filteredTasks = taskFilter === 'all'
    ? tasks
    : tasks.filter(t => t.status === taskFilter);

  const taskStats = {
    completed: tasks.filter(t => t.status === 'Done').length,
    pending: tasks.filter(t => t.status === 'Open').length,
    inReview: tasks.filter(t => t.status === 'InReview').length,
  };

  const gradingStats = {
    totalReviewed: homeworkReviews.length,
    averageScore: homeworkReviews.length > 0
      ? Math.round(homeworkReviews.reduce((sum, r) => sum + (r.overallScore || 0), 0) / homeworkReviews.length)
      : 0,
  };

  if (loading) {
    return (
      <AdminShellChrome
        activePath="/admin/assistants"
        sectionLabel="المساعدين"
        pageTitle="الملف التعريفي للمساعد"
        subtitle="جاري تحميل التفاصيل..."
      >
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!assistant) return null;

  return (
    <AdminShellChrome
      activePath="/admin/assistants"
      sectionLabel="المساعدين"
      pageTitle="ملف المساعد الشامل"
      subtitle="تفاصيل شاملة للمهام، التصحيح، والموارد البشرية"
      action={
        <div className="flex gap-4">
          <button
            onClick={() => {
              setEditFields({
                fullName: assistant.fullName || '',
                phone: assistant.phoneNumber || '',
              });
              setModalOpen('editProfile');
            }}
            className="flex items-center gap-2 rounded-2xl bg-[var(--admin-primary-15)] px-4 py-2 font-medium text-[var(--admin-primary)] hover:bg-[var(--admin-primary)] hover:text-white transition-colors"
          >
            <PenLine size={20} />
            تعديل البيانات
          </button>
          <button
            onClick={() => { setPasswordInput(''); setModalOpen('password'); }}
            className="flex items-center gap-2 rounded-2xl bg-amber-500/10 px-4 py-2 font-medium text-amber-600 hover:bg-amber-500/20 transition-colors"
          >
            <KeyRound size={20} />
            تغيير الباسورد
          </button>
          <button
            onClick={() => {
              if (assistant.status === 'Active') {
                setModalOpen('status');
              } else {
                void handleStatusToggle();
              }
            }}
            className={`flex items-center gap-2 rounded-2xl px-4 py-2 font-medium transition-colors 
               ${assistant.status === 'Active' ? 'bg-red-500/10 text-red-500 hover:bg-red-500/20' : 'bg-green-500/10 text-green-500 hover:bg-green-500/20'}`}
          >
            <Power size={20} />
            {assistant.status === 'Active' ? 'إيقاف الحساب' : 'تفعيل الحساب'}
          </button>
          <button
            onClick={() => router.push('/admin/assistants')}
            className="flex items-center gap-2 rounded-2xl bg-[var(--admin-surface-low)] px-4 py-2 text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-border)]"
          >
            <Users size={20} />
            العودة للقائمة
          </button>
        </div>
      }
    >
      <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />

      <div className="mt-8 animate-in fade-in slide-in-from-bottom-2 duration-300">

        {/* ═══════════════════ TAB 1: OVERVIEW ═══════════════════ */}
        {activeTab === 'overview' && (
          <div className="flex flex-col gap-8">
            {/* Stat Cards */}
            <div>
              <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-4">ملخص الإحصاءات</h3>
              <div className="grid grid-cols-1 gap-6 md:grid-cols-4">
                <AdminStatCard variant="accent" icon={CheckCircle2} label="المهام المكتملة" value={stats.completedTasksCount} />
                <AdminStatCard variant="light" icon={Clock} label="المهام المعلقة" value={stats.pendingTasksCount} />
                <AdminStatCard variant="muted" icon={FileCheck} label="الواجبات المصححة" value={stats.homeworkReviewsCount} />
                <AdminStatCard variant="light" icon={Shield} label="التحذيرات المحلولة" value={stats.warningsResolvedCount} />
              </div>
            </div>

            {/* البيانات الشخصية */}
            <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
              <div className="flex items-center gap-3 mb-5">
                <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                  <Users size={20} />
                </div>
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                  البيانات الشخصية
                </h3>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                <div>
                  <p className="text-[var(--admin-muted)] text-sm mb-1">الاسم بالكامل</p>
                  <p className="text-[var(--admin-text)] font-semibold">{assistant.fullName || 'غير متوفر'}</p>
                </div>
                <div>
                  <p className="text-[var(--admin-muted)] text-sm mb-1">رقم الهاتف</p>
                  <p className="text-[var(--admin-text)] font-semibold font-mono">{assistant.phoneNumber || 'غير متوفر'}</p>
                </div>
                <div>
                  <p className="text-[var(--admin-muted)] text-sm mb-1">الدور الوظيفي</p>
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
                    <Shield className="h-3.5 w-3.5" />
                    {mapRole(assistant.roles)}
                  </span>
                </div>
                <div>
                  <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ الانضمام</p>
                  <p className="text-[var(--admin-text)] font-semibold">{formatDate(assistant.createdAt)}</p>
                </div>
                <div>
                  <p className="text-[var(--admin-muted)] text-sm mb-1">حالة الحساب</p>
                  <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${
                    assistant.status === 'Active'
                      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
                      : 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400'
                  }`}>
                    <span className="h-1.5 w-1.5 rounded-full bg-current" />
                    {assistant.status === 'Active' ? 'نشط' : 'معطل'}
                  </span>
                </div>
              </div>
            </div>

            {/* إعدادات الوظيفة (display only) */}
            <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-3">
                  <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                    <Briefcase size={20} />
                  </div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                    إعدادات الوظيفة
                  </h3>
                </div>
                <button
                  onClick={() => setModalOpen('hrSettings')}
                  className="text-sm font-bold text-[var(--admin-primary)] bg-[var(--admin-card-strong)] px-4 py-2 flex items-center gap-2 rounded-xl hover:bg-[var(--admin-hover)] transition-colors"
                >
                  <PenLine size={14} />
                  تعديل الإعدادات
                </button>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-sm text-[var(--admin-muted)] font-bold">الراتب الأساسي</span>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.basicSalary ?? 0} ج.م
                  </span>
                </div>
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-sm text-[var(--admin-muted)] font-bold">موعد الحضور الرسمي</span>
                  <span className="text-sm font-bold text-[var(--admin-text)] font-mono">
                    {employeeProfile?.employeeProfile?.standardStartTime ?? '09:00:00'}
                  </span>
                </div>
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-sm text-[var(--admin-muted)] font-bold">ساعات العمل اليومية</span>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.targetDailyHours ?? 8} ساعات
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}


        {/* ═══════════════════ TAB 2: TASKS ═══════════════════ */}
        {activeTab === 'tasks' && (
          <div className="flex flex-col gap-6">
            {/* Task stats */}
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              <AdminStatCard variant="accent" icon={CheckCircle2} label="مهام مكتملة" value={taskStats.completed} />
              <AdminStatCard variant="light" icon={Clock} label="مهام معلقة" value={taskStats.pending} />
              <AdminStatCard variant="muted" icon={ListTodo} label="قيد المراجعة" value={taskStats.inReview} />
            </div>

            {/* Filter pills */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="flex items-center justify-between mb-5">
                <div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">قائمة المهام</h3>
                  <p className="text-[var(--admin-muted)]">مهام المساعد وحالاتها مع تفاصيل الطالب المرتبط</p>
                </div>
              </div>

              <div className="flex flex-wrap gap-2 mb-5">
                {[
                  { key: 'all', label: 'الكل' },
                  { key: 'Open', label: 'مفتوحة' },
                  { key: 'InReview', label: 'قيد المراجعة' },
                  { key: 'Done', label: 'مكتملة' },
                ].map(f => (
                  <button
                    key={f.key}
                    onClick={() => setTaskFilter(f.key)}
                    className={`rounded-full px-4 py-1.5 text-xs font-bold transition-colors ${
                      taskFilter === f.key
                        ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                        : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                  >
                    {f.label}
                  </button>
                ))}
              </div>

              <AdminDataTable<AssistantTaskDto>
                columns={[
                  { key: 'taskType', label: 'نوع المهمة', render: (row) => (
                    <span className="font-bold text-[var(--admin-text)]">{translateTaskType(row.taskType)}</span>
                  )},
                  { key: 'studentName', label: 'اسم الطالب', render: (row) => (
                    <span className="text-[var(--admin-text)]">{row.studentName || '—'}</span>
                  )},
                  { key: 'status', label: 'الحالة', render: (row) => (
                    <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${taskStatusBadge(row.status)}`}>
                      <span className="h-1.5 w-1.5 rounded-full bg-current" />
                      {translateTaskStatus(row.status)}
                    </span>
                  )},
                  { key: 'createdAt', label: 'تاريخ الإنشاء', render: (row) => row.createdAt ? new Date(row.createdAt).toLocaleDateString('en-GB') : '—' },
                  { key: 'completedAt', label: 'تاريخ الاكتمال', render: (row) => row.completedAt ? new Date(row.completedAt).toLocaleDateString('en-GB') : '—' },
                ]}
                data={filteredTasks}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد مهام مسجلة لهذا المساعد حالياً"
              />
            </div>
          </div>
        )}


        {/* ═══════════════════ TAB 3: GRADING ═══════════════════ */}
        {activeTab === 'grading' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              <AdminStatCard variant="accent" icon={BookOpenCheck} label="إجمالي التصحيحات" value={gradingStats.totalReviewed} />
              <AdminStatCard variant="light" icon={FileCheck} label="متوسط الدرجات" value={gradingStats.averageScore > 0 ? `${gradingStats.averageScore}%` : '—'} />
            </div>

            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل تصحيح الواجبات</h3>
                <p className="text-[var(--admin-muted)]">الواجبات التي قام المساعد بتصحيحها مع الدرجات والملاحظات</p>
              </div>

              <AdminDataTable<HomeworkReviewDto>
                columns={[
                  { key: 'studentName', label: 'اسم الطالب', render: (row) => (
                    <span className="font-bold text-[var(--admin-text)]">{row.studentName}</span>
                  )},
                  { key: 'lessonTitle', label: 'عنوان الحصة', render: (row) => (
                    <span className="text-[var(--admin-text)]">{row.lessonTitle}</span>
                  )},
                  { key: 'overallScore', label: 'الدرجة', render: (row) => (
                    <span className={`font-mono font-bold ${row.overallScore >= 70 ? 'text-emerald-600 dark:text-emerald-400' : row.overallScore >= 50 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}>
                      {row.overallScore}%
                    </span>
                  )},
                  { key: 'assistantNotes', label: 'ملاحظات المساعد', render: (row) => (
                    <span className="text-sm text-[var(--admin-text)]">{row.assistantNotes || '—'}</span>
                  )},
                  { key: 'status', label: 'الحالة', render: (row) => (
                    <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${
                      row.status === 'Graded' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' :
                      'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400'
                    }`}>
                      <span className="h-1.5 w-1.5 rounded-full bg-current" />
                      {row.status === 'Graded' ? 'مصحح' : row.status}
                    </span>
                  )},
                  { key: 'gradedAt', label: 'تاريخ التصحيح', render: (row) => row.gradedAt ? new Date(row.gradedAt).toLocaleDateString('en-GB') : '—' },
                ]}
                data={homeworkReviews}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد تصحيحات مسجلة لهذا المساعد حالياً"
              />
            </div>
          </div>
        )}


        {/* ═══════════════════ TAB 4: WARNINGS ═══════════════════ */}
        {activeTab === 'warnings' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              <AdminStatCard variant="accent" icon={ShieldAlert} label="إجمالي التحذيرات المحلولة" value={warningsResolved.length} />
              <AdminStatCard variant="muted" icon={Shield} label="آخر حل" value={warningsResolved.length > 0 && warningsResolved[0].resolvedAt ? formatRelativeDate(warningsResolved[0].resolvedAt) : '—'} />
            </div>

            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل التحذيرات المحلولة</h3>
                <p className="text-[var(--admin-muted)]">التحذيرات التي تمت معالجتها بواسطة هذا المساعد</p>
              </div>

              <AdminDataTable<WarningResolvedDto>
                columns={[
                  { key: 'studentName', label: 'اسم الطالب', render: (row) => (
                    <span className="font-bold text-[var(--admin-text)]">{row.studentName}</span>
                  )},
                  { key: 'warningType', label: 'نوع التحذير', render: (row) => (
                    <span className="text-[var(--admin-text)]">{row.warningType}</span>
                  )},
                  { key: 'resolutionNotes', label: 'ملاحظات الحل', render: (row) => (
                    <span className="text-sm text-[var(--admin-text)]">{row.resolutionNotes || '—'}</span>
                  )},
                  { key: 'createdAt', label: 'تاريخ الإنشاء', render: (row) => row.createdAt ? new Date(row.createdAt).toLocaleDateString('en-GB') : '—' },
                  { key: 'resolvedAt', label: 'تاريخ الحل', render: (row) => row.resolvedAt ? new Date(row.resolvedAt).toLocaleDateString('en-GB') : '—' },
                ]}
                data={warningsResolved}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد تحذيرات محلولة لهذا المساعد حالياً"
              />
            </div>
          </div>
        )}


        {/* ═══════════════════ TAB 5: HR ═══════════════════ */}
        {activeTab === 'hr' && (
          <div className="flex flex-col gap-6">
            {/* Job Settings Card */}
            <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-3">
                  <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                    <Building2 size={20} />
                  </div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                    إعدادات الوظيفة والراتب
                  </h3>
                </div>
                <button
                  onClick={() => setModalOpen('hrSettings')}
                  className="flex items-center gap-2 rounded-xl bg-[var(--admin-primary-15)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-primary)] hover:text-white transition-all duration-300 shadow-sm"
                >
                  <PenLine size={14} />
                  تعديل الإعدادات الوظيفية
                </button>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <div className="flex items-center gap-2">
                    <DollarSign size={16} className="text-[var(--admin-muted)]" />
                    <span className="text-sm text-[var(--admin-muted)] font-bold">الراتب الأساسي</span>
                  </div>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.basicSalary ?? 0} ج.م
                  </span>
                </div>
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <div className="flex items-center gap-2">
                    <Clock size={16} className="text-[var(--admin-muted)]" />
                    <span className="text-sm text-[var(--admin-muted)] font-bold">موعد الحضور</span>
                  </div>
                  <span className="text-sm font-bold text-[var(--admin-text)] font-mono">
                    {employeeProfile?.employeeProfile?.standardStartTime ?? '09:00:00'}
                  </span>
                </div>
                <div className="flex items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)]">
                  <div className="flex items-center gap-2">
                    <CalendarClock size={16} className="text-[var(--admin-muted)]" />
                    <span className="text-sm text-[var(--admin-muted)] font-bold">ساعات العمل اليومية</span>
                  </div>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.targetDailyHours ?? 8} ساعات
                  </span>
                </div>
              </div>
            </div>

            {/* Attendance Table */}
            <div className="rounded-3xl bg-[var(--admin-bg)] p-6 shadow-sm">
              <div className="flex items-center gap-3 mb-5">
                <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                  <CalendarClock size={20} />
                </div>
                <div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">سجل الحضور والانصراف</h3>
                  <p className="text-[var(--admin-muted)] text-sm">سجل الحضور الأخير مع ساعات العمل والتأخير</p>
                </div>
              </div>

              <AdminDataTable<AdminAttendanceLogDto>
                columns={[
                  { key: 'date', label: 'التاريخ', render: (a) => (
                    <span className="font-bold text-[var(--admin-text)]">
                      {new Date(a.date).toLocaleDateString('ar-EG', { dateStyle: 'medium' })}
                    </span>
                  )},
                  { key: 'clockIn', label: 'وقت الحضور', render: (a) => (
                    <span className="font-mono text-sm">
                      {new Date(a.clockIn).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' })}
                    </span>
                  )},
                  { key: 'clockOut', label: 'وقت الانصراف', render: (a) =>
                    a.clockOut ? (
                      <span className="font-mono text-sm">
                        {new Date(a.clockOut).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' })}
                      </span>
                    ) : (
                      <span className="text-xs font-bold text-amber-500 bg-amber-500/10 px-2 py-1 rounded-lg">
                        قيد العمل
                      </span>
                    )
                  },
                  { key: 'duration', label: 'ساعات العمل', render: (a) => {
                    if (!a.durationMinutes) return <span className="text-[var(--admin-muted)]">—</span>;
                    const hrs = Math.floor(a.durationMinutes / 60);
                    const mins = Math.round(a.durationMinutes % 60);
                    return <span className="font-bold text-sm">{hrs} ساعة و {mins} دقيقة</span>;
                  }},
                  { key: 'late', label: 'التأخير', render: (a) =>
                    a.lateMinutes > 0 ? (
                      <span className="text-xs font-bold text-red-600 bg-red-100 dark:bg-red-950/40 dark:text-red-400 px-2.5 py-1 rounded-full">
                        {a.lateMinutes} دقيقة
                      </span>
                    ) : (
                      <span className="text-xs font-bold text-emerald-600 bg-emerald-100 dark:bg-emerald-950/40 dark:text-emerald-400 px-2.5 py-1 rounded-full">
                        لا يوجد
                      </span>
                    )
                  },
                  { key: 'status', label: 'الحالة', render: (a) => (
                    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold ${
                      a.status === 'Present'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/45 dark:text-emerald-400'
                        : 'bg-amber-100 text-amber-700 dark:bg-amber-950/45 dark:text-amber-400'
                    }`}>
                      {a.status === 'Present' ? 'حاضر' : 'متأخر'}
                    </span>
                  )},
                ]}
                data={attendance}
                rowKey={(a) => a.id}
                emptyMessage="لا يوجد أي سجل حضور مسجل لهذا المساعد حالياً."
              />
            </div>

            {/* Vacations Section */}
            <div className="rounded-3xl bg-[var(--admin-bg)] p-6 shadow-sm">
              <div className="flex items-center gap-3 mb-5">
                <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                  <FileText size={20} />
                </div>
                <div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">طلبات الإجازات</h3>
                  <p className="text-[var(--admin-muted)] text-sm">إدارة طلبات الإجازة والموافقة أو الرفض</p>
                </div>
              </div>

              {vacations.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16 text-[var(--admin-muted)] bg-[var(--admin-card-soft)] rounded-3xl">
                  <span className="text-5xl mb-4">🏖️</span>
                  <p className="font-bold text-[var(--admin-text)]">لا توجد طلبات إجازة مقدمة</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {vacations.map((v) => (
                    <div
                      key={v.id}
                      className="flex flex-col md:flex-row md:items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)]/10 gap-3"
                    >
                      <div>
                        <p className="text-xs text-[var(--admin-muted)] font-bold">فترة الإجازة:</p>
                        <p className="text-sm font-bold text-[var(--admin-text)] mt-0.5">
                          من {new Date(v.startDate).toLocaleDateString('ar-EG')}{' '}
                          إلى {new Date(v.endDate).toLocaleDateString('ar-EG')}
                        </p>
                        <p className="text-xs text-[var(--admin-muted)] mt-1">السبب: {v.reason}</p>
                      </div>

                      <div className="flex items-center gap-2">
                        {v.status === 'Pending' ? (
                          <>
                            <button
                              onClick={() => handleApproveVacation(v.id)}
                              className="flex items-center gap-1 px-3 py-1.5 bg-emerald-500/10 hover:bg-emerald-500 hover:text-white text-emerald-600 rounded-xl text-xs font-bold transition-all duration-200"
                            >
                              <CheckCircle className="h-3.5 w-3.5" />
                              موافقة
                            </button>
                            <button
                              onClick={() => handleRejectVacation(v.id)}
                              className="flex items-center gap-1 px-3 py-1.5 bg-red-500/10 hover:bg-red-500 hover:text-white text-red-500 rounded-xl text-xs font-bold transition-all duration-200"
                            >
                              <XCircle className="h-3.5 w-3.5" />
                              رفض
                            </button>
                          </>
                        ) : (
                          <span className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-extrabold ${
                            v.status === 'Approved'
                              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
                              : 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400'
                          }`}>
                            {v.status === 'Approved' ? (
                              <CheckCircle className="h-3.5 w-3.5" />
                            ) : (
                              <XCircle className="h-3.5 w-3.5" />
                            )}
                            {v.status === 'Approved' ? 'مقبولة' : 'مرفوضة'}
                          </span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}


        {/* ═══════════════════ TAB 6: AUDIT ═══════════════════ */}
        {activeTab === 'audit' && (
          <div className="flex flex-col gap-6">
            <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div>
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل النشاط</h3>
                <p className="text-[var(--admin-muted)]">كافة الإجراءات والعمليات التي تمت بواسطة أو على حساب هذا المساعد</p>
              </div>
            </div>

            {auditLogs.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] bg-[var(--admin-card-soft)] rounded-3xl shadow-sm">
                <p className="text-sm font-bold">لا يوجد أي نشاطات مسجلة لهذا الحساب حالياً.</p>
              </div>
            ) : (
              <div className="bg-[var(--admin-bg)] p-8 rounded-3xl shadow-sm">
                <div className="relative border-r border-[var(--admin-border)]/60 mr-3 pr-6 space-y-6">
                  {auditLogs.map((log) => (
                    <div key={log.id} className="relative group">
                      {/* Timeline node */}
                      <div className="absolute right-[-31px] top-1.5 flex h-4.5 w-4.5 items-center justify-center rounded-full border-2 border-[var(--admin-bg)] bg-[var(--admin-primary)] ring-4 ring-[var(--admin-primary-15)]" />

                      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-1.5">
                        <div>
                          <p className="text-sm font-bold text-[var(--admin-text)]">
                            {translateAction(log.action)}
                          </p>
                          <p className="text-xs text-[var(--admin-muted)] mt-0.5">
                            الكيان: {log.entityType}
                            {log.ipAddress && ` | IP: ${log.ipAddress}`}
                          </p>
                        </div>
                        <span className="text-xs text-[var(--admin-muted)] font-medium shrink-0">
                          {formatRelativeDate(log.createdAt)}
                        </span>
                      </div>

                      {/* Changed values visualization */}
                      {renderChangedValues(log.oldValues, log.newValues)}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* ═══════════════════ MODALS ═══════════════════ */}

      {/* Edit Profile Modal */}
      <AdminModal open={modalOpen === 'editProfile'} onClose={() => !submitting && setModalOpen('none')} title="تعديل بيانات المساعد">
        <form onSubmit={handleEditProfileSubmit} className="flex flex-col gap-4">
          {[
            { key: 'fullName', label: 'الاسم الكامل', type: 'text' },
            { key: 'phone', label: 'رقم الهاتف', type: 'text' },
          ].map(f => (
            <div key={f.key}>
              <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">{f.label}</label>
              <input
                type={f.type}
                disabled={submitting}
                className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                value={String(editFields[f.key] ?? '')}
                onChange={e => setEditFields(p => ({ ...p, [f.key]: e.target.value }))}
              />
            </div>
          ))}
          <div className="flex gap-4 mt-4">
            <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
            <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
              {submitting ? 'جاري الحفظ...' : 'حفظ التعديلات'}
            </button>
          </div>
        </form>
      </AdminModal>

      {/* Password Modal */}
      <AdminModal open={modalOpen === 'password'} onClose={() => !submitting && setModalOpen('none')} title="تغيير كلمة المرور">
        <form onSubmit={handlePasswordSubmit} className="flex flex-col gap-4">
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">كلمة المرور الجديدة</label>
            <input
              type="text"
              required
              minLength={4}
              disabled={submitting}
              className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50 font-mono text-lg text-center"
              value={passwordInput}
              onChange={e => setPasswordInput(e.target.value)}
              placeholder="أدخل كلمة المرور الجديدة"
            />
          </div>
          <div className="flex gap-4 mt-4">
            <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
            <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-amber-500 text-white hover:bg-amber-600 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
              {submitting ? 'جاري الحفظ...' : 'تغيير كلمة المرور'}
            </button>
          </div>
        </form>
      </AdminModal>

      {/* Status Toggle Modal */}
      <AdminModal open={modalOpen === 'status'} onClose={() => !submitting && setModalOpen('none')} title="تأكيد إيقاف الحساب">
        <div className="flex flex-col gap-4">
          <p className="text-sm text-[var(--admin-muted)] text-center">
            هل أنت متأكد أنك تريد إيقاف حساب <strong className="text-[var(--admin-text)]">{assistant.fullName}</strong>؟
          </p>
          <div className="flex gap-4 mt-2">
            <button
              disabled={submitting}
              onClick={() => setModalOpen('none')}
              className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50"
            >
              إلغاء
            </button>
            <button
              disabled={submitting}
              onClick={handleStatusToggle}
              className="flex-1 px-4 py-3 rounded-xl font-bold bg-red-500 text-white hover:bg-red-600 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Power size={16} />}
              {submitting ? 'جاري الإيقاف...' : 'تأكيد الإيقاف'}
            </button>
          </div>
        </div>
      </AdminModal>

      {/* HR Settings Modal */}
      <AdminModal open={modalOpen === 'hrSettings'} onClose={() => !submitting && setModalOpen('none')} title="تعديل الإعدادات الوظيفية">
        <form onSubmit={handleHrSettingsSubmit} className="flex flex-col gap-4">
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">الراتب الأساسي (جنيه مصري)</label>
            <div className="relative">
              <input
                type="number"
                value={hrFields.salary}
                onChange={(e) => setHrFields(p => ({ ...p, salary: e.target.value }))}
                className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                placeholder="0"
                min={0}
                required
                disabled={submitting}
              />
              <DollarSign className="absolute left-3 top-3 h-4 w-4 text-[var(--admin-muted)]" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">موعد الحضور الرسمي</label>
            <input
              type="text"
              value={hrFields.startTime}
              onChange={(e) => setHrFields(p => ({ ...p, startTime: e.target.value }))}
              className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50 font-mono"
              placeholder="09:00:00"
              required
              disabled={submitting}
            />
          </div>
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">ساعات العمل اليومية المطلوبة</label>
            <input
              type="number"
              value={hrFields.dailyHours}
              onChange={(e) => setHrFields(p => ({ ...p, dailyHours: e.target.value }))}
              className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
              placeholder="8"
              min={1}
              max={24}
              required
              disabled={submitting}
            />
          </div>
          <div className="flex gap-4 mt-4">
            <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
            <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
              {submitting ? 'جاري الحفظ...' : 'حفظ التغييرات'}
            </button>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
