'use client';

export function YouTubePlaybackOptions({ hlsEnabled, qualityEnabled, onHlsChange, onQualityChange }: {
  hlsEnabled: boolean;
  qualityEnabled: boolean;
  onHlsChange: (enabled: boolean) => void;
  onQualityChange: (enabled: boolean) => void;
}) {
  return <div className="space-y-3">
    <fieldset className="space-y-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4">
      <legend className="px-1 text-xs font-bold text-[var(--admin-muted)]">مشغل الفيديو</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        <button type="button" onClick={() => onHlsChange(true)} aria-pressed={hlsEnabled} className={`min-h-14 rounded-xl border px-4 py-3 text-right transition-colors ${hlsEnabled ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'}`}>
          <span className="block text-sm font-black">مشغل المنصة HLS</span>
          <span className="mt-1 block text-xs font-semibold">نفس تحكم مشغل Bunny مع اختيار الجودة</span>
        </button>
        <button type="button" onClick={() => onHlsChange(false)} aria-pressed={!hlsEnabled} className={`min-h-14 rounded-xl border px-4 py-3 text-right transition-colors ${!hlsEnabled ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'}`}>
          <span className="block text-sm font-black">مشغل يوتيوب</span>
          <span className="mt-1 block text-xs font-semibold">تشغيل الفيديو بالمشغل الحالي</span>
        </button>
      </div>
      {hlsEnabled && <p className="text-xs text-[var(--admin-muted)]">تشغيل مباشر من يوتيوب. الميزة تجريبية وتحتاج متصفحًا يدعم HLS مباشرة، مثل Safari.</p>}
    </fieldset>
    {!hlsEnabled && <label className="flex min-h-14 cursor-pointer items-start gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
      <input type="checkbox" checked={qualityEnabled} onChange={event => onQualityChange(event.target.checked)} className="mt-1 size-5 shrink-0 accent-[var(--admin-primary)]" />
      <span>
        <span className="block text-sm font-bold text-[var(--admin-text)]">السماح بتغيير الجودة</span>
        <span className="mt-1 block text-xs text-[var(--admin-muted)]">يعرض إعدادات جودة يوتيوب، بما فيها «تلقائي». قد تظهر عناصر وروابط من يوتيوب أثناء الاختيار.</span>
      </span>
    </label>}
  </div>;
}
