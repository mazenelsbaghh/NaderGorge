'use client';

import { useState } from 'react';
import { Archive, EyeOff, RotateCcw, UsersRound } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminModal } from './AdminModal';
import {
  adminService,
  type ContentArchiveMode,
  type ContentArchiveTargetType,
} from '@/services/admin-service';

type Props = {
  targetType: ContentArchiveTargetType;
  targetId: string;
  title: string;
  archiveMode?: ContentArchiveMode;
  onChanged?: () => void | Promise<void>;
  compact?: boolean;
};

const modeLabels: Record<Exclude<ContentArchiveMode, 'None'>, string> = {
  ActiveSubscribersOnly: 'للمشتركين الحاليين فقط',
  HiddenFromEveryone: 'مخفي عن الجميع',
};

export function ContentArchiveControl({
  targetType,
  targetId,
  title,
  archiveMode = 'None',
  onChanged,
  compact = false,
}: Props) {
  const [open, setOpen] = useState(false);
  const [savingMode, setSavingMode] = useState<ContentArchiveMode | null>(null);
  const archived = archiveMode !== 'None';

  async function update(nextMode: ContentArchiveMode) {
    try {
      setSavingMode(nextMode);
      await adminService.setContentArchiveState(targetType, targetId, nextMode);
      toast.success(nextMode === 'None' ? 'تمت إعادة المحتوى إلى المحتوى الحالي.' : 'تمت أرشفة المحتوى.');
      setOpen(false);
      await onChanged?.();
    } catch {
      toast.error('تعذر تحديث حالة الأرشفة. حاول مرة أخرى.');
    } finally {
      setSavingMode(null);
    }
  }

  return (
    <>
      <button
        type="button"
        onClick={(event) => {
          event.preventDefault();
          event.stopPropagation();
          if (archived) void update('None');
          else setOpen(true);
        }}
        disabled={savingMode !== null}
        className={`inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl border px-3 text-xs font-black transition disabled:cursor-wait disabled:opacity-60 ${
          archived
            ? 'border-amber-400/50 bg-amber-50 text-amber-800 hover:bg-amber-100 dark:bg-amber-950/30 dark:text-amber-200'
            : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)] hover:text-[var(--admin-primary)]'
        }`}
        aria-label={archived ? `إعادة ${title} إلى المحتوى الحالي` : `أرشفة ${title}`}
        title={archived ? modeLabels[archiveMode] : 'أرشفة المحتوى'}
      >
        {archived ? <RotateCcw className="h-4 w-4" /> : <Archive className="h-4 w-4" />}
        {archived ? modeLabels[archiveMode] : !compact ? 'أرشفة' : null}
      </button>

      <AdminModal
        open={open}
        onClose={() => !savingMode && setOpen(false)}
        title={`أرشفة «${title}»`}
        subtitle="اختر من يمكنه الوصول إلى المحتوى بعد نقله إلى المؤرشف."
        maxWidth="max-w-lg"
      >
        <div className="space-y-3" dir="rtl">
          <button
            type="button"
            disabled={savingMode !== null}
            onClick={() => void update('ActiveSubscribersOnly')}
            className="flex min-h-24 w-full items-start gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 text-right transition hover:border-[var(--admin-primary)] disabled:opacity-60"
          >
            <UsersRound className="mt-0.5 h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
            <span>
              <span className="block font-black text-[var(--admin-text)]">للمشتركين الحاليين فقط</span>
              <span className="mt-1 block text-sm leading-6 text-[var(--admin-muted)]">
                يختفي من البيع ومن غير المشتركين، ويظل ظاهرًا للطلاب أصحاب الاشتراك النشط فقط.
              </span>
            </span>
          </button>

          <button
            type="button"
            disabled={savingMode !== null}
            onClick={() => void update('HiddenFromEveryone')}
            className="flex min-h-24 w-full items-start gap-3 rounded-2xl border border-red-300/60 bg-red-50/70 p-4 text-right transition hover:border-red-500 disabled:opacity-60 dark:bg-red-950/25"
          >
            <EyeOff className="mt-0.5 h-5 w-5 shrink-0 text-red-600 dark:text-red-300" />
            <span>
              <span className="block font-black text-red-800 dark:text-red-200">مخفي عن الجميع</span>
              <span className="mt-1 block text-sm leading-6 text-red-700/80 dark:text-red-200/80">
                لا يظهر لأي طالب، حتى المشتركين الحاليين، ولا يمكن شراؤه أو فتحه من رابط مباشر.
              </span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => setOpen(false)}
            disabled={savingMode !== null}
            className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-card)] disabled:opacity-60"
          >
            إلغاء
          </button>
        </div>
      </AdminModal>
    </>
  );
}
