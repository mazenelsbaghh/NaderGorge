'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AnimatePresence, motion } from 'framer-motion';
import {
  ArrowRight,
  Check,
  CheckCircle2,
  Eye,
  EyeOff,
  GraduationCap,
  Loader2,
  LockKeyhole,
  MapPinned,
  ShieldCheck,
  UserRound,
  UsersRound,
} from 'lucide-react';
import { ARAB_NATIONALITIES } from '@/data/arab-nationalities';
import { SCHOOL_TYPES } from '@/data/school-types';
import { computeBirthdayInfo } from '@/utils/birthday-utils';
import { useWhatsAppCheck } from '@/utils/whatsapp-utils';
import { z } from 'zod';
import Image from 'next/image';
import { AVATAR_LIST } from '@/data/avatars';

import { AcademicFields, requiresTrack } from '@/components/registration/AcademicFields';
import type { AcademicData } from '@/components/registration/AcademicFields';
import { FeatureCarousel } from '@/components/ui/feature-carousel';
import { Checkbox, Label } from '@/components/ui/checkbox';
import { RadioGroup, Radio } from '@/components/ui/radio-group';
import { authService, getDeviceFingerprint } from '@/services/auth-service';

import { useAuthStore } from '@/stores/auth-store';
import { getDistrictsForGovernorate } from '@/data/governorate-districts';
import { InteractiveHoverButton } from '@/components/ui/interactive-hover-button';
import { UserAvatar } from '@/components/ui/UserAvatar';

const EGYPTIAN_GOVERNORATES = [
  'القاهرة', 'الجيزة', 'الإسكندرية', 'الدقهلية', 'البحيرة', 'الفيوم',
  'الغربية', 'الإسماعيلية', 'المنوفية', 'المنيا', 'القليوبية', 'الوادي الجديد',
  'السويس', 'أسوان', 'أسيوط', 'بني سويف', 'بورسعيد', 'دمياط',
  'الشرقية', 'جنوب سيناء', 'كفر الشيخ', 'مطروح', 'الأقصر', 'قنا',
  'شمال سيناء', 'سوهاج', 'البحر الأحمر',
];

const egyptianPhoneRegex = /^01[0125]\d{8}$/;

