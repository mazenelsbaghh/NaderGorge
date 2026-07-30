import type { GiftIssuanceStatus } from '@/services/admin-gifts-service';

const labels: Record<string, string> = {
  Active: 'نشطة',
  PartiallySuccessful: 'نجاح جزئي',
  Completed: 'مكتملة',
  Expired: 'منتهية',
  Revoked: 'ملغاة',
  AlreadyEntitled: 'مشترك مسبقاً',
  Failed: 'فشل',
  PartiallyUsed: 'مستخدمة جزئياً',
  Granted: 'ممنوحة',
};

export function GiftStatusBadge({ status }: { status: GiftIssuanceStatus | string }) {
  const tone = status === 'Active'
    ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300'
    : status === 'Failed' || status === 'Expired'
      ? 'bg-red-500/10 text-red-700 dark:text-red-300'
      : status === 'Revoked'
        ? 'bg-slate-500/10 text-slate-700 dark:text-slate-300'
        : 'bg-amber-500/10 text-amber-700 dark:text-amber-300';

  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-bold ${tone}`}>{labels[status] ?? status}</span>;
}
