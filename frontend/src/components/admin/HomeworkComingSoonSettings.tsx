'use client';

import { useEffect, useState } from 'react';
import { CalendarClock, Eye, EyeOff, Save } from 'lucide-react';
import toast from 'react-hot-toast';

import { Checkbox, Label } from '@/components/ui/checkbox';
import { cairoCurrentDate } from '@/lib/cairo-time';
import {
  getDefaultHomeworkComingSoonDate,
  getHomeworkComingSoonLabel,
} from '@/lib/homework-coming-soon';
import { getApiErrorSummary } from '@/lib/api-errors';
import { adminService } from '@/services/admin-service';

interface HomeworkComingSoonSettingsProps {
  lessonId: string;
  expectedOn?: string | null;
  onSaved?: () => void | Promise<void>;
}

export function HomeworkComingSoonSettings({
  lessonId,
  expectedOn,
  onSaved,
}: HomeworkComingSoonSettingsProps) {
  const [enabled, setEnabled] = useState(Boolean(expectedOn));
  const [date, setDate] = useState(
    expectedOn || getDefaultHomeworkComingSoonDate()
  );
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setEnabled(Boolean(expectedOn));
    setDate(expectedOn || getDefaultHomeworkComingSoonDate());
  }, [expectedOn]);

  async function save() {
    if (enabled && !date) {
      toast.error('حدد الموعد المتوقع للواجب');
      return;
    }

    try {
      setSaving(true);
      await adminService.setLessonHomeworkComingSoon(
        lessonId,
        enabled ? date : null
      );
      toast.success(
        enabled
          ? 'سيظهر إعلان الواجب للطلاب حتى يتم نشره.'
          : 'تم إخفاء إعلان الواجب من الطلاب.'
      );
      await onSaved?.();
    } catch (error: unknown) {
      toast.error(getApiErrorSummary(error, 'تعذر حفظ إعداد إعلان الواجب.'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section
      className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6"
      aria-labelledby="homework-coming-soon-title"
    >
      <div className="flex items-start gap-3">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
          <CalendarClock className="h-5 w-5" aria-hidden="true" />
        </div>
        <div>
          <h3
            id="homework-coming-soon-title"
            className="text-lg font-black text-[var(--admin-text)]"
          >
            إعلان الواجب قبل نشره
          </h3>
          <p className="mt-1 max-w-2xl text-sm font-medium leading-6 text-[var(--admin-muted)]">
            أخبر الطلاب بموعد الواجب المتوقع، من غير فتح صفحة واجب فارغة أو
            تعطيل تقدمهم في الدروس.
          </p>
        </div>
      </div>

      <div className="mt-5 rounded-xl bg-[var(--admin-card-soft)] p-4">
        <Checkbox isSelected={enabled} onChange={setEnabled}>
          <Checkbox.Control>
            <Checkbox.Indicator />
          </Checkbox.Control>
          <Checkbox.Content>
            <Label className="font-bold">
              إظهار زر «الذهاب للواجب» بحالة مقفولة
            </Label>
          </Checkbox.Content>
        </Checkbox>

        {enabled && (
          <div className="mt-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
            <label className="block text-sm font-bold text-[var(--admin-text)]">
              الموعد المتوقع
              <input
                type="date"
                required
                min={cairoCurrentDate()}
                value={date}
                onChange={(event) => setDate(event.target.value)}
                className="admin-input mt-2 w-full"
              />
            </label>
            <div
              className="inline-flex min-h-12 items-center gap-2 rounded-xl bg-[var(--admin-card)] px-4 py-3 text-sm font-black text-[var(--admin-primary)]"
              aria-live="polite"
            >
              <Eye className="h-4 w-4" aria-hidden="true" />
              معاينة: {getHomeworkComingSoonLabel(date)}
            </div>
          </div>
        )}
      </div>

      <button
        type="button"
        disabled={saving}
        onClick={() => void save()}
        className="admin-btn-primary mt-5 inline-flex min-h-12 w-full items-center justify-center gap-2 px-6 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
      >
        {enabled ? (
          <Save className="h-4 w-4" aria-hidden="true" />
        ) : (
          <EyeOff className="h-4 w-4" aria-hidden="true" />
        )}
        {saving ? 'جارٍ الحفظ...' : enabled ? 'حفظ موعد الإعلان' : 'إخفاء الإعلان'}
      </button>
    </section>
  );
}
