'use client';

import { useId, useRef, useState, type FormEvent } from 'react';
import { Star } from 'lucide-react';
import {
  getLiveSupportApiError,
  liveSupportService,
} from '@/services/live-support-service';

const ratingLabels = ['سيئة جدًا', 'سيئة', 'متوسطة', 'جيدة', 'ممتازة'];

export function ConversationRating({
  conversationId,
  onRated,
}: {
  conversationId: string;
  onRated?: (stars: number) => void;
}) {
  const fieldId = useId();
  const submitting = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stars, setStars] = useState<number | null>(null);
  const [preview, setPreview] = useState<number | null>(null);
  const [comment, setComment] = useState('');
  const displayedStars = preview ?? stars;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting.current || stars === null) return;
    submitting.current = true;
    setBusy(true);
    setError(null);
    try {
      await liveSupportService.submitRating(conversationId, {
        stars,
        comment: comment.trim() || undefined,
      });
      onRated?.(stars);
    } catch (cause) {
      setError(
        getLiveSupportApiError(cause, 'تعذر حفظ التقييم. حاول مرة أخرى.')
      );
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-3 text-right" dir="rtl">
      <fieldset disabled={busy} className="space-y-2 text-center">
        <legend className="w-full text-sm font-bold text-slate-900">
          كيف كانت تجربتك مع الدعم؟
        </legend>
        <div
          className="flex justify-center gap-1"
          dir="ltr"
          onMouseLeave={() => setPreview(null)}
        >
          {[1, 2, 3, 4, 5].map((value) => (
            <label
              key={value}
              className="relative grid size-11 cursor-pointer place-items-center rounded-lg hover:bg-amber-50"
            >
              <input
                type="radio"
                name={fieldId}
                value={value}
                checked={stars === value}
                onChange={() => {
                  setStars(value);
                  setPreview(null);
                  setError(null);
                }}
                aria-label={`${value} من 5، ${ratingLabels[value - 1]}`}
                className="peer sr-only"
              />
              <Star
                aria-hidden="true"
                size={29}
                onMouseEnter={() => setPreview(value)}
                className={`rounded-sm text-amber-600 peer-focus-visible:outline-2 peer-focus-visible:outline-offset-4 peer-focus-visible:outline-slate-900 ${displayedStars !== null && value <= displayedStars ? 'fill-amber-500' : 'fill-transparent'}`}
              />
            </label>
          ))}
        </div>
        <p
          className="min-h-5 text-sm font-semibold text-slate-700"
          aria-live="polite"
        >
          {displayedStars === null
            ? 'اختر من نجمة واحدة إلى 5 نجوم'
            : `${displayedStars} من 5، ${ratingLabels[displayedStars - 1]}`}
        </p>
      </fieldset>
      <label
        htmlFor={`${fieldId}-comment`}
        className="block text-sm font-semibold text-slate-900"
      >
        ملاحظتك للإدارة{' '}
        <span className="font-normal text-slate-600">(اختياري)</span>
      </label>
      <textarea
        id={`${fieldId}-comment`}
        value={comment}
        onChange={(event) => setComment(event.target.value)}
        disabled={busy}
        maxLength={1000}
        rows={3}
        placeholder="اكتب ملاحظتك عن المساعدة أو أسلوب التعامل"
        className="w-full resize-y rounded-xl border border-slate-300 bg-white p-3 text-sm text-slate-900 placeholder:text-slate-600 focus-visible:outline-2 focus-visible:outline-cyan-700 disabled:opacity-60"
      />
      {error && (
        <p role="alert" className="text-sm text-red-700">
          {error}
        </p>
      )}
      <button
        type="submit"
        disabled={busy || stars === null}
        className="min-h-11 w-full rounded-xl bg-cyan-800 px-4 py-2 text-sm font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-800 disabled:opacity-60"
      >
        {busy
          ? 'جارٍ إرسال التقييم…'
          : error
            ? 'إعادة إرسال التقييم'
            : 'إرسال التقييم'}
      </button>
    </form>
  );
}
