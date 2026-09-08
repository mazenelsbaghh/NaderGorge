export function LearningProgress({ percent, label }: { percent: number | null; label: string }) {
  return (
    <div className="min-w-0 space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm font-bold text-[var(--admin-text)]">
        <span>{label}</span>
        <span className="shrink-0 tabular-nums">{percent === null ? 'المدة غير متاحة بعد' : `${percent}%`}</span>
      </div>
      {percent !== null && <div role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}
        className="h-2 overflow-hidden rounded-full bg-[var(--admin-card-strong)]">
        <div className="h-full rounded-full bg-[var(--admin-accent)]" style={{ width: `${percent}%` }} />
      </div>}
    </div>
  );
}
