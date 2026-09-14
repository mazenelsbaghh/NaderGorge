'use client';

import React from 'react';
import { motion } from 'framer-motion';
import { X, Download } from 'lucide-react';
import NextImage from 'next/image';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';
import { downloadMindmap } from '@/utils/mindmap-downloads';
import toast from 'react-hot-toast';

interface ImageZoomModalProps {
  isOpen: boolean;
  imageUrl: string;
  title: string;
  onClose: () => void;
}

export function ImageZoomModal({
  isOpen,
  imageUrl,
  title,
  onClose,
}: ImageZoomModalProps) {
  const handleDownload = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await downloadMindmap(imageUrl, `${title}_mindmap`);
    } catch {
      toast.error('تعذر تنزيل الصورة. حاول مرة أخرى.');
    }
  };

  return (
    <AccessibleOverlay
      open={isOpen}
      onClose={onClose}
      label={`عرض الصورة: ${title}`}
      backdropClassName="bg-black/90 backdrop-blur-md"
      className="inset-4 flex flex-col items-center justify-center"
    >
      {/* Top Actions Bar */}
      <div
        className="absolute top-4 start-4 end-4 flex items-center justify-between z-[var(--z-modal-toolbar)]"
        onClick={(e) => e.stopPropagation()}
      >
        <div
          className="max-w-[60%] truncate text-start text-sm font-bold text-white select-none md:text-base"
          dir="auto"
        >
          {title}
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={handleDownload}
            className="flex items-center gap-1.5 rounded-xl bg-white/10 hover:bg-white/20 border border-white/10 px-4 py-2 text-xs md:text-sm font-bold text-white transition-[color,background-color,border-color,opacity,transform,box-shadow] focus:outline-none focus:ring-2 focus:ring-teal-500"
            title="تنزيل الخريطة الذهنية"
          >
            <Download className="w-4 h-4" />
            تنزيل الصورة
          </button>
          <button
            type="button"
            onClick={onClose}
            className="flex items-center justify-center w-10 h-10 rounded-xl bg-white/10 hover:bg-white/20 border border-white/10 text-white transition-[color,background-color,border-color,opacity,transform,box-shadow] focus:outline-none focus:ring-2 focus:ring-teal-500"
            title="إغلاق"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Image Container */}
      <motion.div
        initial={{ scale: 0.95, y: 10 }}
        animate={{ scale: 1, y: 0 }}
        exit={{ scale: 0.95, y: 10 }}
        transition={{ type: 'spring', damping: 25, stiffness: 300 }}
        className="relative max-w-full max-h-[80vh] w-fit h-fit flex items-center justify-center rounded-2xl overflow-hidden shadow-2xl border border-white/10"
      >
        <NextImage
          src={resolveMediaUrl(imageUrl)}
          alt={title}
          width={1600}
          height={900}
          unoptimized
          className="object-contain max-w-full max-h-[80vh] w-auto h-auto select-none"
        />
      </motion.div>
    </AccessibleOverlay>
  );
}
