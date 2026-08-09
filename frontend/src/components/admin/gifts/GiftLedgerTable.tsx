'use client';

import Link from 'next/link';
import { ExternalLink } from 'lucide-react';
import { giftTargetLabels, type GiftListItemDto } from '@/services/admin-gifts-service';
import { GiftStatusBadge } from './GiftStatusBadge';

const date = (value?: string | null) => value ? new Intl.DateTimeFormat('ar-EG', { timeZone: 'Africa/Cairo', dateStyle: 'medium' }).format(new Date(value)) : 'بدون انتهاء';

export function GiftLedgerTable({ items }: { items: GiftListItemDto[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <table className="w-full min-w-[900px] text-right text-sm">
        <thead className="bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"><tr><th className="px-4 py-3">الهدف</th><th className="px-4 py-3">الحالة</th><th className="px-4 py-3">المستفيدون</th><th className="px-4 py-3">القيمة المتبقية</th><th className="px-4 py-3">الانتهاء</th><th className="px-4 py-3">المُصدر</th><th className="px-4 py-3"><span className="sr-only">فتح</span></th></tr></thead>
        <tbody className="divide-y divide-[var(--admin-border)]">
          {items.map((gift) => <tr key={gift.id} className="hover:bg-[var(--admin-hover)]"><td className="px-4 py-3"><strong className="block text-[var(--admin-text)]">{gift.targetName}</strong><small className="text-[var(--admin-muted)]">{giftTargetLabels[gift.targetType]} · {date(gift.issuedAt)}</small></td><td className="px-4 py-3"><GiftStatusBadge status={gift.status} /></td><td className="px-4 py-3 font-bold text-[var(--admin-text)]">{gift.successfulCount} / {gift.recipientCount}</td><td className="px-4 py-3">{gift.originalValue == null ? 'وصول مباشر' : `${gift.availableValue ?? 0} / ${gift.originalValue} ج.م`}</td><td className="px-4 py-3 text-[var(--admin-muted)]">{date(gift.expiresAt)}</td><td className="px-4 py-3 text-[var(--admin-muted)]">{gift.issuerName}</td><td className="px-4 py-3"><Link href={`/admin/gifts/${gift.id}`} className="admin-btn-icon" title="فتح التفاصيل" aria-label={`فتح هدية ${gift.targetName}`}><ExternalLink className="h-4 w-4" /></Link></td></tr>)}
        </tbody>
      </table>
    </div>
  );
}
