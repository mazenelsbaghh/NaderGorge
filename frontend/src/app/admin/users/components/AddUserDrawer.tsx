'use client';

import { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { CalendarClock, Eye, EyeOff, X, UserPlus, Loader2, Package, Shield, GraduationCap, Headphones, Plus, Trash2 } from 'lucide-react';
import {
  adminService,
  AdminCreateUserPayload,
  AdminPackageListItemDto,
} from '@/services/admin-service';
import toast from 'react-hot-toast';
import { useCreateEmployee, useProvisionEmployee } from '@/features/employee';
import { liveSupportService, type LiveSupportScheduleWindow } from '@/services/live-support-service';
import {
  hrService,
  type ShiftTemplateDto,
  type WorkCalendarDto,
} from '@/services/hr-service';
import { cairoCurrentDate } from '@/lib/cairo-time';

type Role = string;

interface AddUserDrawerProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  defaultRole?: 'Student' | 'Assistant' | 'Admin';
}

const ROLES: { value: string; label: string; icon: React.ReactNode; desc: string }[] = [
  {
    value: 'Admin',
    label: 'مدير',
    icon: <Shield className="h-4 w-4" />,
    desc: 'وصول كامل للوحة التحكم',
  },
  {
    value: 'Assistant',
    label: 'موظف / Staff',
    icon: <GraduationCap className="h-4 w-4" />,
    desc: 'حساب موظف بدور وصلاحيات مخصصة',
  },
  {
    value: 'Student',
    label: 'طالب',
    icon: <Package className="h-4 w-4" />,
    desc: 'وصول للمحتوى التعليمي',
  },
];

const supportDays = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];
const defaultSupportWindow: LiveSupportScheduleWindow = { dayOfWeek: 0, startLocalTime: '09:00:00', endLocalTime: '17:00:00' };
const todayInputValue = cairoCurrentDate;

function supportScheduleFromShift(
  shift: ShiftTemplateDto | undefined,
  workCalendar: WorkCalendarDto | undefined,
): LiveSupportScheduleWindow[] {
  if (!shift || !workCalendar) return [];

  const windows = new Map<string, LiveSupportScheduleWindow>();

  for (let dayOfWeek = 0; dayOfWeek < supportDays.length; dayOfWeek += 1) {
    if ((workCalendar.workingDaysMask & (1 << dayOfWeek)) === 0) continue;

    for (const segment of shift.segments) {
      if (segment.dayOfWeek != null && segment.dayOfWeek !== dayOfWeek) continue;

      const startLocalTime = segment.startsAt.length === 5 ? `${segment.startsAt}:00` : segment.startsAt;
      const endLocalTime = segment.endsAt.length === 5 ? `${segment.endsAt}:00` : segment.endsAt;
      const key = `${dayOfWeek}-${startLocalTime}-${endLocalTime}`;

      windows.set(key, { dayOfWeek, startLocalTime, endLocalTime });
    }
  }

  return [...windows.values()].sort(
    (first, second) => first.dayOfWeek - second.dayOfWeek
      || first.startLocalTime.localeCompare(second.startLocalTime),
  );
}

interface FieldError {
  fullName?: string;
  phoneNumber?: string;
  password?: string;
  general?: string;
}

