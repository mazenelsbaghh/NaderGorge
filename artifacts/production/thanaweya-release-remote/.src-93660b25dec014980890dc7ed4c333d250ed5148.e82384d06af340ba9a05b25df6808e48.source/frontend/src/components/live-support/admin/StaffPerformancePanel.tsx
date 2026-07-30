import { Star } from 'lucide-react';
import type { LiveSupportStaffPerformance } from '@/services/live-support-service';
export function StaffPerformancePanel({ staff }: { staff: LiveSupportStaffPerformance[] }) {
  return <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)]">
    <h2 className="mb-4 font-bold text-[var(--admin-text)]">أداء الموظفين والتقييمات</h2>
    {staff.length === 0 ? <p className="rounded-xl bg-[var(--admin-card-soft)] p-6 text-center text-sm text-[var(--admin-muted)]">لا توجد بيانات أداء للموظفين حتى الآن.</p> : <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{staff.map((item) => <article key={item.staffUserId} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4"><div className="flex justify-between gap-3"><strong className="truncate text-[var(--admin-text)]">{item.staffName}</strong><span className="flex shrink-0 gap-1 text-[var(--admin-warning)]"><Star size={15}/>{item.averageRating?.toFixed(1) || '—'}</span></div><p className="mt-2 text-xs text-[var(--admin-muted)]">شارك في {item.participatedConversations} · أغلق {item.closedConversations} · {item.ratingCount} تقييم</p></article>)}</div>}
  </section>;
}
