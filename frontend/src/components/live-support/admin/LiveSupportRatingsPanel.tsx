'use client';

import { useState } from 'react';
import { Star } from 'lucide-react';
import { cairoDateTimeLocalToUtcISOString, formatCairoTimestamp } from '@/lib/cairo-time';
import { liveSupportService, type LiveSupportRating } from '@/services/live-support-service';

export function LiveSupportRatingsPanel({ openConversation }: { openConversation: (conversationId: string) => void }) {
  const [ratings, setRatings] = useState<LiveSupportRating[]>([]);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [loading, setLoading] = useState(false);

  async function loadRatings() {
    setLoading(true);
    try {
      setRatings(await liveSupportService.getAdminRatings({
        from: from ? cairoDateTimeLocalToUtcISOString(from) : undefined,
        to: to ? cairoDateTimeLocalToUtcISOString(to) : undefined,
      }));
    } finally { setLoading(false); }
  }

  return <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)]"><div className="flex flex-wrap items-end justify-between gap-4"><div><h2 className="flex items-center gap-2 font-bold text-[var(--admin-text)]"><Star className="size-5 text-amber-500" />تقييمات الطلاب</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">اعرف النجوم والتعليق والطالب الذي وضع التقييم خلال أي فترة.</p></div><div className="flex flex-wrap items-end gap-3"><label className="text-sm font-semibold">من<input type="datetime-local" value={from} onChange={(event) => setFrom(event.target.value)} className="mt-1 block h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]" /></label><label className="text-sm font-semibold">إلى<input type="datetime-local" value={to} onChange={(event) => setTo(event.target.value)} className="mt-1 block h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]" /></label><button type="button" onClick={() => void loadRatings()} disabled={loading} className="h-10 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-60">{loading ? 'جارٍ التحميل…' : 'عرض التقييمات'}</button></div></div><div className="mt-5 overflow-x-auto"><table className="w-full min-w-[650px] text-right text-sm"><thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr><th className="p-3">الطالب</th><th className="p-3">النجوم</th><th className="p-3">التعليق</th><th className="p-3">وقت التقييم</th><th className="p-3">المحادثة</th></tr></thead><tbody>{ratings.length === 0 ? <tr><td colSpan={5} className="p-8 text-center text-[var(--admin-muted)]">اختر فترة ثم اضغط «عرض التقييمات».</td></tr> : ratings.map((rating) => <tr key={rating.id} className="border-t border-[var(--admin-border)]"><td className="p-3 font-semibold">{rating.submittedByName}<span className="mr-2 text-xs font-normal text-[var(--admin-muted)]">{rating.isStudent ? 'طالب' : 'زائر'}</span></td><td className="p-3 text-amber-500">{'★'.repeat(rating.stars)}{'☆'.repeat(5 - rating.stars)}</td><td className="max-w-sm p-3 text-[var(--admin-muted)]">{rating.comment || '—'}</td><td className="p-3"><time dateTime={rating.submittedAt}>{formatCairoTimestamp(rating.submittedAt)}</time></td><td className="p-3"><button type="button" onClick={() => openConversation(rating.conversationId)} className="font-semibold text-[var(--admin-primary)]">فتح المحادثة</button></td></tr>)}</tbody></table></div></section>;
}
