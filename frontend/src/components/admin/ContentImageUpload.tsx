'use client';

import { ImageIcon, Upload } from 'lucide-react';
import { useRef, useState } from 'react';
import toast from 'react-hot-toast';

import { adminService, type ContentImageType } from '@/services/admin-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

interface ContentImageUploadProps {
  entityId: string;
  contentType: ContentImageType;
  imageUrl?: string | null;
  label: string;
  onUploaded: (imageUrl: string) => void;
}

export function ContentImageUpload({
  entityId,
  contentType,
  imageUrl,
  label,
  onUploaded,
}: ContentImageUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);

  async function uploadSelectedImage(file?: File) {
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      toast.error('اختر ملف صورة صالحًا.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      toast.error('حجم الصورة يجب ألا يتجاوز 10 ميجابايت.');
      return;
    }

    try {
      setUploading(true);
      const uploadedImageUrl = await adminService.uploadContentImage(contentType, entityId, file);
      onUploaded(uploadedImageUrl);
      toast.success('تم تحويل الصورة إلى WebP وحفظها بنجاح.');
    } catch {
      toast.error('تعذر رفع الصورة. تأكد أن الملف صورة سليمة.');
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  return (
    <section className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <div className="relative aspect-[21/8] bg-[var(--admin-card-strong)]">
        {imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={resolveMediaUrl(imageUrl)}
            alt={label}
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-2 text-[var(--admin-muted)]">
            <ImageIcon className="h-8 w-8" />
            <span className="text-sm font-bold">لا توجد صورة</span>
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 p-4">
        <div>
          <h3 className="font-black text-[var(--admin-text)]">{label}</h3>
          <p className="mt-1 text-xs text-[var(--admin-muted)]">
            يتم التحويل تلقائيًا إلى WebP باسم عشوائي. الحد الأقصى 10 ميجابايت.
          </p>
        </div>
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={(event) => void uploadSelectedImage(event.target.files?.[0])}
        />
        <button
          type="button"
          disabled={uploading}
          onClick={() => inputRef.current?.click()}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:cursor-not-allowed disabled:opacity-60"
        >
          <Upload className="h-4 w-4" />
          {uploading ? 'جاري التحويل...' : imageUrl ? 'تغيير الصورة' : 'رفع صورة'}
        </button>
      </div>
    </section>
  );
}
