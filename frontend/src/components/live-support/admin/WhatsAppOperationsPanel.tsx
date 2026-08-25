import { Clock3, MessageCircle, RefreshCw } from 'lucide-react';

import {
  cairoCurrentDate,
  formatCairoTimestamp,
  parseUtcDateTime,
} from '@/lib/cairo-time';
import type {
  LiveSupportAdminDashboard,
  LiveSupportWhatsAppAdminSummary,
  LiveSupportWhatsAppTemplate,
} from '@/services/live-support-service';

interface WhatsAppOperationsPanelProps {
  dashboard: LiveSupportAdminDashboard;
  templates: LiveSupportWhatsAppTemplate[];
  syncing: boolean;
  syncFeedback: string;
  onSync: () => void;
}

export function WhatsAppOperationsPanel({
  dashboard,
  templates,
  syncing,
  syncFeedback,
  onSync,
}: WhatsAppOperationsPanelProps) {
  const summary = dashboard.whatsApp ?? fallbackSummary(dashboard, templates);
  const templateStatuses = summarizeTemplateStatuses(templates);
  const hasServerSummary = Boolean(dashboard.whatsApp);
  const metrics = [
    { label: 'محادثات مفتوحة', value: summary.open },
    { label: 'في الانتظار', value: summary.waiting },
    { label: 'قيد المتابعة', value: summary.active },
    { label: 'أُغلقت اليوم', value: summary.closedToday },
    {
      label: 'إرسال فاشل',
      value: hasServerSummary ? summary.failedOutbound : '—',
      danger: summary.failedOutbound > 0,
    },
    { label: 'قوالب معتمدة', value: summary.approvedTemplates },
  ];

  return (
    <section
      aria-labelledby="whatsapp-operations-heading"
      className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)]"
    >
      <div className="flex flex-col gap-4 border-b border-[var(--admin-border)] p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
        <div className="flex min-w-0 items-start gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-success-10)] text-[var(--admin-success)]">
            <MessageCircle aria-hidden="true" size={20} />
          </span>
          <div className="min-w-0">
            <h2
              id="whatsapp-operations-heading"
              className="font-bold text-[var(--admin-text)]"
            >
              ملخص واتساب التشغيلي
            </h2>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
              متابعة القناة، نوافذ الرد، وحالة القوالب المعتمدة من مكان واحد.
            </p>
            <div className="mt-2 flex flex-wrap gap-1.5 text-[11px] font-black">
              <StatusCount label="APPROVED" value={templateStatuses.approved} tone="success" />
              <StatusCount label="PENDING" value={templateStatuses.pending} tone="warning" />
              <StatusCount label="REJECTED" value={templateStatuses.rejected} tone="danger" />
              <StatusCount label="STALE" value={templateStatuses.stale} />
            </div>
          </div>
        </div>
        <button
          type="button"
          disabled={syncing}
          onClick={onSync}
          className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-60"
        >
          <RefreshCw
            aria-hidden="true"
            size={17}
            className={syncing ? 'animate-spin' : ''}
          />
          {syncing ? 'جارٍ مزامنة كل القوالب…' : 'مزامنة كل القوالب'}
        </button>
      </div>

      <div
        className="grid grid-cols-2 divide-x divide-y divide-[var(--admin-border)] sm:grid-cols-3 xl:grid-cols-6 xl:divide-y-0"
        dir="rtl"
      >
        {metrics.map((metric) => (
          <div key={metric.label} className="min-w-0 px-4 py-4">
            <p className="text-xs font-bold text-[var(--admin-muted)]">
              {metric.label}
            </p>
            <p
              className={`mt-1 text-2xl font-black ${metric.danger ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-text)]'}`}
            >
              {metric.value}
            </p>
          </div>
        ))}
      </div>

      <div className="flex flex-col gap-2 border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-3 text-xs text-[var(--admin-muted)] sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-5 sm:px-5">
        <Timestamp label="آخر رسالة واردة" value={summary.lastInboundAt} />
        <Timestamp label="آخر رسالة صادرة" value={summary.lastOutboundAt} />
        <Timestamp
          label="آخر مزامنة للقوالب"
          value={summary.lastTemplateSyncAt}
        />
      </div>
      {syncFeedback ? (
        <p
          role={syncFeedback.startsWith('تعذر') ? 'alert' : 'status'}
          className={`border-t border-[var(--admin-border)] px-5 py-3 text-sm font-medium ${syncFeedback.startsWith('تعذر') ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-success)]'}`}
        >
          {syncFeedback}
        </p>
      ) : null}
    </section>
  );
}

