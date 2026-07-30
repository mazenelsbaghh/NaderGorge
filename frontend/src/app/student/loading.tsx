import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function StudentLoading() {
  return (
    <AsyncRegionState
      status="loading"
      message="جاري تحميل محتوى الطالب"
    >
      <div className="animate-pulse space-y-6" dir="rtl">
        {/* Header skeleton */}
        <div className="space-y-3">
          <div className="h-8 w-48 rounded-2xl bg-[var(--admin-border)]" />
          <div className="h-4 w-72 rounded-xl bg-[var(--admin-border)] opacity-60" />
        </div>

        {/* Stat cards skeleton */}
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div
              key={i}
              className="h-28 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]"
            />
          ))}
        </div>

        {/* Content area skeleton */}
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div
              key={i}
              className="h-20 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]"
            />
          ))}
        </div>
      </div>
    </AsyncRegionState>
  );
}
