'use client';

/* eslint-disable @next/next/no-img-element */

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ImageIcon, X } from 'lucide-react';
import type { VideoChapterDto } from '@/services/content-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

interface LessonMindmapDisplayProps {
  chapters: VideoChapterDto[];
  currentTime: number;
}

export function LessonMindmapDisplay({ chapters, currentTime }: LessonMindmapDisplayProps) {
  const [dismissedChapterId, setDismissedChapterId] = useState<string | null>(null);
  const [expandedChapterId, setExpandedChapterId] = useState<string | null>(null);
  const currentChapter = chapters?.find(
    c => currentTime >= c.startTime && currentTime <= c.endTime
  );

  if (!chapters || chapters.length === 0) return null;

  if (!currentChapter || !currentChapter.mindmapImageUrl) {
    return null;
  }

  const imageUrl = resolveMediaUrl(currentChapter.mindmapImageUrl);
  if (!imageUrl) {
    return null;
  }

  const isExpanded = expandedChapterId === currentChapter.id;

  if (dismissedChapterId === currentChapter.id) {
    return (
      <div className="mt-8 flex justify-end">
        <button
          type="button"
          onClick={() => setDismissedChapterId(null)}
          className="inline-flex items-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-sm font-bold uppercase tracking-[0.12em] text-[var(--admin-primary)] shadow-sm transition-colors hover:bg-[var(--admin-card-soft)]"
          aria-label="إظهار الخريطة الذهنية"
          title="إظهار الخريطة الذهنية"
        >
          <span>الخريطة الذهنية</span>
        </button>
      </div>
    );
  }

  return (
    <>
      <AnimatePresence mode="wait">
        <motion.div
          key={currentChapter.id}
          initial={{ opacity: 0, scale: 0.98, y: 10 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.98, y: -10 }}
          transition={{ duration: 0.5, ease: [0.23, 1, 0.32, 1] }}
          className="mt-8 rounded-3xl overflow-hidden shadow-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] relative group"
        >
          <div className="absolute inset-0 bg-gradient-to-t from-[var(--admin-primary-15)] to-transparent pointer-events-none opacity-50" />
          
          <div className="flex items-center justify-between gap-3 p-5 sm:px-8 border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
            <button
              type="button"
              onClick={() => setDismissedChapterId(currentChapter.id)}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-text)]"
              aria-label="إخفاء الخريطة الذهنية"
              title="إخفاء الخريطة"
            >
              <X className="h-4 w-4" />
            </button>
  
            <div className="mr-auto flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] flex items-center justify-center">
                <ImageIcon className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-lg font-black text-[var(--admin-text)]">الخريطة الذهنية للفصل</h3>
                <p className="text-sm text-[var(--admin-muted)] font-medium">{currentChapter.title}</p>
              </div>
            </div>
          </div>
  
          <div className="relative w-full p-4 sm:p-8 flex justify-center bg-[linear-gradient(to_bottom,transparent,var(--admin-card-soft))]">
            <button
              type="button"
              onClick={() => setExpandedChapterId(currentChapter.id)}
              className="block max-w-full cursor-zoom-in"
              aria-label="تكبير الخريطة الذهنية"
              title="تكبير الخريطة"
            >
              <img 
                src={imageUrl} 
                alt={`خريطة ذهنيه لِـ ${currentChapter.title}`} 
                className="max-w-full rounded-2xl shadow-lg border border-[var(--admin-primary)]/20 object-contain hover:scale-105 transition-transform duration-500"
                style={{ maxHeight: '600px' }}
              />
            </button>
          </div>
        </motion.div>
      </AnimatePresence>

      <AnimatePresence>
        {isExpanded && (
          <motion.div
            className="fixed inset-0 z-[120] flex items-center justify-center bg-black/82 p-4 backdrop-blur-md"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setExpandedChapterId(null)}
          >
            <button
              type="button"
              onClick={() => setExpandedChapterId(null)}
              className="absolute right-4 top-4 flex h-11 w-11 items-center justify-center rounded-full border border-white/15 bg-black/45 text-white transition-colors hover:bg-black/65"
              aria-label="إغلاق المعاينة"
            >
              <X className="h-5 w-5" />
            </button>
  
            <motion.img
              src={imageUrl}
              alt={`خريطة ذهنيه لِـ ${currentChapter.title}`}
              className="max-h-[90vh] max-w-[95vw] rounded-3xl object-contain shadow-2xl"
              initial={{ scale: 0.94, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.96, opacity: 0 }}
              transition={{ duration: 0.22 }}
              onClick={(event) => event.stopPropagation()}
            />
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}
