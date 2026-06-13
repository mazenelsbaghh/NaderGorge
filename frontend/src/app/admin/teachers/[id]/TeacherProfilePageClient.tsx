'use client';

import { devConsole } from '@/utils/dev-console';
import { useCallback, useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard, AdminDataTable } from '@/components/admin';
import { adminService, type UserAuditLogDto } from '@/services/admin-service';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { hrService, type EmployeeDto, type AdminAttendanceLogDto, type AdminVacationDto } from '@/services/hr-service';
import { formatRelativeDate, getInitials } from '@/components/admin/admin-utils';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import {
  Users, Package, BookOpen, PenLine, DollarSign, Wallet,
  GraduationCap, Activity, Phone, User, Clock3,
  FileText, Briefcase, ArrowLeft,
} from 'lucide-react';
import toast from 'react-hot-toast';

/* ─── Social Media Icons (reused from AdminTeachersPageClient) ─── */
const FacebookIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z"/>
  </svg>
);
const YoutubeIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33A2.78 2.78 0 0 0 3.4 19c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2 29 29 0 0 0 .46-5.25 29 29 0 0 0-.46-5.33z"/>
    <polygon points="9.75 15.02 15.5 11.75 9.75 8.48 9.75 15.02"/>
  </svg>
);
const TelegramIcon = (props: React.SVGProps<SVGSVGElement>) => (
  <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="m22 2-7 20-4-9-9-4Z"/>
    <path d="M22 2 11 13"/>
  </svg>
);

/* ─── Grade name map ─── */
const GRADE_NAMES: Record<string, string> = {
  FirstSecondary: 'الأول الثانوي', SecondSecondary: 'الثاني الثانوي',
  SecondaryGrade3: 'الثالث الثانوي', FirstBaccalaureate: 'الأول بكالوريا',
  SecondBaccalaureate: 'الثاني بكالوريا', PrimaryGrade1: 'الأول الابتدائي',
  PrimaryGrade2: 'الثاني الابتدائي', PrimaryGrade3: 'الثالث الابتدائي',
  PrimaryGrade4: 'الرابع الابتدائي', PrimaryGrade5: 'الخامس الابتدائي',
  PrimaryGrade6: 'السادس الابتدائي', PrepGrade1: 'الأول الإعدادي',
  PrepGrade2: 'الثاني الإعدادي', PrepGrade3: 'الثالث الإعدادي',
  AzhariPrimary1: 'الأول الابتدائي الأزهري', AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري', AmericanGrade9: 'Grade 9',
  AmericanGrade10: 'Grade 10', AmericanGrade11: 'Grade 11', AmericanGrade12: 'Grade 12',
};

/* ─── Helpers ─── */
const translateAction = (action: string): string => {
  const map: Record<string, string> = {
    AdjustBalance: 'تعديل رصيد', OverrideVideoLimit: 'تجاوز حد مشاهدة الفيديو',
    ToggleStudentSystemAccess: 'تعديل صلاحية وصول', TOGGLE_ACCESS: 'تعديل صلاحية وصول',
    ResetWatchLimit: 'إعادة تعيين حد المشاهدة', AdjustGamificationPoints: 'تعديل نقاط',
    AdjustGamification: 'تعديل نقاط', ApproveWatchRequest: 'الموافقة على طلب مشاهدة',
    AddStudentNote: 'إضافة ملاحظة', DeleteStudentNote: 'حذف ملاحظة',
    UpdateStudentProfile: 'تحديث ملف شخصي', DisconnectStudentDevice: 'فصل جهاز',
    DisconnectDevice: 'فصل جهاز', DisconnectAllDevices: 'فصل جميع الأجهزة',
    RemoveDevice: 'حذف جهاز مسجل', CreateUser: 'إنشاء مستخدم',
    UpdateUserStatus: 'تحديث حالة المستخدم', UpdateUserRoles: 'تحديث أدوار المستخدم',
    SetWatchCount: 'تعديل عدد المشاهدات', RejectWatchRequest: 'رفض طلب مشاهدة',
  };
  return map[action] || action;
};

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

const formatDate = (d?: string | null) => {
  if (!d) return 'غير متوفر';
  return new Date(d).toLocaleDateString('en-GB');
};

