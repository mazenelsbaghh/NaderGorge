'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { learningError, timeLabel, videoLearningService, type LearningReview } from '@/services/video-learning-service';

export function LearningReviewList() {
  const [items, setItems] = useState<LearningReview[] | null>(null);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    let current = true;
    videoLearningService.review().then(rows => { if (current) { setItems(rows); setError(''); } }).catch(e => { if (current) setError(learningError(e)); });
    return () => { current = false; };
  }, [retry]);
  return <section className="space-y-4 rounded-xl border border-[var(--admin-border)] p-5">
    <h2 className="text-xl font-bold">أجزاء الفيديو اللي محتاج أراجعها</h2>
    {error ? <p role="alert">{error} <button className="min-h-11 underline" onClick={() => setRetry(r => r + 1)}>إعادة المحاولة</button></p>
      : items === null ? <p role="status">جارٍ تحميل أجزائك الصعبة…</p>
        : items.length === 0 ? <p>لما تعلّم جزء «مش فاهم» أو تحتاج تراجع سؤال في الفيديو، هتلاقيه هنا.</p>
          : <ul className="divide-y divide-[var(--admin-border)]">{items.map(item => <li key={item.id}>
            <Link className="flex min-h-11 flex-wrap items-center justify-between gap-2 py-3 text-[var(--admin-primary)]" href={`/student/lessons/${item.lessonId}?videoId=${item.lessonVideoId}&t=${Math.max(0, item.seconds - 10)}`}>
              <span>{item.videoTitle}{item.title ? ` · ${item.title}` : ''}</span><span>{timeLabel(item.seconds)} ← راجع الجزء</span>
            </Link>
          </li>)}</ul>}
  </section>;
}
