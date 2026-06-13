'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { AnimatePresence } from 'framer-motion';
import { 
  Plus, 
  Edit, 
  GraduationCap, 
  X, 
  Check, 
  User, 
  Phone, 
  Image as ImageIcon,
  BookOpen,
  Eye,
  EyeOff,
  Activity,
  Database,
  Loader2,
  Lock,
  Send,
  Sparkles,
} from 'lucide-react';
import { 
  AdminShellChrome, 
  AdminDataTable, 
  AdminColumn, 
  AdminStatCard, 
  AdminSearchToolbar, 
  AdminPageSkeleton,
} from '@/components/admin';
import { 
  formatRelativeDate, 
  getInitials 
} from '@/components/admin/admin-utils';
import { teacherService, type TeacherDto, type SubjectDto } from '@/services/teacher-service';
import { adminService, type UserAuditLogDto } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { compressImage, renameFileToMatchBase64 } from '@/utils/image-compressor';

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

const GRADE_NAMES: Record<string, string> = {
  FirstSecondary: 'الأول الثانوي',
  SecondSecondary: 'الثاني الثانوي',
  SecondaryGrade3: 'الثالث الثانوي',
  FirstBaccalaureate: 'الأول بكالوريا',
  SecondBaccalaureate: 'الثاني بكالوريا',
  PrimaryGrade1: 'الأول الابتدائي',
  PrimaryGrade2: 'الثاني الابتدائي',
  PrimaryGrade3: 'الثالث الابتدائي',
  PrimaryGrade4: 'الرابع الابتدائي',
  PrimaryGrade5: 'الخامس الابتدائي',
  PrimaryGrade6: 'السادس الابتدائي',
  PrepGrade1: 'الأول الإعدادي',
  PrepGrade2: 'الثاني الإعدادي',
  PrepGrade3: 'الثالث الإعدادي',
  AzhariPrimary1: 'الأول الابتدائي الأزهري',
  AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري',
  AmericanGrade9: 'Grade 9',
  AmericanGrade10: 'Grade 10',
  AmericanGrade11: 'Grade 11',
  AmericanGrade12: 'Grade 12',
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

// Helper for rendering audit action translations
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
  };
  return map[action] || action;
}

// Helper for parsing json differences in audit logs
function renderChangedValues(oldVal?: string, newVal?: string) {
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
    return null;
  }
}

interface TeacherProfileModalProps {
  open: boolean;
  onClose: () => void;
  teacher: TeacherDto | null;
}