function Timestamp({ label, value }: { label: string; value?: string | null }) {
  return (
    <span className="inline-flex min-w-0 items-center gap-1.5">
      <Clock3 aria-hidden="true" size={14} />
      <strong className="shrink-0 text-[var(--admin-text)]">{label}:</strong>
      <time dateTime={value ?? undefined} className="truncate">
        {formatOptionalTimestamp(value)}
      </time>
    </span>
  );
}

function formatOptionalTimestamp(value?: string | null) {
  if (!value) return 'لا يوجد بعد';
  const parsed = parseUtcDateTime(value);
  return Number.isNaN(parsed.getTime())
    ? 'غير متاح'
    : formatCairoTimestamp(parsed);
}

function summarizeTemplateStatuses(templates: LiveSupportWhatsAppTemplate[]) {
  const counts = { approved: 0, pending: 0, rejected: 0, stale: 0 };
  for (const template of templates) {
    const status = template.status.toUpperCase();
    if (status === 'APPROVED') counts.approved += 1;
    else if (status === 'PENDING' || status === 'IN_APPEAL') counts.pending += 1;
    else if (status === 'REJECTED' || status === 'DISABLED') counts.rejected += 1;
    else if (status === 'STALE' || status === 'PAUSED') counts.stale += 1;
  }
  return counts;
}

function StatusCount({ label, value, tone }: { label: string; value: number; tone?: 'success' | 'warning' | 'danger' }) {
  const color = tone === 'success'
    ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]'
    : tone === 'warning'
      ? 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]'
      : tone === 'danger'
        ? 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
        : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]';
  return <span className={`rounded-full px-2 py-1 ${color}`}>{label}: {new Intl.NumberFormat('ar-EG').format(value)}</span>;
}

function fallbackSummary(
  dashboard: LiveSupportAdminDashboard,
  templates: LiveSupportWhatsAppTemplate[]
): LiveSupportWhatsAppAdminSummary {
  const conversations = dashboard.conversations.filter(
    (conversation) => conversation.channel === 'WhatsApp'
  );
  const today = cairoCurrentDate();
  const syncedAt = templates
    .map((template) => template.lastSyncedAt)
    .filter(Boolean)
    .sort(
      (left, right) =>
        parseUtcDateTime(right).getTime() - parseUtcDateTime(left).getTime()
    )[0];

  return {
    open: conversations.filter(
      (conversation) => !['Closed', 'Abandoned'].includes(conversation.status)
    ).length,
    waiting: conversations.filter(
      (conversation) => conversation.status === 'Waiting'
    ).length,
    active: conversations.filter(
      (conversation) =>
        conversation.status === 'Assigned' || conversation.status === 'Active'
    ).length,
    closedToday: conversations.filter(
      (conversation) =>
        conversation.status === 'Closed' &&
        conversation.closedAt &&
        cairoCurrentDate(parseUtcDateTime(conversation.closedAt)) === today
    ).length,
    failedOutbound: 0,
    approvedTemplates: templates.filter(
      (template) => template.status.toUpperCase() === 'APPROVED'
    ).length,
    lastInboundAt: null,
    lastOutboundAt: null,
    lastTemplateSyncAt: syncedAt ?? null,
  };
}
