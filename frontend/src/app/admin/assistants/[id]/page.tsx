'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
  User,
  Shield,
  Calendar,
  DollarSign,
  Clock,
  CheckCircle,
  XCircle,
  Loader2,
  ArrowRight,
  Activity,
  CalendarClock,
  Briefcase,
  UserCheck,
  UserX,
  FileText,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminPageSkeleton,
  AdminDataTable,
  AdminColumn,
} from '@/components/admin';
import {
  adminService,
  AdminUserListDto,
  UserAuditLogDto,
} from '@/services/admin-service';
import {
  hrService,
  EmployeeDto,
  AdminAttendanceLogDto,
  AdminVacationDto,
} from '@/services/hr-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import { getInitials, formatRelativeDate } from '@/components/admin/admin-utils';
import toast from 'react-hot-toast';

function translateAction(action: string): string {
  const map: Record<string, string> = {
    AdjustBalance: 'تعديل رصيد الطالب',
    OverrideVideoLimit: 'تجاوز حد مشاهدة الفيديو',
    ToggleStudentSystemAccess: 'تعديل صلاحية وصول الطالب',
    ResetWatchLimit: 'إعادة تعيين حد مشاهدة الفيديو',
    AdjustGamificationPoints: 'تعديل نقاط الطالب',
    ApproveWatchRequest: 'الموافقة على طلب مشاهدة إضافية',
    AddStudentNote: 'إضافة ملاحظة للطالب',
    DeleteStudentNote: 'حذف ملاحظة الطالب',
    UpdateStudentProfile: 'تحديث الملف الشخصي لطالب',
    DisconnectStudentDevice: 'فصل جهاز الطالب',
    RemoveDevice: 'حذف جهاز مسجل',
    CreateUser: 'إنشاء مستخدم جديد',
    UpdateUserStatus: 'تحديث حالة المستخدم',
    UpdateUserRoles: 'تحديث أدوار المستخدم',
    CreateEmployeeProfile: 'إنشاء ملف موظف',
    UpdateEmployeeProfile: 'تحديث ملف الموظف',
  };
  return map[action] || action;
}