function TeacherProfileModal({ open, onClose, teacher }: TeacherProfileModalProps) {
  const [logs, setLogs] = useState<UserAuditLogDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open && teacher) {
      setLoading(true);
      adminService.getUserAuditLogs(teacher.userId)
        .then(data => {
          setLogs(data || []);
        })
        .catch(err => {
          console.error("Failed to load audit logs", err);
          toast.error("فشل في تحميل سجل النشاطات");
        })
        .finally(() => {
          setLoading(false);
        });
    } else {
      setLogs([]);
    }
  }, [open, teacher]);

  if (!teacher) return null;

  const gradeList = teacher.specialization ? teacher.specialization.split(',') : [];

  return (
    <AnimatePresence>
      {open && (
        <>
          {/* Backdrop */}
          <div
            onClick={onClose}
            className="fixed inset-0 z-[90] bg-black/60 backdrop-blur-sm"
          />

          {/* Modal */}
          <div
            className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6"
            dir="rtl"
            role="dialog"
            aria-modal="true"
            aria-labelledby="teacher-profile-title"
          >
            <div className="flex max-h-[min(880px,calc(100dvh-2rem))] w-full max-w-4xl flex-col overflow-hidden rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-2xl">
              
              {/* Header */}
              <div className="flex shrink-0 items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5">
                <div className="flex items-center gap-4">
                  {teacher.profileImageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={resolveMediaUrl(teacher.profileImageUrl)}
                      alt={teacher.fullName}
                      className="h-12 w-12 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
                    />
                  ) : (
                    <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-lg shadow-sm">
                      {getInitials(teacher.fullName)}
                    </div>
                  )}
                  <div>
                    <h2
                      id="teacher-profile-title"
                      className="text-lg font-black text-[var(--admin-text)] tracking-tight"
                    >
                      {teacher.fullName}
                    </h2>
                    <p className="text-xs text-[var(--admin-muted)] mt-0.5">الملف التعريفي الكامل للمعلم وسجل العمليات</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={onClose}
                  aria-label="إغلاق الملف التعريفي"
                  className="flex h-9 w-9 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)]"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Scrollable Content */}
              <div className="min-h-0 flex-1 overflow-y-auto p-6 space-y-6">
                
                {/* Info Block */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-5 bg-[var(--admin-card)] p-5 rounded-3xl">
                  <div className="flex items-center gap-3">
                    <User className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">رقم الهاتف</p>
                      <p className="text-sm font-bold text-[var(--admin-text)] font-mono">{teacher.phoneNumber}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Phone className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">أرقام هواتف المساعدين</p>
                      <p className="text-sm font-bold text-[var(--admin-text)] font-mono" title={teacher.assistantPhoneNumbers}>
                        {teacher.assistantPhoneNumbers || '—'}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Phone className="h-5 w-5 text-[var(--admin-primary)]" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">معلومات الاتصال المباشر</p>
                      <p className="text-sm font-bold text-[var(--admin-text)] truncate max-w-[200px]" title={teacher.contactInfo}>
                        {teacher.contactInfo || '—'}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Social Media Links */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-5 bg-[var(--admin-card)] p-5 rounded-3xl">
                  <div className="flex items-center gap-3">
                    <FacebookIcon className="h-5 w-5 text-blue-600" />
                    <div>
                      <p className="text-xs text-[var(--admin-muted)]">فيسبوك</p>
                      {teacher.facebookUrl ? (
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
                      {teacher.youtubeUrl ? (
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
                      {teacher.telegramUrl ? (
                        <a href={teacher.telegramUrl} target="_blank" rel="noopener noreferrer" className="text-sm font-bold text-[var(--admin-primary)] hover:underline truncate max-w-[180px] block font-mono">
                          {teacher.telegramUrl}
                        </a>
                      ) : (
                        <p className="text-sm text-[var(--admin-muted)]">—</p>
                      )}
                    </div>
                  </div>
                </div>

                {/* Bio & Subjects Section */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                  <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 md:col-span-1">
                    <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-2">
                      <User className="h-4 w-4 text-[var(--admin-primary)]" />
                      الوصف
                    </h4>
                    <p className="text-sm text-[var(--admin-muted)] leading-relaxed whitespace-pre-wrap">
                      {teacher.bio || 'لا يوجد وصف مسجل حالياً.'}
                    </p>
                  </div>

                  <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 md:col-span-1">
                    <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-2">
                      <GraduationCap className="h-4 w-4 text-[var(--admin-primary)]" />
                      المراحل والصفوف الدراسية
                    </h4>
                    <div className="flex flex-wrap gap-2">
                      {gradeList.length > 0 ? (
                        gradeList.map((val, idx) => (
                          <span
                            key={idx}
                            className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10"
                          >
                            {GRADE_NAMES[val] || val}
                          </span>
                        ))
                      ) : (
                        <p className="text-xs text-red-500 font-bold">غير محدد</p>
                      )}
                    </div>
                  </div>

                  <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 md:col-span-1">
                    <h4 className="text-sm font-black text-[var(--admin-text)] mb-3 flex items-center gap-2">
                      <BookOpen className="h-4 w-4 text-[var(--admin-primary)]" />
                      المواد الدراسية التابعة له
                    </h4>
                    <div className="flex flex-wrap gap-2">
                      {teacher.subjectNames && teacher.subjectNames.length > 0 ? (
                        teacher.subjectNames.map((name, idx) => (
                          <span
                            key={idx}
                            className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10"
                          >
                            <BookOpen className="h-3 w-3" />
                            {name}
                          </span>
                        ))
                      ) : (
                        <p className="text-xs text-red-500 font-bold">لم يتم تعيين أي مادة بعد</p>
                      )}
                    </div>
                  </div>
                </div>

                {/* Audit logs Timeline Section */}
                <div>
                  <h3 className="text-sm font-black text-[var(--admin-text)] mb-4 flex items-center gap-2">
                    <Activity className="h-4 w-4 text-[var(--admin-primary)]" />
                    سجل نشاطات المعلم
                  </h3>

                  {loading ? (
                    <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] gap-3">
                      <Loader2 className="h-8 w-8 animate-spin text-[var(--admin-primary)]" />
                      <p className="text-sm font-bold">جاري تحميل سجل النشاطات...</p>
                    </div>
                  ) : logs.length === 0 ? (
                    <div className="flex flex-col items-center justify-center py-20 text-[var(--admin-muted)] bg-[var(--admin-card)]/50 rounded-3xl">
                      <Database className="h-10 w-10 mb-3 opacity-40 text-[var(--admin-primary)]" />
                      <p className="text-sm font-bold">لا يوجد أي نشاطات مسجلة لهذا الحساب حالياً.</p>
                    </div>
                  ) : (
                    <div className="relative border-r border-[var(--admin-border)]/60 mr-3 pr-6 space-y-6">
                      {logs.map((log) => (
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
                  )}
                </div>

              </div>

              {/* Footer */}
              <div className="shrink-0 border-t border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-4">
                <button
                  type="button"
                  onClick={onClose}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                >
                  إغلاق
                </button>
              </div>

            </div>
          </div>
        </>
      )}
    </AnimatePresence>
  );
}

export default function AdminTeachersPageClient() {
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const router = useRouter();
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTeacher, setEditingTeacher] = useState<TeacherDto | null>(null);
  const [selectedTeacherProfile, setSelectedTeacherProfile] = useState<TeacherDto | null>(null);

  // Direct onboarding form states
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  // Form states
  const [bio, setBio] = useState('');
  const [contactInfo, setContactInfo] = useState('');
  const [profileImageUrl, setProfileImageUrl] = useState('');
  const [selectedSubjectIds, setSelectedSubjectIds] = useState<string[]>([]);
  const [selectedGrades, setSelectedGrades] = useState<string[]>([]);
  const [isSaving, setIsSaving] = useState(false);

  // New fields states
  const [assistantPhoneNumbers, setAssistantPhoneNumbers] = useState('');
  const [facebookUrl, setFacebookUrl] = useState('');
  const [youtubeUrl, setYouTubeUrl] = useState('');
  const [telegramUrl, setTelegramUrl] = useState('');

  // Upload previews and loading states
  const [profileImagePreview, setProfileImagePreview] = useState<string | null>(null);
  const [isUploadingProfile, setIsUploadingProfile] = useState(false);
  const [aiPhotoPreview, setAiPhotoPreview] = useState<string | null>(null);
  const [isUploadingAi, setIsUploadingAi] = useState(false);

  // Pending uploads for create mode
  const [pendingProfileImage, setPendingProfileImage] = useState<{ base64: string; name: string } | null>(null);
  const [pendingAiPhoto, setPendingAiPhoto] = useState<{ base64: string; name: string } | null>(null);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      const [teachersRes, subjectsRes] = await Promise.all([
        teacherService.getTeachers(),
        teacherService.getSubjects(),
      ]);

      if (teachersRes.success) {
        setTeachers(teachersRes.data || []);
      }
      if (subjectsRes.success) {
        setSubjects(subjectsRes.data || []);
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء تحميل البيانات');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleOpenModal = (teacher: TeacherDto | null = null) => {
    if (teacher) {
      setEditingTeacher(teacher);
      setFullName(teacher.fullName);
      setPhoneNumber(teacher.phoneNumber);
      setPassword('');
      setBio(teacher.bio || '');
      setContactInfo(teacher.contactInfo || '');
      setProfileImageUrl(teacher.profileImageUrl || '');
      setSelectedSubjectIds(teacher.subjectIds || []);
      setSelectedGrades(teacher.specialization ? teacher.specialization.split(',') : []);
      setAssistantPhoneNumbers(teacher.assistantPhoneNumbers || '');
      setFacebookUrl(teacher.facebookUrl || '');
      setYouTubeUrl(teacher.youtubeUrl || '');
      setTelegramUrl(teacher.telegramUrl || '');
      setProfileImagePreview(teacher.profileImageUrl || null);
      setAiPhotoPreview(null);
      setPendingProfileImage(null);
      setPendingAiPhoto(null);
    } else {
      setEditingTeacher(null);
      setFullName('');
      setPhoneNumber('');
      setPassword('');
      setBio('');
      setContactInfo('');
      setProfileImageUrl('');
      setSelectedSubjectIds([]);
      setSelectedGrades([]);
      setAssistantPhoneNumbers('');
      setFacebookUrl('');
      setYouTubeUrl('');
      setTelegramUrl('');
      setProfileImagePreview(null);
      setAiPhotoPreview(null);
      setPendingProfileImage(null);
      setPendingAiPhoto(null);
    }
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setEditingTeacher(null);
    setFullName('');
    setPhoneNumber('');
    setPassword('');
    setBio('');
    setContactInfo('');
    setProfileImageUrl('');
    setSelectedSubjectIds([]);
    setSelectedGrades([]);
    setAssistantPhoneNumbers('');
    setFacebookUrl('');
    setYouTubeUrl('');
    setTelegramUrl('');
    setProfileImagePreview(null);
    setAiPhotoPreview(null);
    setPendingProfileImage(null);
    setPendingAiPhoto(null);
  };

  const handleSubjectToggle = (subjectId: string) => {
    setSelectedSubjectIds((prev) =>
      prev.includes(subjectId)
        ? prev.filter((id) => id !== subjectId)
        : [...prev, subjectId]
    );
  };

  const validate = (): boolean => {
    if (!fullName.trim()) {
      toast.error('الاسم الكامل مطلوب');
      return false;
    }
    if (fullName.trim().split(/\s+/).length < 2) {
      toast.error('الاسم يجب أن يكون كلمتين على الأقل');
      return false;
    }
    if (!phoneNumber.trim()) {
      toast.error('رقم الهاتف مطلوب');
      return false;
    }
    if (!/^01[0125]\d{8}$/.test(phoneNumber.trim())) {
      toast.error('رقم الهاتف يجب أن يكون رقم مصري صحيح (01x xxxxxxxx)');
      return false;
    }
    if (!editingTeacher && (!password || password.length < 6)) {
      toast.error('كلمة السر مطلوبة ويجب أن تكون 6 أحرف على الأقل');
      return false;
    }
    if (!contactInfo.trim()) {
      toast.error('معلومات الاتصال / البريد الإلكتروني مطلوبة');
      return false;
    }
    if (selectedGrades.length === 0) {
      toast.error('يرجى تحديد صف دراسي واحد على الأقل يدرّسه المعلم');
      return false;
    }
    if (selectedSubjectIds.length === 0) {
      toast.error('يرجى تحديد مادة دراسية واحدة على الأقل');
      return false;
    }
    return true;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    setIsSaving(true);
    try {
      const gradesString = selectedGrades.join(',');

      if (editingTeacher) {
        // 1. Edit existing teacher profile (User edits are read-only here)
        const res = await teacherService.updateTeacher(editingTeacher.id, {
          bio: bio.trim(),
          specialization: gradesString, // Store selected grades as specialization
          commissionRate: 0, 
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
          handleCloseModal();
          loadData();
        } else {
          toast.error(res.message || 'فشل في تحديث ملف المعلم');
        }
      } else {
        // 2. Direct onboarding: create User account first, then create Teacher Profile
        const userPayload = {
          fullName: fullName.trim(),
          phoneNumber: phoneNumber.trim(),
          password,
          role: 'Teacher',
        };

        const userRes = await adminService.createUser(userPayload);
        if (userRes && userRes.success && userRes.data?.id) {
          const userId = userRes.data.id;
          
          const teacherRes = await teacherService.createTeacher({
            userId,
            bio: bio.trim(),
            specialization: gradesString, // Store selected grades as specialization
            commissionRate: 0, 
            contactInfo: contactInfo.trim(),
            profileImageUrl: profileImageUrl.trim() || undefined,
            subjectIds: selectedSubjectIds,
            assistantPhoneNumbers: assistantPhoneNumbers.trim() || undefined,
            facebookUrl: facebookUrl.trim() || undefined,
            youtubeUrl: youtubeUrl.trim() || undefined,
            telegramUrl: telegramUrl.trim() || undefined,
          });

          if (teacherRes.success) {
            // Sequential uploads for pending images in Create Mode
            if (pendingProfileImage) {
              try {
                const uploadProfileRes = await adminService.uploadTeacherProfileImage(userId, pendingProfileImage.base64, pendingProfileImage.name);
                if (!uploadProfileRes.success) {
                  toast.error('فشل في رفع الصورة الشخصية المحددة');
                }
              } catch (e) {
                console.error('Error uploading profile image during create:', e);
              }
            }

            if (pendingAiPhoto) {
              try {
                const uploadAiRes = await adminService.uploadTeacherPhoto(userId, pendingAiPhoto.base64, pendingAiPhoto.name);
                if (!uploadAiRes.success) {
                  toast.error('فشل في رفع صورة الـ AI المحددة');
                }
              } catch (e) {
                console.error('Error uploading AI photo during create:', e);
              }
            }

            toast.success('تم إنشاء حساب المعلم وتهيئة ملفه الأكاديمي بنجاح ✅');
            handleCloseModal();
            loadData();
          } else {
            toast.error(teacherRes.message || 'فشل في تهيئة الملف التعريفي للمعلم');
          }
        } else {
          toast.error(userRes?.message || 'فشل في إنشاء حساب مستخدم المعلم');
        }
      }
    } catch (err: any) {
      console.error(err);
      const errMsg = err?.response?.data?.message || 'حدث خطأ أثناء حفظ البيانات';
      toast.error(errMsg);
    } finally {
      setIsSaving(false);
    }
  };

  const filteredTeachers = teachers.filter(
    (t) =>
      t.fullName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.specialization.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const columns: AdminColumn<TeacherDto>[] = [
    {
      key: 'teacher',
      label: 'المعلم',
      render: (t) => (
        <div className="flex items-center gap-4">
          {t.profileImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={resolveMediaUrl(t.profileImageUrl)}
              alt={t.fullName}
              className="h-12 w-12 rounded-full object-cover border border-[var(--admin-border)] shadow-sm"
            />
          ) : (
            <div className="flex h-12 w-12 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-primary-15)] font-bold text-[var(--admin-primary)] shadow-sm">
              {getInitials(t.fullName)}
            </div>
          )}
          <div>
            <div className="font-bold text-[var(--admin-text)]">
              {t.fullName}
            </div>
            <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono tracking-wider">
              {t.phoneNumber}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: 'grades',
      label: 'المراحل والصفوف الدراسية',
      render: (t) => {
        const gradeList = t.specialization ? t.specialization.split(',') : [];
        return (
          <div className="flex flex-wrap gap-1 max-w-[250px]">
            {gradeList.length > 0 ? (
              gradeList.map((val, i) => (
                <span
                  key={i}
                  className="inline-flex items-center rounded-full bg-[var(--admin-primary-15)] px-2.5 py-0.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10"
                >
                  {GRADE_NAMES[val] || val}
                </span>
              ))
            ) : (
              <span className="text-xs font-bold text-red-500">غير محدد</span>
            )}
          </div>
        );
      },
    },
    {
      key: 'subjects',
      label: 'المواد الدراسية',
      render: (t) => (
        <div className="flex flex-wrap gap-1 max-w-[200px]">
          {t.subjectNames && t.subjectNames.length > 0 ? (
            t.subjectNames.map((name, i) => (
              <span
                key={i}
                className="inline-flex items-center gap-1 rounded-full bg-[var(--admin-primary-15)] px-2 py-0.5 text-xs font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10"
              >
                {name}
              </span>
            ))
          ) : (
            <span className="text-xs font-bold text-red-500">لا يوجد مواد</span>
          )}
        </div>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (t) => (
        <div className="flex items-center justify-end gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
          <NeumorphButton
            type="button"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              router.push(`/admin/teachers/${t.id}`);
            }}
            intent="primary"
            size="icon"
            title="الملف التعريفي الكامل"
          >
            <User className="h-5 w-5" />
          </NeumorphButton>
          <NeumorphButton
            type="button"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              handleOpenModal(t);
            }}
            intent="primary"
            size="icon"
            title="تعديل ملف المعلم"
          >
            <Edit className="h-5 w-5" />
          </NeumorphButton>
        </div>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/teachers"
      sectionLabel="المستخدمين"
      pageTitle="المعلمين"
      subtitle="إدارة الحسابات التعليمية المعتمدة وتعيين المراحل والصفوف الدراسية والمواد التابعة لهم."
      action={
        <NeumorphButton
          intent="primary"
          size="lg"
          pill
          onClick={() => handleOpenModal()}
        >
          <Plus className="h-4 w-4" />
          إضافة معلم جديد
        </NeumorphButton>
      }
    >
      {/* Stats Strip */}
      <section className="mb-8 grid grid-cols-1 gap-6 md:grid-cols-2">
        <AdminStatCard
          variant="light"
          icon={GraduationCap}
          label="إجمالي المعلمين"
          value={teachers.length}
          subtitle="إجمالي معلمي المنصة المسجلين"
        />
        <AdminStatCard
          variant="accent"
          icon={BookOpen}
          label="المواد النشطة"
          value={subjects.length}
          subtitle="إجمالي المواد الأكاديمية المهيأة"
        />
      </section>

      {/* Search Bar */}
      <AdminSearchToolbar
        value={searchQuery}
        onChange={setSearchQuery}
        placeholder="ابحث عن معلم بالاسم أو التخصص..."
      />

      {isLoading ? (
        <AdminPageSkeleton />
      ) : (
        <AdminDataTable
          data={filteredTeachers}
          columns={columns}
          loading={isLoading}
          rowKey={(t) => t.id}
          emptyMessage="لا توجد نتائج مطابقة لفلترة المعلمين."
          onRowClick={(t) => {
            router.push(`/admin/teachers/${t.id}`);
          }}
        />
      )}

      {/* Onboarding / Edit Drawer Modal */}
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
                {editingTeacher ? 'تعديل ملف المعلم' : 'إضافة معلم جديد'}
              </h2>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">
                {editingTeacher 
                  ? 'قم بتعديل تخصص المعلم ومعلومات التواصل والمواد الدراسية المرتبطة بملفه.' 
                  : 'أدخل بيانات المعلم لإنشاء حساب مستخدم وتهيئة ملفه الأكاديمي مباشرة.'}
              </p>

              <form onSubmit={handleSave} className="mt-6 space-y-6">
                
                {/* Account Details (Create Mode: Input, Edit Mode: Readonly) */}
                <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 space-y-4">
                  <h4 className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-2 mb-2">
                    <User className="h-4 w-4 text-[var(--admin-primary)]" />
                    بيانات حساب الدخول للمنصة
                  </h4>

                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">الاسم الكامل *</label>
                      <input
                        type="text"
                        required
                        disabled={!!editingTeacher || isSaving}
                        value={fullName}
                        onChange={(e) => setFullName(e.target.value)}
                        placeholder="أحمد محمد علي"
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">رقم الهاتف *</label>
                      <div className="relative">
                        <input
                          type="tel"
                          required
                          maxLength={11}
                          disabled={!!editingTeacher || isSaving}
                          value={phoneNumber}
                          onChange={(e) => setPhoneNumber(e.target.value.replace(/\D/g, ''))}
                          placeholder="01xxxxxxxxx"
                          dir="ltr"
                          className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                        />
                        <Phone className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                  </div>

                  {!editingTeacher && (
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">كلمة السر *</label>
                      <div className="relative">
                        <input
                          type={showPassword ? 'text' : 'password'}
                          required
                          disabled={isSaving}
                          value={password}
                          onChange={(e) => setPassword(e.target.value)}
                          placeholder="6 أحرف على الأقل"
                          className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-12 pr-4 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                        />
                        <button
                          type="button"
                          onClick={() => setShowPassword(!showPassword)}
                          className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--admin-muted)] transition hover:text-[var(--admin-text)]"
                        >
                          {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                        </button>
                      </div>
                    </div>
                  )}
                  {editingTeacher && (
                    <p className="text-xs text-[var(--admin-muted)] flex items-center gap-1.5 mt-1.5">
                      <Lock className="h-3 w-3" />
                      بيانات تسجيل الدخول مدارة من قبل قسم شؤون المستخدمين ولا يمكن تعديلها من هنا.
                    </p>
                  )}
                </div>

                {/* Profile Details */}
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">معلومات الاتصال المباشر للطلاب *</label>
                    <div className="relative">
                      <input
                        type="text"
                        required
                        disabled={isSaving}
                        value={contactInfo}
                        onChange={(e) => setContactInfo(e.target.value)}
                        placeholder="رقم هاتف إضافي، حساب الدعم الفني، إلخ..."
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                      <Phone className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-[var(--admin-text)] mb-2">أرقام هواتف المساعدين (اختياري)</label>
                    <div className="relative">
                      <input
                        type="text"
                        disabled={isSaving}
                        value={assistantPhoneNumbers}
                        onChange={(e) => setAssistantPhoneNumbers(e.target.value)}
                        placeholder="01xxxxxxxxx, 01xxxxxxxxx"
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                      <Phone className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                    </div>
                  </div>
                </div>

                {/* Social Media Urls */}
                <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 space-y-4">
                  <h4 className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-2 mb-2">
                    <Send className="h-4 w-4 text-[var(--admin-primary)]" />
                    روابط السوشيال ميديا (اختياري)
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
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
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
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
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
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-xs text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none focus:border-[var(--admin-primary)] disabled:opacity-60 transition"
                      />
                    </div>
                  </div>
                </div>

                {/* Images Upload Area */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5">
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
                            if (editingTeacher) {
                              const res = await adminService.uploadTeacherProfileImage(editingTeacher.id, base64, finalFileName);
                              if (res.success && res.data) {
                                setProfileImageUrl(res.data);
                                toast.success('تم رفع الصورة الشخصية بنجاح ✅');
                                loadData();
                              } else {
                                toast.error(res.message || 'فشل رفع الصورة الشخصية');
                              }
                            } else {
                              setPendingProfileImage({ base64, name: finalFileName });
                              toast.success('تم اختيار الصورة الشخصية بنجاح (سيتم حفظها عند إرسال النموذج) 📸');
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
                          {getInitials(fullName || 'معلم')}
                        </div>
                      )}
                      <span className="text-xs text-[var(--admin-muted)] mt-2">
                        {isUploadingProfile ? 'جاري الرفع...' : 'اسحب صورة أو انقر للرفع'}
                      </span>
                    </div>
                  </div>

                  {/* AI Photo Upload */}
                  <div className="space-y-3">
                    <label className="block text-xs font-bold text-[var(--admin-text)] flex items-center gap-2">
                      <Sparkles className="h-4 w-4 text-[var(--admin-primary)]" />
                      صورة التحليل للذكاء الاصطناعي (AI)
                    </label>
                    <div className="flex flex-col items-center justify-center border-2 border-dashed border-[var(--admin-border)] rounded-2xl p-4 bg-[var(--admin-bg)] hover:border-[var(--admin-primary)] transition relative">
                      <input
                        type="file"
                        accept="image/*"
                        className="absolute inset-0 opacity-0 cursor-pointer"
                        disabled={isUploadingAi}
                        onChange={async (e) => {
                          const file = e.target.files?.[0];
                          if (!file) return;
                          setIsUploadingAi(true);
                          try {
                            const base64 = await compressImage(file);
                            const finalFileName = renameFileToMatchBase64(file.name, base64);
                            setAiPhotoPreview(base64);
                            if (editingTeacher) {
                              const res = await adminService.uploadTeacherPhoto(editingTeacher.userId, base64, finalFileName);
                              if (res.success) {
                                toast.success('تم رفع صورة تحليل الـ AI بنجاح ✅');
                              } else {
                                toast.error(res.message || 'فشل رفع صورة تحليل الـ AI');
                              }
                            } else {
                              setPendingAiPhoto({ base64, name: finalFileName });
                              toast.success('تم اختيار صورة تحليل الـ AI بنجاح (سيتم حفظها عند إرسال النموذج) 🤖');
                            }
                          } catch (err) {
                            console.error(err);
                            toast.error('حدث خطأ أثناء معالجة ورفع صورة التحليل');
                          } finally {
                            setIsUploadingAi(false);
                          }
                        }}
                      />
                      {aiPhotoPreview ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={resolveMediaUrl(aiPhotoPreview)}
                          alt="AI Preview"
                          className="h-24 w-24 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
                        />
                      ) : (
                        <div className="flex h-24 w-24 items-center justify-center rounded-2xl bg-[var(--admin-hover)] text-[var(--admin-muted)]">
                          <Sparkles className="h-8 w-8" />
                        </div>
                      )}
                      <span className="text-xs text-[var(--admin-muted)] mt-2">
                        {isUploadingAi ? 'جاري الرفع...' : 'اسحب صورة أو انقر للرفع'}
                      </span>
                    </div>
                  </div>
                </div>

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
                        return (
                          <div
                            key={sub.id}
                            onClick={() => !isSaving && handleSubjectToggle(sub.id)}
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
                  <NeumorphButton
                    type="button"
                    onClick={handleCloseModal}
                    disabled={isSaving}
                    intent="primary"
                    size="md"
                    pill
                  >
                    إلغاء
                  </NeumorphButton>
                  <NeumorphButton
                    type="submit"
                    disabled={isSaving}
                    intent="primary"
                    size="md"
                    pill
                  >
                    {isSaving && <Loader2 className="h-4 w-4 animate-spin" />}
                    حفظ البيانات
                  </NeumorphButton>
                </div>
              </form>
            </div>
          </div>
        )}
      </AnimatePresence>

      {/* Teacher Profile details modal */}
      <TeacherProfileModal
        open={!!selectedTeacherProfile}
        onClose={() => setSelectedTeacherProfile(null)}
        teacher={selectedTeacherProfile}
      />
    </AdminShellChrome>
  );
}
