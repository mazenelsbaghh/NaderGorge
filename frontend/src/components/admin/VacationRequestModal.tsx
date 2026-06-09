'use client';

import { useState } from 'react';
import { Calendar, Loader2, Send } from 'lucide-react';
import { AdminModal } from './AdminModal';
import { hrService } from '@/services/hr-service';
import toast from 'react-hot-toast';

interface VacationRequestModalProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function VacationRequestModal({
  open,
  onClose,
  onSuccess,
}: VacationRequestModalProps) {
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');
  const [reason, setReason] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!startDate || !endDate || !reason.trim()) {
      toast.error('يرجى ملء جميع الحقول المطلوبة');
      return;
    }

    if (new Date(startDate) > new Date(endDate)) {
      toast.error('تاريخ البدء يجب أن يكون قبل أو مساوٍ لتاريخ الانتهاء');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const res = await hrService.submitVacation({
        startDate,
        endDate,
        reason: reason.trim(),
      });

      if (res.success) {
        toast.success('تم تقديم طلب الإجازة بنجاح ✅');
        // Reset form
        setStartDate('');
        setEndDate('');
        setReason('');
        onSuccess();
        onClose();
      } else {
        setError(res.message || 'فشل تقديم طلب الإجازة');
      }
    } catch (err: any) {
      setError(err?.response?.data?.message || 'حدث خطأ أثناء تقديم الطلب.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminModal
      open={open}
      onClose={onClose}
      title="تقديم طلب إجازة جديد"
      subtitle="يرجى ملء التواريخ وسبب الطلب ليتم مراجعته من قبل إدارة الموارد البشرية."
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-5 py-2" dir="rtl">
        {error && (
          <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-600 dark:border-red-800/30 dark:bg-red-950/20 dark:text-red-400 text-right">
            {error}
          </div>
        )}

        {/* Start Date */}
        <div>
          <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right flex items-center gap-2">
            <Calendar className="h-4 w-4 text-[var(--admin-muted)]" />
            تاريخ بدء الإجازة
          </label>
          <input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
            required
          />
        </div>

        {/* End Date */}
        <div>
          <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right flex items-center gap-2">
            <Calendar className="h-4 w-4 text-[var(--admin-muted)]" />
            تاريخ انتهاء الإجازة
          </label>
          <input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
            required
          />
        </div>

        {/* Reason */}
        <div>
          <label className="mb-2 block text-sm font-bold text-[var(--admin-text)] text-right">
            سبب طلب الإجازة
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="اكتب سبب طلب الإجازة بالتفصيل..."
            rows={4}
            className="w-full rounded-[14px] border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20"
            required
          />
        </div>

        {/* Submit */}
        <div className="flex gap-3 pt-2">
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
                جاري التقديم...
              </>
            ) : (
              <>
                <Send className="h-4 w-4" />
                تقديم الطلب
              </>
            )}
          </button>
        </div>
      </form>
    </AdminModal>
  );
}
