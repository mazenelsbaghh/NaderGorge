'use client';

import React, { useEffect } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { X, Download } from 'lucide-react';
import NextImage from 'next/image';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

interface ImageZoomModalProps {
  isOpen: boolean;
  imageUrl: string;
  title: string;
  onClose: () => void;
}

export function ImageZoomModal({ isOpen, imageUrl, title, onClose }: ImageZoomModalProps) {
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = prevOverflow;
    };
  }, [isOpen, onClose]);

  const handleDownload = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      const resolvedUrl = resolveMediaUrl(imageUrl);
      const response = await fetch(resolvedUrl);
      const blob = await response.blob();
      const blobUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = blobUrl;
      const ext = imageUrl.split('.').pop()?.split('?')[0] || 'webp';
      a.download = `${title.replace(/\s+/g, '_')}_mindmap.${ext}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(blobUrl);
    } catch {
      // Fallback if fetch is blocked by CORS or network error
      const a = document.createElement('a');
      a.href = resolveMediaUrl(imageUrl);
      a.target = '_blank';
      a.download = `${title}_mindmap`;
      a.click();
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
          onClick={onClose}
          className="fixed inset-0 z-[100] flex flex-col items-center justify-center bg-black/90 p-4 backdrop-blur-md"
        >
          {/* Top Actions Bar */}
          <div 
            className="absolute top-4 left-4 right-4 flex items-center justify-between z-[101]" 
            onClick={e => e.stopPropagation()}
          >
            <div className="text-white font-bold text-sm md:text-base max-w-[60%] truncate select-none">
              {title}
            </div>
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={handleDownload}
                className="flex items-center gap-1.5 rounded-xl bg-white/10 hover:bg-white/20 border border-white/10 px-4 py-2 text-xs md:text-sm font-bold text-white transition-all focus:outline-none focus:ring-2 focus:ring-teal-500"
                title="تنزيل الخريطة الذهنية"
              >
                <Download className="w-4 h-4" />
                تنزيل الصورة
              </button>
              <button
                type="button"
                onClick={onClose}
                className="flex items-center justify-center w-10 h-10 rounded-xl bg-white/10 hover:bg-white/20 border border-white/10 text-white transition-all focus:outline-none focus:ring-2 focus:ring-teal-500"
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
            onClick={e => e.stopPropagation()}
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
        </motion.div>
      )}
    </AnimatePresence>
  );
}
