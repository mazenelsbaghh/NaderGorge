'use client';

import {
  AlertTriangle,
  Ban,
  BookOpenCheck,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CirclePause,
  CirclePlay,
  Clock3,
  Eye,
  LoaderCircle,
  MessageCircleCheck,
  RefreshCw,
  Send,
  X,
} from 'lucide-react';
import { useState } from 'react';

import { formatCairoTimestamp } from '@/lib/cairo-time';
import type {
  WhatsAppCampaignPage,
  WhatsAppCampaignStatus,
  WhatsAppCampaignSummary,
} from '@/services/live-support-service';

interface WhatsAppCampaignHistoryProps {
  page?: WhatsAppCampaignPage;
  loading: boolean;
  error: string;
  changingCampaignId?: string;
  onReload: () => void;
  onPageChange: (page: number) => void;
  onOperation: (
    campaign: WhatsAppCampaignSummary,
    operation: 'pause' | 'resume' | 'cancel',
    reason?: string,
  ) => void;
}

const statusPresentation: Record<WhatsAppCampaignStatus, { label: string; className: string }> = {
  Draft: { label: 'مسودة', className: 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]' },
  Locked: { label: 'جاهزة للإرسال', className: 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' },
  Running: { label: 'قيد الإرسال', className: 'bg-[var(--admin-accent-soft)] text-[var(--admin-accent)]' },
  Paused: { label: 'متوقفة مؤقتًا', className: 'bg-[var(--admin-warning-10)] text-[var(--admin-warning)]' },
  Completed: { label: 'اكتملت', className: 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' },
  Cancelled: { label: 'ملغاة', className: 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]' },
  Failed: { label: 'فشلت', className: 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]' },
};

export function WhatsAppCampaignHistory({
  page,
  loading,
  error,
  changingCampaignId,
  onReload,
  onPageChange,
  onOperation,
}: WhatsAppCampaignHistoryProps) {
  const [pendingAction, setPendingAction] = useState<{
    campaign: WhatsAppCampaignSummary;
    operation: 'pause' | 'cancel';
    reason: string;
  }>();

  if (loading && !page) {
    return <HistorySkeleton />;
  }

  if (error && !page) {
    return (
      <div role="alert" className="rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-5 text-[var(--admin-danger)]">
        <p className="font-bold">{error}</p>
        <button type="button" onClick={onReload} className="mt-3 inline-flex min-h-11 items-center gap-2 rounded-xl border border-current px-4 text-sm font-black focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current">
          <RefreshCw aria-hidden="true" size={16} /> إعادة المحاولة
        </button>
      </div>
    );
  }

  const campaigns = page?.items ?? [];
  const totalPages = page ? Math.max(1, Math.ceil(page.total / page.pageSize)) : 1;

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="font-black text-[var(--admin-text)]">سجل الحملات</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">الإيقاف أو الإلغاء يوقف الرسائل غير المحجوزة فقط؛ الرسالة التي قبلتها Meta لا يمكن استرجاعها.</p>
        </div>
        <button type="button" onClick={onReload} disabled={loading} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-60">
          <RefreshCw aria-hidden="true" size={16} className={loading ? 'animate-spin' : ''} /> تحديث الحالات
        </button>
      </div>

      {error ? <p role="alert" className="rounded-xl bg-[var(--admin-warning-10)] p-3 text-sm font-semibold text-[var(--admin-warning)]">{error}</p> : null}

      {campaigns.length === 0 ? (
        <div className="rounded-xl border border-dashed border-[var(--admin-border)] p-8 text-center">
          <BookOpenCheck aria-hidden="true" size={28} className="mx-auto text-[var(--admin-muted)]" />
          <p className="mt-3 font-black text-[var(--admin-text)]">لا توجد حملات بعد</p>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">ابدأ من تبويب «حملة جديدة»؛ ستظهر مراحل التسليم هنا.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {campaigns.map((campaign) => {
            const presentation = statusPresentation[campaign.status];
            const isChanging = changingCampaignId === campaign.id;
            const isPendingHere = pendingAction?.campaign.id === campaign.id;
            return (
              <article key={campaign.id} className="overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
                <div className="grid min-w-0 gap-4 p-4 xl:grid-cols-[minmax(12rem,0.75fr)_minmax(0,1.5fr)_auto] xl:items-center">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h4 className="min-w-0 truncate font-black text-[var(--admin-text)]" title={campaign.name}>{campaign.name}</h4>
                      <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-black ${presentation.className}`}>{presentation.label}</span>
                    </div>
                    <p className="mt-1 truncate text-xs font-semibold text-[var(--admin-muted)]" dir="auto" title={campaign.templateName}>
                      {campaign.templateName} · {campaign.templateLanguage} · {campaign.templateCategory}
                    </p>
                    <p className="mt-1 text-xs font-semibold text-[var(--admin-warning)]">
                      {new Intl.NumberFormat('ar-EG').format(campaign.excludedCount)} مستبعد قبل الإرسال بسبب الموافقة أو صلاحية البيانات
                    </p>
                    <p className="mt-2 flex items-center gap-1.5 text-xs text-[var(--admin-muted)]">
                      <Clock3 aria-hidden="true" size={14} />
                      <time dateTime={campaign.createdAt}>{formatCairoTimestamp(campaign.createdAt)}</time>
                    </p>
                  </div>

                  <dl className="grid grid-cols-3 gap-px overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-border)] sm:grid-cols-4 lg:grid-cols-8">
                    <Metric icon={Send} label="الجمهور" value={campaign.recipientCount} />
                    <Metric icon={Clock3} label="معلّق" value={campaign.pendingCount} />
                    <Metric icon={MessageCircleCheck} label="أُرسل" value={campaign.sentCount} />
                    <Metric icon={CheckCircle2} label="وصل" value={campaign.deliveredCount} />
                    <Metric icon={Eye} label="قُرئ" value={campaign.readCount} />
                    <Metric icon={AlertTriangle} label="فشل" value={campaign.failedCount} danger={campaign.failedCount > 0} />
                    <Metric icon={Ban} label="تخطّى" value={campaign.skippedCount} danger={campaign.skippedCount > 0} />
                    <Metric icon={LoaderCircle} label="غير محسوم" value={campaign.uncertainCount} danger={campaign.uncertainCount > 0} />
                  </dl>

                  <div className="flex flex-wrap gap-2 xl:max-w-52 xl:justify-end">
                    {campaign.status === 'Running' ? (
                      <button type="button" disabled={isChanging} onClick={() => setPendingAction({ campaign, operation: 'pause', reason: '' })} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-warning-20)] px-3 text-sm font-bold text-[var(--admin-warning)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-warning)] disabled:opacity-60">
                        <CirclePause aria-hidden="true" size={16} /> إيقاف مؤقت
                      </button>
                    ) : null}
                    {campaign.status === 'Paused' ? (
                      <button type="button" disabled={isChanging} onClick={() => onOperation(campaign, 'resume')} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-60">
                        <CirclePlay aria-hidden="true" size={16} /> استئناف
                      </button>
                    ) : null}
                    {['Draft', 'Locked', 'Running', 'Paused'].includes(campaign.status) ? (
                      <button type="button" disabled={isChanging} onClick={() => setPendingAction({ campaign, operation: 'cancel', reason: '' })} className="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-bold text-[var(--admin-danger)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)] disabled:opacity-60">
                        <Ban aria-hidden="true" size={16} /> إلغاء
                      </button>
                    ) : null}
                  </div>
                </div>

                {campaign.pauseReason ? <p className="border-t border-[var(--admin-border)] bg-[var(--admin-warning-10)] px-4 py-2 text-xs font-semibold text-[var(--admin-warning)]">سبب الإيقاف: {campaign.pauseReason}</p> : null}

                {isPendingHere ? (
                  <form
                    className="border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4"
                    onSubmit={(event) => {
                      event.preventDefault();
                      if (!pendingAction.reason.trim() || isChanging) return;
                      onOperation(campaign, pendingAction.operation, pendingAction.reason.trim());
                    }}
                  >
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
                      <label className="min-w-0 flex-1">
                        <span className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">
                          سبب {pendingAction.operation === 'pause' ? 'الإيقاف المؤقت' : 'الإلغاء'} للتوثيق
                        </span>
                        <input
                          autoFocus
                          value={pendingAction.reason}
                          onChange={(event) => setPendingAction({ ...pendingAction, reason: event.target.value.slice(0, 500) })}
                          maxLength={500}
                          required
                          placeholder="اكتب سببًا واضحًا يظهر في سجل التدقيق"
                          className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
                        />
                      </label>
                      <div className="flex gap-2">
                        <button type="button" onClick={() => setPendingAction(undefined)} disabled={isChanging} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-60">
                          <X aria-hidden="true" size={16} /> رجوع
                        </button>
                        <button type="submit" disabled={!pendingAction.reason.trim() || isChanging} className={`inline-flex min-h-11 items-center gap-2 rounded-xl px-4 text-sm font-black text-white focus-visible:outline-none focus-visible:ring-2 disabled:cursor-not-allowed disabled:opacity-60 ${pendingAction.operation === 'cancel' ? 'bg-[var(--admin-danger)] focus-visible:ring-[var(--admin-danger)]' : 'bg-[var(--admin-warning)] focus-visible:ring-[var(--admin-warning)]'}`}>
                          {isChanging ? <LoaderCircle aria-hidden="true" size={16} className="animate-spin" /> : null}
                          تأكيد {pendingAction.operation === 'pause' ? 'الإيقاف' : 'الإلغاء'}
                        </button>
                      </div>
                    </div>
                  </form>
                ) : null}
              </article>
            );
          })}
        </div>
      )}

      {page && page.total > 0 ? (
        <nav aria-label="صفحات سجل حملات واتساب" className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--admin-border)] pt-4">
          <p className="text-xs font-semibold text-[var(--admin-muted)]">صفحة {page.page} من {totalPages} · {page.total} حملة</p>
          <div className="flex gap-2" dir="ltr">
            <button type="button" aria-label="الصفحة السابقة" disabled={loading || page.page <= 1} onClick={() => onPageChange(page.page - 1)} className="grid size-11 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-40"><ChevronLeft aria-hidden="true" size={18} /></button>
            <button type="button" aria-label="الصفحة التالية" disabled={loading || page.page >= totalPages} onClick={() => onPageChange(page.page + 1)} className="grid size-11 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-40"><ChevronRight aria-hidden="true" size={18} /></button>
          </div>
        </nav>
      ) : null}
    </div>
  );
}

function Metric({ icon: Icon, label, value, danger = false }: { icon: typeof Send; label: string; value: number; danger?: boolean }) {
  return (
    <div className="min-w-0 bg-[var(--admin-card-soft)] px-2 py-2.5 text-center">
      <dt className={`flex items-center justify-center gap-1 text-[0.68rem] font-bold ${danger ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-muted)]'}`}><Icon aria-hidden="true" size={12} />{label}</dt>
      <dd className={`mt-1 text-sm font-black ${danger ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-text)]'}`}>{new Intl.NumberFormat('ar-EG').format(value)}</dd>
    </div>
  );
}

function HistorySkeleton() {
  return (
    <div aria-label="جارٍ تحميل سجل حملات واتساب" aria-busy="true" className="space-y-3">
      {Array.from({ length: 3 }, (_, index) => <div key={index} className="h-36 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" />)}
    </div>
  );
}
