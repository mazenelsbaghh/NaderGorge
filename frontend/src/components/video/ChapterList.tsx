"use client";

import React, { useRef, useEffect } from "react";
import { motion } from "framer-motion";
import { cn } from "@/lib/utils";

// Assuming VideoChapterDto definition here to avoid breaking if not imported properly
interface VideoChapterDto {
  id: string;
  title: string;
  startTime: number;
  endTime: number;
  summaryText?: string;
  mindmapImageUrl?: string;
}

interface ChapterListProps {
  chapters: VideoChapterDto[];
  currentTime: number;
  onSeek: (time: number) => void;
}

function formatTime(seconds: number) {
  if (isNaN(seconds)) return "0:00";
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s < 10 ? "0" : ""}${s}`;
}

export function ChapterList({ chapters, currentTime, onSeek }: ChapterListProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  const [activeChapterIndex, setActiveChapterIndex] = React.useState<number>(-1);

  // Determine active chapter safely without re-rendering continuously
  useEffect(() => {
    if (!chapters || chapters.length === 0) return;
    const index = chapters.findIndex((chapter, idx) => currentTime >= chapter.startTime && (currentTime < chapter.endTime || (idx === chapters.length - 1 && currentTime <= chapter.endTime)));
    if (index !== -1 && index !== activeChapterIndex) {
      setActiveChapterIndex(index);
    }
  }, [currentTime, chapters, activeChapterIndex]);

  useEffect(() => {
    // Only scroll when the active chapter actually changes
    if (!containerRef.current || activeChapterIndex === -1) return;
    const activeEl = containerRef.current.querySelector('[data-active="true"]') as HTMLElement;
    if (activeEl) {
      // Scroll the container locally rather than hijacking window scroll
      const container = containerRef.current;
      const topPos = activeEl.offsetTop - container.offsetTop - (container.clientHeight / 2) + (activeEl.clientHeight / 2);
      container.scrollTo({ top: Math.max(0, topPos), behavior: 'smooth' });
    }
  }, [activeChapterIndex]);

  if (!chapters || chapters.length === 0) return null;

  return (
    <div className="flex flex-col w-full h-[400px] overflow-hidden bg-[var(--admin-card)] rounded-[24px] border border-[var(--admin-border)] shadow-xl relative">
      <div className="px-6 py-4 border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] flex items-center justify-between z-10 relative">
        <h3 className="font-black text-lg text-[var(--admin-text)]">فصول الفيديو</h3>
        <span className="text-xs font-bold px-2 py-1 bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] rounded-md">
          {chapters.length} فصل
        </span>
      </div>

      <div 
        ref={containerRef}
        className="flex-1 overflow-y-auto w-full p-2 space-y-1 relative"
        dir="rtl"
      >
        {chapters.map((chapter, i) => {
          const isActive = i === activeChapterIndex;
          return (
            <button
              key={`${chapter.id}-${i}`}
              data-active={isActive}
              onClick={() => onSeek(chapter.startTime)}
              className={cn(
                "w-full flex items-center justify-between p-3 rounded-xl transition-all duration-300 group text-right",
                isActive 
                  ? "bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-md" 
                  : "hover:bg-[var(--admin-border)] text-[var(--admin-text)]"
              )}
            >
              <div className="flex items-center gap-3 overflow-hidden">
                <div className={cn(
                  "w-8 h-8 rounded-full flex items-center justify-center shrink-0 transition-colors",
                  isActive ? "bg-white/20" : "bg-[var(--admin-card-soft)] text-[var(--admin-primary)] group-hover:bg-[var(--admin-card)]"
                )}>
                  {isActive ? (
                    <motion.div 
                      key="playing"
                      initial={{ scale: 0.5 }} animate={{ scale: 1 }}
                      className="w-2 h-2 rounded-full bg-white animate-pulse" 
                    />
                  ) : (
                    <span className="text-xs font-bold">{i + 1}</span>
                  )}
                </div>
                
                <div className="flex flex-col overflow-hidden text-right">
                  <span className="font-bold text-sm truncate w-full">{chapter.title}</span>
                  {chapter.summaryText && isActive && (
                    <motion.span 
                      initial={{ opacity: 0, height: 0 }} 
                      animate={{ opacity: 1, height: 'auto' }}
                      className="text-xs w-full mt-1.5 text-white/80 leading-relaxed font-medium"
                    >
                      {chapter.summaryText}
                    </motion.span>
                  )}
                </div>
              </div>
              
              <div className={cn(
                "text-xs font-black shrink-0 px-2 py-1 rounded bg-black/10",
                isActive ? "text-white" : "text-[var(--admin-muted)]"
              )}>
                {formatTime(chapter.startTime)}
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