export function AddUserDrawer({ open, onClose, onSuccess, defaultRole }: AddUserDrawerProps) {
  const [role, setRole] = useState<Role>(defaultRole || 'Student');
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [selectedPackageIds, setSelectedPackageIds] = useState<string[]>([]);
  const [packages, setPackages] = useState<AdminPackageListItemDto[]>([]);
  const [loadingPackages, setLoadingPackages] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errors, setErrors] = useState<FieldError>({});
  const [dynamicRoles, setDynamicRoles] = useState<any[]>([]);
  const [selectedAssistantRole, setSelectedAssistantRole] = useState<string>('');
  const [basicSalary, setBasicSalary] = useState('0');
  const [standardStartTime, setStandardStartTime] = useState('09:00');
  const [targetDailyHours, setTargetDailyHours] = useState('8');
  const [shiftTemplates, setShiftTemplates] = useState<ShiftTemplateDto[]>([]);
  const [workCalendars, setWorkCalendars] = useState<WorkCalendarDto[]>([]);
  const [selectedShiftTemplateId, setSelectedShiftTemplateId] = useState('');
  const [shiftEffectiveFrom, setShiftEffectiveFrom] = useState(todayInputValue);
  const [loadingShifts, setLoadingShifts] = useState(false);
  const [enableLiveSupport, setEnableLiveSupport] = useState(false);
  const [supportCapacity, setSupportCapacity] = useState('1');
  const [supportSchedule, setSupportSchedule] = useState<LiveSupportScheduleWindow[]>([]);
  const createEmployee = useCreateEmployee();
  const provisionEmployee = useProvisionEmployee();

  // Load packages when Student role is selected
  useEffect(() => {
    if (role === 'Student' && packages.length === 0) {
      setLoadingPackages(true);
      adminService
        .listAllPackages()
        .then(setPackages)
        .catch(() => toast.error('تعذر تحميل الباقات'))
        .finally(() => setLoadingPackages(false));
    }
  }, [role, packages.length]);

  // Reset form when drawer closes
  useEffect(() => {
    if (!open) {
      setRole(defaultRole || 'Student');
      setFullName('');
      setPhoneNumber('');
      setPassword('');
      setShowPassword(false);
      setSelectedPackageIds([]);
      setErrors({});
      setSelectedAssistantRole('');
      setBasicSalary('0');
      setStandardStartTime('09:00');
      setTargetDailyHours('8');
      setSelectedShiftTemplateId('');
      setShiftEffectiveFrom(todayInputValue());
      setEnableLiveSupport(false);
      setSupportCapacity('1');
      setSupportSchedule([]);
    }
  }, [open, defaultRole]);

  useEffect(() => {
    if (!open || role !== 'Assistant') return;

    setLoadingShifts(true);
    Promise.all([
      hrService.listShiftTemplates(),
      hrService.listWorkCalendars(),
    ])
      .then(([templates, calendars]) => {
        setShiftTemplates(templates);
        setWorkCalendars(calendars);
        setSelectedShiftTemplateId((current) => current || templates[0]?.id || '');
      })
      .catch(() => toast.error('تعذر تحميل الشيفتات وتقويمات العمل'))
      .finally(() => setLoadingShifts(false));
  }, [open, role]);

  // Load dynamic roles on open
  useEffect(() => {
    if (open) {
      adminService.listRoles()
        .then((data) => {
          if (data) {
            setDynamicRoles(data);
            const assistants = data.filter((r: any) => r.name !== 'Admin' && r.name !== 'Student' && r.name !== 'Teacher');
            if (assistants.length > 0) {
              setSelectedAssistantRole(assistants[0].name);
            }
          }
        })
        .catch(() => toast.error('تعذر تحميل قائمة الأدوار'));
    }
  }, [open]);

  const selectedShift = useMemo(
    () => shiftTemplates.find((template) => template.id === selectedShiftTemplateId),
    [selectedShiftTemplateId, shiftTemplates],
  );
  const selectedWorkCalendar = useMemo(
    () => workCalendars.find((calendar) => calendar.id === selectedShift?.workCalendarId),
    [selectedShift?.workCalendarId, workCalendars],
  );
  const weeklyRestDays = selectedWorkCalendar
    ? supportDays.filter((_, dayOfWeek) => (selectedWorkCalendar.workingDaysMask & (1 << dayOfWeek)) === 0)
    : [];

  useEffect(() => {
    const segment = selectedShift?.segments[0];
    if (!segment) return;

    const start = segment.startsAt.slice(0, 5);
    const [startHour, startMinute] = start.split(':').map(Number);
    const [endHour, endMinute] = segment.endsAt.slice(0, 5).split(':').map(Number);
    const startMinutes = startHour * 60 + startMinute;
    let endMinutes = endHour * 60 + endMinute;
    if (endMinutes <= startMinutes) endMinutes += 24 * 60;
    const paidMinutes = Math.max(
      60,
      endMinutes - startMinutes - segment.unpaidBreakMinutes,
    );

    setStandardStartTime(start);
    setTargetDailyHours(String(Math.max(1, Math.round(paidMinutes / 60))));
  }, [selectedShift]);

  useEffect(() => {
    if (!enableLiveSupport) return;

    const generatedSchedule = supportScheduleFromShift(selectedShift, selectedWorkCalendar);
    if (generatedSchedule.length > 0) {
      setSupportSchedule(generatedSchedule);
    }
  }, [enableLiveSupport, selectedShift, selectedWorkCalendar]);

  function selectShift(shiftTemplateId: string) {
    setSelectedShiftTemplateId(shiftTemplateId);
  }

  function validate(): boolean {
    const newErrors: FieldError = {};
    const nameParts = fullName.trim().split(/\s+/);

    if (!fullName.trim()) {
      newErrors.fullName = 'الاسم مطلوب';
    } else if (role === 'Student' && nameParts.length < 4) {
      newErrors.fullName = 'الاسم يجب أن يكون رباعياً (4 كلمات على الأقل)';
    } else if (nameParts.length < 2) {
      newErrors.fullName = 'الاسم يجب أن يكون كلمتين على الأقل';
    }

    if (!phoneNumber.trim()) {
      newErrors.phoneNumber = 'رقم الهاتف مطلوب';
    } else if (!/^01[0125]\d{8}$/.test(phoneNumber.trim())) {
      newErrors.phoneNumber = 'رقم الهاتف يجب أن يكون مصرياً صحيحاً (01x xxxxxxxx)';
    }

    if (!password) {
      newErrors.password = 'كلمة السر مطلوبة';
    } else if (password.length < 6) {
      newErrors.password = 'كلمة السر يجب أن تكون 6 أحرف على الأقل';
    }

    if (role === 'Assistant' && !selectedAssistantRole) {
      newErrors.general = 'يرجى اختيار دور الموظف، أو إنشاء دور جديد في الإعدادات';
    }
    if (role === 'Assistant' && !selectedShiftTemplateId) {
      newErrors.general = 'اختر شفت الحضور للموظف قبل إنشاء الحساب';
    }
    if (role === 'Assistant' && (!Number.isFinite(Number(basicSalary)) || Number(basicSalary) < 0)) {
      newErrors.general = 'الراتب الأساسي يجب أن يكون رقماً موجباً أو صفراً';
    }
    if (role === 'Assistant' && (!Number.isInteger(Number(targetDailyHours)) || Number(targetDailyHours) < 1 || Number(targetDailyHours) > 24)) {
      newErrors.general = 'ساعات العمل اليومية يجب أن تكون بين 1 و24';
    }
    if (role === 'Assistant' && enableLiveSupport && (!Number.isInteger(Number(supportCapacity)) || Number(supportCapacity) < 1 || Number(supportCapacity) > 50)) {
      newErrors.general = 'سعة الدعم المباشر يجب أن تكون بين محادثة واحدة و50 محادثة';
    }
    if (role === 'Assistant' && enableLiveSupport && supportSchedule.some((window) => window.startLocalTime >= window.endLocalTime)) {
      newErrors.general = 'وقت نهاية كل فترة دعم يجب أن يكون بعد وقت البداية';
    }
    if (role === 'Assistant' && enableLiveSupport && supportSchedule.length === 0) {
      newErrors.general = 'أضف يومًا وفترة واحدة على الأقل لموظف الدعم';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setSubmitting(true);
    setErrors({});

    const payload: AdminCreateUserPayload = {
      fullName: fullName.trim(),
      phoneNumber: phoneNumber.trim(),
      password,
      role: role === 'Assistant' ? selectedAssistantRole : role,
      packageIds: role === 'Student' ? selectedPackageIds : [],
    };

    try {
      if (role === 'Assistant') {
        const created = await provisionEmployee.mutateAsync({
            fullName: payload.fullName,
            phoneNumber: payload.phoneNumber,
            password: payload.password,
            role: payload.role,
            basicSalary: Number(basicSalary),
            standardStartTime,
            targetDailyHours: Number(targetDailyHours),
            shiftTemplateId: selectedShiftTemplateId,
            shiftEffectiveFrom,
          });

        if (enableLiveSupport) {
          try {
            await liveSupportService.updateStaffConfig(created.userId, {
              enabled: true,
              capacity: Number(supportCapacity),
              schedule: supportSchedule,
            });
          } catch {
            toast.error('تم إنشاء الموظف، لكن تعذر حفظ إعدادات الدعم. افتح الدعم المباشر لإكمالها.');
          }
        }
        toast.success(`تم إنشاء حساب "${created.fullName}" بنجاح ✅`);
        onSuccess();
        onClose();
      } else {
        const created = await createEmployee.mutateAsync(payload);
        if (created) {
          toast.success(`تم إنشاء حساب "${created.fullName}" بنجاح ✅`);
          onSuccess();
          onClose();
        }
      }
    } catch (err: any) {
      const errorCode = err?.response?.data?.errors?.[0];
      if (errorCode === 'PHONE_ALREADY_EXISTS') {
        setErrors({ phoneNumber: 'رقم الهاتف مسجل بالفعل' });
      } else {
        setErrors({ general: err?.response?.data?.message || 'حدث خطأ، يرجى المحاولة مرة أخرى' });
      }
    } finally {
      setSubmitting(false);
    }
  }

  function togglePackage(id: string) {
    setSelectedPackageIds((prev) =>
      prev.includes(id) ? prev.filter((p) => p !== id) : [...prev, id],
    );
  }

  function updateSupportWindow(index: number, change: Partial<LiveSupportScheduleWindow>) {
    setSupportSchedule((current) => current.map((window, position) => position === index ? { ...window, ...change } : window));
  }

  if (typeof document === 'undefined') return null;

  return createPortal(
    <AnimatePresence>
      {open && (
        <>
          {/* Backdrop */}
          <motion.div
            key="backdrop"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="fixed inset-0 z-[var(--z-floating)] bg-[var(--admin-text)]/35 backdrop-blur-sm"
            onClick={() => {
              if (!submitting) onClose();
            }}
          />

          {/* Modal */}
          <motion.div
            key="modal"
            initial={{ opacity: 0, scale: 0.96, y: 18 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.98, y: 12 }}
            transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
            className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center p-4 sm:p-6"
            dir="rtl"
            role="dialog"
            aria-modal="true"
            aria-labelledby="add-user-title"
          >
            <div className="flex max-h-[min(860px,calc(100dvh-2rem))] w-full max-w-3xl flex-col overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-2xl">
              {/* Header */}
              <div className="flex shrink-0 items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                    <UserPlus className="h-5 w-5" />
                  </div>
                  <div>
                    <h2
                      id="add-user-title"
                      className="text-lg font-black text-[var(--admin-text)] tracking-tight"
                    >
                      إضافة مستخدم جديد
                    </h2>
                    <p className="text-xs text-[var(--admin-muted)]">أنشئ حساباً جديداً في النظام</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={onClose}
                  aria-label="إغلاق إضافة مستخدم"
                  disabled={submitting}
                  className="flex h-9 w-9 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Form */}
              <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
                <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-6 py-6">

                {/* Role Selector */}
                {!defaultRole && (
                  <div>
                    <label className="mb-3 block text-sm font-bold text-[var(--admin-text)]">
                      الدور
                    </label>
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                      {ROLES.map((r) => (
                        <button
                          key={r.value}
                          type="button"
                          onClick={() => setRole(r.value)}
                          className={`flex flex-col items-center gap-1.5 rounded-2xl border p-3 text-center transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 ${
                            role === r.value
                              ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)] shadow-[0_0_0_1px_var(--admin-primary)]'
                              : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/40 hover:text-[var(--admin-text)]'
                          }`}
                        >
                          {r.icon}
                          <span className="text-xs font-bold">{r.label}</span>
                          <span className="text-xs opacity-70 leading-tight">{r.desc}</span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {/* Custom employee role */}
                {role === 'Assistant' && (
                  <div className="mt-4 space-y-2">
                    <label className="block text-sm font-bold text-[var(--admin-text)] text-right">
                      دور الموظف
                    </label>
                    {dynamicRoles.filter((r: any) => r.name !== 'Admin' && r.name !== 'Student' && r.name !== 'Teacher').length === 0 ? (
                      <div className="text-sm text-amber-500 bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900/30 rounded-xl p-3 text-right">
                        لا توجد أدوار موظفين مخصصة حاليًا. أنشئ دورًا مثل Staff أو خدمة عملاء من صفحة الإعدادات أولًا.
                      </div>
                    ) : (
                      <select
                        value={selectedAssistantRole}
                        onChange={(e) => setSelectedAssistantRole(e.target.value)}
                        className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-right focus:border-[var(--admin-primary)] focus:outline-none text-sm text-[var(--admin-text)]"
                      >
                        {dynamicRoles
                          .filter((r: any) => r.name !== 'Admin' && r.name !== 'Student' && r.name !== 'Teacher')
                          .map((r: any) => (
                            <option key={r.id} value={r.name}>
                              {r.name}
                            </option>
                          ))}
                      </select>
                    )}
                  </div>
                )}

                {/* Full Name */}
                <div>
                  <label
                    htmlFor="add-user-name"
                    className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]"
                  >
                    الاسم الكامل
                    {role === 'Student' && (
                      <span className="mr-1 text-xs font-normal text-[var(--admin-muted)]">
                        (رباعي)
                      </span>
                    )}
                  </label>
                  <input
                    id="add-user-name"
                    type="text"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    placeholder={role === 'Student' ? 'أحمد محمد علي حسن' : 'اسم المستخدم'}
                    className={`w-full rounded-[14px] border bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:ring-2 ${
                      errors.fullName
                        ? 'border-red-400 focus:ring-red-200'
                        : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary)]/20'
                    }`}
                    autoComplete="name"
                  />
                  {errors.fullName && (
                    <p className="mt-1 text-xs text-red-500">{errors.fullName}</p>
                  )}
                </div>

                {/* Phone Number */}
                <div>
                  <label
                    htmlFor="add-user-phone"
                    className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]"
                  >
                    رقم الهاتف
                  </label>
                  <div className="relative">
                    <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm font-bold text-[var(--admin-muted)]">
                      🇪🇬
                    </span>
                    <input
                      id="add-user-phone"
                      type="tel"
                      value={phoneNumber}
                      onChange={(e) => setPhoneNumber(e.target.value.replace(/\D/g, ''))}
                      placeholder="01xxxxxxxxx"
                      maxLength={11}
                      dir="ltr"
                      className={`w-full rounded-[14px] border bg-[var(--admin-bg)] py-3 pl-4 pr-12 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:ring-2 ${
                        errors.phoneNumber
                          ? 'border-red-400 focus:ring-red-200'
                          : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary)]/20'
                      }`}
                      autoComplete="tel"
                    />
                  </div>
                  {errors.phoneNumber && (
                    <p className="mt-1 text-xs text-red-500">{errors.phoneNumber}</p>
                  )}
                </div>

                {/* Password */}
                <div>
                  <label
                    htmlFor="add-user-password"
                    className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]"
                  >
                    كلمة السر
                  </label>
                  <div className="relative">
                    <input
                      id="add-user-password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder="6 أحرف على الأقل"
                      className={`w-full rounded-[14px] border bg-[var(--admin-bg)] py-3 pl-12 pr-4 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:ring-2 ${
                        errors.password
                          ? 'border-red-400 focus:ring-red-200'
                          : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary)]/20'
                      }`}
                      autoComplete="new-password"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--admin-muted)] transition hover:text-[var(--admin-text)]"
                    >
                      {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>
                  {errors.password && (
                    <p className="mt-1 text-xs text-red-500">{errors.password}</p>
                  )}
                </div>

                {/* Packages (Student only) */}
                {role === 'Assistant' && (
                  <section className="space-y-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4" aria-labelledby="employee-shift-title">
                    <div className="flex items-start gap-3">
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                        <CalendarClock className="h-5 w-5" aria-hidden="true" />
                      </span>
                      <div>
                        <h3 id="employee-shift-title" className="text-sm font-black text-[var(--admin-text)]">شيفت الحضور والانصراف (HR)</h3>
                        <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">هذا الشيفت للدوام والراتب والحضور فقط، وهو منفصل تمامًا عن جدول استقبال محادثات الدعم.</p>
                      </div>
                    </div>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <label className="text-sm font-bold text-[var(--admin-text)]">
                        شفت العمل
                        <select
                          required
                          value={selectedShiftTemplateId}
                          onChange={(event) => selectShift(event.target.value)}
                          disabled={loadingShifts}
                          className="admin-input mt-2 w-full"
                        >
                          <option value="">{loadingShifts ? 'جارٍ تحميل الشيفتات...' : 'اختر الشفت'}</option>
                          {shiftTemplates.map((template) => (
                            <option key={template.id} value={template.id}>
                              {template.name} ({template.code})
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="text-sm font-bold text-[var(--admin-text)]">
                        سريان الشفت من
                        <input
                          required
                          type="date"
                          value={shiftEffectiveFrom}
                          onChange={(event) => setShiftEffectiveFrom(event.target.value)}
                          className="admin-input mt-2 w-full"
                        />
                      </label>
                    </div>
                    {selectedShift && (
                      <div className="rounded-xl bg-[var(--admin-bg)] p-3 text-xs text-[var(--admin-muted)]">
                        <p>
                          <span className="font-black text-[var(--admin-text)]">المواعيد: </span>
                          {selectedShift.segments.map((segment) => `${segment.startsAt.slice(0, 5)}–${segment.endsAt.slice(0, 5)}`).join('، ')}
                        </p>
                        <p className="mt-2">
                          <span className="font-black text-[var(--admin-text)]">أيام الراحة الأسبوعية: </span>
                          {weeklyRestDays.length > 0 ? weeklyRestDays.join('، ') : 'لا توجد أيام راحة محددة'}
                        </p>
                      </div>
                    )}
                    {!loadingShifts && shiftTemplates.length === 0 && (
                      <p role="alert" className="rounded-xl bg-amber-100 p-3 text-xs font-bold text-amber-900">
                        لا توجد شيفتات جاهزة. أنشئ شفتًا أولًا من إدارة الموارد البشرية ← الشيفتات.
                      </p>
                    )}
                  </section>
                )}

                {role === 'Assistant' && (
                  <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
                    <div className="max-w-sm">
                      <label htmlFor="employee-salary" className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">الراتب الأساسي</label>
                      <input id="employee-salary" type="number" min="0" step="0.01" value={basicSalary} onChange={(event) => setBasicSalary(event.target.value)} className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]" />
                    </div>
                  </div>
                )}

                {role === 'Assistant' && (
                  <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4" aria-labelledby="live-support-setup-title">
                    <div className="flex flex-wrap items-start justify-between gap-4">
                      <div className="flex gap-3">
                        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                          <Headphones className="h-5 w-5" aria-hidden="true" />
                        </span>
                        <div>
                          <h3 id="live-support-setup-title" className="text-sm font-black text-[var(--admin-text)]">هل هذا الموظف يعمل في الدعم المباشر؟</h3>
                          <p className="mt-1 max-w-lg text-xs leading-5 text-[var(--admin-muted)]">اختيار مستقل عن دور Staff وشيفت الحضور. فعّله فقط لو الموظف سيستقبل محادثات الطلاب.</p>
                        </div>
                      </div>
                      <div className="grid min-w-60 grid-cols-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-1">
                        <button
                          type="button"
                          onClick={() => {
                            setEnableLiveSupport(true);
                            const generatedSchedule = supportScheduleFromShift(selectedShift, selectedWorkCalendar);
                            setSupportSchedule(generatedSchedule.length > 0 ? generatedSchedule : [{ ...defaultSupportWindow }]);
                          }}
                          className={`min-h-10 rounded-lg px-3 text-xs font-black transition ${enableLiveSupport ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)]'}`}
                        >
                          نعم، موظف دعم
                        </button>
                        <button
                          type="button"
                          onClick={() => setEnableLiveSupport(false)}
                          className={`min-h-10 rounded-lg px-3 text-xs font-black transition ${!enableLiveSupport ? 'bg-[var(--admin-card)] text-[var(--admin-text)] shadow-sm' : 'text-[var(--admin-muted)]'}`}
                        >
                          لا، موظف عادي
                        </button>
                      </div>
                    </div>

                    {enableLiveSupport && (
                      <div className="mt-5 border-t border-[var(--admin-border)] pt-4">
                        <div className="grid gap-4 sm:grid-cols-[minmax(0,220px)_1fr] sm:items-start">
                          <label className="text-sm font-bold text-[var(--admin-text)]">
                            الحد الأقصى للمحادثات
                            <input type="number" min="1" max="50" value={supportCapacity} onChange={(event) => setSupportCapacity(event.target.value)} className="mt-2 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-3 text-sm outline-none focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)]" />
                            <span className="mt-1 block text-xs font-normal text-[var(--admin-muted)]">عدد المحادثات التي يمكن للمساعد التعامل معها في نفس الوقت.</span>
                          </label>
                          <div>
                            <div className="flex items-center justify-between gap-3">
                              <div>
                                <p className="text-sm font-bold text-[var(--admin-text)]">أيام وساعات استقبال الدعم</p>
                                <p className="mt-1 text-xs text-[var(--admin-muted)]">تمت تعبئة كل أيام ومواعيد شفت الحضور تلقائيًا. يمكنك تعديل أي فترة، واليوم غير المضاف إجازة من الدعم.</p>
                              </div>
                              <div className="flex items-center gap-3">
                                <button type="button" onClick={() => setSupportSchedule(supportScheduleFromShift(selectedShift, selectedWorkCalendar))} disabled={!selectedShift || !selectedWorkCalendar} className="text-xs font-bold text-[var(--admin-muted)] hover:text-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-50">
                                  مطابقة شفت الحضور
                                </button>
                                <button type="button" onClick={() => setSupportSchedule((current) => [...current, { ...defaultSupportWindow }])} className="inline-flex min-h-10 items-center gap-1 text-sm font-bold text-[var(--admin-primary)] hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]">
                                  <Plus className="h-4 w-4" />إضافة فترة
                                </button>
                              </div>
                            </div>
                            <div className="mt-3 space-y-2">
                              {supportSchedule.map((window, index) => (
                                <div key={`${window.dayOfWeek}-${index}`} className="grid grid-cols-[1fr_92px_92px_40px] gap-2">
                                  <select value={window.dayOfWeek} onChange={(event) => updateSupportWindow(index, { dayOfWeek: Number(event.target.value) })} aria-label={`يوم الدعم رقم ${index + 1}`} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-2 text-sm outline-none focus:border-[var(--admin-primary)]">
                                    {supportDays.map((day, value) => <option key={day} value={value}>{day}</option>)}
                                  </select>
                                  <input type="time" value={window.startLocalTime.slice(0, 5)} onChange={(event) => updateSupportWindow(index, { startLocalTime: `${event.target.value}:00` })} aria-label={`وقت بداية الدعم رقم ${index + 1}`} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-2 text-sm outline-none focus:border-[var(--admin-primary)]" />
                                  <input type="time" value={window.endLocalTime.slice(0, 5)} onChange={(event) => updateSupportWindow(index, { endLocalTime: `${event.target.value}:00` })} aria-label={`وقت نهاية الدعم رقم ${index + 1}`} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-2 text-sm outline-none focus:border-[var(--admin-primary)]" />
                                  <button type="button" onClick={() => setSupportSchedule((current) => current.filter((_, position) => position !== index))} aria-label="حذف فترة الدعم" className="flex min-h-11 items-center justify-center rounded-xl text-[var(--admin-danger)] hover:bg-[var(--admin-danger-10)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)]"><Trash2 className="h-4 w-4" /></button>
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </section>
                )}

                <AnimatePresence>
                  {role === 'Student' && (
                    <motion.div
                      key="packages"
                      initial={{ opacity: 0, height: 0 }}
                      animate={{ opacity: 1, height: 'auto' }}
                      exit={{ opacity: 0, height: 0 }}
                      transition={{ duration: 0.25, ease: 'easeInOut' }}
                      className="overflow-hidden"
                    >
                      <label className="mb-2 block text-sm font-bold text-[var(--admin-text)]">
                        الباقات
                        <span className="mr-1 text-xs font-normal text-[var(--admin-muted)]">
                          (اختياري)
                        </span>
                      </label>

                      {loadingPackages ? (
                        <div className="flex items-center gap-2 py-4 text-sm text-[var(--admin-muted)]">
                          <Loader2 className="h-4 w-4 animate-spin" />
                          جاري تحميل الباقات...
                        </div>
                      ) : packages.length === 0 ? (
                        <p className="py-3 text-sm text-[var(--admin-muted)]">
                          لا توجد باقات متاحة حالياً
                        </p>
                      ) : (
                        <div className="space-y-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                          {packages.map((pkg) => {
                            const checked = selectedPackageIds.includes(pkg.id);
                            return (
                              <label
                                key={pkg.id}
                                className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                                  checked
                                    ? 'border-[var(--admin-primary)]/40 bg-[var(--admin-primary-15)]'
                                    : 'border-transparent bg-[var(--admin-bg)] hover:bg-[var(--admin-hover)]'
                                }`}
                              >
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  onChange={() => togglePackage(pkg.id)}
                                  className="h-4 w-4 rounded accent-[var(--admin-primary)]"
                                />
                                <span
                                  className={`text-sm font-medium ${checked ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-text)]'}`}
                                >
                                  {pkg.name}
                                </span>
                              </label>
                            );
                          })}
                        </div>
                      )}

                      {selectedPackageIds.length > 0 && (
                        <p className="mt-1.5 text-xs text-[var(--admin-primary)] font-medium">
                          {selectedPackageIds.length} باقة مختارة
                        </p>
                      )}
                    </motion.div>
                  )}
                </AnimatePresence>

                {/* General error */}
                {errors.general && (
                  <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-600 dark:border-red-800/30 dark:bg-red-950/20 dark:text-red-400">
                    {errors.general}
                  </div>
                )}
              </div>

                {/* Footer */}
                <div className="shrink-0 border-t border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-4">
                <div className="flex gap-3">
                  <button
                    type="button"
                    onClick={onClose}
                    disabled={submitting}
                    className="flex-1 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-50"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="flex flex-1 items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] py-3 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-sm transition hover:bg-[var(--admin-primary-strong)] active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {submitting ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        جاري الإنشاء...
                      </>
                    ) : (
                      <>
                        <UserPlus className="h-4 w-4" />
                        إضافة المستخدم
                      </>
                    )}
                  </button>
                </div>
              </div>
              </form>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>,
    document.body,
  );
}
