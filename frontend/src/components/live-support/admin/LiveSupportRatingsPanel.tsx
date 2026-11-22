'use client';

import { useEffect, useState } from 'react';
import { Star } from 'lucide-react';
import {
  cairoDateTimeLocalToUtcISOString,
  formatCairoTimestamp,
} from '@/lib/cairo-time';
import {
  getLiveSupportApiError,
  liveSupportService,
  type LiveSupportRating,
} from '@/services/live-support-service';

export function LiveSupportRatingsPanel({
  openConversation,
}: {
  openConversation: (conversationId: string) => void;
}) {
  const [ratings, setRatings] = useState<LiveSupportRating[]>([]);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    if (from && to && from > to) {
      setError('بداية الفترة يجب أن تسبق نهايتها.');
      setLoading(false);
      return () => controller.abort();
    }
    async function load() {
      try {
        const result = await liveSupportService.getAdminRatings(
          {
            from: from ? cairoDateTimeLocalToUtcISOString(from) : undefined,
            to: to ? cairoDateTimeLocalToUtcISOString(to) : undefined,
          },
          controller.signal
        );
        if (!controller.signal.aborted) setRatings(result);
      } catch (cause) {
        if (!controller.signal.aborted)
          setError(
            getLiveSupportApiError(
              cause,
              'تعذر تحميل التقييمات. حاول مرة أخرى.'
            )
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => controller.abort();
  }, [from, to, retry]);

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="flex items-center gap-2 font-bold text-[var(--admin-text)]">
            <Star className="size-5 text-[var(--admin-warning)]" />
            تقييمات الطلاب وملاحظاتهم
          </h2>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">
            الفترة حسب وقت إرسال التقييم بتوقيت القاهرة. افتح المحادثة لمراجعة
            الملاحظة.
          </p>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm font-semibold text-[var(--admin-text)]">
            من
            <input
              type="datetime-local"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              className="mt-1 block min-h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2"
            />
          </label>
          <label className="text-sm font-semibold text-[var(--admin-text)]">
            إلى
            <input
              type="datetime-local"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              className="mt-1 block min-h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2"
            />
          </label>
          <button
            type="button"
            onClick={() => setRetry((value) => value + 1)}
            disabled={loading}
            className="min-h-11 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-60"
          >
            {loading ? 'جارٍ التحميل…' : 'عرض التقييمات'}
          </button>
        </div>
      </div>
      {error && (
        <p role="alert" className="mt-4 text-sm text-[var(--admin-danger)]">
          {error}
        </p>
      )}
      {!loading && !error && ratings.length === 500 && (
        <p className="mt-4 text-sm text-[var(--admin-muted)]">
          المعروض أحدث 500 تقييم خلال الفترة. اختر فترة أصغر للتقييمات الأقدم،
          أو ابحث في كل المحادثات عن ملاحظات الطلاب.
        </p>
      )}
      <div className="mt-5 overflow-x-auto">
        <table className="w-full min-w-[650px] text-right text-sm">
          <thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]">
            <tr>
              <th className="p-3">الطالب</th>
              <th className="p-3">النجوم</th>
              <th className="p-3">الملاحظة للإدارة</th>
              <th className="p-3">وقت التقييم</th>
              <th className="p-3">المحادثة</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td
                  colSpan={5}
                  className="p-8 text-center text-[var(--admin-muted)]"
                >
                  جارٍ تحميل التقييمات…
                </td>
              </tr>
            ) : error ? null : ratings.length === 0 ? (
              <tr>
                <td
                  colSpan={5}
                  className="p-8 text-center text-[var(--admin-muted)]"
                >
                  لا توجد تقييمات خلال الفترة المختارة.
                </td>
              </tr>
            ) : (
              ratings.map((rating) => (
                <tr
                  key={rating.id}
                  className="border-t border-[var(--admin-border)]"
                >
                  <td className="p-3 font-semibold text-[var(--admin-text)]">
                    {rating.submittedByName}
                    <span className="mr-2 text-xs font-normal text-[var(--admin-muted)]">
                      {rating.isStudent ? 'طالب' : 'زائر'}
                    </span>
                  </td>
                  <td className="whitespace-nowrap p-3 font-semibold text-[var(--admin-text)]">
                    {rating.stars} من 5
                  </td>
                  <td className="max-w-sm whitespace-pre-wrap break-words p-3 text-[var(--admin-text)]">
                    {rating.comment || 'بدون ملاحظة'}
                  </td>
                  <td className="p-3 text-[var(--admin-text)]">
                    <time dateTime={rating.submittedAt}>
                      {formatCairoTimestamp(rating.submittedAt)}
                    </time>
                  </td>
                  <td className="p-3">
                    <button
                      type="button"
                      onClick={() => openConversation(rating.conversationId)}
                      className="min-h-11 font-semibold text-[var(--admin-primary)]"
                    >
                      فتح المحادثة
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
