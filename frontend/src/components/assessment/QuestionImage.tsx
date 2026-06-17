'use client';

import Image from 'next/image';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type QuestionImageProps = {
  imageUrl?: string | null;
  alt: string;
};

export function QuestionImage({ imageUrl, alt }: QuestionImageProps) {
  if (!imageUrl) return null;

  return (
    <div className="overflow-hidden rounded-2xl border border-border bg-background/60 p-3">
      <Image
        src={resolveMediaUrl(imageUrl)}
        alt={alt}
        width={960}
        height={540}
        unoptimized
        className="h-auto max-h-[420px] w-full object-contain"
      />
    </div>
  );
}