const mapVacationStatus = (s: string): { label: string; cls: string } => {
  const m: Record<string, { label: string; cls: string }> = {
    Pending: { label: 'قيد الانتظار', cls: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400' },
    Approved: { label: 'موافق عليها', cls: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' },
    Rejected: { label: 'مرفوضة', cls: 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400' },
  };
  return m[s] || { label: s, cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-900 dark:text-zinc-300' };
};

const mapAttendanceStatus = (s: string): { label: string; cls: string } => {
  const m: Record<string, { label: string; cls: string }> = {
    Present: { label: 'حاضر', cls: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' },
    Late: { label: 'متأخر', cls: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400' },
    Absent: { label: 'غائب', cls: 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400' },
    Sick: { label: 'إجازة مرضية', cls: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400' },
    Leave: { label: 'إجازة', cls: 'bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400' },
  };
  return m[s] || { label: s, cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-900 dark:text-zinc-300' };
};

/* ─── Tab type ─── */
type TabKey = 'overview' | 'content' | 'students' | 'essays' | 'financials' | 'hr' | 'audit';

/* ──────────────────────────────────────────────────────────────────
   Component
   ────────────────────────────────────────────────────────────────── */
export default function TeacherProfilePageClient({ params }: { params: { id: string } }) {
  const { id } = params;
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [loading, setLoading] = useState(true);

  // ── Data slices ──
  const [teacher, setTeacher] = useState<TeacherDto | null>(null);
  const [stats, setStats] = useState<any>(null);
  const [students, setStudents] = useState<any[]>([]);
  const [essays, setEssays] = useState<any[]>([]);
  const [activations, setActivations] = useState<any[]>([]);
  const [payouts, setPayouts] = useState<any[]>([]);
  const [codeGroups, setCodeGroups] = useState<any[]>([]);
  const [auditLogs, setAuditLogs] = useState<UserAuditLogDto[]>([]);

  // HR data
  const [employee, setEmployee] = useState<EmployeeDto | null>(null);
  const [attendance, setAttendance] = useState<AdminAttendanceLogDto[]>([]);
  const [vacations, setVacations] = useState<AdminVacationDto[]>([]);

  const TABS: AdminTab<TabKey>[] = [
    { key: 'overview', label: 'نظرة عامة' },
    { key: 'content', label: 'المحتوى' },
    { key: 'students', label: 'الطلاب' },
    { key: 'essays', label: 'المقالات' },
    { key: 'financials', label: 'الماليات' },
    { key: 'hr', label: 'الموارد البشرية' },
    { key: 'audit', label: 'سجل النشاط' },
  ];

  const fetchTeacher = useCallback(async () => {
    setLoading(true);
    try {
      // Core data — always needed
      const teacherRes = await teacherService.getTeacherById(id);
      const t = teacherRes?.data ?? teacherRes;
      setTeacher(t as TeacherDto);

      // Fire all secondary requests in parallel — each wrapped in try/catch
      const [
        statsRes, studentsRes, essaysRes, activationsRes,
        payoutsRes, codeGroupsRes, auditRes,
        employeesRes, attendanceRes, vacationsRes,
      ] = await Promise.all([
        adminService.getTeacherStats(id).catch(() => null),
        adminService.getTeacherStudents(id).catch(() => []),
        adminService.getTeacherEssays(id).catch(() => []),
        adminService.getTeacherActivations(id).catch(() => []),
        adminService.getFinancePayouts(id).catch(() => []),
        adminService.listCodeGroups({ force: true }).catch(() => []),
        (t as TeacherDto)?.userId
          ? adminService.getUserAuditLogs((t as TeacherDto).userId).catch(() => [])
          : Promise.resolve([]),
        hrService.listEmployees().catch(() => []),
        (t as TeacherDto)?.userId
          ? hrService.getAttendance(undefined, undefined, undefined).catch(() => [])
          : Promise.resolve([]),
        (t as TeacherDto)?.userId
          ? hrService.getVacations().catch(() => [])
          : Promise.resolve([]),
      ]);

      setStats(statsRes);
      setStudents(studentsRes ?? []);
      setEssays(essaysRes ?? []);
      setActivations(activationsRes ?? []);
      setPayouts(payoutsRes ?? []);
      // Filter code groups for this teacher
      setCodeGroups(
        ((codeGroupsRes ?? []) as any[]).filter(
          (g: any) => g.teacherId === id
        )
      );
      setAuditLogs((auditRes ?? []) as UserAuditLogDto[]);

      // HR — find this teacher's employee record
      const userId = (t as TeacherDto)?.userId;
      if (userId) {
        const empMatch = ((employeesRes ?? []) as EmployeeDto[]).find(e => e.userId === userId);
        setEmployee(empMatch ?? null);

        // Filter attendance/vacations for this user
        setAttendance(
          ((attendanceRes ?? []) as AdminAttendanceLogDto[]).filter(
            (a) => a.employeeId === empMatch?.id
          )
        );
        setVacations(
          ((vacationsRes ?? []) as AdminVacationDto[]).filter(
            (v) => v.employeeId === empMatch?.id
          )
        );
      }
    } catch (err) {
      devConsole.error("Failed to load teacher profile", err);
      toast.error('فشل في تحميل بيانات المعلم');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchTeacher();
  }, [fetchTeacher]);

  const gradeList = teacher?.specialization ? teacher.specialization.split(',') : [];

  /* ─────────── Empty state reusable ─────────── */
  const EmptyState = ({ icon: Icon, text }: { icon: React.ElementType; text: string }) => (
    <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] bg-[var(--admin-card-soft)] rounded-3xl shadow-sm">
      <Icon size={48} className="mb-4 opacity-20 text-[var(--admin-primary)]" />
      <p className="text-sm font-bold">{text}</p>
    </div>
  );

  /* ─────────── Info field reusable ─────────── */
  const InfoField = ({ label, value, mono }: { label: string; value: string; mono?: boolean }) => (
    <div>
      <p className="text-[var(--admin-muted)] text-sm mb-1">{label}</p>
      <p className={`text-[var(--admin-text)] font-semibold ${mono ? 'font-mono' : ''}`}>{value || 'غير متوفر'}</p>
    </div>
  );

  /* ─────────── Section card reusable ─────────── */
  const SectionCard = ({ icon: Icon, title, children }: { icon: React.ElementType; title: string; children: React.ReactNode }) => (
    <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
      <div className="flex items-center gap-3 mb-5">
        <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
          <Icon size={20} />
        </div>
        <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
          {title}
        </h3>
      </div>
      {children}
    </div>
  );

  return (
    <AdminShellChrome
      activePath="/admin/teachers"
      sectionLabel="المستخدمين"
      pageTitle="ملف المعلم الشامل"
      subtitle="تفاصيل شاملة للمحتوى، الطلاب، المالية، والموارد البشرية"
      action={
        <div className="flex gap-4">
          <button
            onClick={() => router.push('/admin/teachers')}
            className="flex items-center gap-2 rounded-2xl bg-[var(--admin-surface-low)] px-4 py-2 text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-border)]"
          >
            <ArrowLeft size={20} />
            العودة للقائمة
          </button>
        </div>
      }
    >
      {/* ── Header card with teacher avatar ── */}
      {!loading && teacher && (
        <div className="mb-6 flex items-center gap-5 rounded-3xl bg-[var(--admin-bg)] p-6 shadow-sm">
          {teacher.profileImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={resolveMediaUrl(teacher.profileImageUrl)}
              alt={teacher.fullName}
              className="h-16 w-16 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
            />
          ) : (
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-xl shadow-sm">
              {getInitials(teacher.fullName)}
            </div>
          )}
          <div>
            <h2 className="text-xl font-black text-[var(--admin-text)] tracking-tight">{teacher.fullName}</h2>
            <p className="text-sm text-[var(--admin-muted)] font-mono mt-0.5">{teacher.phoneNumber}</p>
          </div>
          <div className="mr-auto flex flex-wrap gap-2">
            {teacher.subjectNames?.map((name, i) => (
              <span key={i} className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10">
                <BookOpen className="h-3 w-3" />
                {name}
              </span>
            ))}
          </div>
        </div>
      )}

      <AdminTabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />

      <div className="mt-8 animate-in fade-in slide-in-from-bottom-2 duration-300">

        {/* ══════════════════════════════════════════
            TAB 1: Overview — نظرة عامة
            ══════════════════════════════════════════ */}
        {activeTab === 'overview' && (
          <div className="flex flex-col gap-8">
            {/* Stat cards */}
            <div>
              <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-4">ملخص الإحصاءات</h3>
              <div className="grid grid-cols-1 gap-6 md:grid-cols-4">
                <AdminStatCard variant="accent" icon={Users} label="عدد الطلاب" value={stats?.studentsCount ?? students.length ?? 0} />
                <AdminStatCard variant="light" icon={Package} label="عدد الباقات" value={stats?.packagesCount ?? 0} />
                <AdminStatCard variant="muted" icon={FileText} label="عدد الامتحانات" value={stats?.examsCount ?? 0} />
                <AdminStatCard variant="accent" icon={PenLine} label="مقالات قيد التصحيح" value={stats?.pendingEssaysCount ?? essays.length ?? 0} />
              </div>
            </div>

            {/* Personal info */}
            <SectionCard icon={User} title="البيانات الشخصية">
              {loading ? (
                <div className="text-[var(--admin-muted)]">جاري التحميل...</div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                  <InfoField label="الاسم بالكامل" value={teacher?.fullName || ''} />
                  <InfoField label="رقم الهاتف" value={teacher?.phoneNumber || ''} mono />
                  <InfoField label="أرقام هواتف المساعدين" value={teacher?.assistantPhoneNumbers || ''} mono />
                  <InfoField label="معلومات الاتصال" value={teacher?.contactInfo || ''} />
                  <InfoField label="نسبة العمولة" value={teacher?.commissionRate != null ? `${teacher.commissionRate}%` : 'غير محددة'} />
                </div>
              )}
            </SectionCard>

            {/* Social media */}
            <SectionCard icon={Phone} title="روابط التواصل الاجتماعي">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
                <div className="flex items-center gap-3">
                  <FacebookIcon className="h-5 w-5 text-blue-600" />
                  <div>
                    <p className="text-xs text-[var(--admin-muted)]">فيسبوك</p>
                    {teacher?.facebookUrl ? (
                      <a href={teacher.facebookUrl} target="_blank" rel="noopener noreferrer" className="text-sm font-bold text-[var(--admin-primary)] hover:underline truncate max-w-[180px] block font-mono">
                        {teacher.facebookUrl}
                      </a>
                    ) : (
                      <p className="text-sm text-[var(--admin-muted)]">—</p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <YoutubeIcon className="h-5 w-5 text-red-600" />
                  <div>
                    <p className="text-xs text-[var(--admin-muted)]">يوتيوب</p>
                    {teacher?.youtubeUrl ? (
                      <a href={teacher.youtubeUrl} target="_blank" rel="noopener noreferrer" className="text-sm font-bold text-[var(--admin-primary)] hover:underline truncate max-w-[180px] block font-mono">
                        {teacher.youtubeUrl}
                      </a>
                    ) : (
                      <p className="text-sm text-[var(--admin-muted)]">—</p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <TelegramIcon className="h-5 w-5 text-sky-500" />
                  <div>
                    <p className="text-xs text-[var(--admin-muted)]">تيليجرام</p>
                    {teacher?.telegramUrl ? (
                      <a href={teacher.telegramUrl} target="_blank" rel="noopener noreferrer" className="text-sm font-bold text-[var(--admin-primary)] hover:underline truncate max-w-[180px] block font-mono">
                        {teacher.telegramUrl}
                      </a>
                    ) : (
                      <p className="text-sm text-[var(--admin-muted)]">—</p>
                    )}
                  </div>
                </div>
              </div>
            </SectionCard>

            {/* Specialization & Bio */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5">
                <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-2">
                  <GraduationCap className="h-4 w-4 text-[var(--admin-primary)]" />
                  المراحل والصفوف الدراسية
                </h4>
                <div className="flex flex-wrap gap-2">
                  {gradeList.length > 0 ? gradeList.map((val, idx) => (
                    <span key={idx} className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10">
                      {GRADE_NAMES[val] || val}
                    </span>
                  )) : (
                    <p className="text-xs text-red-500 font-bold">غير محدد</p>
                  )}
                </div>
              </div>

              <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5">
                <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-2">
                  <User className="h-4 w-4 text-[var(--admin-primary)]" />
                  الوصف
                </h4>
                <p className="text-sm text-[var(--admin-muted)] leading-relaxed whitespace-pre-wrap">
                  {teacher?.bio || 'لا يوجد وصف مسجل حالياً.'}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 2: Content — المحتوى
            ══════════════════════════════════════════ */}
        {activeTab === 'content' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              <AdminStatCard variant="accent" icon={Package} label="عدد الباقات" value={stats?.packagesCount ?? 0} />
              <AdminStatCard variant="light" icon={FileText} label="عدد الامتحانات" value={stats?.examsCount ?? 0} />
              <AdminStatCard variant="muted" icon={BookOpen} label="مجموعات الأكواد" value={codeGroups.length} />
            </div>

            {/* Code groups table */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">مجموعات الأكواد</h3>
                <p className="text-[var(--admin-muted)]">جميع مجموعات أكواد الشحن التي أنشأها هذا المعلم.</p>
              </div>

              <AdminDataTable<any>
                columns={[
                  { key: 'name', label: 'اسم المجموعة', render: (row) => <span className="font-bold text-[var(--admin-text)]">{row.name}</span> },
                  { key: 'codeCount', label: 'عدد الأكواد', render: (row) => <span className="font-mono">{row.codeCount}</span> },
                  { key: 'usedCount', label: 'المستخدمة', render: (row) => (
                    <span className={`font-mono font-bold ${row.usedCount > 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-[var(--admin-muted)]'}`}>{row.usedCount}</span>
                  )},
                  { key: 'createdAt', label: 'تاريخ الإنشاء', render: (row) => formatDate(row.createdAt) },
                ]}
                data={codeGroups}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد مجموعات أكواد لهذا المعلم"
              />
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 3: Students — الطلاب
            ══════════════════════════════════════════ */}
        {activeTab === 'students' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              <AdminStatCard variant="accent" icon={Users} label="إجمالي الطلاب المسجلين" value={students.length} />
              <AdminStatCard variant="light" icon={Activity} label="طلاب نشطون" value={students.filter((s: any) => s.isActive !== false).length} />
            </div>

            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">قائمة الطلاب</h3>
                <p className="text-[var(--admin-muted)]">الطلاب المسجلين في باقات هذا المعلم مع حالة النشاط.</p>
              </div>

              <AdminDataTable<any>
                columns={[
                  { key: 'fullName', label: 'اسم الطالب', render: (row) => (
                    <span className="font-bold text-[var(--admin-text)]">{row.fullName || row.studentName || '—'}</span>
                  )},
                  { key: 'phoneNumber', label: 'رقم الهاتف', render: (row) => (
                    <span className="font-mono text-sm text-[var(--admin-text)]">{row.phoneNumber || '—'}</span>
                  )},
                  { key: 'packageName', label: 'الباقة', render: (row) => (
                    <span className="text-sm text-[var(--admin-text)]">{row.activatedPackageName || row.packageName || '—'}</span>
                  )},
                  { key: 'activatedAt', label: 'تاريخ التفعيل', render: (row) => formatDate(row.activatedAt || row.enrolledAt) },
                  { key: 'status', label: 'الحالة', render: (row) => {
                    const active = row.isActive !== false;
                    return (
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${active ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' : 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400'}`}>
                        <span className="h-1.5 w-1.5 rounded-full bg-current" />
                        {active ? 'نشط' : 'غير نشط'}
                      </span>
                    );
                  }},
                ]}
                data={students}
                rowKey={(row) => row.id || row.studentId || row.fullName}
                emptyMessage="لا يوجد طلاب مسجلين حالياً"
              />
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 4: Essays — المقالات
            ══════════════════════════════════════════ */}
        {activeTab === 'essays' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              <AdminStatCard variant="accent" icon={PenLine} label="إجمالي المقالات" value={essays.length} />
              <AdminStatCard variant="light" icon={Clock3} label="قيد التصحيح" value={essays.filter((e: any) => e.status === 'Pending' || e.status === 'PendingTeacherReview').length} />
              <AdminStatCard variant="muted" icon={FileText} label="تم تصحيحها" value={essays.filter((e: any) => e.status === 'Graded' || e.status === 'TeacherGraded').length} />
            </div>

            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل المقالات</h3>
                <p className="text-[var(--admin-muted)]">جميع المقالات المقدمة من طلاب هذا المعلم مع حالة التصحيح.</p>
              </div>

              <AdminDataTable<any>
                columns={[
                  { key: 'studentName', label: 'الطالب', render: (row) => <span className="font-bold text-[var(--admin-text)]">{row.studentName || '—'}</span> },
                  { key: 'examTitle', label: 'الامتحان', render: (row) => <span className="text-sm text-[var(--admin-text)]">{row.examTitle || row.questionText || '—'}</span> },
                  { key: 'status', label: 'الحالة', render: (row) => {
                    const isPending = row.status === 'Pending' || row.status === 'PendingTeacherReview';
                    return (
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${isPending ? 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400' : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'}`}>
                        <span className="h-1.5 w-1.5 rounded-full bg-current" />
                        {isPending ? 'قيد التصحيح' : 'تم التصحيح'}
                      </span>
                    );
                  }},
                  { key: 'aiInitialScore', label: 'درجة AI', render: (row) => (
                    <span className="font-mono text-sm">{row.aiInitialScore ?? '—'}</span>
                  )},
                  { key: 'submittedAt', label: 'تاريخ التقديم', render: (row) => formatDate(row.submittedAt) },
                ]}
                data={essays}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد مقالات مسجلة"
              />
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 5: Financials — الماليات
            ══════════════════════════════════════════ */}
        {activeTab === 'financials' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              <AdminStatCard variant="accent" icon={Wallet} label="نسبة العمولة" value={`${teacher?.commissionRate ?? 0}%`} />
              <AdminStatCard variant="light" icon={DollarSign} label="عدد التحويلات" value={payouts.length} />
              <AdminStatCard variant="muted" icon={Package} label="عدد التفعيلات" value={activations.length} />
            </div>

            {/* Payouts table */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل التحويلات المالية</h3>
                <p className="text-[var(--admin-muted)]">جميع عمليات الدفع والتحويلات المالية للمعلم.</p>
              </div>

              <AdminDataTable<any>
                columns={[
                  { key: 'amount', label: 'المبلغ', render: (row) => (
                    <span className="font-mono font-bold text-emerald-600 dark:text-emerald-400">
                      {row.amount ?? 0} ج.م
                    </span>
                  )},
                  { key: 'description', label: 'البيان', render: (row) => (
                    <span className="text-sm text-[var(--admin-text)]">{row.description || row.note || '—'}</span>
                  )},
                  { key: 'status', label: 'الحالة', render: (row) => (
                    <span className="inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
                      <span className="h-1.5 w-1.5 rounded-full bg-current" />
                      {row.status || 'مكتمل'}
                    </span>
                  )},
                  { key: 'createdAt', label: 'التاريخ', render: (row) => row.createdAt ? new Date(row.createdAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }) : '—' },
                ]}
                data={payouts}
                rowKey={(row) => row.id || `${row.amount}-${row.createdAt}`}
                emptyMessage="لا توجد تحويلات مالية مسجلة"
              />
            </div>

            {/* Activations table */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل التفعيلات</h3>
                <p className="text-[var(--admin-muted)]">جميع عمليات تفعيل الأكواد والباقات الخاصة بهذا المعلم.</p>
              </div>

              <AdminDataTable<any>
                columns={[
                  { key: 'studentName', label: 'الطالب', render: (row) => <span className="font-bold text-[var(--admin-text)]">{row.studentName || '—'}</span> },
                  { key: 'packageName', label: 'الباقة', render: (row) => <span className="text-sm text-[var(--admin-text)]">{row.packageName || '—'}</span> },
                  { key: 'code', label: 'الكود', render: (row) => <span className="font-mono text-sm text-[var(--admin-text)]">{row.code || '—'}</span> },
                  { key: 'activatedAt', label: 'تاريخ التفعيل', render: (row) => row.activatedAt ? new Date(row.activatedAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }) : '—' },
                ]}
                data={activations}
                rowKey={(row) => row.id || `${row.code}-${row.activatedAt}`}
                emptyMessage="لا توجد تفعيلات مسجلة"
              />
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 6: HR — الموارد البشرية
            ══════════════════════════════════════════ */}
        {activeTab === 'hr' && (
          <div className="flex flex-col gap-6">
            {/* Employee settings */}
            <SectionCard icon={Briefcase} title="إعدادات الوظيفة">
              {employee?.employeeProfile ? (
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                  <InfoField label="الراتب الأساسي" value={`${employee.employeeProfile.basicSalary} ج.م`} mono />
                  <InfoField label="ساعة بدء العمل" value={employee.employeeProfile.standardStartTime || '—'} mono />
                  <InfoField label="ساعات العمل اليومية" value={`${employee.employeeProfile.targetDailyHours} ساعة`} />
                </div>
              ) : (
                <div className="text-center py-8 text-[var(--admin-muted)]">
                  <Briefcase size={32} className="mx-auto mb-3 opacity-30" />
                  <p className="text-sm font-bold">لم يتم تهيئة ملف الموظف بعد</p>
                </div>
              )}
            </SectionCard>

            {/* Attendance table */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل الحضور</h3>
                <p className="text-[var(--admin-muted)]">سجل الحضور والانصراف اليومي للمعلم.</p>
              </div>

              <AdminDataTable<AdminAttendanceLogDto>
                columns={[
                  { key: 'date', label: 'التاريخ', render: (row) => <span className="font-bold text-[var(--admin-text)]">{formatDate(row.date)}</span> },
                  { key: 'clockIn', label: 'وقت الحضور', render: (row) => (
                    <span className="font-mono text-sm">{row.clockIn ? new Date(row.clockIn).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' }) : '—'}</span>
                  )},
                  { key: 'clockOut', label: 'وقت الانصراف', render: (row) => (
                    <span className="font-mono text-sm">{row.clockOut ? new Date(row.clockOut).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' }) : '—'}</span>
                  )},
                  { key: 'lateMinutes', label: 'دقائق التأخير', render: (row) => (
                    <span className={`font-mono font-bold ${row.lateMinutes > 0 ? 'text-red-500' : 'text-emerald-500'}`}>{row.lateMinutes} د</span>
                  )},
                  { key: 'durationMinutes', label: 'المدة', render: (row) => (
                    <span className="font-mono text-sm">{row.durationMinutes ? `${Math.floor(row.durationMinutes / 60)}س ${row.durationMinutes % 60}د` : '—'}</span>
                  )},
                  { key: 'status', label: 'الحالة', render: (row) => {
                    const { label, cls } = mapAttendanceStatus(row.status);
                    return (
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${cls}`}>
                        <span className="h-1.5 w-1.5 rounded-full bg-current" />
                        {label}
                      </span>
                    );
                  }},
                ]}
                data={attendance}
                rowKey={(row) => row.id}
                emptyMessage="لا يوجد سجل حضور مسجل"
              />
            </div>

            {/* Vacations table */}
            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div className="mb-5">
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">الإجازات</h3>
                <p className="text-[var(--admin-muted)]">طلبات الإجازات وحالتها.</p>
              </div>

              <AdminDataTable<AdminVacationDto>
                columns={[
                  { key: 'startDate', label: 'من', render: (row) => <span className="font-bold text-[var(--admin-text)]">{formatDate(row.startDate)}</span> },
                  { key: 'endDate', label: 'إلى', render: (row) => formatDate(row.endDate) },
                  { key: 'reason', label: 'السبب', render: (row) => <span className="text-sm text-[var(--admin-text)]">{row.reason || '—'}</span> },
                  { key: 'status', label: 'الحالة', render: (row) => {
                    const { label, cls } = mapVacationStatus(row.status);
                    return (
                      <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${cls}`}>
                        <span className="h-1.5 w-1.5 rounded-full bg-current" />
                        {label}
                      </span>
                    );
                  }},
                  { key: 'handledByName', label: 'بواسطة', render: (row) => <span className="text-sm font-semibold text-[var(--admin-text)]">{row.handledByName || '—'}</span> },
                  { key: 'handledAt', label: 'تاريخ القرار', render: (row) => row.handledAt ? new Date(row.handledAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }) : '—' },
                ]}
                data={vacations}
                rowKey={(row) => row.id}
                emptyMessage="لا توجد إجازات مسجلة"
              />
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════
            TAB 7: Audit — سجل النشاط
            ══════════════════════════════════════════ */}
        {activeTab === 'audit' && (
          <div className="flex flex-col gap-6">
            <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              <div>
                <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل النشاط</h3>
                <p className="text-[var(--admin-muted)]">كافة الإجراءات والعمليات التي تمت على حساب هذا المعلم</p>
              </div>
            </div>

            {auditLogs.length === 0 ? (
              <EmptyState icon={Activity} text="لا يوجد أي نشاطات مسجلة لهذا الحساب حالياً." />
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
                            الكيان المتأثر: <span className="font-mono text-[var(--admin-text)]">{log.entityType}</span>
                            {log.ipAddress && <> | عنوان الـ IP: <span className="font-mono">{log.ipAddress}</span></>}
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
    </AdminShellChrome>
  );
}
