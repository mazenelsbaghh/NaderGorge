'use client';

import Image from 'next/image';
import { useEffect, useState } from 'react';
import { AudioLines, Check, CheckCheck, ImageIcon, LoaderCircle } from 'lucide-react';
import { liveSupportService, type LiveSupportMessage } from '@/services/live-support-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

interface LiveSupportMessageContentProps {
  message: LiveSupportMessage;
  audience: 'participant' | 'staff';
}

export function LiveSupportMessageMeta({ message, audience }: LiveSupportMessageContentProps) {
  const outgoing = audience === 'staff'
    ? ['Staff', 'Admin'].includes(message.senderType)
    : ['Student', 'Guest'].includes(message.senderType);
  const label = message.readAt ? 'تمت القراءة' : message.deliveredAt ? 'تم الوصول' : 'تم الإرسال';
  return <span className="mt-1 flex items-center justify-end gap-1 text-[10px] opacity-75" dir="rtl">
    {message.editedAt && !message.deletedAt ? <span>معدّلة</span> : null}
    <time dateTime={message.sentAt}>{formatCairoDateTime(message.sentAt, { hour: '2-digit', minute: '2-digit' })}</time>
    {outgoing && (message.readAt ? <CheckCheck size={14} className="text-sky-400" aria-label={label}/> : message.deliveredAt ? <CheckCheck size={14} aria-label={label}/> : <Check size={14} aria-label={label}/>)}
  </span>;
}

export function LiveSupportMessageContent({ message, audience }: LiveSupportMessageContentProps) {
  const [attachmentUrl, setAttachmentUrl] = useState<string>();
  const [attachmentFailed, setAttachmentFailed] = useState(false);

  useEffect(() => {
    if (!['Image', 'Audio'].includes(message.type) || !message.attachmentId) {
      setAttachmentUrl(undefined);
      return;
    }
    let active = true;
    let objectUrl: string | undefined;
    setAttachmentUrl(undefined);
    setAttachmentFailed(false);
    void liveSupportService.getAttachmentBlob(audience, message.conversationId, message.attachmentId)
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
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [audience, message.attachmentId, message.conversationId, message.type]);

  if (message.deletedAt) return <span className="italic opacity-75">تم حذف الرسالة</span>;

  if (['Image', 'Audio'].includes(message.type) && message.attachmentId) {
    if (attachmentFailed) {
      return <span className="inline-flex items-center gap-2">{message.type === 'Audio' ? <AudioLines size={17}/> : <ImageIcon size={17}/>}تعذر تحميل المرفق</span>;
    }
    if (!attachmentUrl) {
      return <span role="status" className="inline-flex items-center gap-2"><LoaderCircle className="animate-spin" size={17}/>جارٍ تحميل المرفق…</span>;
    }
    if (message.type === 'Audio') {
      return <div className="flex min-w-0 max-w-full flex-col gap-1.5" dir="rtl">
        <audio controls preload="metadata" src={attachmentUrl} className="h-10 max-w-full" aria-label="تشغيل التسجيل الصوتي" />
        <span className="max-w-full truncate text-xs opacity-80">{message.content || 'تسجيل صوتي'}</span>
      </div>;
    }
    return (
      <a href={attachmentUrl} target="_blank" rel="noreferrer noopener" className="block" aria-label="فتح الصورة بالحجم الكامل">
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

function linkifyMessage(content: string) {
  const urlPattern = /(https?:\/\/[^\s<]+)/gu;
  return content.split(urlPattern).map((part, index) => {
    if (!/^https?:\/\//u.test(part)) return part;
    return <a key={`${part}-${index}`} href={part} target="_blank" rel="noreferrer noopener" className="font-bold underline underline-offset-2" onClick={(event) => event.stopPropagation()}>{part}</a>;
  });
}
