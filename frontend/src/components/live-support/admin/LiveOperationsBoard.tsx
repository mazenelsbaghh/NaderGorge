import type { LiveSupportAdminDashboard } from '@/services/live-support-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

export function LiveOperationsBoard({ dashboard }: { dashboard: LiveSupportAdminDashboard }) {
  const oldest = dashboard.conversations.filter((conversation) => conversation.status === 'Waiting').sort((a, b) => a.createdAt.localeCompare(b.createdAt))[0];
  const metrics = [
    ['في الطابور', dashboard.waitingCount],
    ['محادثات جارية', dashboard.activeCount],
    ['أُغلقت اليوم', dashboard.closedToday],
    ['أقدم انتظار', oldest ? formatCairoDateTime(oldest.createdAt, { hour: '2-digit', minute: '2-digit' }) : '—'],
  ];

  return <section aria-label="العمليات الآن" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
    {metrics.map(([label, value]) => <div key={String(label)} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-[var(--admin-shadow)]">
      <p className="text-xs font-bold text-[var(--admin-muted)]">{label}</p>
      <p className="mt-1 text-2xl font-black text-[var(--admin-text)]">{value}</p>
    </div>)}
  </section>;
}
