/**
 * AdminPageSkeleton
 * A beautiful, integrated loading skeleton for admin pages.
 * Shows placeholders for metrics, toolbars, and data tables.
 * To be used INSIDE AdminShellChrome when data is fetching,
 * so it preserves the real Sidebar/Navbar without ugly flashes.
 */
export function AdminPageSkeleton() {
  return (
    <div className="flex w-full flex-col gap-8 animate-in fade-in duration-500">
      {/* Metrics Row Skeleton */}
      <section className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {[1, 2, 3].map((i) => (
          <div
            key={i}
            className="relative h-36 overflow-hidden rounded-[2rem] bg-[var(--admin-card-strong)] shadow-sm"
          >
            <div className="absolute inset-0 bg-gradient-to-r from-transparent via-[var(--admin-bg)] to-transparent opacity-10 animate-[shimmer_2s_infinite]" style={{ backgroundSize: '200% 100%' }} />
            <div className="absolute left-6 top-6 h-12 w-12 rounded-2xl bg-[var(--admin-muted)] opacity-20" />
            <div className="absolute right-6 top-6 h-6 w-24 rounded-md bg-[var(--admin-muted)] opacity-20" />
            <div className="absolute right-6 bottom-6 h-8 w-32 rounded-lg bg-[var(--admin-muted)] opacity-30" />
          </div>
        ))}
      </section>

      {/* Toolbar Skeleton */}
      <div className="flex h-16 w-full items-center justify-between rounded-full bg-[var(--admin-card-strong)] px-6 shadow-sm">
         <div className="h-6 w-48 rounded-md bg-[var(--admin-muted)] opacity-20" />
         <div className="h-10 w-32 rounded-full bg-[var(--admin-muted)] opacity-20" />
      </div>

      {/* Table Area Skeleton */}
      <div className="h-[500px] w-full overflow-hidden rounded-[2.5rem] bg-[var(--admin-card-strong)] shadow-sm">
        {/* Header */}
        <div className="h-16 w-full border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)]" />
        {/* Rows */}
        <div className="flex flex-col p-4 space-y-4">
           {[1, 2, 3, 4, 5].map((i) => (
             <div key={i} className="h-16 w-full rounded-2xl bg-[var(--admin-card)] opacity-40 animate-pulse" style={{ animationDelay: `${i * 100}ms` }} />
           ))}
        </div>
      </div>
      
      <style dangerouslySetInnerHTML={{__html: `
        @keyframes shimmer {
          0% { transform: translateX(-100%); }
          100% { transform: translateX(100%); }
        }
      `}} />
    </div>
  );
}
