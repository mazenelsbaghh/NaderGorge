'use client';
import { useState } from 'react';
import { Star } from 'lucide-react';
import { getLiveSupportApiError, liveSupportService } from '@/services/live-support-service';
export function ConversationRating({ conversationId, onRated }: { conversationId: string; onRated?: (stars: number) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastStars, setLastStars] = useState<number | null>(null);

  async function submit(stars: number) {
    if (busy) return;
    setBusy(true);
    setLastStars(stars);
    setError(null);
    try {
      await liveSupportService.submitRating(conversationId, { stars });
      onRated?.(stars);
    } catch (cause) {
      setError(getLiveSupportApiError(cause, 'تعذر حفظ التقييم. حاول مرة أخرى.'));
    } finally {
      setBusy(false);
    }
  }

  return <fieldset disabled={busy} className="text-center">
    <legend className="mb-2 text-sm text-slate-600">قيّم تجربتك</legend>
    <div className="flex justify-center gap-1">{[1, 2, 3, 4, 5].map((stars) => <button key={stars} type="button" aria-label={`${stars} نجوم`} onClick={() => void submit(stars)} className="grid size-11 place-items-center text-amber-600 focus-visible:outline-2"><Star size={22}/></button>)}</div>
    {error && <div role="alert" className="mt-2 text-xs text-red-600"><p>{error}</p>{lastStars !== null && <button type="button" onClick={() => void submit(lastStars)} className="font-semibold underline">إعادة المحاولة</button>}</div>}
  </fieldset>;
}
