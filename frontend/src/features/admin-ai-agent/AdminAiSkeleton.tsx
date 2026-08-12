export function AdminAiSkeleton() {
  return (
    <div aria-busy="true" aria-label="جارٍ التحميل" className="space-y-4 p-5">
      {[72, 55, 80].map((width) => (
        <div
          key={width}
          className="h-20 motion-safe:animate-pulse rounded-2xl bg-[var(--admin-card-soft)]"
          style={{ width: `${width}%` }}
        />
      ))}
    </div>
  );
}
