'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AnimatePresence, motion } from 'framer-motion';
import {
  ArrowLeft,
  ArrowRight,
  Check,
  CheckCircle2,
  Eye,
  EyeOff,
  GraduationCap,
  LockKeyhole,
  MapPinned,
  ShieldCheck,
  Sparkles,
  UserRound,
  UsersRound,
} from 'lucide-react';
import { z } from 'zod';

import { AcademicFields, requiresTrack } from '@/components/registration/AcademicFields';
import type { AcademicData } from '@/components/registration/AcademicFields';
import { FeatureCarousel } from '@/components/ui/feature-carousel';
import { authService } from '@/services/auth-service';

const EGYPTIAN_GOVERNORATES = [
  'القاهرة', 'الجيزة', 'الإسكندرية', 'الدقهلية', 'البحيرة', 'الفيوم',
  'الغربية', 'الإسماعيلية', 'المنوفية', 'المنيا', 'القليوبية', 'الوادي الجديد',
  'السويس', 'أسوان', 'أسيوط', 'بني سويف', 'بورسعيد', 'دمياط',
  'الشرقية', 'جنوب سيناء', 'كفر الشيخ', 'مطروح', 'الأقصر', 'قنا',
  'شمال سيناء', 'سوهاج', 'البحر الأحمر',
];

