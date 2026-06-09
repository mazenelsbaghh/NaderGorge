'use client';

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Eye, EyeOff, X, UserPlus, Loader2, Package, Shield, GraduationCap } from 'lucide-react';
import {
  adminService,
  AdminCreateUserPayload,
  AdminPackageListItemDto,
} from '@/services/admin-service';
import toast from 'react-hot-toast';

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
    label: 'مساعد مخصص',
    icon: <GraduationCap className="h-4 w-4" />,
    desc: 'صلاحيات مخصصة للمساعدين',
  },
  {
    value: 'Student',
    label: 'طالب',
    icon: <Package className="h-4 w-4" />,
    desc: 'وصول للمحتوى التعليمي',
  },
];

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
    }
  }, [open, defaultRole]);

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
      newErrors.general = 'يرجى اختيار دور المساعد المخصص، أو إنشاء دور جديد في الإعدادات';
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
      const res = await adminService.createUser(payload);
      if (res?.data) {
        toast.success(`تم إنشاء حساب "${res.data.fullName}" بنجاح ✅`);
        onSuccess();
        onClose();
      } else {
        const errorCode = (res as any)?.errors?.[0];
        if (errorCode === 'PHONE_ALREADY_EXISTS') {
          setErrors({ phoneNumber: 'رقم الهاتف مسجل بالفعل' });
        } else {
          setErrors({ general: (res as any)?.message || 'حدث خطأ، يرجى المحاولة مرة أخرى' });
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

  return (
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
            className="fixed inset-0 z-[90] bg-[var(--admin-text)]/35 backdrop-blur-sm"
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
            className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6"
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
                          className={`flex flex-col items-center gap-1.5 rounded-2xl border p-3 text-center transition-all duration-200 ${
                            role === r.value
                              ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)] shadow-[0_0_0_1px_var(--admin-primary)]'
                              : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/40 hover:text-[var(--admin-text)]'
                          }`}
                        >
                          {r.icon}
                          <span className="text-xs font-bold">{r.label}</span>
                          <span className="text-[10px] opacity-70 leading-tight">{r.desc}</span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {/* Custom Assistant Role Dropdown */}
                {role === 'Assistant' && (
                  <div className="mt-4 space-y-2">
                    <label className="block text-sm font-bold text-[var(--admin-text)] text-right">
                      اختر دور المساعد المخصص
                    </label>
                    {dynamicRoles.filter((r: any) => r.name !== 'Admin' && r.name !== 'Student' && r.name !== 'Teacher').length === 0 ? (
                      <div className="text-sm text-amber-500 bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900/30 rounded-xl p-3 text-right">
                        لا توجد أدوار مساعد مخصصة حالياً. يرجى إنشاء دور جديد في صفحة الإعدادات أولاً.
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
                                className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-2.5 transition-all ${
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
                    className="flex flex-1 items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] py-3 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)] transition hover:bg-[var(--admin-primary-strong)] active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-60"
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
    </AnimatePresence>
  );
}
