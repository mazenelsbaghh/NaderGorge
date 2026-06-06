'use client';

import { devConsole } from '@/utils/dev-console';
import { useCallback, useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import { AdminShellChrome, AdminTabBar, AdminTab, AdminStatCard, AdminModal, AdminDataTable } from '@/components/admin';
import { adminService, type StudentPackageDto, type StudentProfileExtendedDto } from '@/services/admin-service';
import { Users, FileText, MonitorPlay, MonitorUp, Power, Video, Clock3, MapPin, GraduationCap, UsersRound, Wallet, Package, PenLine, DollarSign, KeyRound, StickyNote, Trash2, Pin, ChevronDown, ChevronRight, Lock, Unlock, BookOpen } from 'lucide-react';
import toast from 'react-hot-toast';
import { Checkbox } from '@/components/ui/checkbox';

export default function AdminStudentProfile({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<'overview' | 'academic' | 'devices' | 'financials' | 'overrides' | 'audit' | 'notes'>('overview');
  const [loading, setLoading] = useState(true);
  const [studentData, setStudentData] = useState<StudentProfileExtendedDto | null>(null);
  const [modalOpen, setModalOpen] = useState<'none'|'override'|'disconnect'|'gamification'|'status'|'watchCount'|'balance'|'editProfile'|'password'|'cancelPackage'>('none');
  const [overrideInput, setOverrideInput] = useState({ videoId: '', addedViews: 1, reason: '' });
  const [gamificationInput, setGamificationInput] = useState({ points: 10, reason: '' });
  const [watchCountEdit, setWatchCountEdit] = useState({ lessonVideoId: '', videoTitle: '', currentCount: 0, newCount: 0, maxCount: 0 });
  const [balanceInput, setBalanceInput] = useState({ amount: 0, reason: '' });
  const [editFields, setEditFields] = useState<Record<string, string | boolean | null>>({});
  const [passwordInput, setPasswordInput] = useState('');
  const [noteInput, setNoteInput] = useState({ content: '', isPinned: false });
  const [suspensionReasonInput, setSuspensionReasonInput] = useState('');
  const [selectedPackageForCancel, setSelectedPackageForCancel] = useState<{
    accessGrantId: string;
    name: string;
    purchaseMethod: string;
    price: number;
  } | null>(null);
  const [refundBalanceOption, setRefundBalanceOption] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [expandedPackages, setExpandedPackages] = useState<Record<string, boolean>>({});
  const [expandedTerms, setExpandedTerms] = useState<Record<string, boolean>>({});
  const [expandedLessons, setExpandedLessons] = useState<Record<string, boolean>>({});

  const formatDuration = (seconds: number) => {
    if (!seconds) return '0 دقيقة';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;

    if (hours > 0) {
      return `${hours}س ${minutes}د`;
    }

    if (minutes > 0) {
      return `${minutes}د ${remainingSeconds}ث`;
    }

    return `${remainingSeconds}ث`;
  };

  const TABS: AdminTab<'overview' | 'academic' | 'devices' | 'financials' | 'overrides' | 'audit' | 'notes'>[] = [
     { key: 'overview', label: 'نظرة عامة' },
     { key: 'academic', label: 'الأكاديمية' },
     { key: 'devices', label: 'الأجهزة والجلسات' },
     { key: 'overrides', label: 'التجاوزات' },
     { key: 'financials', label: 'الماليات' },
     { key: 'notes', label: 'الملاحظات' },
     { key: 'audit', label: 'سجل النشاط' }
  ];

  const fetchStudent = useCallback(() => {
      setLoading(true);
      adminService.getStudentProfile(id).then(res => {
          setStudentData(res);
          setLoading(false);
      }).catch(err => {
          devConsole.error("Failed to load student", err);
          setLoading(false);
      });
  }, [id]);

  useEffect(() => {
    fetchStudent();
  }, [fetchStudent]);

  const handleOverrideSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    try {
        await adminService.overrideVideoLimit(id, overrideInput.videoId, overrideInput.addedViews, overrideInput.reason);
        setModalOpen('none');
        toast.success('تمت الإضافة بنجاح');
        fetchStudent();
    } catch {
        toast.error('فشل التجاوز');
    } finally {
        setSubmitting(false);
    }
  };

  const handleDisconnectAll = async () => {
    if (submitting) return;
    setSubmitting(true);
    try {
        await adminService.disconnectAllDevices(id);
        toast.success('تم الفصل بنجاح');
        fetchStudent();
    } catch {
        toast.error('فشل الفصل');
    } finally {
        setSubmitting(false);
    }
  };

  const handleGamificationSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    try {
        await adminService.adjustGamification(id, gamificationInput.points, gamificationInput.reason);
        setModalOpen('none');
        toast.success('تم التعديل بنجاح');
        fetchStudent();
    } catch {
        toast.error('فشل تعديل النقاط');
    } finally {
        setSubmitting(false);
    }
  };

  const toggleStatusDirect = async (isActive: boolean, reason: string) => {
    if (submitting) return;
    setSubmitting(true);
    try {
        await adminService.toggleStudentStatus(id, isActive, reason);
        fetchStudent();
        setModalOpen('none');
        toast.success(isActive ? 'تم تفعيل الحساب بنجاح' : 'تم إيقاف الحساب بنجاح');
    } catch {
        toast.error('فشل تغيير الحالة');
    } finally {
        setSubmitting(false);
    }
  };

  const handleStatusToggleClick = () => {
    if (studentData?.isActive) {
      setSuspensionReasonInput('');
      setModalOpen('status');
    } else {
      void toggleStatusDirect(true, '');
    }
  };

  const handleOpenCancelPackageModal = (p: StudentPackageDto) => {
    setSelectedPackageForCancel({
      accessGrantId: p.accessGrantId,
      name: p.name,
      purchaseMethod: p.purchaseMethod,
      price: p.price
    });
    setRefundBalanceOption(p.purchaseMethod === 'Balance');
    setModalOpen('cancelPackage');
  };

  const handleCancelPackageConfirm = async () => {
    if (!selectedPackageForCancel || submitting) return;
    setSubmitting(true);
    try {
      await adminService.cancelStudentPackage(
        id,
        selectedPackageForCancel.accessGrantId,
        refundBalanceOption
      );
      toast.success('تم إلغاء الاشتراك في الباقة بنجاح');
      setModalOpen('none');
      setSelectedPackageForCancel(null);
      fetchStudent();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'فشل إلغاء الاشتراك');
    } finally {
      setSubmitting(false);
    }
  };

  const handleWatchCountSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    try {
      await adminService.setWatchCount(watchCountEdit.lessonVideoId, id, watchCountEdit.newCount);
      toast.success(`تم تعديل المشاهدات من ${watchCountEdit.currentCount} إلى ${watchCountEdit.newCount}`);
      setModalOpen('none');
      fetchStudent();
    } catch {
      toast.error('فشل تعديل المشاهدات');
    } finally {
      setSubmitting(false);
    }
  };

  const handleBalanceSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    try {
      await adminService.adjustBalance(id, balanceInput.amount, balanceInput.reason);
      toast.success(`تم تعديل الرصيد بمقدار ${balanceInput.amount >= 0 ? '+' : ''}${balanceInput.amount} ج.م`);
      setModalOpen('none');
      fetchStudent();
    } catch {
      toast.error('فشل تعديل الرصيد');
    } finally {
      setSubmitting(false);
    }
  };

  const handleEditProfileSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    try {
      await adminService.updateStudentProfile(id, editFields);
      toast.success('تم تحديث البيانات');
      setModalOpen('none');
      fetchStudent();
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

  // ── Arabic enum label mappers ─────────────────────────────────────
  const mapGender = (g?: string) => {
    if (!g) return 'غير متوفر';
    const m: Record<string, string> = { Male: 'ذكر', Female: 'أنثى' };
    return m[g] || g;
  };

  const mapSchoolType = (t?: string) => {
    if (!t) return 'غير متوفر';
    const m: Record<string, string> = {
      Government: 'حكومية', Language: 'لغات', Experimental: 'تجريبية',
      Private: 'خاصة', Azhari: 'أزهرية', American: 'أمريكية',
    };
    return m[t] || t;
  };

  const mapEducationStage = (s?: string) => {
    if (!s) return 'غير متوفر';
    const m: Record<string, string> = { Secondary: 'ثانوية', Baccalaureate: 'بكالوريا' };
    return m[s] || s;
  };

  const mapGradeLevel = (g?: string) => {
    if (!g) return 'غير متوفر';
    const m: Record<string, string> = {
      FirstSecondary: 'أولى ثانوي', SecondSecondary: 'ثانية ثانوي',
      FirstBaccalaureate: 'أولى بكالوريا', SecondBaccalaureate: 'ثانية بكالوريا',
    };
    return m[g] || g;
  };

  const mapStudyTrack = (t?: string) => {
    if (!t) return 'لا ينطبق';
    const m: Record<string, string> = {
      Science: 'علمي', Arts: 'أدبي',
      MedicineAndLifeSciences: 'الطب وعلوم الحياة',
      EngineeringAndComputerScience: 'الهندسة وعلوم الحاسب',
      Business: 'قطاع الأعمال', ArtsAndHumanities: 'الآداب والفنون',
    };
    return m[t] || t;
  };

  const formatDate = (d?: string | null) => {
    if (!d) return 'غير متوفر';
    return new Date(d).toLocaleDateString('en-GB');
  };

  return (
    <AdminShellChrome
       activePath="/admin/users"
       sectionLabel="إدارة المستخدمين"
       pageTitle="ملف الطالب الشامل"
       subtitle="تفاصيل شاملة للمنهج، الأجهزة، والماليات"
       action={
          <div className="flex gap-4">
            <button 
              onClick={() => {
                setEditFields({
                  fullName: studentData?.fullName || '',
                  phone: studentData?.phone || '',
                  parentPhone: studentData?.parentPhone || '',
                  secondaryPhone: studentData?.secondaryPhone || '',
                  motherPhone: studentData?.motherPhone || '',
                  governorate: studentData?.governorate || '',
                  district: studentData?.district || '',
                  address: studentData?.address || '',
                  schoolName: studentData?.schoolName || '',
                  dateOfBirth: studentData?.dateOfBirth ? new Date(studentData.dateOfBirth).toISOString().split('T')[0] : '',
                  gender: studentData?.gender || '',
                  educationStage: studentData?.educationStage || '',
                  gradeLevel: studentData?.grade || '',
                  studyTrack: studentData?.studyTrack || '',
                  schoolType: studentData?.schoolType || '',
                  isFatherAlive: studentData?.isFatherAlive ?? true,
                  isMotherAlive: studentData?.isMotherAlive ?? true,
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
              onClick={handleStatusToggleClick}
              className={`flex items-center gap-2 rounded-2xl px-4 py-2 font-medium transition-colors 
                 ${studentData?.isActive ? 'bg-red-500/10 text-red-500 hover:bg-red-500/20' : 'bg-green-500/10 text-green-500 hover:bg-green-500/20'}`}
            >
               <Power size={20} />
               {studentData?.isActive ? 'إيقاف الحساب' : 'تفعيل الحساب'}
            </button>
            <button 
               onClick={() => router.push('/admin/users')}
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
         {activeTab === 'overview' && (
            <div className="flex flex-col gap-8">
               <div className="flex justify-between items-center mb-[-0.5rem]">
                  <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">ملخص الإحصاءات</h3>
                  <button onClick={() => setModalOpen('gamification')} className="text-sm font-bold text-[var(--admin-primary)] bg-[var(--admin-card-strong)] px-4 py-2 flex items-center gap-2 rounded-xl hover:bg-[var(--admin-hover)] transition-colors">
                     تعديل نقاط الطالب
                  </button>
               </div>
               <div className="grid grid-cols-1 gap-6 md:grid-cols-4">
                  <AdminStatCard variant="accent" icon={Users} label="إجمالي النقاط" value={studentData?.gamification?.totalPoints || 0} />
                  <AdminStatCard variant="light" icon={MonitorUp} label="أجهزة مسجلة" value={studentData?.devices?.length || 0} />
                  <AdminStatCard variant="muted" icon={FileText} label="باقات نشطة" value={studentData?.packages?.length || 0} />
                  <AdminStatCard variant="accent" icon={MonitorPlay} label="تجاوزات نشطة" value={studentData?.overrides?.length || 0} />
               </div>
               
               {/* ── Section 1: البيانات الشخصية ── */}
               <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
                   <div className="flex items-center gap-3 mb-5">
                     <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                       <Users size={20} />
                     </div>
                     <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                       البيانات الشخصية
                     </h3>
                   </div>
                   {loading ? (
                       <div className="text-[var(--admin-muted)]">جاري التحميل...</div>
                   ) : (
                       <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">الاسم بالكامل</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.fullName || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">رقم الهاتف</p>
                               <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.phone || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">هاتف إضافي</p>
                               <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.secondaryPhone || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ الميلاد</p>
                               <p className="text-[var(--admin-text)] font-semibold">{formatDate(studentData?.dateOfBirth)}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">النوع</p>
                               <p className="text-[var(--admin-text)] font-semibold">{mapGender(studentData?.gender)}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">الجنسية</p>
                               <p className="text-[var(--admin-text)] font-semibold">{studentData?.nationality || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">كود الطالب</p>
                               <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.studentCode || 'غير متوفر'}</p>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">حالة الملف</p>
                               <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${studentData?.isProfileComplete ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400' : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400'}`}>
                                 <span className="h-1.5 w-1.5 rounded-full bg-current" />
                                 {studentData?.isProfileComplete ? 'مكتمل' : 'غير مكتمل'}
                               </span>
                           </div>
                           <div>
                               <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ الإنضمام</p>
                               <p className="text-[var(--admin-text)] font-semibold">{formatDate(studentData?.createdAt)}</p>
                           </div>
                       </div>
                   )}
               </div>

               {/* ── Section 2: بيانات الوالدين ── */}
               <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
                   <div className="flex items-center gap-3 mb-5">
                     <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                       <UsersRound size={20} />
                     </div>
                     <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                       بيانات الوالدين
                     </h3>
                   </div>
                   <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">هاتف ولي الأمر (أب)</p>
                           <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.parentPhone || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">هاتف الأم</p>
                           <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.motherPhone || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">هاتف ولي أمر إضافي</p>
                           <p className="text-[var(--admin-text)] font-semibold font-mono">{studentData?.secondaryParentPhone || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">حالة الأب</p>
                           <span className={`text-sm font-bold ${studentData?.isFatherAlive === false ? 'text-red-500' : 'text-emerald-500'}`}>
                             {studentData?.isFatherAlive === false ? 'متوفى' : 'على قيد الحياة'}
                           </span>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">حالة الأم</p>
                           <span className={`text-sm font-bold ${studentData?.isMotherAlive === false ? 'text-red-500' : 'text-emerald-500'}`}>
                             {studentData?.isMotherAlive === false ? 'متوفاة' : 'على قيد الحياة'}
                           </span>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ ميلاد الأب</p>
                           <p className="text-[var(--admin-text)] font-semibold">{formatDate(studentData?.fatherDateOfBirth)}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">تاريخ ميلاد الأم</p>
                           <p className="text-[var(--admin-text)] font-semibold">{formatDate(studentData?.motherDateOfBirth)}</p>
                       </div>
                   </div>
               </div>

               {/* ── Section 3: البيانات الأكاديمية ── */}
               <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
                   <div className="flex items-center gap-3 mb-5">
                     <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                       <GraduationCap size={20} />
                     </div>
                     <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                       البيانات الأكاديمية
                     </h3>
                   </div>
                   <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">المرحلة الدراسية</p>
                           <p className="text-[var(--admin-text)] font-semibold">{mapEducationStage(studentData?.educationStage)}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">الصف الدراسي</p>
                           <p className="text-[var(--admin-text)] font-semibold">{mapGradeLevel(studentData?.grade)}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">الشعبة / التخصص</p>
                           <p className="text-[var(--admin-text)] font-semibold">{mapStudyTrack(studentData?.studyTrack)}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">اسم المدرسة</p>
                           <p className="text-[var(--admin-text)] font-semibold">{studentData?.schoolName || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">نوع المدرسة</p>
                           <p className="text-[var(--admin-text)] font-semibold">{mapSchoolType(studentData?.schoolType)}</p>
                       </div>
                   </div>
               </div>

               {/* ── Section 4: العنوان ── */}
               <div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">
                   <div className="flex items-center gap-3 mb-5">
                     <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                       <MapPin size={20} />
                     </div>
                     <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">
                       العنوان ومعلومات الاتصال
                     </h3>
                   </div>
                   <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">المحافظة</p>
                           <p className="text-[var(--admin-text)] font-semibold">{studentData?.governorate || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">المنطقة / الحي</p>
                           <p className="text-[var(--admin-text)] font-semibold">{studentData?.district || 'غير متوفر'}</p>
                       </div>
                       <div>
                           <p className="text-[var(--admin-muted)] text-sm mb-1">العنوان التفصيلي</p>
                           <p className="text-[var(--admin-text)] font-semibold">{studentData?.address || 'غير متوفر'}</p>
                       </div>
                   </div>
               </div>
            </div>
         )}

          {activeTab === 'financials' && (
              <div className="flex flex-col gap-6">
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                     <AdminStatCard
                       variant="accent"
                       icon={Package}
                       label="باقات نشطة"
                       value={studentData?.packages?.length || 0}
                     />
                     <AdminStatCard
                       variant="light"
                       icon={Wallet}
                       label="إجمالي النقاط"
                       value={studentData?.gamification?.totalPoints || 0}
                     />
                     <AdminStatCard
                        variant="muted"
                        icon={DollarSign}
                        label="الرصيد"
                        value={`${studentData?.currentBalance ?? 0} ج.م`}
                      >
                        <button
                          onClick={() => { setBalanceInput({ amount: 0, reason: '' }); setModalOpen('balance'); }}
                          className="mt-4 flex items-center justify-center gap-2 w-full rounded-xl bg-[var(--admin-primary-15)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-primary)] hover:text-white transition-all duration-300 shadow-sm"
                          title="تعديل الرصيد"
                        >
                          <PenLine size={14} />
                          <span>تعديل الرصيد</span>
                        </button>
                      </AdminStatCard>
                  </div>

                  <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                     <div className="mb-5">
                       <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">الباقات المسجلة</h3>
                       <p className="text-[var(--admin-muted)]">قائمة بالباقات التي اشترك فيها الطالب مع تاريخ الاشتراك والانتهاء.</p>
                     </div>

                     <AdminDataTable<StudentPackageDto>
                        columns={[
                          {key: 'name', label: 'اسم الباقة', render: (row) => (
                            <span className="font-bold text-[var(--admin-text)]">{row.name}</span>
                          )},
                          {key: 'price', label: 'السعر', render: (row) => (
                            <span className="font-medium text-[var(--admin-text)]">{row.price} ج.م</span>
                          )},
                          {key: 'purchaseMethod', label: 'طريقة الشراء', render: (row) => (
                            <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${row.purchaseMethod === 'Code' ? 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400' : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400'}`}>
                              {row.purchaseMethod === 'Code' ? 'كود شحن' : 'رصيد محفظة'}
                            </span>
                          )},
                          {key: 'enrolledAt', label: 'تاريخ الاشتراك', render: (row) => row.enrolledAt ? new Date(row.enrolledAt).toLocaleDateString('en-GB') : 'غير محدد'},
                          {key: 'expiresAt', label: 'تاريخ الانتهاء', render: (row) => row.expiresAt ? new Date(row.expiresAt).toLocaleDateString('en-GB') : 'غير محدد'},
                          {key: 'status', label: 'الحالة', render: (row) => {
                            const isExpired = row.expiresAt && new Date(row.expiresAt) < new Date();
                            const isGrantActive = row.isActive;
                            let statusClass = 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400';
                            if (!isGrantActive) {
                              statusClass = 'bg-zinc-100 text-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-100';
                            } else if (isExpired) {
                              statusClass = 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400';
                            }
                            return (
                              <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${statusClass}`}>
                                <span className="h-1.5 w-1.5 rounded-full bg-current" />
                                {!isGrantActive ? 'ملغية من الإدارة' : isExpired ? 'منتهية' : 'نشطة'}
                              </span>
                            );
                          }},
                          {key: 'actions', label: 'إجراءات الإلغاء', render: (row) => {
                            if (!row.isActive) return <span className="text-[var(--admin-muted)] text-xs">غير متاحة</span>;
                            return (
                              <button
                                onClick={() => handleOpenCancelPackageModal(row)}
                                className="flex items-center gap-1 px-2.5 py-1 bg-red-500/10 hover:bg-red-500 hover:text-white text-red-500 rounded-lg text-xs font-bold transition-all duration-200"
                              >
                                إلغاء الباقة
                              </button>
                            );
                          }}
                        ]}
                        data={studentData?.packages || []}
                        rowKey={(row) => row.accessGrantId || row.id || row.name}
                        emptyMessage="لا توجد باقات مسجلة لهذا الطالب"
                      />
                  </div>

                  <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                     <div className="mb-5">
                       <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل معاملات الرصيد</h3>
                       <p className="text-[var(--admin-muted)]">سجل بكافة عمليات الإيداع، الخصم، التعديل اليدوي، واسترجاع المبالغ.</p>
                     </div>

                     <AdminDataTable<any>
                        columns={[
                          {key: 'transactionType', label: 'نوع العملية', render: (row) => {
                            const types: Record<string, string> = {
                              CodeRedemption: 'شحن كود',
                              ContentPurchase: 'شراء باقة',
                              AdminAdjustment: 'تعديل إداري',
                              Refund: 'استرجاع رصيد'
                            };
                            return (
                              <span className="font-bold text-[var(--admin-text)]">
                                {types[row.transactionType] || row.transactionType}
                              </span>
                            );
                          }},
                          {key: 'amount', label: 'القيمة', render: (row) => {
                            const isPositive = row.amount >= 0;
                            return (
                              <span className={`font-mono font-bold ${isPositive ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'}`}>
                                {isPositive ? '+' : ''}{row.amount} ج.م
                              </span>
                            );
                          }},
                          {key: 'balanceAfter', label: 'الرصيد بعد العملية', render: (row) => (
                            <span className="font-mono text-[var(--admin-text)]">{row.balanceAfter} ج.م</span>
                          )},
                          {key: 'description', label: 'البيان / الملاحظات', render: (row) => (
                            <span className="text-sm text-[var(--admin-text)]">{row.description || '—'}</span>
                          )},
                          {key: 'adminName', label: 'بواسطة', render: (row) => (
                            <span className="text-sm font-semibold text-[var(--admin-text)]">{row.adminName}</span>
                          )},
                          {key: 'createdAt', label: 'التاريخ والوقت', render: (row) => row.createdAt ? new Date(row.createdAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }) : '—'}
                        ]}
                        data={studentData?.balanceTransactions || []}
                        rowKey={(row) => row.id}
                        emptyMessage="لا توجد عمليات رصيد مسجلة لهذا الطالب"
                      />
                  </div>
              </div>
          )}
         
         {activeTab === 'overrides' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">تجاوزات الفيديوهات</h3>
                        <p className="text-[var(--admin-muted)]">إضافة مشاهدات أو رفع الحظر مؤقتاً للطالب</p>
                    </div>
                    <button onClick={() => setModalOpen('override')} className="bg-[var(--admin-accent)] text-white px-6 py-3 rounded-xl flex items-center gap-2 font-bold hover:brightness-110">
                        <Video size={18} />
                        إضافة تجاوز جديد
                    </button>
                 </div>
                 
                 <AdminDataTable<any> 
                    columns={[
                        {key: 'videoId', label:'رقم الفيديو (ID)', render: (row: any) => row.videoId}, 
                        {key: 'addedViews', label:'المشاهدات المضافة', render: (row: any) => row.addedViews}, 
                        {key: 'reason', label:'السبب', render: (row: any) => row.reason}
                    ]}
                    data={studentData?.overrides || []}
                    rowKey={(row: any) => row.id || `${row.videoId}-${row.addedViews}-${row.reason || 'override'}`}
                    emptyMessage="لا توجد تجاوزات لهذا الطالب"
                 />
             </div>
         )}

         {activeTab === 'devices' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">الأجهزة المُسجلة</h3>
                        <p className="text-[var(--admin-muted)]">قائمة بالأجهزة التي قام الطالب بالدخول من خلالها</p>
                    </div>
                    <button onClick={handleDisconnectAll} className="bg-red-500/10 text-red-500 px-6 py-3 rounded-xl font-bold hover:bg-red-500/20">
                        فصل جميع الأجهزة
                    </button>
                 </div>

                 {(studentData?.devices?.length ?? 0) === 0 ? (
                   <div className="flex flex-col items-center justify-center py-16 text-[var(--admin-muted)] bg-[var(--admin-card-soft)] rounded-3xl">
                     <span className="text-5xl mb-4">📱</span>
                     <p className="font-bold text-[var(--admin-text)]">لا توجد أجهزة مسجلة</p>
                   </div>
                 ) : (
                   <div className="grid gap-4 sm:grid-cols-2">
                     {(studentData?.devices ?? []).map((device: any) => {
                       const osIcon: Record<string, string> = {
                         'Windows 10/11': '🪟', 'Windows': '🪟',
                         'Android': '🤖', 'iOS': '🍎', 'iPadOS': '🍎',
                         'macOS': '🍎', 'Linux': '🐧', 'ChromeOS': '🌐',
                       };
                       const deviceIcon: Record<string, string> = {
                         'Mobile': '📱', 'Tablet': '📟', 'Desktop': '🖥️',
                       };
                       return (
                         <div
                           key={device.id}
                           className={`relative flex flex-col gap-4 rounded-3xl border p-5 transition-all ${
                             device.isActive
                               ? 'bg-[var(--admin-card)] border-[var(--admin-border)]'
                               : 'bg-[var(--admin-card-soft)] border-[var(--admin-border)]/40 opacity-60'
                           }`}
                         >
                           {/* Header row */}
                           <div className="flex items-center justify-between gap-3">
                             <div className="flex items-center gap-3">
                               <span className="text-3xl">{deviceIcon[device.deviceType ?? 'Desktop'] ?? '🖥️'}</span>
                               <div>
                                 <p className="font-black text-[var(--admin-text)] text-sm">
                                   {osIcon[device.osName ?? ''] ?? '💻'} {device.osName ?? 'Unknown OS'}
                                 </p>
                                 <p className="text-xs text-[var(--admin-muted)] font-medium">{device.deviceType ?? 'Desktop'}</p>
                               </div>
                             </div>
                             <span className={`shrink-0 inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${
                               device.isActive
                                 ? 'bg-emerald-500/10 text-emerald-500'
                                 : 'bg-zinc-500/10 text-zinc-400'
                             }`}>
                               <span className="h-1.5 w-1.5 rounded-full bg-current" />
                               {device.isActive ? 'نشط' : 'معطل'}
                             </span>
                           </div>

                           {/* Details grid */}
                           <div className="grid grid-cols-2 gap-3">
                             <div className="rounded-xl bg-[var(--admin-surface-low)] px-3 py-2">
                               <p className="text-[10px] text-[var(--admin-muted)] font-semibold mb-0.5">المتصفح</p>
                               <p className="text-sm font-bold text-[var(--admin-text)]">{device.browserName ?? 'Unknown'}</p>
                             </div>
                             <div className="rounded-xl bg-[var(--admin-surface-low)] px-3 py-2">
                               <p className="text-[10px] text-[var(--admin-muted)] font-semibold mb-0.5">عنوان IP</p>
                               <p className="text-sm font-bold text-[var(--admin-text)] font-mono">{device.ipAddress ?? '—'}</p>
                             </div>
                             <div className="rounded-xl bg-[var(--admin-surface-low)] px-3 py-2 col-span-2">
                               <p className="text-[10px] text-[var(--admin-muted)] font-semibold mb-0.5">آخر نشاط</p>
                               <p className="text-sm font-bold text-[var(--admin-text)]">{device.lastActiveAt ? new Date(device.lastActiveAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }) : '—'}</p>
                             </div>
                           </div>

                           {/* Disconnect button */}
                           {device.isActive && (
                             <button
                               onClick={async () => {
                                 if (!confirm('هل تريد فصل هذا الجهاز؟')) return;
                                 try {
                                   await adminService.disconnectDevice(id, device.id);
                                   toast.success('تم فصل الجهاز');
                                   fetchStudent();
                                 } catch { toast.error('فشل فصل الجهاز'); }
                               }}
                               className="w-full rounded-2xl bg-red-500/10 py-2 text-sm font-bold text-red-500 hover:bg-red-500/20 transition-colors"
                             >
                               فصل الجهاز
                             </button>
                           )}
                         </div>
                       );
                     })}
                   </div>
                 )}
             </div>
         )}

         {activeTab === 'audit' && (
             <div className="flex flex-col gap-6">
                 <div className="flex justify-between items-center bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                    <div>
                        <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل النشاط</h3>
                        <p className="text-[var(--admin-muted)]">كافة الإجراءات والعمليات التي تمت على حساب هذا الطالب</p>
                    </div>
                 </div>

                 <AdminDataTable<any> 
                    columns={[
                        {key: 'date', label:'الوقت والتاريخ', render: (row: any) => new Date(row.date).toLocaleString('ar-EG')},
                        {key: 'action', label:'الإجراء', render: (row: any) => {
                          const m: Record<string, string> = {
                            UpdateUserStatus: 'تغيير حالة المستخدم', TOGGLE_ACCESS: 'تبديل صلاحية الوصول',
                            ResetWatchLimit: 'إعادة تعيين المشاهدات', SetWatchCount: 'تعديل عدد المشاهدات',
                            OverrideVideoLimit: 'تجاوز حد الفيديو', AdjustGamification: 'تعديل النقاط',
                            DisconnectDevice: 'فصل جهاز', DisconnectAllDevices: 'فصل جميع الأجهزة',
                            ApproveWatchRequest: 'قبول طلب مشاهدة', RejectWatchRequest: 'رفض طلب مشاهدة',
                          };
                          return <span className="font-bold">{m[row.action] || row.action}</span>;
                        }},
                        {key: 'adminName', label:'المسؤول', render: (row: any) => row.adminName === 'System Admin' ? 'مدير النظام' : row.adminName},
                        {key: 'details', label:'التفاصيل', render: (row: any) => {
                          const d = typeof row.details === 'string' ? row.details : JSON.stringify(row.details);
                          const dm: Record<string, string> = { 'Status Active': 'الحالة: نشط', 'Status Suspended': 'الحالة: موقوف' };
                          return <span className="text-xs max-w-xs inline-block">{dm[d] || d}</span>;
                        }}
                    ]}
                    data={studentData?.auditTrail || []}
                    rowKey={(row: any) => row.id}
                    emptyMessage="لا يوجد سجل نشاط"
                 />
             </div>
          )}

          {activeTab === 'notes' && (
              <div className="flex flex-col gap-6">
                  <div className="bg-[var(--admin-card)] p-8 rounded-3xl shadow-sm">
                     <h3 className="text-xl font-extrabold text-[var(--admin-text)] mb-5">إضافة ملاحظة جديدة</h3>
                     <form onSubmit={async (e) => {
                       e.preventDefault();
                       if (!noteInput.content.trim()) return;
                       try {
                         await adminService.addStudentNote(id, noteInput.content, noteInput.isPinned);
                         toast.success('تم إضافة الملاحظة');
                         setNoteInput({ content: '', isPinned: false });
                         fetchStudent();
                       } catch { toast.error('فشل إضافة الملاحظة'); }
                     }} className="flex flex-col gap-4">
                          <textarea 
                            required 
                            rows={4} 
                            placeholder="اكتب ملاحظتك هنا عن الطالب..."
                            className="w-full bg-[var(--admin-bg)] p-4 rounded-2xl text-[var(--admin-text)] border border-[var(--admin-border)]/40 focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)] outline-none resize-none transition-all duration-200 placeholder:text-[var(--admin-muted)]/70 text-sm shadow-[inset_0_2px_4px_rgba(78,70,57,0.03)]"
                            value={noteInput.content} 
                            onChange={e => setNoteInput(p => ({...p, content: e.target.value}))} 
                          />
                          <div className="flex flex-wrap gap-4 justify-between items-center mt-1">
                            <Checkbox
                              isSelected={noteInput.isPinned}
                              onChange={(checked) => setNoteInput(p => ({...p, isPinned: checked}))}
                              className="group"
                            >
                              <Checkbox.Control>
                                <Checkbox.Indicator />
                              </Checkbox.Control>
                              <Checkbox.Content>
                                <span className="flex items-center gap-1.5 text-sm font-bold text-[var(--admin-muted)] group-hover:text-[var(--admin-text)] transition-colors select-none">
                                  <Pin size={14} className={noteInput.isPinned ? "text-[var(--admin-primary)] fill-[var(--admin-primary)] animate-pulse" : "text-[var(--admin-muted)]"} />
                                  تثبيت الملاحظة في الأعلى
                                </span>
                              </Checkbox.Content>
                            </Checkbox>

                            <button 
                              type="submit" 
                              className="inline-flex items-center justify-center gap-2 rounded-full bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-6 py-2.5 font-bold text-xs text-[var(--admin-primary-contrast)] cursor-pointer hover:filter hover:brightness-110 active:scale-[0.98] transition-all duration-200 shadow-[0_4px_12px_var(--admin-primary-15)]"
                            >
                              <PenLine size={16} />
                              <span>إضافة ملاحظة</span>
                            </button>
                          </div>
                     </form>
                  </div>
                  {(studentData?.notes?.length ?? 0) > 0 ? (
                    <div className="flex flex-col gap-4">
                      {studentData!.notes.map(note => (
                        <div 
                          key={note.id} 
                          className={note.isPinned 
                            ? "bg-gradient-to-br from-[var(--admin-primary-15)] to-[var(--admin-card-soft)] p-6 rounded-3xl shadow-sm transition-all duration-200 hover:shadow-md hover:scale-[1.005]"
                            : "bg-[var(--admin-card-soft)] p-6 rounded-3xl shadow-sm transition-all duration-200 hover:shadow-md hover:scale-[1.005] hover:bg-[var(--admin-card-strong)]"
                          }
                        >
                          <div className="flex flex-col justify-between h-full min-h-[90px]">
                            <div className="flex-1">
                              {note.isPinned && (
                                <span className="inline-flex items-center gap-1 text-[11px] font-black tracking-wider text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-3 py-1 rounded-full mb-3 self-start">
                                  <Pin size={10} className="fill-[var(--admin-primary)]" /> مثبتة في الأعلى
                                </span>
                              )}
                              <p className="text-[var(--admin-text)] whitespace-pre-wrap leading-relaxed text-sm mb-5">{note.content}</p>
                            </div>
                            <div className="flex justify-between items-center text-xs text-[var(--admin-muted)] border-t border-[var(--admin-border)]/30 pt-3">
                              <div className="flex items-center gap-1.5">
                                <span className="h-5 w-5 rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] flex items-center justify-center font-bold text-[10px]">
                                  {note.adminName.substring(0, 1).toUpperCase()}
                                </span>
                                <span>بواسطة <strong className="text-[var(--admin-text)] font-semibold">{note.adminName}</strong></span>
                                <span>•</span>
                                <span>{new Date(note.createdAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' })}</span>
                              </div>
                              <button 
                                onClick={async () => { 
                                  try { 
                                    await adminService.deleteStudentNote(id, note.id); 
                                    toast.success('تم حذف الملاحظة'); 
                                    fetchStudent(); 
                                  } catch { 
                                    toast.error('فشل حذف الملاحظة'); 
                                  } 
                                }}
                                className="flex items-center justify-center p-2 rounded-xl text-[var(--admin-muted)] hover:bg-[var(--admin-danger-10)] hover:text-[var(--admin-danger)] transition-colors duration-200" 
                                title="حذف الملاحظة"
                              >
                                <Trash2 size={16} />
                              </button>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="flex flex-col items-center justify-center py-16 text-[var(--admin-muted)] bg-[var(--admin-card-soft)] rounded-3xl shadow-sm">
                      <StickyNote size={64} className="mb-6 opacity-20 text-[var(--admin-primary)] stroke-[1.5]" />
                      <h4 className="text-lg font-bold text-[var(--admin-text)] mb-2">لا توجد ملاحظات بعد</h4>
                      <p className="text-sm max-w-sm text-center leading-relaxed">أضف ملاحظة أو تنبيهًا لتتبع حالة الطالب وتفاصيل متابعته مع المساعدين.</p>
                    </div>
                  )}
              </div>
          )}

         {activeTab === 'academic' && (
              <div className="flex flex-col gap-6">
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                     <AdminStatCard
                       variant="accent"
                       icon={Video}
                       label="فيديوهات تم تتبعها"
                       value={studentData?.watchTracking?.watchedVideosCount || 0}
                     />
                     <AdminStatCard
                       variant="light"
                       icon={Clock3}
                       label="إجمالي زمن المشاهدة"
                       value={formatDuration(studentData?.watchTracking?.totalWatchedSeconds || 0)}
                     />
                     <AdminStatCard
                       variant="muted"
                       icon={MonitorPlay}
                       label="جلسات محتسبة"
                       value={studentData?.watchTracking?.activities?.reduce((sum, activity) => sum + activity.watchCount, 0) || 0}
                     />
                  </div>

                  <div className="bg-[var(--admin-bg)] p-6 rounded-3xl shadow-sm">
                     <div className="mb-5">
                       <h3 className="text-[length:var(--admin-font-title-md)] font-bold mb-1">سجل مشاهدة الفيديوهات</h3>
                       <p className="text-[var(--admin-muted)]">آخر الفيديوهات التي شاهدها الطالب مع الزمن التراكمي الفعلي وآخر نشاط.</p>
                     </div>

                     {(() => {
                       const activities = studentData?.watchTracking?.activities || [];
                       const groupedData: {
                         packageName: string;
                         terms: {
                           termTitle: string;
                           lessons: {
                             lessonTitle: string;
                             activities: typeof activities;
                           }[];
                         }[];
                       }[] = [];

                       activities.forEach(activity => {
                         const pkgName = activity.packageName || "باقات أخرى / مباشرة";
                         const tTitle = activity.termTitle || "ترم عام";
                         const lesTitle = activity.lessonTitle || "حصة عامة";

                         let pkg = groupedData.find(p => p.packageName === pkgName);
                         if (!pkg) {
                           pkg = { packageName: pkgName, terms: [] };
                           groupedData.push(pkg);
                         }

                         let term = pkg.terms.find(t => t.termTitle === tTitle);
                         if (!term) {
                           term = { termTitle: tTitle, lessons: [] };
                           pkg.terms.push(term);
                         }

                         let lesson = term.lessons.find(l => l.lessonTitle === lesTitle);
                         if (!lesson) {
                           lesson = { lessonTitle: lesTitle, activities: [] };
                           term.lessons.push(lesson);
                         }

                         lesson.activities.push(activity);
                       });

                       if (activities.length === 0) {
                         return (
                           <div className="text-center py-12 text-[var(--admin-muted)] border border-dashed border-[var(--admin-border)]/50 rounded-2xl">
                             لا توجد بيانات مشاهدة لهذا الطالب بعد
                           </div>
                         );
                       }

                       return (
                         <div className="space-y-4" dir="rtl">
                           {groupedData.map((pkg) => {
                             const isPkgExpanded = !!expandedPackages[pkg.packageName];
                             return (
                               <div key={pkg.packageName} className="border border-[var(--admin-border)]/40 rounded-3xl overflow-hidden bg-[var(--admin-card-soft)]">
                                 {/* Package Row */}
                                 <div 
                                   onClick={() => setExpandedPackages(prev => ({ ...prev, [pkg.packageName]: !prev[pkg.packageName] }))}
                                   className="flex items-center justify-between p-4 cursor-pointer hover:bg-[var(--admin-card-strong)]/40 transition-colors select-none"
                                 >
                                   <div className="flex items-center gap-3">
                                     <div className="p-2 rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                                       <Package size={18} />
                                     </div>
                                     <span className="font-extrabold text-sm text-[var(--admin-text)]">{pkg.packageName}</span>
                                   </div>
                                   {isPkgExpanded ? <ChevronDown size={18} className="text-[var(--admin-muted)]" /> : <ChevronRight size={18} className="text-[var(--admin-muted)]" />}
                                 </div>

                                 {isPkgExpanded && (
                                   <div className="border-t border-[var(--admin-border)]/30 p-4 space-y-3 bg-[var(--admin-bg)]">
                                     {pkg.terms.map((term) => {
                                       const termKey = `${pkg.packageName}-${term.termTitle}`;
                                       const isTermExpanded = !!expandedTerms[termKey];
                                       return (
                                         <div key={term.termTitle} className="border border-[var(--admin-border)]/20 rounded-2xl overflow-hidden bg-[var(--admin-card-soft)]/50 mr-4">
                                           {/* Term Row */}
                                           <div 
                                             onClick={() => setExpandedTerms(prev => ({ ...prev, [termKey]: !prev[termKey] }))}
                                             className="flex items-center justify-between p-3.5 cursor-pointer hover:bg-[var(--admin-card-strong)]/30 transition-colors select-none"
                                           >
                                             <div className="flex items-center gap-2.5">
                                               <div className="p-1.5 rounded-lg bg-[var(--admin-muted)]/10 text-[var(--admin-muted)]">
                                                 <GraduationCap size={16} />
                                               </div>
                                               <span className="font-bold text-xs text-[var(--admin-text)]">{term.termTitle}</span>
                                             </div>
                                             {isTermExpanded ? <ChevronDown size={16} className="text-[var(--admin-muted)]" /> : <ChevronRight size={16} className="text-[var(--admin-muted)]" />}
                                           </div>

                                           {isTermExpanded && (
                                             <div className="border-t border-[var(--admin-border)]/20 p-3 space-y-2 bg-[var(--admin-bg)]">
                                               {term.lessons.map((lesson) => {
                                                 const lessonKey = `${termKey}-${lesson.lessonTitle}`;
                                                 const isLessonExpanded = !!expandedLessons[lessonKey];
                                                 return (
                                                   <div key={lesson.lessonTitle} className="border border-[var(--admin-border)]/10 rounded-xl overflow-hidden mr-4 bg-[var(--admin-card-soft)]/20">
                                                     {/* Lesson Row */}
                                                     <div 
                                                       onClick={() => setExpandedLessons(prev => ({ ...prev, [lessonKey]: !prev[lessonKey] }))}
                                                       className="flex items-center justify-between p-3 cursor-pointer hover:bg-[var(--admin-card-strong)]/20 transition-colors select-none"
                                                     >
                                                       <div className="flex items-center gap-2">
                                                         <BookOpen size={14} className="text-[var(--admin-muted)]" />
                                                         <span className="font-semibold text-xs text-[var(--admin-text)]">{lesson.lessonTitle}</span>
                                                       </div>
                                                       {isLessonExpanded ? <ChevronDown size={14} className="text-[var(--admin-muted)]" /> : <ChevronRight size={14} className="text-[var(--admin-muted)]" />}
                                                     </div>

                                                     {isLessonExpanded && (
                                                       <div className="p-3 bg-[var(--admin-bg)] space-y-2 border-t border-[var(--admin-border)]/10">
                                                         {lesson.activities.map((activity) => (
                                                           <div key={activity.lessonVideoId} className="flex flex-wrap sm:flex-nowrap items-center justify-between gap-4 p-3 bg-[var(--admin-card-soft)]/40 hover:bg-[var(--admin-card-soft)] border border-[var(--admin-border)]/20 rounded-xl transition-all mr-4">
                                                             {/* Video Details */}
                                                             <div className="flex items-center gap-2 min-w-0">
                                                               <MonitorPlay size={14} className="text-[var(--admin-primary)] shrink-0" />
                                                               <span className="font-medium text-xs text-[var(--admin-text)] truncate">{activity.videoTitle}</span>
                                                             </div>

                                                             {/* Metrics */}
                                                             <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-[11px] text-[var(--admin-muted)]">
                                                               <div>
                                                                 <span className="font-bold">المشاهدة:</span> {formatDuration(activity.watchedSeconds)}
                                                               </div>
                                                               <div>
                                                                 <span className="font-bold">المشاهدات:</span> {activity.watchCount} / {activity.maxWatchCount === 0 ? '∞' : activity.maxWatchCount}
                                                               </div>
                                                               <div>
                                                                 <span className="font-bold">آخر نشاط:</span> {activity.lastWatchedAt ? new Date(activity.lastWatchedAt).toLocaleDateString('ar-EG', { dateStyle: 'medium' }) : 'غير متوفر'}
                                                               </div>
                                                               <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold ${activity.isLocked ? 'bg-red-500/10 text-red-500' : 'bg-emerald-500/10 text-emerald-500'}`}>
                                                                 {activity.isLocked ? <Lock size={10} /> : <Unlock size={10} />}
                                                                 {activity.isLocked ? 'مقفول' : 'نشط'}
                                                               </span>
                                                             </div>

                                                             {/* Action */}
                                                             <button
                                                               onClick={(e) => {
                                                                 e.stopPropagation();
                                                                 setWatchCountEdit({
                                                                   lessonVideoId: activity.lessonVideoId,
                                                                   videoTitle: activity.videoTitle,
                                                                   currentCount: activity.watchCount,
                                                                   newCount: activity.watchCount,
                                                                   maxCount: activity.maxWatchCount
                                                                 });
                                                                 setModalOpen('watchCount');
                                                               }}
                                                               className="flex items-center gap-1.5 rounded-xl bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-primary)] hover:text-white transition-colors"
                                                               title="تعديل عدد المشاهدات"
                                                             >
                                                               <PenLine size={14} />
                                                               تعديل
                                                             </button>
                                                           </div>
                                                         ))}
                                                       </div>
                                                     )}
                                                   </div>
                                                 );
                                               })}
                                             </div>
                                           )}
                                         </div>
                                       );
                                     })}
                                   </div>
                                 )}
                               </div>
                             );
                           })}
                         </div>
                       );
                     })()}
                  </div>
              </div>
          )}
         
         <AdminModal open={modalOpen === 'gamification'} onClose={() => !submitting && setModalOpen('none')} title="تعديل نقاط الطالب">
             <form onSubmit={handleGamificationSubmit} className="flex flex-col gap-4">
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">إضافة / خصم نقاط (يمكن استخدام قيم سالبة)</label>
                     <input required type="number" disabled={submitting} className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50" 
                            value={gamificationInput.points} onChange={e => setGamificationInput({...gamificationInput, points: parseInt(e.target.value) || 0})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">السبب</label>
                     <input required type="text" disabled={submitting} className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50" 
                            value={gamificationInput.reason} onChange={e => setGamificationInput({...gamificationInput, reason: e.target.value})} />
                 </div>
                 <div className="flex gap-4 mt-4">
                     <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                     <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                       {submitting ? 'جاري الحفظ...' : 'حفظ التعديلات'}
                     </button>
                 </div>
             </form>
         </AdminModal>

         <AdminModal open={modalOpen === 'override'} onClose={() => !submitting && setModalOpen('none')} title="إضافة مشاهدات فيديو">
             <form onSubmit={handleOverrideSubmit} className="flex flex-col gap-4">
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">رقم الفيديو المتجاوز (UUID)</label>
                     <input required type="text" disabled={submitting} className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50" 
                            value={overrideInput.videoId} onChange={e => setOverrideInput({...overrideInput, videoId: e.target.value})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">عدد المشاهدات الإضافية</label>
                     <input required type="number" min="1" disabled={submitting} className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50" 
                            value={overrideInput.addedViews} onChange={e => setOverrideInput({...overrideInput, addedViews: parseInt(e.target.value) || 1})} />
                 </div>
                 <div>
                     <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">السبب (يظهر للآدمنز)</label>
                     <input required type="text" disabled={submitting} className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50" 
                            value={overrideInput.reason} onChange={e => setOverrideInput({...overrideInput, reason: e.target.value})} />
                 </div>
                 <div className="flex gap-4 mt-4">
                     <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                     <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                       {submitting ? 'جاري الحفظ...' : 'حفظ التجاوز'}
                     </button>
                 </div>
             </form>
          </AdminModal>

          <AdminModal open={modalOpen === 'watchCount'} onClose={() => !submitting && setModalOpen('none')} title={`تعديل مشاهدات: ${watchCountEdit.videoTitle}`}>
              <form onSubmit={handleWatchCountSubmit} className="flex flex-col gap-4">
                  <div className="text-center">
                      <p className="text-sm text-[var(--admin-muted)] mb-1">العدد الحالي: <strong className="text-[var(--admin-text)] text-lg">{watchCountEdit.currentCount}</strong> / {watchCountEdit.maxCount === 0 ? '∞' : watchCountEdit.maxCount}</p>
                      <div className="flex items-center justify-center gap-4 mt-4">
                          <button type="button" disabled={submitting} onClick={() => setWatchCountEdit(p => ({...p, newCount: Math.max(0, p.newCount - 1)}))}
                            className="h-12 w-12 rounded-xl bg-red-500/10 text-red-500 font-bold text-xl hover:bg-red-500/20 transition-colors disabled:opacity-50">−</button>
                          <input type="number" min="0" required disabled={submitting}
                            className="w-24 text-center bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] text-2xl font-bold border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                            value={watchCountEdit.newCount} onChange={e => setWatchCountEdit(p => ({...p, newCount: Math.max(0, parseInt(e.target.value) || 0)}))} />
                          <button type="button" disabled={submitting} onClick={() => setWatchCountEdit(p => ({...p, newCount: p.newCount + 1}))}
                            className="h-12 w-12 rounded-xl bg-emerald-500/10 text-emerald-500 font-bold text-xl hover:bg-emerald-500/20 transition-colors disabled:opacity-50">+</button>
                      </div>
                  </div>
                  <div className="flex gap-4 mt-4">
                      <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                      <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                        {submitting ? 'جاري الحفظ...' : 'حفظ'}
                      </button>
                  </div>
              </form>
          </AdminModal>

          <AdminModal open={modalOpen === 'balance'} onClose={() => !submitting && setModalOpen('none')} title="تعديل الرصيد">
              <form onSubmit={handleBalanceSubmit} className="flex flex-col gap-4">
                  <div className="text-center mb-2">
                      <p className="text-sm text-[var(--admin-muted)]">الرصيد الحالي</p>
                      <p className="text-3xl font-bold text-[var(--admin-text)]">{studentData?.currentBalance ?? 0} <span className="text-base font-normal text-[var(--admin-muted)]">ج.م</span></p>
                  </div>
                  <div>
                      <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">المبلغ (موجب للإضافة، سالب للخصم)</label>
                      <input required type="number" step="0.01" disabled={submitting}
                        className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] text-lg font-bold text-center border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                        value={balanceInput.amount} onChange={e => setBalanceInput(p => ({...p, amount: parseFloat(e.target.value) || 0}))} />
                  </div>
                  <div>
                      <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">السبب</label>
                      <input required type="text" disabled={submitting}
                        className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                        placeholder="مثال: تعديل إداري"
                        value={balanceInput.reason} onChange={e => setBalanceInput(p => ({...p, reason: e.target.value}))} />
                  </div>
                  {balanceInput.amount !== 0 && (
                    <p className={`text-sm text-center font-bold ${balanceInput.amount > 0 ? 'text-emerald-500' : 'text-red-500'}`}>
                      الرصيد الجديد: {((studentData?.currentBalance ?? 0) + balanceInput.amount).toFixed(2)} ج.م
                    </p>
                  )}
                  <div className="flex gap-4 mt-2">
                      <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                      <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                        {submitting ? 'جاري الحفظ...' : 'حفظ'}
                      </button>
                  </div>
              </form>
          </AdminModal>

          <AdminModal open={modalOpen === 'editProfile'} onClose={() => !submitting && setModalOpen('none')} title="تعديل بيانات الطالب">
              <form onSubmit={handleEditProfileSubmit} className="flex flex-col gap-3 max-h-[60vh] overflow-y-auto pr-1">
                  {[
                    { key: 'fullName', label: 'الاسم الكامل', type: 'text' },
                    { key: 'phone', label: 'رقم الهاتف', type: 'text' },
                    { key: 'parentPhone', label: 'هاتف ولي الأمر', type: 'text' },
                    { key: 'secondaryPhone', label: 'هاتف إضافي', type: 'text' },
                    { key: 'motherPhone', label: 'هاتف الأم', type: 'text' },
                    { key: 'governorate', label: 'المحافظة', type: 'text' },
                    { key: 'district', label: 'المنطقة / الحي', type: 'text' },
                    { key: 'address', label: 'العنوان', type: 'text' },
                    { key: 'schoolName', label: 'اسم المدرسة', type: 'text' },
                    { key: 'dateOfBirth', label: 'تاريخ الميلاد', type: 'date' },
                  ].map(f => (
                    <div key={f.key}>
                      <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">{f.label}</label>
                      <input type={f.type} disabled={submitting} className="w-full bg-[var(--admin-surface)] p-2.5 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none text-sm disabled:opacity-50"
                        value={String(editFields[f.key] ?? '')} onChange={e => setEditFields(p => ({...p, [f.key]: e.target.value}))} />
                    </div>
                  ))}
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">النوع</label>
                      <select disabled={submitting} className="w-full bg-[var(--admin-surface)] p-2.5 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none text-sm disabled:opacity-50"
                        value={String(editFields.gender ?? '')} onChange={e => setEditFields(p => ({...p, gender: e.target.value}))}>
                        <option value="">---</option><option value="Male">ذكر</option><option value="Female">أنثى</option>
                      </select>
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">نوع المدرسة</label>
                      <select disabled={submitting} className="w-full bg-[var(--admin-surface)] p-2.5 rounded-xl text-[var(--admin-text)] border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none text-sm disabled:opacity-50"
                        value={String(editFields.schoolType ?? '')} onChange={e => setEditFields(p => ({...p, schoolType: e.target.value}))}>
                        <option value="">---</option><option value="Government">حكومية</option><option value="Language">لغات</option><option value="Experimental">تجريبية</option><option value="Private">خاصة</option><option value="Azhari">أزهرية</option>
                      </select>
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <label className="flex items-center gap-2 cursor-pointer text-sm text-[var(--admin-text)] disabled:opacity-50">
                      <input type="checkbox" disabled={submitting} checked={editFields.isFatherAlive === true} onChange={e => setEditFields(p => ({...p, isFatherAlive: e.target.checked}))} className="rounded" />
                      الأب على قيد الحياة
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer text-sm text-[var(--admin-text)] disabled:opacity-50">
                      <input type="checkbox" disabled={submitting} checked={editFields.isMotherAlive === true} onChange={e => setEditFields(p => ({...p, isMotherAlive: e.target.checked}))} className="rounded" />
                      الأم على قيد الحياة
                    </label>
                  </div>
                  <div className="flex gap-4 mt-4 sticky bottom-0 bg-[var(--admin-bg)] pt-3">
                      <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                      <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                        {submitting ? 'جاري الحفظ...' : 'حفظ التعديلات'}
                      </button>
                  </div>
              </form>
          </AdminModal>

          <AdminModal open={modalOpen === 'password'} onClose={() => !submitting && setModalOpen('none')} title="تغيير كلمة المرور">
              <form onSubmit={handlePasswordSubmit} className="flex flex-col gap-4">
                  <div>
                      <label className="block text-sm font-bold text-[var(--admin-text)] mb-2">كلمة المرور الجديدة</label>
                      <input required type="text" minLength={4} disabled={submitting}
                        className="w-full bg-[var(--admin-surface)] p-3 rounded-xl text-[var(--admin-text)] text-lg font-mono text-center border border-[var(--admin-border)] focus:border-[var(--admin-primary)] outline-none disabled:opacity-50"
                        placeholder="••••••"
                        value={passwordInput} onChange={e => setPasswordInput(e.target.value)} />
                      <p className="text-xs text-[var(--admin-muted)] mt-2 text-center">الحد الأدنى 4 أحرف</p>
                  </div>
                  <div className="flex gap-4 mt-2">
                      <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                      <button type="submit" disabled={submitting} className="flex-1 px-4 py-3 rounded-xl font-bold bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] hover:brightness-110 active:scale-[0.98] transition-all disabled:opacity-50 disabled:cursor-not-allowed">
                        {submitting ? 'جاري التغيير...' : 'تغيير كلمة المرور'}
                      </button>
                  </div>
              </form>
          </AdminModal>

          <AdminModal open={modalOpen === 'status'} onClose={() => !submitting && setModalOpen('none')} title="إيقاف حساب الطالب">
              <div className="space-y-4">
                  <p className="text-sm text-[var(--admin-muted)] font-bold">يرجى كتابة سبب إيقاف الحساب. سيظهر هذا السبب للطالب عند محاولة تسجيل الدخول:</p>
                  <div>
                      <label className="block text-xs font-bold text-[var(--admin-muted)] mb-2">سبب الإيقاف</label>
                      <input 
                        type="text" 
                        disabled={submitting}
                        value={suspensionReasonInput}
                        onChange={(e) => setSuspensionReasonInput(e.target.value)}
                        placeholder="مثال: عدم دفع المصاريف أو مخالفة شروط الاستخدام"
                        className="w-full px-4 py-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] placeholder-[var(--admin-muted)]/50 focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50"
                      />
                  </div>
                  <div className="flex gap-3">
                      <button 
                        disabled={submitting}
                        onClick={() => toggleStatusDirect(false, suspensionReasonInput)}
                        className="flex-1 px-4 py-3 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 transition-all disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98]"
                      >
                         {submitting ? 'جاري الإيقاف...' : 'تأكيد الإيقاف'}
                      </button>
                      <button type="button" disabled={submitting} onClick={() => setModalOpen('none')} className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50">إلغاء</button>
                  </div>
              </div>
          </AdminModal>

          <AdminModal open={modalOpen === 'cancelPackage'} onClose={() => !submitting && setModalOpen('none')} title="إلغاء اشتراك باقة طالب">
              {selectedPackageForCancel && (
                <div className="space-y-5">
                    <div className="p-4 bg-red-500/10 border border-red-500/20 text-red-600 rounded-xl text-sm font-bold leading-relaxed">
                        ⚠️ تنبيه هام: أنت تقوم بإلغاء اشتراك الطالب في باقة: <span className="underline">{selectedPackageForCancel.name}</span>.
                        <br />
                        طريقة الشراء الأصلية: <span className="underline">{selectedPackageForCancel.purchaseMethod === 'Code' ? 'كود شحن' : 'رصيد محفظة'}</span>.
                        {selectedPackageForCancel.purchaseMethod === 'Code' && (
                          <div className="mt-2 text-xs font-semibold text-red-500 bg-red-500/20 p-2 rounded-lg">
                             تحذير: تم تفعيل هذه الباقة بواسطة كود شحن. قد ترغب في عدم إرجاع قيمة الباقة نقدًا كـ رصيد ما لم يطلب الطالب ذلك.
                          </div>
                        )}
                    </div>

                    <div className="space-y-3">
                        <label className="flex items-center gap-3 cursor-pointer p-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:bg-[var(--admin-hover)] transition-colors">
                            <input 
                              type="checkbox" 
                              disabled={submitting}
                              checked={refundBalanceOption} 
                              onChange={(e) => setRefundBalanceOption(e.target.checked)} 
                              className="w-4 h-4 text-[var(--admin-primary)] focus:ring-[var(--admin-primary)] border-gray-300 rounded disabled:opacity-50" 
                            />
                            <div>
                                <span className="block text-sm font-bold text-[var(--admin-text)]">إرجاع قيمة الباقة إلى محفظة الطالب</span>
                                <span className="block text-xs text-[var(--admin-muted)]">سيتم إضافة مبلغ ({selectedPackageForCancel.price} ج.م) لرصيد الطالب بعد الإلغاء.</span>
                            </div>
                        </label>
                    </div>

                    <div className="flex gap-4">
                        <button 
                          disabled={submitting}
                          onClick={handleCancelPackageConfirm}
                          className="flex-1 px-4 py-3 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 transition-all disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98]"
                        >
                            {submitting ? 'جاري الإلغاء...' : 'تأكيد إلغاء الباقة'}
                        </button>
                        <button 
                          type="button" 
                          disabled={submitting}
                          onClick={() => setModalOpen('none')} 
                          className="flex-1 px-4 py-3 rounded-xl font-bold text-[var(--admin-text)] bg-[var(--admin-hover)] hover:bg-[var(--admin-border)] transition-colors disabled:opacity-50"
                        >
                            تراجع
                        </button>
                    </div>
                </div>
              )}
          </AdminModal>
       </div>
    </AdminShellChrome>
  );
}
