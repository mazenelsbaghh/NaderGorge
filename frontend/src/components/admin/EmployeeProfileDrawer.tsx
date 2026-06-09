'use client';

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Briefcase, Loader2, Coins, Clock, Hourglass } from 'lucide-react';
import { hrService, SaveEmployeeProfilePayload } from '@/services/hr-service';
import toast from 'react-hot-toast';

interface EmployeeProfileDrawerProps {
  open: boolean;
  onClose: () => void;
  userId: string;
  userName: string;
  onSuccess: () => void;
}

export function EmployeeProfileDrawer({
  open,
  onClose,
  userId,
  userName,
  onSuccess,
}: EmployeeProfileDrawerProps) {
  const [basicSalary, setBasicSalary] = useState<number>(0);
  const [standardStartTime, setStandardStartTime] = useState<string>('09:00');
  const [targetDailyHours, setTargetDailyHours] = useState<number>(8);
  const [loading, setLoading] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open && userId) {
      const loadProfile = async () => {
        setLoading(true);
        setError(null);
        try {
          const employees = await hrService.listEmployees();
          const currentEmp = employees.find((emp) => emp.id === userId);
          if (currentEmp?.employeeProfile) {
            setBasicSalary(currentEmp.employeeProfile.basicSalary);
            // Format standardStartTime from "hh:mm:ss" or similar to "hh:mm"
            const timeStr =
              currentEmp.employeeProfile.standardStartTime || '09:00';
            const parts = timeStr.split(':');
            if (parts.length >= 2) {
              setStandardStartTime(`${parts[0]}:${parts[1]}`);
            } else {
              setStandardStartTime(timeStr);
            }
            setTargetDailyHours(currentEmp.employeeProfile.targetDailyHours);
          } else {
            // Reset to default settings
            setBasicSalary(0);
            setStandardStartTime('09:00');
            setTargetDailyHours(8);
          }
        } catch {
          setError('حدث خطأ أثناء تحميل بيانات الملف التعريفي للموظف.');
        } finally {
          setLoading(false);
        }
      };
      loadProfile();
    }
  }, [open, userId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (basicSalary < 0) {
      toast.error('الراتب الأساسي لا يمكن أن يكون سالباً');
      return;
    }
    if (targetDailyHours < 1 || targetDailyHours > 24) {
      toast.error('ساعات العمل اليومية المستهدفة يجب أن تكون بين 1 و 24 ساعة');
      return;
    }

    setSubmitting(true);
    setError(null);

    // Backend expects time formatted as hh:mm:ss
    const formattedTime =
      standardStartTime.includes(':') &&
      standardStartTime.split(':').length === 2
        ? `${standardStartTime}:00`
        : standardStartTime;

    const payload: SaveEmployeeProfilePayload = {
      userId,
      basicSalary,
      standardStartTime: formattedTime,
      targetDailyHours,
    };

    try {
      const res = await hrService.saveEmployeeProfile(payload);
      if (res.success) {
        toast.success('تم حفظ إعدادات ملف الموظف بنجاح ✅');
        onSuccess();
        onClose();
      } else {
        setError(res.message || 'فشل حفظ الملف التعريفي');
      }
    } catch (err: any) {
      setError(
        err?.response?.data?.message || 'حدث خطأ غير متوقع أثناء الحفظ.'
      );
    } finally {
      setSubmitting(false);
    }
  };

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

          {/* Drawer Wrapper */}
          <motion.div
            key="drawer"
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', damping: 25, stiffness: 220 }}
            className="fixed inset-y-0 right-0 z-[100] flex w-full max-w-md flex-col border-l border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-2xl"
            dir="rtl"
            role="dialog"
            aria-modal="true"
            aria-labelledby="employee-profile-title"
          >
            {/* Header */}
            <div className="flex shrink-0 items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                  <Briefcase className="h-5 w-5" />
                </div>
                <div>
                  <h2
                    id="employee-profile-title"
                    className="text-lg font-black text-[var(--admin-text)] tracking-tight"
                  >
                    الملف الوظيفي للموظف
                  </h2>
                  <p className="text-xs text-[var(--admin-muted)] mt-0.5">
                    {userName}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={onClose}
                aria-label="إغلاق"
                disabled={submitting}
                className="flex h-9 w-9 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] disabled:cursor-not-allowed disabled:opacity-50"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Content */}
            {loading ? (
              <div className="flex flex-1 flex-col items-center justify-center gap-3 p-6 text-[var(--admin-muted)]">
                <Loader2 className="h-8 w-8 animate-spin text-[var(--admin-primary)]" />
                <span className="text-sm">جاري تحميل إعدادات الوظيفة...</span>
              </div>
            ) : (
              <form
                onSubmit={handleSubmit}
                className="flex min-h-0 flex-1 flex-col"
              >
                <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-6 py-6">
                  {error && (
                    <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-600 dark:border-red-800/30 dark:bg-red-950/20 dark:text-red-400">
                      {error}
                    </div>
                  )}

                  {/* Basic Salary */}
                  <div>
                    <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] flex items-center gap-2">
                      <Coins className="h-4 w-4 text-[var(--admin-muted)]" />
                      الراتب الأساسي (ج.م)
                    </label>
                    <input
                      type="number"
                      value={basicSalary || ''}
                      onChange={(e) => setBasicSalary(Number(e.target.value))}
                      placeholder="5000"
                      min="0"
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
                      required
                    />
                  </div>

                  {/* Standard Start Time */}
                  <div>
                    <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] flex items-center gap-2">
                      <Clock className="h-4 w-4 text-[var(--admin-muted)]" />
                      وقت بدء العمل الافتراضي
                    </label>
                    <input
                      type="time"
                      value={standardStartTime}
                      onChange={(e) => setStandardStartTime(e.target.value)}
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
                      required
                    />
                  </div>

                  {/* Target Daily Hours */}
                  <div>
                    <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] flex items-center gap-2">
                      <Hourglass className="h-4 w-4 text-[var(--admin-muted)]" />
                      ساعات العمل اليومية المستهدفة
                    </label>
                    <input
                      type="number"
                      value={targetDailyHours || ''}
                      onChange={(e) =>
                        setTargetDailyHours(Number(e.target.value))
                      }
                      placeholder="8"
                      min="1"
                      max="24"
                      className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
                      required
                    />
                  </div>
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
                          جاري الحفظ...
                        </>
                      ) : (
                        'حفظ الإعدادات'
                      )}
                    </button>
                  </div>
                </div>
              </form>
            )}
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
