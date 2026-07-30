'use client';

import Image from 'next/image';
import { useEffect, useState } from 'react';
import { ImageIcon, LoaderCircle } from 'lucide-react';
import { liveSupportService, type LiveSupportMessage } from '@/services/live-support-service';

interface LiveSupportMessageContentProps {
  message: LiveSupportMessage;
  audience: 'participant' | 'staff';
}

export function LiveSupportMessageContent({ message, audience }: LiveSupportMessageContentProps) {
  const [imageUrl, setImageUrl] = useState<string>();
  const [imageFailed, setImageFailed] = useState(false);

  useEffect(() => {
    if (message.type !== 'Image' || !message.attachmentId) return;
    let active = true;
    let objectUrl: string | undefined;
    setImageFailed(false);
    void liveSupportService.getAttachmentBlob(audience, message.conversationId, message.attachmentId)
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setImageUrl(objectUrl);
      })
      .catch(() => {
        if (active) setImageFailed(true);
      });
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [audience, message.attachmentId, message.conversationId, message.type]);

  if (message.type === 'Image' && message.attachmentId) {
    if (imageFailed) {
      return <span className="inline-flex items-center gap-2"><ImageIcon size={17}/>تعذر عرض الصورة</span>;
    }
    if (!imageUrl) {
      return <span role="status" className="inline-flex items-center gap-2"><LoaderCircle className="animate-spin" size={17}/>جارٍ تحميل الصورة…</span>;
    }
    return (
      <a href={imageUrl} target="_blank" rel="noreferrer noopener" className="block" aria-label="فتح الصورة بالحجم الكامل">
        <Image
          src={imageUrl}
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
