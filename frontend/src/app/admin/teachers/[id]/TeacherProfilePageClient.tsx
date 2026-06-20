'use client';

import { devConsole } from '@/utils/dev-console';
import { useCallback, useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard, AdminDataTable, AdminTeacherPhotoUpload } from '@/components/admin';
import { adminService, type UserAuditLogDto } from '@/services/admin-service';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { formatRelativeDate, getInitials } from '@/components/admin/admin-utils';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import {
  Users, Package, BookOpen, PenLine, DollarSign, Wallet,
  GraduationCap, Activity, Phone, User, Clock3,
  FileText, ArrowLeft, Download, X, Check,
  Image as ImageIcon, Loader2
} from 'lucide-react';
import toast from 'react-hot-toast';
import { AnimatePresence } from 'framer-motion';
import { compressImage, renameFileToMatchBase64 } from '@/utils/image-compressor';

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

const GRADE_GROUPS = [
  {
    label: 'المرحلة الثانوية العامة',
    grades: [
      { value: 'FirstSecondary', label: 'الأول الثانوي' },
      { value: 'SecondSecondary', label: 'الثاني الثانوي' },
      { value: 'SecondaryGrade3', label: 'الثالث الثانوي' },
    ]
  },
  {
    label: 'بكالوريا',
    grades: [
      { value: 'FirstBaccalaureate', label: 'الأول بكالوريا' },
      { value: 'SecondBaccalaureate', label: 'الثاني بكالوريا' },
    ]
  },
  {
    label: 'المرحلة الإعدادية',
    grades: [
      { value: 'PrepGrade1', label: 'الأول الإعدادي' },
      { value: 'PrepGrade2', label: 'الثاني الإعدادي' },
      { value: 'PrepGrade3', label: 'الثالث الإعدادي' },
    ]
  },
  {
    label: 'المرحلة الابتدائية',
    grades: [
      { value: 'PrimaryGrade1', label: 'الأول الابتدائي' },
      { value: 'PrimaryGrade2', label: 'الثاني الابتدائي' },
      { value: 'PrimaryGrade3', label: 'الثالث الابتدائي' },
      { value: 'PrimaryGrade4', label: 'الرابع الابتدائي' },
      { value: 'PrimaryGrade5', label: 'الخامس الابتدائي' },
      { value: 'PrimaryGrade6', label: 'السادس الابتدائي' },
    ]
  },
  {
    label: 'التعليم الأزهري',
    grades: [
      { value: 'AzhariPrimary1', label: 'الأول الابتدائي الأزهري' },
      { value: 'AzhariPrep1', label: 'الأول الإعدادي الأزهري' },
      { value: 'AzhariSecondary1', label: 'الأول الثانوي الأزهري' },
    ]
  },
  {
    label: 'التعليم الأمريكي (American)',
    grades: [
      { value: 'AmericanGrade9', label: 'Grade 9' },
      { value: 'AmericanGrade10', label: 'Grade 10' },
      { value: 'AmericanGrade11', label: 'Grade 11' },
      { value: 'AmericanGrade12', label: 'Grade 12' },
    ]
  }
];

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

/* ─── Tab type ─── */
type TabKey = 'overview' | 'content' | 'students' | 'essays' | 'financials' | 'audit';