export default function AssistantProfilePage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [assistant, setAssistant] = useState<AdminUserListDto | null>(null);
  const [employeeProfile, setEmployeeProfile] = useState<EmployeeDto | null>(
    null
  );
  const [attendance, setAttendance] = useState<AdminAttendanceLogDto[]>([]);
  const [auditLogs, setAuditLogs] = useState<UserAuditLogDto[]>([]);
  const [vacations, setVacations] = useState<AdminVacationDto[]>([]);

  const [loading, setLoading] = useState(true);
  const [updatingStatus, setUpdatingStatus] = useState(false);
  const [savingSettings, setSavingSettings] = useState(false);

  // Profile Settings Form states
  const [salary, setSalary] = useState<string>('0');
  const [startTime, setStartTime] = useState<string>('09:00:00');
  const [dailyHours, setDailyHours] = useState<string>('8');
  const [isEditingSettings, setIsEditingSettings] = useState(false);

  const loadAllData = useCallback(async () => {
    try {
      setLoading(true);
      // 1. Load users list to find this assistant
      const usersData = await adminService.listUsers(1, 1000, '');
      const user = usersData.items.find((u) => u.id === id);

      if (!user) {
        toast.error('المساعد غير موجود');
        router.push('/admin/assistants');
        return;
      }
      setAssistant(user);

      // 2. Load employee profile settings
      const employees = await hrService.listEmployees(user.phoneNumber);
      const emp = employees.find((e) => e.userId === id);
      setEmployeeProfile(emp || null);

      if (emp && emp.employeeProfile) {
        setSalary(emp.employeeProfile.basicSalary.toString());
        setStartTime(emp.employeeProfile.standardStartTime || '09:00:00');
        setDailyHours(emp.employeeProfile.targetDailyHours.toString());
      }

      // 3. Load attendance records filtered by phone number
      const attData = await hrService.getAttendance(user.phoneNumber);
      setAttendance(attData.filter((a) => a.employeePhone === user.phoneNumber));

      // 4. Load vacations filtered by phone number
      const vacData = await hrService.getVacations(user.phoneNumber);
      setVacations(vacData.filter((v) => v.employeePhone === user.phoneNumber));

      // 5. Load audit logs for user
      const logs = await adminService.getUserAuditLogs(id);
      setAuditLogs(logs || []);
    } catch (err) {
      console.error(err);
      toast.error('فشل في تحميل بيانات الملف التعريفي');
    } finally {
      setLoading(false);
    }
  }, [id, router]);

  useEffect(() => {
    loadAllData();
  }, [loadAllData]);

  const handleToggleStatus = async () => {
    if (!assistant) return;
    const nextStatus = assistant.status === 'Active' ? 'Disabled' : 'Active';
    try {
      setUpdatingStatus(true);
      await adminService.updateUserStatus(assistant.id, nextStatus);
      setAssistant({ ...assistant, status: nextStatus });
      toast.success(
        nextStatus === 'Active' ? 'تم تنشيط المساعد' : 'تم تعليق المساعد'
      );
    } catch {
      toast.error('فشل في تحديث حالة الحساب');
    } finally {
      setUpdatingStatus(false);
    }
  };

  const handleSaveSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assistant) return;
    try {
      setSavingSettings(true);
      const res = await hrService.saveEmployeeProfile({
        userId: assistant.id,
        basicSalary: Number(salary) || 0,
        standardStartTime: startTime,
        targetDailyHours: Number(dailyHours) || 8,
      });

      if (res.success) {
        toast.success('تم حفظ إعدادات الموظف بنجاح');
        setIsEditingSettings(false);
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
      setSavingSettings(false);
    }
  };

  const handleApproveVacation = async (vacId: string) => {
    try {
      const res = await hrService.approveVacation(vacId);
      if (res.success) {
        toast.success('تمت الموافقة على الإجازة');
        // Refresh vacations list
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
        // Refresh vacations list
        if (assistant) {
          const vacData = await hrService.getVacations(assistant.phoneNumber);
          setVacations(vacData.filter((v) => v.employeePhone === assistant.phoneNumber));
        }
      }
    } catch {
      toast.error('فشل في رفض الإجازة');
    }
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

  const attendanceColumns: AdminColumn<AdminAttendanceLogDto>[] = [
    {
      key: 'date',
      label: 'التاريخ',
      render: (a) => (
        <span className="font-bold text-[var(--admin-text)]">
          {new Date(a.date).toLocaleDateString('ar-EG', { dateStyle: 'medium' })}
        </span>
      ),
    },
    {
      key: 'clockIn',
      label: 'وقت الحضور',
      render: (a) => (
        <span className="font-mono text-sm">
          {new Date(a.clockIn).toLocaleTimeString('ar-EG', {
            hour: '2-digit',
            minute: '2-digit',
          })}
        </span>
      ),
    },
    {
      key: 'clockOut',
      label: 'وقت الانصراف',
      render: (a) =>
        a.clockOut ? (
          <span className="font-mono text-sm">
            {new Date(a.clockOut).toLocaleTimeString('ar-EG', {
              hour: '2-digit',
              minute: '2-digit',
            })}
          </span>
        ) : (
          <span className="text-xs font-bold text-amber-500 bg-amber-500/10 px-2 py-1 rounded-lg">
            قيد العمل
          </span>
        ),
    },
    {
      key: 'duration',
      label: 'ساعات العمل',
      render: (a) => {
        if (!a.durationMinutes) return '-';
        const hrs = Math.floor(a.durationMinutes / 60);
        const mins = Math.round(a.durationMinutes % 60);
        return (
          <span className="font-bold text-sm">
            {hrs} ساعة و {mins} دقيقة
          </span>
        );
      },
    },
    {
      key: 'late',
      label: 'التأخير',
      render: (a) =>
        a.lateMinutes > 0 ? (
          <span className="text-xs font-bold text-red-600 bg-red-100 dark:bg-red-950/40 dark:text-red-400 px-2.5 py-1 rounded-full">
            {a.lateMinutes} دقيقة
          </span>
        ) : (
          <span className="text-xs font-bold text-emerald-600 bg-emerald-100 dark:bg-emerald-950/40 dark:text-emerald-400 px-2.5 py-1 rounded-full">
            لا يوجد
          </span>
        ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (a) => (
        <span
          className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold ${
            a.status === 'Present'
              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/45 dark:text-emerald-400'
              : 'bg-amber-100 text-amber-700 dark:bg-amber-950/45 dark:text-amber-400'
          }`}
        >
          {a.status === 'Present' ? 'حاضر' : 'متأخر'}
        </span>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/assistants"
      sectionLabel="المساعدين"
      pageTitle={assistant.fullName}
      subtitle="الملف التعريفي والمهني وإدارة سجل الدوام والعمليات."
      action={
        <NeumorphButton
          intent="ghost"
          size="md"
          pill
          onClick={() => router.push('/admin/assistants')}
        >
          <ArrowRight className="h-4 w-4 ml-1" />
          العودة للقائمة
        </NeumorphButton>
      }
    >
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 items-start dir-rtl">
        {/* Right Column: Account overview & settings */}
        <div className="space-y-6 lg:col-span-1">
          {/* Card 1: Basic Info */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <div className="flex flex-col items-center text-center gap-4">
              <div className="flex h-20 w-20 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-primary-15)] font-black text-2xl text-[var(--admin-primary)] shadow-sm">
                {getInitials(assistant.fullName)}
              </div>
              <div>
                <h3 className="text-xl font-black text-[var(--admin-text)]">
                  {assistant.fullName}
                </h3>
                <p className="text-sm text-[var(--admin-muted)] mt-1 font-mono">
                  {assistant.phoneNumber}
                </p>
              </div>

              <div className="flex flex-wrap justify-center gap-2 mt-2">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3.5 py-1.5 text-xs font-bold text-[var(--admin-primary)]">
                  <Shield className="h-3.5 w-3.5" />
                  {assistant.roles.filter((r) => r !== 'Assistant').join(', ') ||
                    'مساعد تعليمي عام'}
                </span>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] px-3.5 py-1.5 text-xs font-bold text-[var(--admin-muted)]">
                  <Calendar className="h-3.5 w-3.5" />
                  انضم في:{' '}
                  {new Date(assistant.createdAt).toLocaleDateString('ar-EG')}
                </span>
              </div>

              <div className="w-full border-t border-[var(--admin-border)]/60 my-4" />

              <div className="flex items-center justify-between w-full">
                <span className="text-sm font-bold text-[var(--admin-muted)]">
                  حالة الحساب
                </span>
                <button
                  onClick={handleToggleStatus}
                  disabled={updatingStatus}
                  className={`inline-flex items-center gap-1.5 rounded-full px-4 py-2 text-xs font-extrabold transition active:scale-95 ${
                    assistant.status === 'Active'
                      ? 'bg-emerald-100 text-emerald-700 hover:bg-emerald-200'
                      : 'bg-red-100 text-red-700 hover:bg-red-200'
                  }`}
                >
                  {updatingStatus ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : assistant.status === 'Active' ? (
                    <UserCheck className="h-3.5 w-3.5" />
                  ) : (
                    <UserX className="h-3.5 w-3.5" />
                  )}
                  {assistant.status === 'Active' ? 'نشط' : 'معلق'}
                </button>
              </div>
            </div>
          </div>

          {/* Card 2: Job Settings */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h4 className="text-md font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
              <Briefcase className="h-5 w-5 text-[var(--admin-primary)]" />
              إعدادات الوظيفة والراتب
            </h4>

            {isEditingSettings ? (
              <form onSubmit={handleSaveSettings} className="space-y-4">
                <div>
                  <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">
                    الراتب الأساسي (جنيه مصري)
                  </label>
                  <div className="relative">
                    <input
                      type="number"
                      value={salary}
                      onChange={(e) => setSalary(e.target.value)}
                      className="admin-input"
                      placeholder="0"
                      min={0}
                      required
                    />
                    <DollarSign className="absolute left-3 top-3 h-4 w-4 text-[var(--admin-muted)]" />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">
                    موعد الحضور الرسمي
                  </label>
                  <input
                    type="text"
                    value={startTime}
                    onChange={(e) => setStartTime(e.target.value)}
                    className="admin-input font-mono"
                    placeholder="09:00:00"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">
                    ساعات العمل اليومية المطلوبة
                  </label>
                  <input
                    type="number"
                    value={dailyHours}
                    onChange={(e) => setDailyHours(e.target.value)}
                    className="admin-input"
                    placeholder="8"
                    min={1}
                    max={24}
                    required
                  />
                </div>

                <div className="flex gap-2 pt-2">
                  <NeumorphButton
                    type="submit"
                    intent="primary"
                    size="md"
                    loading={savingSettings}
                    disabled={savingSettings}
                  >
                    حفظ التغييرات
                  </NeumorphButton>
                  <NeumorphButton
                    type="button"
                    intent="ghost"
                    size="md"
                    onClick={() => setIsEditingSettings(false)}
                  >
                    إلغاء
                  </NeumorphButton>
                </div>
              </form>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-between p-3 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-xs text-[var(--admin-muted)] font-bold">
                    الراتب الأساسي
                  </span>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.basicSalary ?? 0} ج.م
                  </span>
                </div>

                <div className="flex items-center justify-between p-3 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-xs text-[var(--admin-muted)] font-bold">
                    موعد الحضور الرسمي
                  </span>
                  <span className="text-sm font-bold text-[var(--admin-text)] font-mono">
                    {employeeProfile?.employeeProfile?.standardStartTime ??
                      '09:00:00'}
                  </span>
                </div>

                <div className="flex items-center justify-between p-3 rounded-2xl bg-[var(--admin-card-soft)]">
                  <span className="text-xs text-[var(--admin-muted)] font-bold">
                    ساعات العمل اليومية
                  </span>
                  <span className="text-sm font-black text-[var(--admin-text)]">
                    {employeeProfile?.employeeProfile?.targetDailyHours ?? 8}{' '}
                    ساعات
                  </span>
                </div>

                <NeumorphButton
                  type="button"
                  intent="primary"
                  size="md"
                  className="w-full mt-2"
                  onClick={() => setIsEditingSettings(true)}
                >
                  تعديل الإعدادات الوظيفية
                </NeumorphButton>
              </div>
            )}
          </div>
        </div>

        {/* Left Column: Logs, Timeline, Attendance & Vacations */}
        <div className="space-y-6 lg:col-span-2">
          {/* Section 1: Attendance Log */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h4 className="text-md font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
              <CalendarClock className="h-5 w-5 text-[var(--admin-primary)]" />
              سجل الحضور والانصراف الأخير
            </h4>

            <AdminDataTable
              data={attendance}
              columns={attendanceColumns}
              loading={false}
              rowKey={(a) => a.id}
              emptyMessage="لا يوجد أي سجل حضور مسجل لهذا المساعد حالياً."
            />
          </div>

          {/* Section 2: Vacations requests */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h4 className="text-md font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
              <FileText className="h-5 w-5 text-[var(--admin-primary)]" />
              طلبات الإجازات
            </h4>

            {vacations.length === 0 ? (
              <p className="text-xs font-bold text-[var(--admin-muted)] text-center py-6">
                لا توجد طلبات إجازة مقدمة.
              </p>
            ) : (
              <div className="space-y-3">
                {vacations.map((v) => (
                  <div
                    key={v.id}
                    className="flex flex-col md:flex-row md:items-center justify-between p-4 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)]/10 gap-3"
                  >
                    <div>
                      <p className="text-xs text-[var(--admin-muted)] font-bold">
                        فترة الإجازة:
                      </p>
                      <p className="text-sm font-bold text-[var(--admin-text)] mt-0.5">
                        من {new Date(v.startDate).toLocaleDateString('ar-EG')}{' '}
                        إلى {new Date(v.endDate).toLocaleDateString('ar-EG')}
                      </p>
                      <p className="text-xs text-[var(--admin-muted)] mt-1">
                        السبب: {v.reason}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {v.status === 'Pending' ? (
                        <>
                          <NeumorphButton
                            intent="primary"
                            size="sm"
                            onClick={() => handleApproveVacation(v.id)}
                          >
                            موافقة
                          </NeumorphButton>
                          <NeumorphButton
                            intent="danger"
                            size="sm"
                            onClick={() => handleRejectVacation(v.id)}
                          >
                            رفض
                          </NeumorphButton>
                        </>
                      ) : (
                        <span
                          className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-extrabold ${
                            v.status === 'Approved'
                              ? 'bg-emerald-100 text-emerald-700'
                              : 'bg-red-100 text-red-700'
                          }`}
                        >
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

          {/* Section 3: Audit Activity Logs */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
            <h4 className="text-md font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
              <Activity className="h-5 w-5 text-[var(--admin-primary)]" />
              سجل النشاطات والأحداث
            </h4>

            {auditLogs.length === 0 ? (
              <p className="text-xs font-bold text-[var(--admin-muted)] text-center py-6">
                لا توجد نشاطات مسجلة لهذا الحساب حالياً.
              </p>
            ) : (
              <div className="relative border-r border-[var(--admin-border)]/40 mr-2 pr-6 space-y-6">
                {auditLogs.map((log) => (
                  <div key={log.id} className="relative group">
                    <div className="absolute right-[-29px] top-1.5 flex h-3 w-3 items-center justify-center rounded-full border border-[var(--admin-bg)] bg-[var(--admin-primary)]" />

                    <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-1">
                      <div>
                        <p className="text-sm font-bold text-[var(--admin-text)]">
                          {translateAction(log.action)}
                        </p>
                        <p className="text-xs text-[var(--admin-muted)] mt-0.5">
                          الكيان: {log.entityType}
                          {log.ipAddress && ` | IP: ${log.ipAddress}`}
                        </p>
                      </div>
                      <span className="text-xs text-[var(--admin-muted)] font-medium">
                        {formatRelativeDate(log.createdAt)}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </AdminShellChrome>
  );
}