const schema = z
  .object({
    fullName: z.string().min(5, 'يرجى إدخال اسمك رباعيًا')
      .refine((n) => n.trim().split(/\s+/).length >= 4, 'يرجى إدخال اسمك رباعيًا (مثال: أحمد محمد محمود علي)'),
    phoneNumber: z.string().regex(egyptianPhoneRegex, 'تأكد من كتابة رقم الهاتف بشكل صحيح، مثال: 01012345678'),
    secondaryPhone: z.string().regex(egyptianPhoneRegex, 'تأكد من كتابة رقم الهاتف بشكل صحيح').optional().or(z.literal('')),
    dateOfBirth: z.string().min(1, 'يرجى تحديد تاريخ ميلادك')
      .refine(d => !d || new Date(d) <= new Date('2019-12-31'), 'يجب أن تكون مولودًا قبل عام 2020 للتسجيل في المنصة'),
    gender: z.enum(['Male', 'Female'], { message: 'يرجى تحديد النوع' }),
    nationality: z.string().min(1, 'يرجى اختيار الجنسية'),
    governorate: z.string().min(1, 'يرجى اختيار المحافظة'),
    district: z.string().min(1, 'يرجى اختيار المنطقة / الحي'),
    address: z.string().min(3, 'يرجى كتابة عنوانك بالتفصيل'),
    parentPhone: z.string().optional().or(z.literal('')),
    secondaryParentPhone: z.string().regex(egyptianPhoneRegex, 'تأكد من كتابة رقم ولي أمر إضافي بشكل صحيح'),
    motherPhone: z.string().optional().or(z.literal('')),
    isFatherAlive: z.boolean(),
    isMotherAlive: z.boolean(),
    fatherDateOfBirth: z.string().optional().or(z.literal('')),
    motherDateOfBirth: z.string().optional().or(z.literal('')),
    schoolName: z.string().min(2, 'يرجى كتابة اسم المدرسة'),
    schoolType: z.string().min(1, 'يرجى اختيار نوع المدرسة'),
    educationStage: z.enum(['Secondary', 'Baccalaureate', 'Primary', 'Preparatory', 'Azhari', 'American'], { message: 'يرجى اختيار المرحلة الدراسية' }),
    gradeLevel: z.string().min(1, 'يرجى اختيار الصف الدراسي'),
    studyTrack: z.string().optional(),
    password: z.string().min(8, 'يجب أن تتكون كلمة المرور من 8 أحرف على الأقل'),
    confirmPassword: z.string(),
    avatarSlug: z.string().optional(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: 'كلمتا المرور غير متطابقتين',
    path: ['confirmPassword'],
  })
  .refine((d) => {
    if (d.isFatherAlive && !d.parentPhone?.match(egyptianPhoneRegex)) return false;
    return true;
  }, { message: 'تأكد من كتابة رقم هاتف الأب بشكل صحيح', path: ['parentPhone'] })
  .refine((d) => {
    if (d.isFatherAlive && !d.fatherDateOfBirth) return false;
    return true;
  }, { message: 'يرجى إدخال تاريخ ميلاد الأب', path: ['fatherDateOfBirth'] })
  .refine((d) => {
    if (d.isMotherAlive && !d.motherPhone?.match(egyptianPhoneRegex)) return false;
    return true;
  }, { message: 'تأكد من كتابة رقم هاتف الأم بشكل صحيح', path: ['motherPhone'] })
  .refine((d) => {
    if (d.isMotherAlive && !d.motherDateOfBirth) return false;
    return true;
  }, { message: 'يرجى إدخال تاريخ ميلاد الأم', path: ['motherDateOfBirth'] })
  .refine((d) => {
    if (requiresTrack(d.gradeLevel) && !d.studyTrack) return false;
    return true;
  }, {
    message: 'يرجى اختيار الشعبة / التخصص',
    path: ['studyTrack'],
  });

type FormError = { field?: string; message: string };

const EMPTY_FORM = {
  fullName: '',
  phoneNumber: '',
  secondaryPhone: '',
  dateOfBirth: '',
  gender: '' as 'Male' | 'Female' | '',
  nationality: '',
  governorate: '',
  district: '',
  address: '',
  parentPhone: '',
  secondaryParentPhone: '',
  motherPhone: '',
  isFatherAlive: true,
  isMotherAlive: true,
  fatherDateOfBirth: '',
  motherDateOfBirth: '',
  schoolName: '',
  schoolType: '',
  educationStage: '' as 'Secondary' | 'Baccalaureate' | 'Primary' | 'Preparatory' | 'Azhari' | 'American' | '',
  gradeLevel: '',
  studyTrack: '',
  password: '',
  confirmPassword: '',
  avatarSlug: '',
};

type RegistrationFormState = typeof EMPTY_FORM;

function normalizeFormData(data: Partial<RegistrationFormState>): RegistrationFormState {
  return {
    fullName: data.fullName ?? '',
    phoneNumber: data.phoneNumber ?? '',
    secondaryPhone: data.secondaryPhone ?? '',
    dateOfBirth: data.dateOfBirth ?? '',
    gender: data.gender ?? '',
    nationality: data.nationality ?? '',
    governorate: data.governorate ?? '',
    district: data.district ?? '',
    address: data.address ?? '',
    parentPhone: data.parentPhone ?? '',
    secondaryParentPhone: data.secondaryParentPhone ?? '',
    motherPhone: data.motherPhone ?? '',
    isFatherAlive: data.isFatherAlive ?? true,
    isMotherAlive: data.isMotherAlive ?? true,
    fatherDateOfBirth: data.fatherDateOfBirth ?? '',
    motherDateOfBirth: data.motherDateOfBirth ?? '',
    schoolName: data.schoolName ?? '',
    schoolType: data.schoolType ?? '',
    educationStage: data.educationStage ?? '',
    gradeLevel: data.gradeLevel ?? '',
    studyTrack: data.studyTrack ?? '',
    password: data.password ?? '',
    confirmPassword: data.confirmPassword ?? '',
    avatarSlug: data.avatarSlug ?? '',
  };
}

const REGISTRATION_STEPS = [
  {
    id: 'identity',
    badge: 'المرحلة 01',
    title: 'بياناتك الشخصية',
    description: 'اكتب بياناتك كما ترغب أن تظهر في ملفك الشخصي.',
    hint: 'ستظهر هذه البيانات في ملفك الشخصي وللمدرسين.',
    icon: UserRound,
  },
  {
    id: 'guardian',
    badge: 'المرحلة 02',
    title: 'ولي الأمر والمتابعة',
    description: 'أضف بيانات ولي الأمر للتواصل المستمر ولمتابعة مستوى الطالب.',
    hint: 'سنستخدم هذا الرقم لإرسال تقارير المستوى والغياب.',
    icon: UsersRound,
  },
  {
    id: 'academic',
    badge: 'المرحلة 03',
    title: 'المسار الدراسي',
    description: 'حدد مرحلتك الدراسية ليتم تخصيص المناهج والامتحانات بما يناسبك.',
    hint: 'تأكد من صحة البيانات لضمان الوصول للمحتوى الصحيح.',
    icon: GraduationCap,
  },
  {
    id: 'security',
    badge: 'المرحلة 04',
    title: 'كلمة المرور',
    description: 'قم بإنشاء كلمة مرور قوية لحماية حسابك والبدء في استخدام المنصة.',
    hint: 'تأكد من حفظ كلمة المرور لتتمكن من تسجيل الدخول لاحقًا.',
    icon: ShieldCheck,
  },
] as const;

const STEP_FIELDS = [
  ['fullName', 'phoneNumber', 'secondaryPhone', 'dateOfBirth', 'gender', 'nationality', 'governorate', 'district', 'address', 'avatarSlug'],
  ['parentPhone', 'secondaryParentPhone', 'motherPhone', 'fatherDateOfBirth', 'motherDateOfBirth'],
  ['educationStage', 'gradeLevel', 'studyTrack', 'schoolName', 'schoolType'],
  ['password', 'confirmPassword'],
] as const;

const PANEL_ANIMATION = {
  initial: { opacity: 0, scale: 0.85, y: 50 },
  animate: { opacity: 1, scale: 1, y: 0 },
  exit: { opacity: 0, scale: 1.05, y: -30 },
  transition: { 
    type: "spring" as const, 
    stiffness: 400, 
    damping: 28, 
    mass: 1 
  },
};

export function RegistrationForm() {
  const router = useRouter();
  const { setAuth } = useAuthStore();

  const [formData, setFormData] = useState<RegistrationFormState>(EMPTY_FORM);
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<FormError[]>([]);
  const [activeStep, setActiveStep] = useState(0);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  // WhatsApp real-time verification via Evolution API
  const whatsAppState = useWhatsAppCheck(formData.phoneNumber);

  const currentStep = REGISTRATION_STEPS[activeStep];
  const carouselSteps = REGISTRATION_STEPS.map((step) => ({
    id: step.id,
    name: step.title,
    title: step.title,
    description: step.hint,
  }));

  const selectStyle = { backgroundColor: 'var(--admin-card-soft)', color: 'var(--admin-text)' };
  const optionStyle = { background: 'var(--admin-bg)', color: 'var(--admin-text)' };

  const fieldError = (name: string) => errors.find((e) => e.field === name)?.message;
  const inputCls = (name: string) => `auth-input${fieldError(name) ? ' auth-input--error' : ''}`;

  const passwordChecklist = useMemo(
    () => [
      { label: '8 أحرف على الأقل', valid: formData.password.length >= 8 },
      { label: 'كلمتا المرور متطابقتان', valid: !!formData.confirmPassword && formData.password === formData.confirmPassword },
      { label: 'جاهز لإنشاء الحساب', valid: formData.password.length >= 8 && formData.password === formData.confirmPassword },
    ],
    [formData.confirmPassword, formData.password],
  );

  const updateFieldValue = (name: string, value: string | boolean) => {
    setFormData((prev) => normalizeFormData({ ...prev, [name]: value }));
    setErrors((prev) => prev.filter((err) => err.field !== name));
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    const newValue = type === 'checkbox' ? (e.target as HTMLInputElement).checked : value;
    updateFieldValue(name, newValue);
    // Reset district when governorate changes
    if (name === 'governorate') {
      setFormData((prev) => normalizeFormData({ ...prev, district: '' }));
      setErrors((prev) => prev.filter((err) => err.field !== 'district'));
    }
  };

  const handleAcademicChange = (academic: AcademicData) => {
    setFormData((prev) => normalizeFormData({ ...prev, ...academic }));
    setErrors((prev) =>
      prev.filter((err) => !['educationStage', 'gradeLevel', 'studyTrack'].includes(err.field || '')),
    );
  };

  const mapIssuesToErrors = (issues: z.ZodIssue[]) =>
    issues.map((issue) => ({
      field: issue.path[0]?.toString(),
      message: issue.message,
    }));

  const findStepIndexForField = (field?: string) => {
    if (!field) return 0;
    const index = STEP_FIELDS.findIndex((fields) => fields.includes(field as never));
    return index === -1 ? 0 : index;
  };

  const validateStep = (stepIndex: number) => {
    const result = schema.safeParse(formData);
    if (result.success) return true;

    const stepFields = STEP_FIELDS[stepIndex];
    const relevantIssues = result.error.issues.filter((issue) => {
      const field = issue.path[0]?.toString();
      return !!field && stepFields.includes(field as never);
    });

    if (relevantIssues.length === 0) return true;

    setErrors((prev) => [
      ...prev.filter((err) => !stepFields.includes((err.field || '') as never)),
      ...mapIssuesToErrors(relevantIssues),
    ]);
    return false;
  };

  const goToNextStep = () => {
    if (!validateStep(activeStep)) return;
    setActiveStep((prev) => Math.min(prev + 1, REGISTRATION_STEPS.length - 1));
  };

  const goToStep = (stepIndex: number) => {
    if (stepIndex <= activeStep) {
      setActiveStep(stepIndex);
      return;
    }

    if (!validateStep(activeStep)) return;
    setActiveStep(stepIndex);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);

    const result = schema.safeParse(formData);
    if (!result.success) {
      const mappedErrors = mapIssuesToErrors(result.error.issues);
      setErrors(mappedErrors);
      setActiveStep(findStepIndexForField(mappedErrors[0]?.field));
      return;
    }

    setLoading(true);
    try {
      await authService.register({
        fullName: formData.fullName,
        phoneNumber: formData.phoneNumber,
        secondaryPhone: formData.secondaryPhone || undefined,
        password: formData.password,
        dateOfBirth: formData.dateOfBirth,
        gender: formData.gender as 'Male' | 'Female',
        nationality: formData.nationality || undefined,
        governorate: formData.governorate,
        district: formData.district,
        address: formData.address,
        parentPhone: formData.parentPhone || undefined,
        secondaryParentPhone: formData.secondaryParentPhone || undefined,
        motherPhone: formData.motherPhone || undefined,
        isFatherAlive: formData.isFatherAlive,
        isMotherAlive: formData.isMotherAlive,
        fatherDateOfBirth: formData.fatherDateOfBirth || undefined,
        motherDateOfBirth: formData.motherDateOfBirth || undefined,
        schoolName: formData.schoolName || undefined,
        schoolType: formData.schoolType || undefined,
        educationStage: formData.educationStage as 'Secondary' | 'Baccalaureate' | 'Primary' | 'Preparatory' | 'Azhari' | 'American',
        gradeLevel: formData.gradeLevel,
        studyTrack: formData.studyTrack || undefined,
        avatarSlug: formData.avatarSlug || undefined,
      });

      // Auto login after successful registration
      const loginRes = await authService.login({
        phoneNumber: formData.phoneNumber,
        password: formData.password,
        deviceFingerprint: getDeviceFingerprint(),
        deviceName: navigator.userAgent.slice(0, 100),
      });

      const { accessToken, refreshToken, user } = loginRes.data.data;

      setAuth(
        {
          id: user.id,
          fullName: user.fullName,
          phone: user.phone,
          roles: user.roles,
          profileComplete: user.profileComplete,
          avatarSlug: user.avatarSlug,
        },
        accessToken,
        refreshToken,
        true // rememberMe: true ensures session persistence
      );

      // Redirect directly to the student dashboard
      router.push('/student');
    } catch (err: unknown) {
      const message =
        typeof err === 'object' &&
        err !== null &&
        'response' in err &&
        typeof (err as { response?: { data?: { message?: unknown } } }).response?.data?.message === 'string'
          ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message as string)
          : 'عذرًا، فشل إنشاء الحساب. يُرجى المحاولة مرة أخرى لاحقًا.';

      const normalizedMessage = message.toLowerCase();
      const isDuplicatePhoneError =
        normalizedMessage.includes('phone number already registered') ||
        normalizedMessage.includes('رقم الهاتف') ||
        normalizedMessage.includes('مسجل بالفعل');

      if (isDuplicatePhoneError) {
        setActiveStep(0);
        setErrors([
          {
            field: 'phoneNumber',
            message: 'هذا الرقم مسجل مسبقًا. يمكنك تسجيل الدخول بدلاً من ذلك، أو تغيير الرقم.',
          },
        ]);
        return;
      }

      setErrors([
        {
          message,
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const renderPreviewPanel = () => {
    switch (activeStep) {
      case 0:
        return (
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-6 shadow-[0_24px_50px_var(--admin-shadow)] flex flex-col justify-between">
              <div>
                <p className="text-[0.65rem] font-black uppercase tracking-[0.25em] text-[var(--admin-primary)]">بطاقة الطالب</p>
                <div className="mt-4 flex items-center gap-3">
                  <UserAvatar avatarSlug={formData.avatarSlug} fullName={formData.fullName || 'طالب'} size="md" />
                  <h3 className="text-xl font-black text-[var(--admin-text)] leading-tight">
                    {formData.fullName || 'الاسم الرباعي'}
                  </h3>
                </div>
              </div>
              <div className="mt-6 flex flex-wrap gap-2 text-xs font-bold text-[var(--admin-muted)]">
                <span className="rounded-full bg-[var(--admin-bg)]/50 px-4 py-2">{formData.phoneNumber || 'رقم الهاتف'}</span>
                <span className="rounded-full bg-[var(--admin-bg)]/50 px-4 py-2">{formData.governorate || 'المحافظة'}</span>
              </div>
            </div>
            <div className="grid gap-4">
              <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                <p className="text-[0.65rem] font-bold text-[var(--admin-muted)] uppercase tracking-wider">المنطقة السكنية</p>
                <p className="mt-2 text-xl font-black text-[var(--admin-text)]">{formData.district || 'اختر المنطقة السكنية'}</p>
              </div>
              {formData.dateOfBirth ? (() => {
                const info = computeBirthdayInfo(formData.dateOfBirth);
                return (
                  <>
                    <div className="rounded-[24px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/5 to-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                      <p className="text-[0.65rem] font-bold text-[var(--admin-muted)] uppercase tracking-wider">سنك دلوقتي</p>
                      <p className="mt-2 text-2xl font-black text-[var(--admin-primary)] flex flex-wrap gap-1 items-baseline">
                        {info.ageYears} <span className="text-base font-bold text-[var(--admin-muted)] ml-1">سنة</span>
                        {info.ageMonths > 0 && <> و {info.ageMonths} <span className="text-base font-bold text-[var(--admin-muted)] ml-1">شهر</span></>}
                        {info.ageDays > 0 && <> و {info.ageDays} <span className="text-base font-bold text-[var(--admin-muted)] ml-1">يوم</span></>}
                      </p>
                    </div>
                    <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                      <p className="text-[0.65rem] font-bold text-[var(--admin-muted)] uppercase tracking-wider">عيد ميلادك 🎂</p>
                      <p className="mt-2 text-2xl font-black text-[var(--admin-text)]">باقي {info.daysToNextBirthday} <span className="text-base font-bold text-[var(--admin-muted)]">يوم</span></p>
                    </div>
                  </>
                );
              })() : (
                <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                  <p className="text-[0.65rem] font-bold text-[var(--admin-muted)] uppercase tracking-wider">تاريخ الميلاد</p>
                  <p className="mt-2 text-xl font-black text-[var(--admin-muted)]/50">اختر تاريخ الميلاد</p>
                </div>
              )}
            </div>
          </div>
        );
      case 1:
        return (
          <div className="space-y-4">
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-6 shadow-[0_24px_50px_var(--admin-shadow)]">
              <p className="text-[0.65rem] font-black uppercase tracking-[0.25em] text-[var(--admin-primary)]">جهة المتابعة</p>
              <h3 className="mt-4 text-2xl font-black text-[var(--admin-text)] tracking-wider">{formData.parentPhone || 'رقم هاتف ولي الأمر'}</h3>
              <p className="mt-2 text-sm text-[var(--admin-muted)] leading-7">هذا الرقم سيستخدم للتواصل والمتابعة عند الحاجة.</p>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                <p className="text-xs font-bold text-[var(--admin-muted)]">الأب</p>
                <p className="mt-2 text-lg font-black text-[var(--admin-text)]">{formData.isFatherAlive ? 'على قيد الحياة' : 'متوفى'}</p>
              </div>
              <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-5 backdrop-blur-sm">
                <p className="text-xs font-bold text-[var(--admin-muted)]">الأم</p>
                <p className="mt-2 text-lg font-black text-[var(--admin-text)]">{formData.isMotherAlive ? 'على قيد الحياة' : 'متوفاة'}</p>
              </div>
            </div>
          </div>
        );
      case 2:
        return (
          <div className="space-y-4">
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-6 shadow-[0_24px_50px_var(--admin-shadow)]">
              <p className="text-[0.65rem] font-black uppercase tracking-[0.25em] text-[var(--admin-primary)]">المسار الحالي</p>
              <div className="mt-5 flex flex-wrap gap-2">
                <span className="rounded-full bg-[var(--admin-bg)]/80 px-4 py-2.5 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)]">
                  {formData.educationStage === 'Secondary' ? 'ثانوية' : formData.educationStage === 'Baccalaureate' ? 'بكالوريا' : 'المرحلة الدراسية'}
                </span>
                <span className="rounded-full bg-[var(--admin-bg)]/80 px-4 py-2.5 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)]">
                  {formData.gradeLevel || 'الصف الدراسي'}
                </span>
                {requiresTrack(formData.gradeLevel) ? (
                  <span className="rounded-full bg-[var(--admin-bg)]/80 px-4 py-2.5 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)]">
                    {formData.studyTrack || 'الشعبة / التخصص'}
                  </span>
                ) : null}
              </div>
            </div>
            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)]/90 p-6 backdrop-blur-sm">
              <p className="text-sm leading-7 text-[var(--admin-muted)] font-medium">
                بمجرد تثبيت هذه الخطوة، النظام سيعرض لك الخطة والواجبات والاختبارات الملائمة تمامًا لمرحلتك.
              </p>
            </div>
          </div>
        );
      default:
        return (
          <div className="space-y-4">
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-6 shadow-[0_24px_50px_var(--admin-shadow)]">
              <p className="text-[0.65rem] font-black uppercase tracking-[0.25em] text-[var(--admin-primary)]">جاهزية الحساب</p>
              <div className="mt-5 space-y-3">
                {passwordChecklist.map((item) => (
                  <div key={item.label} className="flex items-center justify-between rounded-[20px] bg-[var(--admin-bg)]/90 px-5 py-4 backdrop-blur-md">
                    <span className="text-[0.85rem] font-bold text-[var(--admin-text)]">{item.label}</span>
                    <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${item.valid ? 'bg-[var(--admin-primary)] text-[var(--admin-bg)]' : 'bg-[var(--admin-border)] text-[var(--admin-muted)]'}`}>
                      {item.valid ? <Check className="h-4 w-4" /> : <LockKeyhole className="h-4 w-4" />}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        );
    }
  };

  const renderStepFields = () => {
    switch (activeStep) {
      case 0:
        return (
          <div className="space-y-4">
            <div>
              <label className="auth-label">اختر الأفاتار الخاص بك (شخصيات تاريخية وعلماء)</label>
              <div className="flex gap-4 overflow-x-auto pb-3 pt-1 scrollbar-thin scrollbar-thumb-[var(--admin-border)] scrollbar-track-transparent">
                {AVATAR_LIST.map((avatar, index) => {
                  const isSelected = formData.avatarSlug === avatar.slug;
                  return (
                    <button
                      key={avatar.slug}
                      type="button"
                      onClick={() => updateFieldValue('avatarSlug', avatar.slug)}
                      className={`relative flex flex-col items-center gap-2 p-2 rounded-2xl border transition-all duration-300 flex-shrink-0 w-24 hover:scale-105 ${
                        isSelected
                          ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/5 ring-2 ring-[var(--admin-primary)] shadow-[0_8px_20px_var(--admin-shadow)]'
                          : 'border-[var(--admin-border)] bg-[var(--admin-bg)] hover:border-[var(--admin-text)]'
                      }`}
                    >
                      <div className="relative w-16 h-16 rounded-full overflow-hidden border border-[var(--admin-border)]">
                        <Image
                          src={avatar.imageUrl}
                          alt={avatar.name}
                          fill
                          sizes="64px"
                          className="object-cover"
                          loading={index === 0 ? 'eager' : 'lazy'}
                          priority={index === 0}
                          unoptimized
                        />
                      </div>
                      <span className="text-[11px] font-black text-[var(--admin-text)] text-center truncate w-full">
                        {avatar.name}
                      </span>
                      {isSelected && (
                        <span className="absolute top-1 left-1 flex h-5 w-5 items-center justify-center rounded-full bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-md">
                          <Check className="h-3 w-3" />
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
              
              {/* Selected Avatar Detailed Info Box */}
              {formData.avatarSlug && (
                <div className="mt-3 p-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-right flex gap-3 items-center shadow-inner">
                  <div className="relative w-12 h-12 rounded-full overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-bg)] shrink-0">
                    <Image
                      src={AVATAR_LIST.find(a => a.slug === formData.avatarSlug)?.imageUrl || ''}
                      alt="Selected"
                      fill
                      sizes="48px"
                      className="object-cover"
                      unoptimized
                    />
                  </div>
                  <div className="space-y-0.5">
                    <h5 className="text-[12px] font-black text-[var(--admin-primary-strong)]">
                      {AVATAR_LIST.find(a => a.slug === formData.avatarSlug)?.name}
                    </h5>
                    <p className="text-[10px] font-bold text-[var(--admin-muted)] leading-normal">
                      {AVATAR_LIST.find(a => a.slug === formData.avatarSlug)?.info}
                    </p>
                  </div>
                </div>
              )}
            </div>

            <div>
              <label className="auth-label" htmlFor="reg-fullName">الاسم الرباعي</label>
              <input
                id="reg-fullName"
                name="fullName"
                type="text"
                className={inputCls('fullName')}
                placeholder="مثال: أحمد محمد محمود علي"
                value={formData.fullName ?? ''}
                onChange={handleChange}
              />
              {fieldError('fullName') && <p className="auth-field-error">{fieldError('fullName')}</p>}
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-phone">رقم الهاتف الأساسي</label>
                <input
                  id="reg-phone"
                  name="phoneNumber"
                  type="tel"
                  dir="ltr"
                  className={inputCls('phoneNumber')}
                  placeholder="مثال: 01012345678"
                  value={formData.phoneNumber ?? ''}
                  onChange={handleChange}
                />
                {fieldError('phoneNumber') && <p className="auth-field-error">{fieldError('phoneNumber')}</p>}
                {/* WhatsApp auto-check indicator */}
                {whatsAppState.status !== 'idle' && (
                  <div className={`mt-2 flex items-center gap-2 rounded-xl px-3 py-2 text-xs font-bold transition-all ${
                    whatsAppState.color === 'green' ? 'bg-emerald-500/10 text-emerald-600' :
                    whatsAppState.color === 'red' ? 'bg-red-500/10 text-red-500' :
                    whatsAppState.color === 'amber' ? 'bg-amber-500/10 text-amber-600' :
                    'bg-[var(--admin-bg)]/50 text-[var(--admin-muted)]'
                  }`}>
                    {whatsAppState.status === 'checking' && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                    {whatsAppState.label}
                  </div>
                )}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-secondaryPhone">رقم هاتف إضافي <span className="text-[var(--admin-muted)] text-xs">(اختياري)</span></label>
                <input
                  id="reg-secondaryPhone"
                  name="secondaryPhone"
                  type="tel"
                  dir="ltr"
                  className={inputCls('secondaryPhone')}
                  placeholder="مثال: 01112345678"
                  value={formData.secondaryPhone ?? ''}
                  onChange={handleChange}
                />
                {fieldError('secondaryPhone') && <p className="auth-field-error">{fieldError('secondaryPhone')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-dob">تاريخ الميلاد</label>
                <input
                  id="reg-dob"
                  name="dateOfBirth"
                  type="date"
                  dir="ltr"
                  className={inputCls('dateOfBirth')}
                  style={selectStyle}
                  max="2019-12-31"
                  value={formData.dateOfBirth ?? ''}
                  onChange={handleChange}
                />
                {fieldError('dateOfBirth') && <p className="auth-field-error">{fieldError('dateOfBirth')}</p>}

              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-gender">النوع</label>
                <select
                  id="reg-gender"
                  name="gender"
                  data-select
                  className={inputCls('gender')}
                  value={formData.gender ?? ''}
                  onChange={handleChange}
                  style={selectStyle}
                >
                  <option key="gender-placeholder" value="" disabled style={optionStyle}>اختر النوع...</option>
                  <option key="gender-male" value="Male" style={optionStyle}>ذكر</option>
                  <option key="gender-female" value="Female" style={optionStyle}>أنثى</option>
                </select>
                {fieldError('gender') && <p className="auth-field-error">{fieldError('gender')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-nationality">الجنسية</label>
                <select
                  id="reg-nationality"
                  name="nationality"
                  data-select
                  className={inputCls('nationality')}
                  value={formData.nationality ?? ''}
                  onChange={handleChange}
                  style={selectStyle}
                >
                  <option value="" disabled style={optionStyle}>اختر الجنسية...</option>
                  {ARAB_NATIONALITIES.map((n) => (
                    <option key={n} value={n} style={optionStyle}>{n}</option>
                  ))}
                </select>
                {fieldError('nationality') && <p className="auth-field-error">{fieldError('nationality')}</p>}
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-governorate">المحافظة</label>
                <select
                  id="reg-governorate"
                  name="governorate"
                  data-select
                  className={inputCls('governorate')}
                  value={formData.governorate ?? ''}
                  onChange={handleChange}
                  style={selectStyle}
                >
                  <option value="" disabled style={optionStyle}>اختر المحافظة...</option>
                  {EGYPTIAN_GOVERNORATES.map((gov) => (
                    <option key={gov} value={gov} style={optionStyle}>{gov}</option>
                  ))}
                </select>
                {fieldError('governorate') && <p className="auth-field-error">{fieldError('governorate')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-district">المنطقة / الحي</label>
                <select
                  id="reg-district"
                  name="district"
                  data-select
                  className={inputCls('district')}
                  value={formData.district ?? ''}
                  onChange={handleChange}
                  style={selectStyle}
                  disabled={!formData.governorate}
                >
                  <option value="" disabled style={optionStyle}>
                    {formData.governorate ? 'اختر المنطقة / الحي...' : 'اختر المحافظة أولاً'}
                  </option>
                  {getDistrictsForGovernorate(formData.governorate ?? '').map((d) => (
                    <option key={d} value={d} style={optionStyle}>{d}</option>
                  ))}
                </select>
                {fieldError('district') && <p className="auth-field-error">{fieldError('district')}</p>}
              </div>
            </div>

            <div>
              <label className="auth-label" htmlFor="reg-address">العنوان بالتفصيل</label>
              <input
                id="reg-address"
                name="address"
                type="text"
                className={inputCls('address')}
                placeholder="مثال: 15 شارع المحطة، الدور الثاني، بجوار المسجد"
                value={formData.address ?? ''}
                onChange={handleChange}
              />
              {fieldError('address') && <p className="auth-field-error">{fieldError('address')}</p>}
            </div>
          </div>
        );
      case 1:
        return (
          <div className="space-y-4">
            {/* ── RadioGroups: alive status ── */}
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-3">
                <label className="auth-label">حالة الأب</label>
                <RadioGroup
                  name="isFatherAlive"
                  value={String(formData.isFatherAlive)}
                  onChange={(val) => {
                    const isAlive = val === 'true';
                    setFormData((p) => normalizeFormData({ ...p, isFatherAlive: isAlive, parentPhone: isAlive ? p.parentPhone : '', fatherDateOfBirth: isAlive ? p.fatherDateOfBirth : '' }));
                  }}
                  orientation="horizontal"
                >
                  <Radio value="true">
                    <Radio.Indicator />
                    <Radio.Content>على قيد الحياة</Radio.Content>
                  </Radio>
                  <Radio value="false">
                    <Radio.Indicator />
                    <Radio.Content>متوفى</Radio.Content>
                  </Radio>
                </RadioGroup>
              </div>

              <div className="flex flex-col gap-3">
                <label className="auth-label">حالة الأم</label>
                <RadioGroup
                  name="isMotherAlive"
                  value={String(formData.isMotherAlive)}
                  onChange={(val) => {
                    const isAlive = val === 'true';
                    setFormData((p) => normalizeFormData({ ...p, isMotherAlive: isAlive, motherPhone: isAlive ? p.motherPhone : '', motherDateOfBirth: isAlive ? p.motherDateOfBirth : '' }));
                  }}
                  orientation="horizontal"
                >
                  <Radio value="true">
                    <Radio.Indicator />
                    <Radio.Content>على قيد الحياة</Radio.Content>
                  </Radio>
                  <Radio value="false">
                    <Radio.Indicator />
                    <Radio.Content>متوفاة</Radio.Content>
                  </Radio>
                </RadioGroup>
              </div>
            </div>

            {/* ── Father phone + birthday (conditional) ── */}
            {formData.isFatherAlive && (
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="auth-label" htmlFor="reg-parentPhone">رقم تليفون الأب</label>
                  <input id="reg-parentPhone" name="parentPhone" type="tel" dir="ltr" className={inputCls('parentPhone')} placeholder="مثال: 01212345678" value={formData.parentPhone ?? ''} onChange={handleChange} />
                  {fieldError('parentPhone') && <p className="auth-field-error">{fieldError('parentPhone')}</p>}
                </div>
                <div>
                  <label className="auth-label" htmlFor="reg-fatherDob">عيد ميلاد الأب</label>
                  <input id="reg-fatherDob" name="fatherDateOfBirth" type="date" dir="ltr" className={inputCls('fatherDateOfBirth')} style={selectStyle} value={formData.fatherDateOfBirth ?? ''} onChange={handleChange} />
                  {formData.fatherDateOfBirth && (
                    <span className="mt-1 inline-flex rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-bold text-[var(--admin-text)]">
                      باقي {computeBirthdayInfo(formData.fatherDateOfBirth).daysToNextBirthday} يوم على عيد ميلاد الأب
                    </span>
                  )}
                </div>
              </div>
            )}

            {/* ── Mother phone + birthday (conditional) ── */}
            {formData.isMotherAlive && (
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="auth-label" htmlFor="reg-motherPhone">رقم تليفون الأم</label>
                  <input id="reg-motherPhone" name="motherPhone" type="tel" dir="ltr" className={inputCls('motherPhone')} placeholder="مثال: 01512345678" value={formData.motherPhone ?? ''} onChange={handleChange} />
                  {fieldError('motherPhone') && <p className="auth-field-error">{fieldError('motherPhone')}</p>}
                </div>
                <div>
                  <label className="auth-label" htmlFor="reg-motherDob">عيد ميلاد الأم</label>
                  <input id="reg-motherDob" name="motherDateOfBirth" type="date" dir="ltr" className={inputCls('motherDateOfBirth')} style={selectStyle} value={formData.motherDateOfBirth ?? ''} onChange={handleChange} />
                  {formData.motherDateOfBirth && (
                    <span className="mt-1 inline-flex rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-bold text-[var(--admin-text)]">
                      باقي {computeBirthdayInfo(formData.motherDateOfBirth).daysToNextBirthday} يوم على عيد ميلاد الأم
                    </span>
                  )}
                </div>
              </div>
            )}

            {/* ── Secondary parent phone ── */}
            <div>
              <label className="auth-label" htmlFor="reg-secondaryParentPhone">رقم ولي أمر إضافي</label>
              <input id="reg-secondaryParentPhone" name="secondaryParentPhone" type="tel" dir="ltr" className={inputCls('secondaryParentPhone')} placeholder="مثال: 01312345678" value={formData.secondaryParentPhone ?? ''} onChange={handleChange} />
              {fieldError('secondaryParentPhone') && <p className="auth-field-error">{fieldError('secondaryParentPhone')}</p>}
            </div>
          </div>
        );
      case 2:
        return (
          <div className="space-y-4">
            {/* ── School name + type ── */}
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-schoolName">اسم المدرسة</label>
                <input id="reg-schoolName" name="schoolName" type="text" className={inputCls('schoolName')} placeholder="مثال: مدرسة الاتحاد اللغات" value={formData.schoolName ?? ''} onChange={handleChange} />
                {fieldError('schoolName') && <p className="auth-field-error">{fieldError('schoolName')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-schoolType">نوع المدرسة</label>
                <select id="reg-schoolType" name="schoolType" data-select className={inputCls('schoolType')} value={formData.schoolType ?? ''} onChange={handleChange} style={selectStyle}>
                  <option value="" disabled style={optionStyle}>اختر نوع المدرسة...</option>
                  {SCHOOL_TYPES.map((s) => (
                    <option key={s.value} value={s.value} style={optionStyle}>{s.label}</option>
                  ))}
                </select>
                {fieldError('schoolType') && <p className="auth-field-error">{fieldError('schoolType')}</p>}
              </div>
            </div>
            {/* ── Stage / Grade / Track ── */}
            <AcademicFields
              data={{
                educationStage: formData.educationStage as AcademicData['educationStage'],
                gradeLevel: formData.gradeLevel as AcademicData['gradeLevel'],
                studyTrack: formData.studyTrack as AcademicData['studyTrack'],
              }}
              onChange={handleAcademicChange}
              errors={{
                educationStage: fieldError('educationStage'),
                gradeLevel: fieldError('gradeLevel'),
                studyTrack: fieldError('studyTrack'),
              }}
              inputCls={inputCls}
            />
          </div>
        );
      default:
        return (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-password">كلمة المرور</label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reg-password"
                    name="password"
                    type={showPassword ? 'text' : 'password'}
                    className={inputCls('password')}
                    placeholder="••••••••"
                    style={{ paddingRight: '2.75rem' }}
                    value={formData.password ?? ''}
                    onChange={handleChange}
                  />
                  <button
                    type="button"
                    className="auth-input-action"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'إخفاء' : 'إظهار'}
                  >
                    {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
                {fieldError('password') && <p className="auth-field-error">{fieldError('password')}</p>}
              </div>

              <div>
                <label className="auth-label" htmlFor="reg-confirm">تأكيد كلمة المرور</label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reg-confirm"
                    name="confirmPassword"
                    type={showConfirmPassword ? 'text' : 'password'}
                    className={inputCls('confirmPassword')}
                    placeholder="••••••••"
                    style={{ paddingRight: '2.75rem' }}
                    value={formData.confirmPassword ?? ''}
                    onChange={handleChange}
                  />
                  <button
                    type="button"
                    className="auth-input-action"
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    aria-label={showConfirmPassword ? 'إخفاء' : 'إظهار'}
                  >
                    {showConfirmPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
                {fieldError('confirmPassword') && <p className="auth-field-error">{fieldError('confirmPassword')}</p>}
              </div>
            </div>

            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
              <p className="mb-3 text-sm font-bold text-[var(--admin-text)]">متطلبات إنشاء الحساب:</p>
              <div className="space-y-2">
                {passwordChecklist.map((item) => (
                  <div key={item.label} className="flex items-center gap-3 text-sm">
                    <span className={`flex h-6 w-6 items-center justify-center rounded-full ${item.valid ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-bg)] text-[var(--admin-muted)]'}`}>
                      {item.valid ? <Check className="h-3.5 w-3.5" /> : <LockKeyhole className="h-3.5 w-3.5" />}
                    </span>
                    <span className="font-medium text-[var(--admin-text)]">{item.label}</span>
                  </div>
                ))}
              </div>
            </div>


          </div>
        );
    }
  };

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.32 }}
      className="relative flex min-h-[560px] w-full lg:min-h-[650px]"
    >
      <FeatureCarousel
        title={currentStep.title}
        description={currentStep.hint}
        steps={carouselSteps}
        step={activeStep}
        onStepChange={goToStep}
        autoPlay={false}
        clickToAdvance={false}
        bgClass="bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] min-h-[560px] lg:min-h-[650px] shadow-[0_28px_70px_var(--admin-shadow)]"
        step1img1Class="pointer-events-none w-[38%] rounded-2xl left-[8%] top-[24%] shadow-[0_22px_60px_var(--admin-shadow)]"
        step1img2Class="pointer-events-none w-[44%] rounded-2xl left-[34%] top-[38%] shadow-[0_24px_64px_var(--admin-shadow)]"
        step2img1Class="pointer-events-none w-[42%] rounded-2xl left-[8%] top-[24%] shadow-[0_22px_60px_var(--admin-shadow)]"
        step2img2Class="pointer-events-none w-[36%] rounded-2xl left-[44%] top-[38%] shadow-[0_24px_64px_var(--admin-shadow)]"
        step3imgClass="pointer-events-none w-[56%] rounded-2xl left-[14%] top-[24%] shadow-[0_24px_64px_var(--admin-shadow)]"
        step4imgClass="pointer-events-none w-[56%] rounded-2xl left-[14%] top-[24%] shadow-[0_24px_64px_var(--admin-shadow)]"
        image={{
          step1light1: '/images/register-stage-1a.svg',
          step1light2: '/images/register-stage-1b.svg',
          step2light1: '/images/register-stage-2a.svg',
          step2light2: '/images/register-stage-2b.svg',
          step3light: '/images/register-stage-3.svg',
          step4light: '/images/register-stage-4.svg',
          alt: 'مراحل إنشاء حساب جديد',
        }}
      >
        <div className="relative z-10 mt-4 w-full md:mt-10 md:pr-0">
          <div className="flex flex-col lg:flex-row gap-8 lg:gap-12 lg:items-start w-full">
            {/* Right Side: Form Inputs */}
            <div className="w-full lg:w-[50%] xl:w-[50%] flex flex-col gap-5">
              <AnimatePresence>
                {errors.filter((e) => !e.field).map((err, index) => (
                  <motion.div
                    key={`${err.message}-${index}`}
                    initial={{ opacity: 0, height: 0 }}
                    animate={{ opacity: 1, height: 'auto' }}
                    exit={{ opacity: 0, height: 0 }}
                    className="auth-error-banner mb-4"
                  >
                    {err.message}
                  </motion.div>
                ))}
              </AnimatePresence>

              <div className="min-h-[420px] w-full sm:min-h-[520px]">
                <AnimatePresence mode="wait">
                  <motion.div key={activeStep} {...PANEL_ANIMATION} className="space-y-5 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-md sm:rounded-[28px] sm:p-7 shadow-[0_12px_40px_var(--admin-shadow)]">
                    {renderStepFields()}
                  </motion.div>
                </AnimatePresence>
              </div>

              <div className="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-start max-w-[560px]">
                {activeStep > 0 && (
                  <button
                    type="button"
                    onClick={() => setActiveStep((prev) => Math.max(prev - 1, 0))}
                    className="inline-flex items-center justify-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-3 text-sm font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                  >
                    <ArrowRight className="h-4 w-4" />
                    رجوع
                  </button>
                )}

                {activeStep < REGISTRATION_STEPS.length - 1 ? (
                  <InteractiveHoverButton type="button" onClick={goToNextStep} className="w-full sm:w-auto h-12 flex items-center justify-center px-10" style={{ '--btn-primary': 'var(--admin-primary)', '--btn-fg': 'var(--admin-primary-contrast)', '--btn-border': 'var(--admin-border)', '--btn-bg': 'var(--admin-card-soft)', '--btn-text': 'var(--admin-text)' } as React.CSSProperties}>
                    التالي
                  </InteractiveHoverButton>
                ) : (
                  <InteractiveHoverButton type="submit" disabled={loading} icon={<CheckCircle2 className="h-4 w-4" />} className="w-full sm:w-auto h-12 flex items-center justify-center px-10" style={{ '--btn-primary': 'var(--admin-primary)', '--btn-fg': 'var(--admin-primary-contrast)', '--btn-border': 'var(--admin-border)', '--btn-bg': 'var(--admin-card-soft)', '--btn-text': 'var(--admin-text)' } as React.CSSProperties}>
                    {loading ? 'جاري إنشاء الحساب...' : 'إنشاء الحساب'}
                  </InteractiveHoverButton>
                )}
              </div>

              <div className="flex items-start gap-2 text-[0.65rem] font-medium text-[var(--admin-muted)] max-w-[480px] leading-5">
                <MapPinned className="h-3 w-3 mt-1 shrink-0" />
                <span>بياناتك محفوظة محليًا في هذه الصفحة، ولن يتم إرسالها أو حفظها بشكل نهائي إلا بعد الضغط على &quot;إنشاء الحساب&quot;.</span>
              </div>
            </div>

            {/* Left Side: Live Preview Panel */}
            <div className="hidden lg:block lg:w-[50%] xl:w-[45%] relative">
               <AnimatePresence mode="wait">
                 <motion.div key={`preview-${activeStep}`} {...PANEL_ANIMATION} className="sticky top-10">
                   {renderPreviewPanel()}
                 </motion.div>
               </AnimatePresence>
            </div>
          </div>
        </div>
      </FeatureCarousel>
    </motion.form>
  );
}
