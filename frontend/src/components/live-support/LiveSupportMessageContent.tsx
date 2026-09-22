'use client';

import Image from 'next/image';
import { memo, useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  AudioLines,
  Check,
  CheckCheck,
  Clock3,
  FileText,
  ImageIcon,
  LoaderCircle,
} from 'lucide-react';
import {
  liveSupportService,
  type LiveSupportMessage,
} from '@/services/live-support-service';
import { formatCairoTimestamp } from '@/lib/cairo-time';

interface LiveSupportMessageContentProps {
  message: LiveSupportMessage;
  audience: 'participant' | 'staff';
  staffWhatsAppThreadConversationId?: string;
}

export const LiveSupportMessageMeta = memo(function LiveSupportMessageMeta({
  message,
  audience,
}: LiveSupportMessageContentProps) {
  const externalStatus = message.externalDeliveryStatus?.trim().toLowerCase();
  const externalOutgoing = Boolean(
    externalStatus && externalStatus !== 'received'
  );
  const outgoing =
    externalOutgoing ||
    (audience === 'staff'
      ? ['Staff', 'Admin'].includes(message.senderType)
      : ['Student', 'Guest'].includes(message.senderType));
  const failed = externalStatus === 'failed';
  const pending = externalStatus === 'pending' || externalStatus === 'sending';
  const read = externalStatus === 'read' || Boolean(message.readAt);
  const delivered =
    externalStatus === 'delivered' || Boolean(message.deliveredAt);
  const label = failed
    ? 'فشل الإرسال'
    : pending
      ? 'جارٍ الإرسال'
      : read
        ? 'تمت القراءة'
        : delivered
          ? 'تم الوصول'
          : 'تم الإرسال';

  return (
    <span
      className="mt-1 flex items-center justify-end gap-1 text-sm opacity-75"
      dir="rtl"
    >
      {message.editedAt && !message.deletedAt ? <span>معدّلة</span> : null}
      <time dateTime={message.sentAt}>
        {formatCairoTimestamp(message.sentAt)}
      </time>
      {outgoing ? (
        <span
          className={`inline-flex items-center gap-1 ${failed ? 'text-[var(--admin-danger)]' : ''}`}
          title={label}
        >
          {failed ? (
            <AlertCircle aria-hidden="true" size={14} />
          ) : pending ? (
            <Clock3 aria-hidden="true" size={14} />
          ) : read ? (
            <CheckCheck
              aria-hidden="true"
              size={14}
              className="text-[var(--admin-accent)]"
            />
          ) : delivered ? (
            <CheckCheck aria-hidden="true" size={14} />
          ) : (
            <Check aria-hidden="true" size={14} />
          )}
          <span className={failed || pending ? 'text-xs font-bold' : 'sr-only'}>
            {label}
          </span>
        </span>
      ) : null}
    </span>
  );
});

export const LiveSupportMessageContent = memo(function LiveSupportMessageContent(props: LiveSupportMessageContentProps) {
  const { message } = props;
  if (message.deletedAt) return <span className="italic opacity-75">تم حذف الرسالة</span>;
  if (!message.attachmentId || !['Image', 'Audio', 'Pdf', 'Video'].includes(message.type))
    return <>{linkifyMessage(message.content)}</>;
  return <DeferredAttachment key={message.attachmentId} {...props} />;
});

function DeferredAttachment(props: LiveSupportMessageContentProps) {
  const placeholder = useRef<HTMLDivElement>(null);
  const [requested, setRequested] = useState(false);
  const isPdf = props.message.type === 'Pdf';
  useEffect(() => {
    if (isPdf || requested || !placeholder.current) return;
    if (typeof IntersectionObserver === 'undefined') {
      setRequested(true);
      return;
    }
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        setRequested(true);
        observer.disconnect();
      }
    }, { rootMargin: '200px' });
    observer.observe(placeholder.current);
    return () => observer.disconnect();
  }, [isPdf, requested]);
  return <div ref={placeholder} className={props.message.type === 'Image' ? 'min-h-24' : undefined}>
    {requested ? <LoadedAttachment {...props} /> : <button type="button" onClick={() => setRequested(true)} className="inline-flex min-h-11 items-center gap-2 underline">
      {attachmentIcon(props.message.type)}{isPdf ? `تحميل PDF: ${props.message.content || 'ملف مرفق'}` : 'تحميل المرفق'}
    </button>}
  </div>;
}