/** Safely extract an array from an API response that might be { items: [...] }, [...], or null */
const toArray = (v: unknown): any[] => {
  if (Array.isArray(v)) return v;
  if (v && typeof v === 'object' && 'items' in (v as any) && Array.isArray((v as any).items)) return (v as any).items;
  if (v && typeof v === 'object' && 'data' in (v as any) && Array.isArray((v as any).data)) return (v as any).data;
  return [];
};

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
  const [studentPackageFilter, setStudentPackageFilter] = useState<string>('all');

  // ── Edit Modal states ──
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploadingProfile, setIsUploadingProfile] = useState(false);

  const [bio, setBio] = useState('');
  const [contactInfo, setContactInfo] = useState('');
  const [profileImageUrl, setProfileImageUrl] = useState('');
  const [assistantPhoneNumbers, setAssistantPhoneNumbers] = useState('');
  const [facebookUrl, setFacebookUrl] = useState('');
  const [youtubeUrl, setYouTubeUrl] = useState('');
  const [telegramUrl, setTelegramUrl] = useState('');
  const [commissionRate, setCommissionRate] = useState<number>(0);

  const [selectedGrades, setSelectedGrades] = useState<string[]>([]);
  const [selectedSubjectIds, setSelectedSubjectIds] = useState<string[]>([]);

  const [profileImagePreview, setProfileImagePreview] = useState<string | null>(null);

  const [subjects, setSubjects] = useState<any[]>([]);



  const TABS: AdminTab<TabKey>[] = [
    { key: 'overview', label: 'نظرة عامة' },
    { key: 'content', label: 'المحتوى' },
    { key: 'students', label: 'الطلاب' },
    { key: 'essays', label: 'المقالات' },
    { key: 'financials', label: 'الماليات' },
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
        payoutsRes, codeGroupsRes, auditRes, subjectsRes,
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
        teacherService.getSubjects().catch(() => ({ data: [] })),
      ]);

      setSubjects(toArray(subjectsRes));

      setStats(statsRes);
      setStudents(toArray(studentsRes));
      setEssays(toArray(essaysRes));
      setActivations(toArray(activationsRes));
      setPayouts(toArray(payoutsRes));
      setCodeGroups(
        toArray(codeGroupsRes).filter((g: any) => g.teacherId === id)
      );
      setAuditLogs(toArray(auditRes) as UserAuditLogDto[]);
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

  const handleOpenModal = () => {
    if (!teacher) return;
    setBio(teacher.bio || '');
    setContactInfo(teacher.contactInfo || '');
    setProfileImageUrl(teacher.profileImageUrl || '');
    setAssistantPhoneNumbers(teacher.assistantPhoneNumbers || '');
    setFacebookUrl(teacher.facebookUrl || '');
    setYouTubeUrl(teacher.youtubeUrl || '');
    setTelegramUrl(teacher.telegramUrl || '');
    setCommissionRate(teacher.commissionRate || 0);
    setSelectedGrades(teacher.specialization ? teacher.specialization.split(',') : []);
    setSelectedSubjectIds(teacher.subjectIds || []);
    setProfileImagePreview(teacher.profileImageUrl || null);

    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!teacher) return;
    
    if (selectedSubjectIds.length === 0) {
      toast.error('يرجى تحديد مادة دراسية واحدة على الأقل');
      return;
    }
    if (selectedGrades.length === 0) {
      toast.error('يرجى تحديد صف دراسي واحد على الأقل');
      return;
    }

    setIsSaving(true);
    try {
      const gradesString = selectedGrades.join(',');
      const res = await teacherService.updateTeacher(teacher.id, {
        bio: bio.trim(),
        specialization: gradesString,
        commissionRate,
        contactInfo: contactInfo.trim(),
        profileImageUrl: profileImageUrl.trim() || undefined,
        subjectIds: selectedSubjectIds,
        assistantPhoneNumbers: assistantPhoneNumbers.trim() || undefined,
        facebookUrl: facebookUrl.trim() || undefined,
        youtubeUrl: youtubeUrl.trim() || undefined,
        telegramUrl: telegramUrl.trim() || undefined,
      });

      if (res.success) {
        toast.success('تم تحديث ملف المعلم بنجاح ✅');
        setIsModalOpen(false);
        fetchTeacher();
      } else {
        toast.error(res.message || 'فشل في تحديث ملف المعلم');
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء تحديث بيانات المعلم');
    } finally {
      setIsSaving(false);
    }
  };

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
        <div className="flex gap-3">
          <button
            onClick={handleOpenModal}
            className="flex items-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-4 py-2 text-[var(--admin-primary-contrast)] transition-transform hover:-translate-y-0.5 font-bold text-sm shadow-sm cursor-pointer"
          >
            <PenLine size={16} />
            تعديل بيانات المعلم والصور
          </button>
          <button
            onClick={() => router.push('/admin/teachers')}
            className="flex items-center gap-2 rounded-2xl bg-[var(--admin-surface-low)] px-4 py-2 text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-border)] text-sm cursor-pointer"
          >
            <ArrowLeft size={16} />
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

        {activeTab === 'students' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              <AdminStatCard variant="accent" icon={Users} label="إجمالي الطلاب المسجلين" value={students.length} />
              <AdminStatCard variant="light" icon={Activity} label="طلاب نشطون" value={students.filter((s: any) => s.isActive !== false).length} />
            </div>

            <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
              {/* Header with title + download button */}
              <div className="flex flex-wrap items-center justify-between gap-4 mb-5">
                <div>
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">قائمة الطلاب</h3>
                  <p className="text-[var(--admin-muted)]">الطلاب المسجلين في باقات هذا المعلم مع تفاصيل الاشتراك.</p>
                </div>
                <button
                  onClick={() => {
                    const filtered = studentPackageFilter === 'all' ? students : students.filter((s: any) => (s.packageName || s.activatedPackageName) === studentPackageFilter);
                    const csv = [
                      ['اسم الطالب', 'رقم الهاتف', 'الباقة', 'السعر', 'تاريخ التفعيل', 'الحالة'].join(','),
                      ...filtered.map((s: any) => [
                        s.fullName || s.studentName || '',
                        s.phone || s.phoneNumber || '',
                        s.packageName || s.activatedPackageName || '',
                        s.price ?? '',
                        s.enrolledAt || s.activatedAt || s.grantedAt || '',
                        s.isActive !== false ? 'نشط' : 'غير نشط',
                      ].join(','))
                    ].join('\n');
                    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = `teacher-students-${new Date().toISOString().slice(0,10)}.csv`;
                    a.click();
                    URL.revokeObjectURL(url);
                    toast.success('تم تحميل الملف بنجاح');
                  }}
                  className="inline-flex items-center gap-2 px-4 py-2.5 rounded-2xl text-sm font-bold bg-[var(--admin-primary-15)] text-[var(--admin-text)] hover:bg-[var(--admin-primary-15)]/80 transition-all active:scale-95"
                >
                  <Download size={16} />
                  تنزيل CSV
                </button>
              </div>

              {/* Package filter pills */}
              {(() => {
                const packages = [...new Set(students.map((s: any) => s.packageName || s.activatedPackageName).filter(Boolean))];
                if (packages.length <= 1) return null;
                return (
                  <div className="flex flex-wrap gap-2 mb-5">
                    <button
                      onClick={() => setStudentPackageFilter('all')}
                      className={`px-4 py-2 rounded-full text-xs font-bold transition-all ${studentPackageFilter === 'all' ? 'bg-[var(--admin-text)] text-[var(--admin-bg)]' : 'bg-[var(--admin-hover)] text-[var(--admin-muted)] hover:bg-[var(--admin-border)]'}`}
                    >
                      الكل ({students.length})
                    </button>
                    {packages.map((pkg: string) => {
                      const count = students.filter((s: any) => (s.packageName || s.activatedPackageName) === pkg).length;
                      return (
                        <button
                          key={pkg}
                          onClick={() => setStudentPackageFilter(pkg)}
                          className={`px-4 py-2 rounded-full text-xs font-bold transition-all ${studentPackageFilter === pkg ? 'bg-[var(--admin-text)] text-[var(--admin-bg)]' : 'bg-[var(--admin-hover)] text-[var(--admin-muted)] hover:bg-[var(--admin-border)]'}`}
                        >
                          {pkg} ({count})
                        </button>
                      );
                    })}
                  </div>
                );
              })()}

              <AdminDataTable<any>
                columns={[
                  { key: 'fullName', label: 'اسم الطالب', render: (row) => (
                    <span className="font-bold text-[var(--admin-text)]">{row.fullName || row.studentName || '—'}</span>
                  )},
                  { key: 'phone', label: 'رقم الهاتف', render: (row) => (
                    <span className="font-mono text-sm text-[var(--admin-text)] tracking-wide" dir="ltr">{row.phone || row.phoneNumber || '—'}</span>
                  )},
                  { key: 'packageName', label: 'الباقة', render: (row) => (
                    <span className="text-sm text-[var(--admin-text)]">{row.packageName || row.activatedPackageName || '—'}</span>
                  )},
                  { key: 'price', label: 'السعر', render: (row) => (
                    <span className="font-mono font-bold text-sm text-[var(--admin-text)]">{row.price != null ? `${row.price} ج.م` : '—'}</span>
                  )},
                  { key: 'enrolledAt', label: 'تاريخ التفعيل', render: (row) => formatDate(row.enrolledAt || row.activatedAt || row.grantedAt) },
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
                data={studentPackageFilter === 'all' ? students : students.filter((s: any) => (s.packageName || s.activatedPackageName) === studentPackageFilter)}
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

      {/* Edit Modal */}
      <AnimatePresence>
        {isModalOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            {/* Backdrop */}
            <div
              onClick={handleCloseModal}
              className="absolute inset-0 bg-black/60 backdrop-blur-sm"
            />
            {/* Modal Box */}
            <div
              className="relative w-full max-w-2xl overflow-y-auto max-h-[90vh] rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-8 shadow-2xl"
              dir="rtl"
            >
              <button
                onClick={handleCloseModal}
                disabled={isSaving}
                className="absolute left-6 top-6 rounded-xl border border-[var(--admin-border)] p-2 text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] disabled:opacity-50"
              >
                <X className="h-4 w-4" />
              </button>

              <h2 className="text-xl font-black text-[var(--admin-text)]">
                تعديل ملف المعلم
              </h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                قم بتعديل تخصص المعلم ومعلومات التواصل والمواد الدراسية المرتبطة بملفه، بالإضافة لرفع الصور.
              </p>

              <form onSubmit={handleSave} className="mt-6 space-y-6">
                
                {/* Account Details (Readonly) */}
                <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 space-y-4">
                  <h4 className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-2 mb-2">
                    <User className="h-4 w-4 text-[var(--admin-primary)]" />
                    بيانات حساب الدخول للمنصة (للقراءة فقط)
                  </h4>

                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">الاسم الكامل</label>
                      <input
                        type="text"
                        disabled
                        value={teacher?.fullName || ''}
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] opacity-60 outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رقم الهاتف</label>
                      <div className="relative">
                        <input
                          type="tel"
                          disabled
                          value={teacher?.phoneNumber || ''}
                          dir="ltr"
                          className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] opacity-60 outline-none"
                        />
                        <Phone className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Photo Upload Section */}
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                  {/* Main Profile Image Upload */}
                  <div className="space-y-3">
                    <label className="block text-xs font-bold text-[var(--admin-text)] flex items-center gap-2">
                      <ImageIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                      الصورة الشخصية الأساسية
                    </label>
                    <div className="flex flex-col items-center justify-center border-2 border-dashed border-[var(--admin-border)] rounded-2xl p-4 bg-[var(--admin-bg)] hover:border-[var(--admin-primary)] transition relative">
                      <input
                        type="file"
                        accept="image/*"
                        className="absolute inset-0 opacity-0 cursor-pointer"
                        disabled={isUploadingProfile}
                        onChange={async (e) => {
                          const file = e.target.files?.[0];
                          if (!file) return;
                          setIsUploadingProfile(true);
                          try {
                            const base64 = await compressImage(file);
                            const finalFileName = renameFileToMatchBase64(file.name, base64);
                            setProfileImagePreview(base64);
                            if (teacher) {
                              const res = await adminService.uploadTeacherProfileImage(teacher.id, base64, finalFileName);
                              if (res.success && res.data) {
                                setProfileImageUrl(res.data);
                                toast.success('تم رفع الصورة الشخصية بنجاح ✅');
                              } else {
                                toast.error(res.message || 'فشل رفع الصورة الشخصية');
                              }
                            }
                          } catch (err) {
                            console.error(err);
                            toast.error('حدث خطأ أثناء معالجة ورفع الصورة الشخصية');
                          } finally {
                            setIsUploadingProfile(false);
                          }
                        }}
                      />
                      {profileImagePreview ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={resolveMediaUrl(profileImagePreview)}
                          alt="Profile Preview"
                          className="h-24 w-24 rounded-full object-cover border border-[var(--admin-border)] shadow-sm"
                        />
                      ) : (
                        <div className="flex h-24 w-24 items-center justify-center rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-xl">
                          {getInitials(teacher?.fullName || 'معلم')}
                        </div>
                      )}
                      <span className="text-xs text-[var(--admin-muted)] mt-2">
                        {isUploadingProfile ? 'جاري الرفع...' : 'اسحب صورة أو انقر للرفع'}
                      </span>
                    </div>
                  </div>

                  <AdminTeacherPhotoUpload teacherId={teacher?.userId} compact />
                </div>

                {/* Description Bio */}
                <div>
                  <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">الوصف</label>
                  <textarea
                    rows={3}
                    disabled={isSaving}
                    value={bio}
                    onChange={(e) => setBio(e.target.value)}
                    placeholder="اكتب وصفاً ترويجياً قصيراً يظهر للطلاب في صفحة الباقات..."
                    className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition resize-none"
                  />
                </div>

                {/* Additional Details */}
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">معلومات الاتصال</label>
                    <input
                      type="text"
                      disabled={isSaving}
                      value={contactInfo}
                      onChange={(e) => setContactInfo(e.target.value)}
                      placeholder="بريد أو عنوان..."
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">أرقام هواتف المساعدين</label>
                    <input
                      type="text"
                      disabled={isSaving}
                      value={assistantPhoneNumbers}
                      onChange={(e) => setAssistantPhoneNumbers(e.target.value)}
                      placeholder="أرقام هواتف مفصولة بفاصلة..."
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">نسبة العمولة (%)</label>
                    <input
                      type="number"
                      required
                      min={0}
                      max={100}
                      disabled={isSaving}
                      value={commissionRate}
                      onChange={(e) => setCommissionRate(Number(e.target.value))}
                      placeholder="0"
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                    />
                  </div>
                </div>

                {/* Social links */}
                <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 space-y-4">
                  <h4 className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-2 mb-2">
                    <Phone className="h-4 w-4 text-[var(--admin-primary)]" />
                    روابط التواصل الاجتماعي
                  </h4>

                  <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رابط الفيسبوك</label>
                      <input
                        type="url"
                        disabled={isSaving}
                        value={facebookUrl}
                        onChange={(e) => setFacebookUrl(e.target.value)}
                        placeholder="https://facebook.com/..."
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رابط اليوتيوب</label>
                      <input
                        type="url"
                        disabled={isSaving}
                        value={youtubeUrl}
                        onChange={(e) => setYouTubeUrl(e.target.value)}
                        placeholder="https://youtube.com/..."
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رابط التيليجرام</label>
                      <input
                        type="url"
                        disabled={isSaving}
                        value={telegramUrl}
                        onChange={(e) => setTelegramUrl(e.target.value)}
                        placeholder="https://t.me/..."
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                    </div>
                  </div>
                </div>

                {/* Grade levels checkbox checklist */}
                <div>
                  <label className="block text-xs font-bold text-[var(--admin-text)] mb-3">المراحل والصفوف الدراسية التي يدرّسها المعلم *</label>
                  <div className="space-y-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 max-h-60 overflow-y-auto">
                    {GRADE_GROUPS.map((group, gIdx) => (
                      <div key={gIdx} className="space-y-2">
                        <h5 className="text-xs font-black text-[var(--admin-text)] border-b border-[var(--admin-border)]/30 pb-1">{group.label}</h5>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                          {group.grades.map((grade) => {
                            const isChecked = selectedGrades.includes(grade.value);
                            const toggleGrade = () => {
                              setSelectedGrades(prev => 
                                prev.includes(grade.value)
                                  ? prev.filter(v => v !== grade.value)
                                  : [...prev, grade.value]
                              );
                            };
                            return (
                              <div
                                key={grade.value}
                                onClick={() => !isSaving && toggleGrade()}
                                className={`flex items-center gap-3 rounded-xl border p-2.5 cursor-pointer transition select-none ${
                                  isChecked
                                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/5 text-[var(--admin-text)]'
                                    : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                                } ${isSaving ? 'pointer-events-none opacity-60' : ''}`}
                              >
                                <div
                                  className={`flex h-4 w-4 items-center justify-center rounded border transition ${
                                    isChecked
                                      ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                      : 'border-[var(--admin-border)] bg-[var(--admin-bg)]'
                                  }`}
                                >
                                  {isChecked && <Check className="h-3 w-3 stroke-[3]" />}
                                </div>
                                <span className="text-xs font-bold">{grade.label}</span>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Subjects checkboxes */}
                <div>
                  <label className="block text-xs font-bold text-[var(--admin-text)] mb-3">المواد الدراسية التي يدرسها المعلم *</label>
                  {subjects.length === 0 ? (
                    <div className="rounded-2xl border border-dashed border-[var(--admin-border)] p-4 text-center text-xs text-[var(--admin-muted)]">
                      لم يتم إضافة أي مواد دراسية بعد. يرجى تهيئة المواد من قسم إدارة المواد أولاً.
                    </div>
                  ) : (
                    <div className="grid grid-cols-2 gap-3 max-h-40 overflow-y-auto rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                      {subjects.map((sub) => {
                        const isChecked = selectedSubjectIds.includes(sub.id);
                        const toggleSubject = () => {
                          setSelectedSubjectIds(prev => 
                            prev.includes(sub.id)
                              ? prev.filter(id => id !== sub.id)
                              : [...prev, sub.id]
                          );
                        };
                        return (
                          <div
                            key={sub.id}
                            onClick={() => !isSaving && toggleSubject()}
                            className={`flex items-center gap-3 rounded-xl border p-3 cursor-pointer transition select-none ${
                              isChecked
                                ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/5 text-[var(--admin-text)]'
                                : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                            } ${isSaving ? 'pointer-events-none opacity-60' : ''}`}
                          >
                            <div
                              className={`flex h-4 w-4 items-center justify-center rounded border transition ${
                                isChecked
                                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                  : 'border-[var(--admin-border)] bg-[var(--admin-bg)]'
                              }`}
                            >
                              {isChecked && <Check className="h-3 w-3 stroke-[3]" />}
                            </div>
                            <span className="text-xs font-bold">{sub.name}</span>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>

                <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
                  <button
                    type="button"
                    onClick={handleCloseModal}
                    disabled={isSaving}
                    className="flex items-center gap-2 rounded-2xl bg-[var(--admin-surface-low)] px-5 py-2.5 text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-border)] font-bold text-sm cursor-pointer disabled:opacity-50"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={isSaving || isUploadingProfile}
                    className="flex items-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 py-2.5 text-[var(--admin-primary-contrast)] transition-transform hover:-translate-y-0.5 font-bold text-sm shadow-sm cursor-pointer disabled:opacity-50 disabled:translate-y-0"
                  >
                    {isSaving ? (
                      <>
                        <Loader2 size={16} className="animate-spin" />
                        جاري الحفظ...
                      </>
                    ) : (
                      'حفظ التغييرات'
                    )}
                  </button>
                </div>

              </form>
            </div>
          </div>
        )}
      </AnimatePresence>
    </AdminShellChrome>
  );
}
