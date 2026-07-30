export function LiveSupportSkeleton({ rows = 4, label = 'جارٍ تحميل بيانات الدعم' }: { rows?: number; label?: string }) {
  return <div role="status" aria-label={label} className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">{Array.from({ length: rows }, (_, index) => <div key={index} className="h-11 animate-pulse rounded-xl bg-[var(--admin-card-soft)] motion-reduce:animate-none"/>)}<span className="sr-only">{label}</span></div>;
}
