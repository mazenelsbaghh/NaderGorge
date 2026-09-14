import {
  Globe2,
  MessageCircle,
  MessagesSquare,
  type LucideIcon,
} from 'lucide-react';

import {
  getLiveSupportChannelPresentation,
  normalizeLiveSupportChannel,
  type LiveSupportChannel,
  type LiveSupportChannelSource,
} from '@/lib/live-support-channel';
import { cn } from '@/lib/utils';

export interface LiveSupportChannelBadgeProps extends Pick<
  LiveSupportChannelSource,
  'channel' | 'externalPageName'
> {
  className?: string;
}

const CHANNEL_ICONS: Record<LiveSupportChannel, LucideIcon> = {
  Web: Globe2,
  WhatsApp: MessageCircle,
  Messenger: MessagesSquare,
};

const CHANNEL_STYLES: Record<LiveSupportChannel, string> = {
  Web: 'bg-[var(--admin-card-strong)] text-[var(--admin-text)]',
  WhatsApp: 'bg-[var(--admin-success-10)] text-[var(--admin-success)]',
  Messenger: 'bg-[var(--admin-accent-soft)] text-[var(--admin-text)]',
};

export function LiveSupportChannelBadge({
  channel,
  externalPageName,
  className,
}: LiveSupportChannelBadgeProps) {
  const normalizedChannel = normalizeLiveSupportChannel(channel);
  const presentation = getLiveSupportChannelPresentation({
    channel,
    externalPageName,
  });
  const Icon = CHANNEL_ICONS[normalizedChannel];
  const pageName =
    normalizedChannel === 'Messenger' ? externalPageName?.trim() : undefined;

  return (
    <span
      className={cn(
        'inline-flex max-w-full items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold leading-5',
        CHANNEL_STYLES[normalizedChannel],
        className
      )}
      dir="rtl"
    >
      <Icon aria-hidden="true" className="size-3.5 shrink-0" />
      <span className="sr-only">قناة الدعم: </span>
      <span>{presentation.label}</span>
      {pageName ? (
        <>
          <span aria-hidden="true" className="text-[var(--admin-muted)]">
            ·
          </span>
          <span className="sr-only">، الصفحة: </span>
          <bdi
            className="max-w-48 truncate font-medium"
            dir="auto"
            title={pageName}
          >
            {pageName}
          </bdi>
        </>
      ) : null}
    </span>
  );
}
