export const LIVE_SUPPORT_CHANNELS = ['Web', 'WhatsApp', 'Messenger'] as const;

export type LiveSupportChannel = (typeof LIVE_SUPPORT_CHANNELS)[number];

export interface LiveSupportChannelSource {
  channel?: LiveSupportChannel | null;
  canSend?: boolean;
  externalPhoneNumber?: string | null;
  externalPageId?: string | null;
  externalPageName?: string | null;
  customerServiceWindowExpiresAt?: string | null;
}

export interface LiveSupportChannelPresentation {
  channel: LiveSupportChannel;
  label: string;
  detail: string;
}

export interface LiveSupportChannelCapabilities {
  channel: LiveSupportChannel;
  usesExternalThread: boolean;
  isHumanOnly: boolean;
  supportsMessageReply: boolean;
  supportsMessageMutation: boolean;
  supportsParticipantTypingPreview: boolean;
  supportsTemplates: boolean;
  supportsAttachments: boolean;
  requiresCustomerServiceWindow: boolean;
  customerServiceWindowOpen: boolean | null;
  canSendFreeform: boolean;
  canSendTemplate: boolean;
  canSendAttachments: boolean;
}

export const LIVE_SUPPORT_CHANNEL_PRESENTATION: Readonly<
  Record<LiveSupportChannel, Readonly<{ label: string; detail: string }>>
> = Object.freeze({
  Web: Object.freeze({ label: 'الموقع', detail: 'محادثة داخل الموقع' }),
  WhatsApp: Object.freeze({ label: 'واتساب', detail: 'محادثة واتساب' }),
  Messenger: Object.freeze({ label: 'ماسنجر', detail: 'محادثة صفحة فيسبوك' }),
});

type StaticChannelCapabilities = Omit<
  LiveSupportChannelCapabilities,
  | 'channel'
  | 'customerServiceWindowOpen'
  | 'canSendFreeform'
  | 'canSendTemplate'
  | 'canSendAttachments'
>;

const CHANNEL_CAPABILITY_FALLBACKS: Readonly<
  Record<LiveSupportChannel, Readonly<StaticChannelCapabilities>>
> = {
  Web: {
    usesExternalThread: false,
    isHumanOnly: false,
    supportsMessageReply: true,
    supportsMessageMutation: true,
    supportsParticipantTypingPreview: true,
    supportsTemplates: false,
    supportsAttachments: true,
    requiresCustomerServiceWindow: false,
  },
  WhatsApp: {
    usesExternalThread: true,
    isHumanOnly: false,
    supportsMessageReply: false,
    supportsMessageMutation: false,
    supportsParticipantTypingPreview: false,
    supportsTemplates: true,
    supportsAttachments: true,
    requiresCustomerServiceWindow: true,
  },
  Messenger: {
    usesExternalThread: true,
    isHumanOnly: true,
    supportsMessageReply: false,
    supportsMessageMutation: false,
    supportsParticipantTypingPreview: false,
    supportsTemplates: false,
    supportsAttachments: false,
    requiresCustomerServiceWindow: true,
  },
};

export function normalizeLiveSupportChannel(
  channel: unknown
): LiveSupportChannel {
  return channel === 'WhatsApp' || channel === 'Messenger' ? channel : 'Web';
}

export function isExternalChannel(channel: unknown): boolean {
  return normalizeLiveSupportChannel(channel) !== 'Web';
}

export function getLiveSupportChannelPresentation(
  source: LiveSupportChannelSource
): LiveSupportChannelPresentation {
  const channel = normalizeLiveSupportChannel(source.channel);
  const fallback = LIVE_SUPPORT_CHANNEL_PRESENTATION[channel];
  const externalDetail = getExternalDetail(source, channel);

  return {
    channel,
    label: fallback.label,
    detail: externalDetail ?? fallback.detail,
  };
}

export function resolveLiveSupportChannelCapabilities(
  source: LiveSupportChannelSource,
  currentTime = Date.now()
): LiveSupportChannelCapabilities {
  const channel = normalizeLiveSupportChannel(source.channel);
  const fallback = CHANNEL_CAPABILITY_FALLBACKS[channel];
  const customerServiceWindowOpen = fallback.requiresCustomerServiceWindow
    ? isWindowOpen(source.customerServiceWindowExpiresAt, currentTime)
    : null;
  const conversationAllowsSending = source.canSend !== false;
  const canSendFreeform =
    conversationAllowsSending && customerServiceWindowOpen !== false;

  return {
    channel,
    ...fallback,
    customerServiceWindowOpen,
    canSendFreeform,
    canSendTemplate: conversationAllowsSending && fallback.supportsTemplates,
    canSendAttachments: canSendFreeform && fallback.supportsAttachments,
  };
}

function cleanOptionalText(
  value: string | null | undefined
): string | undefined {
  const cleaned = value?.trim();
  return cleaned || undefined;
}

function getExternalDetail(
  source: LiveSupportChannelSource,
  channel: LiveSupportChannel
): string | undefined {
  if (channel === 'Messenger')
    return cleanOptionalText(source.externalPageName);
  if (channel === 'WhatsApp')
    return cleanOptionalText(source.externalPhoneNumber);
  return undefined;
}

function isWindowOpen(
  expiresAt: string | null | undefined,
  currentTime: number
): boolean {
  if (!Number.isFinite(currentTime) || !expiresAt) return false;
  const expiration = Date.parse(expiresAt);
  return Number.isFinite(expiration) && expiration > currentTime;
}