function LoadedAttachment({
  message,
  audience,
  staffWhatsAppThreadConversationId,
}: LiveSupportMessageContentProps) {
  const [attachmentUrl, setAttachmentUrl] = useState<string>();
  const [attachmentFailed, setAttachmentFailed] = useState(false);

  useEffect(() => {
    if (
      !['Image', 'Audio', 'Pdf', 'Video'].includes(message.type) ||
      !message.attachmentId
    ) {
      setAttachmentUrl(undefined);
      return;
    }
    let active = true;
    const controller = new AbortController();
    let objectUrl: string | undefined;
    setAttachmentUrl(undefined);
    setAttachmentFailed(false);
    const attachmentRequest = staffWhatsAppThreadConversationId
      ? liveSupportService.getStaffWhatsAppThreadAttachmentBlob(
          staffWhatsAppThreadConversationId,
          message.attachmentId,
          controller.signal,
        )
      : liveSupportService.getAttachmentBlob(
          audience,
          message.conversationId,
          message.attachmentId,
          controller.signal,
        );
    void attachmentRequest
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setAttachmentUrl(objectUrl);
      })
      .catch(() => {
        if (active) setAttachmentFailed(true);
      });
    return () => {
      active = false;
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [
    audience,
    message.attachmentId,
    message.conversationId,
    message.type,
    staffWhatsAppThreadConversationId,
  ]);

  if (message.deletedAt)
    return <span className="italic opacity-75">تم حذف الرسالة</span>;

  if (
    ['Image', 'Audio', 'Pdf', 'Video'].includes(message.type) &&
    message.attachmentId
  ) {
    if (attachmentFailed) {
      return (
        <span className="inline-flex items-center gap-2">
          {attachmentIcon(message.type)}تعذر تحميل المرفق
        </span>
      );
    }
    if (!attachmentUrl) {
      return (
        <span role="status" className="inline-flex items-center gap-2">
          <LoaderCircle className="animate-spin" size={17} />
          جارٍ تحميل المرفق…
        </span>
      );
    }
    if (message.type === 'Video') {
      return <div className="min-w-0 max-w-full">
        <video controls playsInline preload="metadata" src={attachmentUrl} className="max-h-96 w-full rounded-xl" aria-label="فيديو واتساب" />
        <a href={attachmentUrl} target="_blank" rel="noopener noreferrer" className="underline">فتح الفيديو</a>
      </div>;
    }
    if (message.type === 'Audio') {
      return (
        <div className="flex min-w-0 max-w-full flex-col gap-1.5" dir="rtl">
          <audio
            controls
            preload="metadata"
            src={attachmentUrl}
            className="h-10 max-w-full"
            aria-label="تشغيل التسجيل الصوتي"
          />
          <span className="max-w-full truncate text-xs opacity-80">
            {message.content || 'تسجيل صوتي'}
          </span>
        </div>
      );
    }
    if (message.type === 'Pdf') {
      return (
        <a
          href={attachmentUrl}
          target="_blank"
          rel="noreferrer noopener"
          className="flex min-w-0 items-center gap-3 rounded-xl border border-current/20 bg-[var(--admin-card-soft)] p-3 text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"
          aria-label={`فتح ملف PDF: ${message.content || 'ملف مرفق'}`}
        >
          <FileText aria-hidden="true" size={22} className="shrink-0" />
          <span className="min-w-0">
            <strong className="block truncate text-sm">
              {message.content || 'ملف PDF مرفق'}
            </strong>
            <span className="mt-0.5 block text-xs opacity-75">فتح الملف</span>
          </span>
        </a>
      );
    }
    return (
      <a
        href={attachmentUrl}
        target="_blank"
        rel="noreferrer noopener"
        className="block"
        aria-label="فتح الصورة بالحجم الكامل"
      >
        <Image
          src={attachmentUrl}
          alt={message.content || 'صورة مرفقة'}
          width={640}
          height={480}
          unoptimized
          className="max-h-72 w-auto max-w-full rounded-xl object-contain"
        />
      </a>
    );
  }

  return <>{linkifyMessage(message.content)}</>;
}

function attachmentIcon(type: LiveSupportMessage['type']) {
  if (type === 'Audio') return <AudioLines aria-hidden="true" size={17} />;
  if (type === 'Pdf') return <FileText aria-hidden="true" size={17} />;
  return <ImageIcon aria-hidden="true" size={17} />;
}

function linkifyMessage(content: string) {
  const urlPattern = /(https?:\/\/[^\s<]+)/gu;
  return content.split(urlPattern).map((part, index) => {
    if (!/^https?:\/\//u.test(part)) return part;
    return (
      <a
        key={`${part}-${index}`}
        href={part}
        target="_blank"
        rel="noreferrer noopener"
        className="font-bold underline underline-offset-2"
        onClick={(event) => event.stopPropagation()}
      >
        {part}
      </a>
    );
  });
}