const schema = z
  .object({
    fullName: z.string().min(5, 'يرجى إدخال الاسم الرباعي')
      .refine((n) => n.trim().split(/\s+/).length >= 4, 'الاسم لازم يكون رباعي على الأقل'),
    phoneNumber: z.string().regex(/^01[0125]\d{8}$/, 'رقم هاتف مصري غير صالح'),
    studentCode: z.string().min(1, 'كود الطالب (دوستاب) مطلوب'),
    dateOfBirth: z.string().min(1, 'تاريخ الميلاد مطلوب'),
    gender: z.enum(['Male', 'Female'], { message: 'اختر النوع' }),
    governorate: z.string().min(1, 'اختر المحافظة'),
    address: z.string().min(3, 'يرجى كتابة العنوان'),
    parentPhone: z.string().regex(/^01[0125]\d{8}$/, 'رقم هاتف ولي الأمر غير صالح'),
    isFatherAlive: z.boolean(),
    isMotherAlive: z.boolean(),
    educationStage: z.enum(['Secondary', 'Baccalaureate'], { message: 'اختر المرحلة الدراسية' }),
    gradeLevel: z.string().min(1, 'اختر الصف الدراسي'),
    studyTrack: z.string().optional(),
    password: z.string().min(8, 'كلمة المرور 8 أحرف على الأقل'),
    confirmPassword: z.string(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: 'كلمتا المرور غير متطابقتين',
    path: ['confirmPassword'],
  })
  .refine((d) => {
    if (requiresTrack(d.gradeLevel) && !d.studyTrack) return false;
    return true;
  }, {
    message: 'اختر الشعبة / التخصص',
    path: ['studyTrack'],
  });

type FormError = { field?: string; message: string };

const EMPTY_FORM = {
  fullName: '',
  phoneNumber: '',
  studentCode: '',
  dateOfBirth: '',
  gender: '' as 'Male' | 'Female' | '',
  governorate: '',
  address: '',
  parentPhone: '',
  isFatherAlive: true,
  isMotherAlive: true,
  educationStage: '' as 'Secondary' | 'Baccalaureate' | '',
  gradeLevel: '',
  studyTrack: '',
  password: '',
  confirmPassword: '',
};

const REGISTRATION_STEPS = [
  {
    id: 'identity',
    badge: 'المرحلة 01',
    title: 'هويتك الأساسية',
    description: 'نجمع بياناتك الشخصية كما ستظهر في النظام وفي المتابعة مع المدرس.',
    hint: 'كل ما في هذه الخطوة يحدد بطاقتك داخل المنصة.',
    icon: UserRound,
  },
  {
    id: 'guardian',
    badge: 'المرحلة 02',
    title: 'ولي الأمر والمتابعة',
    description: 'نثبت وسيلة التواصل الرئيسية التي سنرجع لها عند المتابعة والتنبيه.',
    hint: 'هذه الخطوة تجعل الحساب جاهزًا للمتابعة الأسرية والانضباط.',
    icon: UsersRound,
  },
  {
    id: 'academic',
    badge: 'المرحلة 03',
    title: 'المسار الدراسي',
    description: 'نضبط المرحلة والصف والتخصص حتى تكون الخطة والمحتوى مناسبين لك.',
    hint: 'المحتوى والامتحانات سيتشكلان بناءً على هذه الخطوة.',
    icon: GraduationCap,
  },
  {
    id: 'security',
    badge: 'المرحلة 04',
    title: 'تأمين الحساب',
    description: 'آخر خطوة قبل تشغيل الحساب: كلمة مرور قوية تؤكد أنك جاهز للبدء.',
    hint: 'بعد هذه الخطوة يصبح الحساب جاهزًا للدخول مباشرة.',
    icon: ShieldCheck,
  },
] as const;

const STEP_FIELDS = [
  ['fullName', 'studentCode', 'phoneNumber', 'dateOfBirth', 'gender', 'governorate', 'address'],
  ['parentPhone'],
  ['educationStage', 'gradeLevel', 'studyTrack'],
  ['password', 'confirmPassword'],
] as const;

const PANEL_ANIMATION = {
  initial: { opacity: 0, y: 14 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -14 },
  transition: { duration: 0.28, ease: [0.22, 1, 0.36, 1] as const },
};

export function RegistrationForm() {
  const router = useRouter();

  const [formData, setFormData] = useState(EMPTY_FORM);
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<FormError[]>([]);
  const [activeStep, setActiveStep] = useState(0);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

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
      { label: 'تأكيد مطابق', valid: !!formData.confirmPassword && formData.password === formData.confirmPassword },
      { label: 'جاهز لتفعيل الحساب', valid: formData.password.length >= 8 && formData.password === formData.confirmPassword },
    ],
    [formData.confirmPassword, formData.password],
  );

  const updateFieldValue = (name: string, value: string | boolean) => {
    setFormData((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => prev.filter((err) => err.field !== name));
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    const newValue = type === 'checkbox' ? (e.target as HTMLInputElement).checked : value;
    updateFieldValue(name, newValue);
  };

  const handleAcademicChange = (academic: AcademicData) => {
    setFormData((prev) => ({ ...prev, ...academic }));
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
        password: formData.password,
        studentCode: formData.studentCode,
        dateOfBirth: formData.dateOfBirth,
        gender: formData.gender as 'Male' | 'Female',
        governorate: formData.governorate,
        address: formData.address,
        parentPhone: formData.parentPhone,
        isFatherAlive: formData.isFatherAlive,
        isMotherAlive: formData.isMotherAlive,
        educationStage: formData.educationStage as 'Secondary' | 'Baccalaureate',
        gradeLevel: formData.gradeLevel,
        studyTrack: formData.studyTrack || undefined,
      });

      router.push('/login?registered=true');
    } catch (err: unknown) {
      const message =
        typeof err === 'object' &&
        err !== null &&
        'response' in err &&
        typeof (err as { response?: { data?: { message?: unknown } } }).response?.data?.message === 'string'
          ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message as string)
          : 'فشل إنشاء الحساب. ربما رقم الهاتف مسجّل من قبل.';

      setErrors([
        {
          message,
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const renderStepFields = () => {
    switch (activeStep) {
      case 0:
        return (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-fullName">الاسم الرباعي</label>
                <input
                  id="reg-fullName"
                  name="fullName"
                  type="text"
                  className={inputCls('fullName')}
                  placeholder="أدخل اسمك الرباعي"
                  value={formData.fullName}
                  onChange={handleChange}
                />
                {fieldError('fullName') && <p className="auth-field-error">{fieldError('fullName')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-studentCode">كود الطالب</label>
                <input
                  id="reg-studentCode"
                  name="studentCode"
                  type="text"
                  dir="ltr"
                  className={inputCls('studentCode')}
                  placeholder="كود الطالب"
                  value={formData.studentCode}
                  onChange={handleChange}
                />
                {fieldError('studentCode') && <p className="auth-field-error">{fieldError('studentCode')}</p>}
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="auth-label" htmlFor="reg-phone">رقم الهاتف</label>
                <input
                  id="reg-phone"
                  name="phoneNumber"
                  type="tel"
                  dir="ltr"
                  className={inputCls('phoneNumber')}
                  placeholder="01XXXXXXXXX"
                  value={formData.phoneNumber}
                  onChange={handleChange}
                />
                {fieldError('phoneNumber') && <p className="auth-field-error">{fieldError('phoneNumber')}</p>}
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
                  value={formData.dateOfBirth}
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
                  value={formData.gender}
                  onChange={handleChange}
                  style={selectStyle}
                >
                  <option value="" disabled style={optionStyle}>اختر النوع...</option>
                  <option value="Male" style={optionStyle}>ذكر</option>
                  <option value="Female" style={optionStyle}>أنثى</option>
                </select>
                {fieldError('gender') && <p className="auth-field-error">{fieldError('gender')}</p>}
              </div>
              <div>
                <label className="auth-label" htmlFor="reg-governorate">المحافظة</label>
                <select
                  id="reg-governorate"
                  name="governorate"
                  data-select
                  className={inputCls('governorate')}
                  value={formData.governorate}
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
            </div>

            <div>
              <label className="auth-label" htmlFor="reg-address">العنوان</label>
              <input
                id="reg-address"
                name="address"
                type="text"
                className={inputCls('address')}
                placeholder="العنوان بالتفصيل"
                value={formData.address}
                onChange={handleChange}
              />
              {fieldError('address') && <p className="auth-field-error">{fieldError('address')}</p>}
            </div>
          </div>
        );
      case 1:
        return (
          <div className="space-y-4">
            <div>
              <label className="auth-label" htmlFor="reg-parentPhone">رقم ولي الأمر</label>
              <input
                id="reg-parentPhone"
                name="parentPhone"
                type="tel"
                dir="ltr"
                className={inputCls('parentPhone')}
                placeholder="01XXXXXXXXX"
                value={formData.parentPhone}
                onChange={handleChange}
              />
              {fieldError('parentPhone') && <p className="auth-field-error">{fieldError('parentPhone')}</p>}
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="auth-checkbox rounded-[22px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4">
                <input
                  className="auth-checkbox__input"
                  type="checkbox"
                  name="isFatherAlive"
                  checked={formData.isFatherAlive}
                  onChange={handleChange}
                />
                <span className="auth-checkbox__mark" />
                <span className="auth-checkbox__label">الأب على قيد الحياة</span>
              </label>
              <label className="auth-checkbox rounded-[22px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4">
                <input
                  className="auth-checkbox__input"
                  type="checkbox"
                  name="isMotherAlive"
                  checked={formData.isMotherAlive}
                  onChange={handleChange}
                />
                <span className="auth-checkbox__mark" />
                <span className="auth-checkbox__label">الأم على قيد الحياة</span>
              </label>
            </div>
          </div>
        );
      case 2:
        return (
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
                    value={formData.password}
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
                <label className="auth-label" htmlFor="reg-confirm">تأكيد المرور</label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reg-confirm"
                    name="confirmPassword"
                    type={showConfirmPassword ? 'text' : 'password'}
                    className={inputCls('confirmPassword')}
                    placeholder="أعد الكتابة"
                    style={{ paddingRight: '2.75rem' }}
                    value={formData.confirmPassword}
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
              <p className="mb-3 text-sm font-bold text-[var(--admin-text)]">قبل التفعيل النهائي</p>
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

  const StepIcon = currentStep.icon;

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.32 }}
      className="w-full relative min-h-[650px] flex"
    >
      <FeatureCarousel
        title={currentStep.title}
        description={currentStep.hint}
        steps={carouselSteps}
        step={activeStep}
        onStepChange={goToStep}
        autoPlay={false}
        clickToAdvance={false}
        bgClass="!border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] min-h-[650px] shadow-[0_28px_70px_var(--admin-shadow)]"
        step1img1Class="pointer-events-none w-[38%] rounded-2xl left-[8%] top-[24%] border border-white/10 shadow-[0_22px_60px_rgba(0,0,0,0.24)]"
        step1img2Class="pointer-events-none w-[44%] rounded-2xl left-[34%] top-[38%] border border-white/10 shadow-[0_24px_64px_rgba(0,0,0,0.28)]"
        step2img1Class="pointer-events-none w-[42%] rounded-2xl left-[8%] top-[24%] border border-white/10 shadow-[0_22px_60px_rgba(0,0,0,0.24)]"
        step2img2Class="pointer-events-none w-[36%] rounded-2xl left-[44%] top-[38%] border border-white/10 shadow-[0_24px_64px_rgba(0,0,0,0.28)]"
        step3imgClass="pointer-events-none w-[56%] rounded-2xl left-[14%] top-[24%] border border-white/10 shadow-[0_24px_64px_rgba(0,0,0,0.28)]"
        step4imgClass="pointer-events-none w-[56%] rounded-2xl left-[14%] top-[24%] border border-white/10 shadow-[0_24px_64px_rgba(0,0,0,0.28)]"
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
        <div className="relative z-10 mt-10 w-full lg:w-[70%] xl:w-[60%] pr-4 md:pr-0">
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

          <AnimatePresence mode="wait">
            <motion.div key={activeStep} {...PANEL_ANIMATION} className="space-y-5 max-w-[560px] rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/50 p-5 backdrop-blur-md sm:rounded-[28px] sm:p-7 shadow-[0_12px_40px_var(--admin-shadow)]">
              {renderStepFields()}
            </motion.div>
          </AnimatePresence>

          <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-start max-w-[560px]">
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
              <button type="button" onClick={goToNextStep} className="auth-btn-primary w-full sm:w-auto px-10">
                التالي
                <ArrowLeft className="h-4 w-4" />
              </button>
            ) : (
              <button type="submit" disabled={loading} className="auth-btn-primary w-full sm:w-auto px-8">
                {loading ? 'جاري الإنشاء...' : 'تشغيل الحساب'}
                <CheckCircle2 className="h-4 w-4" />
              </button>
            )}
          </div>
          
          <div className="mt-6 flex items-start gap-2 text-[0.65rem] font-medium text-[var(--admin-muted)] max-w-[480px] leading-5">
            <MapPinned className="h-3 w-3 mt-1 shrink-0" />
            <span>نحفظ البيانات تدريجيًا داخل الفورم، ولن نرسلها للخادم إلا عند الضغط على تشغيل الحساب النهائي.</span>
          </div>
        </div>
      </FeatureCarousel>
    </motion.form>
  );
}
