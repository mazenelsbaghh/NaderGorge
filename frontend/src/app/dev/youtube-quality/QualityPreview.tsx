'use client';

import { useState } from 'react';
import SecureVideoPlayer from '@/components/video/SecureVideoPlayer';

export default function QualityPreview() {
  const [narrow, setNarrow] = useState(false);
  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-10" dir="rtl">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[var(--foreground)]">تجربة الجودة على مشغّل مسار</h1>
          <p className="mt-2 text-sm text-[var(--muted-foreground)]">شغّل الفيديو، واضغط الترس أعلى اليسار لفتح الإعدادات، ثم اختر «الجودة».</p>
        </div>
        <button type="button" onClick={() => setNarrow(!narrow)} className="min-h-11 rounded-lg border border-[var(--border)] bg-[var(--card)] px-4 text-sm">
          {narrow ? 'عرض الكمبيوتر' : 'تجربة عرض ضيق'}
        </button>
      </div>
      <div className={narrow ? 'mx-auto max-w-[390px]' : ''}>
        <SecureVideoPlayer lessonVideoId="local-youtube-quality-preview" localYouTubeQualityPreview enableChapterAids={false} />
      </div>
      <p className="mt-5 text-sm text-[var(--muted-foreground)]">نفس مشغّل الدروس وأزراره. تجربة محلية على فيديو عام، بدون احتساب مشاهدات.</p>
    </main>
  );
}
