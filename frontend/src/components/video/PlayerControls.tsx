"use client";

import React, { useState, useCallback } from "react";
import { Play, Pause, Volume2, Volume1, VolumeX, Maximize } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

const CustomSlider = ({
  value,
  onChange,
  className,
  chapters
}: {
  value: number;
  onChange: (value: number) => void;
  className?: string;
  chapters?: { id?: string; title?: string; startPercent: number; endPercent: number }[];
}) => {
  const containerRef = React.useRef<HTMLDivElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [localValue, setLocalValue] = useState(value);

  const [hoverPercent, setHoverPercent] = useState<number | null>(null);

  // Sync with prop when not dragging
  React.useEffect(() => {
    if (!isDragging) {
      setLocalValue(value);
    }
  }, [value, isDragging]);

  const snapToChapter = useCallback((percentage: number) => {
    if (!chapters || chapters.length === 0) return percentage;
    const SNAP_THRESHOLD = 1.0; // Snaps if within 1% of chapter start or end
    for (const ch of chapters) {
      if (Math.abs(percentage - ch.startPercent) < SNAP_THRESHOLD) return ch.startPercent;
      if (Math.abs(percentage - ch.endPercent) < SNAP_THRESHOLD) return ch.endPercent;
    }
    return percentage;
  }, [chapters]);

  const updateProgressLocally = useCallback((clientX: number) => {
    if (!containerRef.current) return undefined;
    const rect = containerRef.current.getBoundingClientRect();
    const x = clientX - rect.left;
    let percentage = Math.min(Math.max((x / rect.width) * 100, 0), 100);
    if (chapters) percentage = snapToChapter(percentage);
    setLocalValue(percentage);
    return percentage;
  }, [chapters, snapToChapter]);

  const handlePointerDown = (e: React.PointerEvent) => {
    if (!containerRef.current) return;
    setIsDragging(true);
    containerRef.current.setPointerCapture(e.pointerId);
    updateProgressLocally(e.clientX);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const percentage = Math.min(Math.max((x / rect.width) * 100, 0), 100);
    setHoverPercent(chapters ? snapToChapter(percentage) : percentage);

    if (isDragging) {
      updateProgressLocally(e.clientX);
    }
  };

  const handlePointerLeave = () => setHoverPercent(null);

  const handlePointerUp = (e: React.PointerEvent) => {
    setIsDragging(false);
    if (containerRef.current && containerRef.current.hasPointerCapture(e.pointerId)) {
      containerRef.current.releasePointerCapture(e.pointerId);
    }
    const finalPercent = updateProgressLocally(e.clientX);
    if (finalPercent !== undefined) {
      onChange(finalPercent);
    }
  };

  const displayChapters = React.useMemo(
    () => (chapters && chapters.length > 0
      ? chapters
      : [{ id: '1', startPercent: 0, endPercent: 100 }]),
    [chapters]
  );

  const hoveredChapter = React.useMemo(() => {
    if (hoverPercent === null || !displayChapters) return null;
    return displayChapters.find(ch => hoverPercent >= ch.startPercent && (hoverPercent < ch.endPercent || ch.endPercent === 100));
  }, [hoverPercent, displayChapters]);

  return (
    <div
      ref={containerRef}
      className={cn(
        "relative w-full h-1.5 bg-transparent rounded-full cursor-pointer group/slider hover:h-2 transition-[height] duration-200 flex items-center touch-none",
        className
      )}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerLeave={handlePointerLeave}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
    >
      {/* Tooltip */}
      {hoverPercent !== null && hoveredChapter && hoveredChapter.title && (
        <div 
          className="absolute bottom-full mb-3 transform -translate-x-1/2 p-2 bg-[#111111ee] backdrop-blur-lg rounded-xl border border-[#EBE2D4]/20 shadow-xl whitespace-nowrap z-50 pointer-events-none"
          style={{ left: `${hoverPercent}%` }}
        >
          <div className="text-[#EBE2D4] text-xs font-bold">{hoveredChapter.title}</div>
        </div>
      )}

      {/* Background Track with Chapter Gaps */}
      <div className="absolute inset-0 flex gap-[3px] overflow-hidden rounded-full">
        {displayChapters.map((ch, i) => {
          const widthPercent = ch.endPercent - ch.startPercent;

          // Calculate how much of this specific segment is filled
          let fillPercent = 0;
          if (localValue >= ch.endPercent) {
            fillPercent = 100;
          } else if (localValue > ch.startPercent) {
            fillPercent = ((localValue - ch.startPercent) / (ch.endPercent - ch.startPercent)) * 100;
          }

          return (
            <div
              key={ch.id || i}
              className="h-full bg-white/20 relative overflow-hidden backdrop-blur-sm transition-all duration-300"
              style={{ width: `${widthPercent}%` }}
            >
              {/* Filled progress bar (Beige / Gold) */}
              <div
                className="absolute top-0 left-0 h-full bg-[#EBE2D4] transition-none origin-left shadow-[0_0_10px_rgba(235,226,212,0.5)]"
                style={{ width: `${fillPercent}%` }}
              />
            </div>
          );
        })}
      </div>

      {/* Scrubber Knob (The White Ball) */}
      <div
        className={cn(
          "absolute top-1/2 w-3.5 h-3.5 bg-white rounded-full shadow-[0_0_15px_rgba(255,255,255,1)] transition-transform duration-100 pointer-events-none z-10",
          isDragging ? "scale-125" : "scale-100"
        )}
        style={{ left: `${localValue}%`, transform: 'translate(-50%, -50%)' }}
      />
    </div>
  );
};

interface PlayerControlsProps {
  isPlaying: boolean;
  onTogglePlay: () => void;
  progress: number;
  onSeek: (percent: number) => void;
  volume: number; // 0 to 100
  isMuted: boolean;
  onVolumeChange: (value: number) => void;
  onToggleMute: () => void;
  onToggleFullscreen: () => void;
  durationFormatted: string;
  currentTimeFormatted: string;
  onPlaybackRateChange?: (rate: number) => void;
  visible: boolean;
  provider?: string;
  onControlHover?: (hovering: boolean) => void;
  chapters?: { id?: string; title?: string; startPercent: number; endPercent: number }[];
}

export default function PlayerControls({
  isPlaying,
  onTogglePlay,
  progress,
  onSeek,
  volume,
  isMuted,
  onVolumeChange,
  onToggleMute,
  onToggleFullscreen,
  durationFormatted,
  currentTimeFormatted,
  onPlaybackRateChange,
  visible,
  provider,
  onControlHover,
  chapters
}: PlayerControlsProps) {

  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const setSpeed = (speed: number) => {
    setPlaybackSpeed(speed);
    if (onPlaybackRateChange) onPlaybackRateChange(speed);
  };

  const currentChapter = React.useMemo(() => {
    if (!chapters || chapters.length === 0) return null;
    return chapters.find(ch => progress >= ch.startPercent && progress <= ch.endPercent) || chapters[chapters.length - 1];
  }, [chapters, progress]);

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className="absolute bottom-0 mx-auto max-w-[90%] md:max-w-xl left-0 right-0 p-4 mb-4 bg-[#11111198] backdrop-blur-md rounded-2xl z-[100]"
          initial={{ y: 20, opacity: 0, filter: "blur(10px)" }}
          animate={{ y: 0, opacity: 1, filter: "blur(0px)" }}
          exit={{ y: 20, opacity: 0, filter: "blur(10px)" }}
          transition={{ duration: 0.6, ease: "circInOut", type: "spring" }}
          dir="ltr"
          onClick={(e) => e.stopPropagation()} // Prevent toggling the video player behind
          onMouseEnter={() => { if (onControlHover) onControlHover(true); }}
          onMouseLeave={() => { if (onControlHover) onControlHover(false); }}
        >
          <div className="flex items-center gap-3 mb-3 px-1">
            <span className="text-white text-xs font-medium w-10 text-center shrink-0">
              {currentTimeFormatted}
            </span>
            <CustomSlider
              value={isFinite(progress) ? progress : 0}
              onChange={onSeek}
              className="flex-1"
              chapters={chapters}
            />
            <span className="text-white text-xs font-medium w-10 text-center shrink-0">
              {durationFormatted}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-white flex-1 min-w-0">
              <motion.div whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.9 }}>
                <Button
                  onClick={(e) => { e.stopPropagation(); onTogglePlay(); }}
                  variant="ghost"
                  size="icon"
                  className="text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)] rounded-full"
                >
                  {isPlaying ? (
                    <Pause className="h-5 w-5" fill="currentColor" />
                  ) : (
                    <Play className="h-5 w-5" fill="currentColor" />
                  )}
                </Button>
              </motion.div>

              <div className="flex items-center gap-x-2 w-24 sm:w-32 ml-1 shrink-0">
                <motion.div whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.9 }}>
                  <Button
                    onClick={(e) => { e.stopPropagation(); onToggleMute(); }}
                    variant="ghost"
                    size="icon"
                    className="text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)] rounded-full shrink-0"
                  >
                    {isMuted || volume === 0 ? (
                      <VolumeX className="h-5 w-5" />
                    ) : volume > 50 ? (
                      <Volume2 className="h-5 w-5" />
                    ) : (
                      <Volume1 className="h-5 w-5" />
                    )}
                  </Button>
                </motion.div>

                <div className="w-full">
                  <CustomSlider
                    value={isMuted ? 0 : volume}
                    onChange={onVolumeChange}
                  />
                </div>
              </div>

              {currentChapter ? (
                <div className="flex items-center gap-2 ml-2 sm:ml-4 text-white font-bold text-xs sm:text-sm whitespace-nowrap overflow-hidden min-w-0">
                  <span className="w-2 h-2 rounded-full bg-[#EBE2D4] shadow-[0_0_8px_rgba(235,226,212,0.8)] shrink-0"></span>
                  <span className="truncate min-w-0 leading-relaxed block mask-image-fade">{(currentChapter as any).title || (currentChapter as any).name || 'الفصل الحالي'}</span>
                </div>
              ) : (
                chapters && chapters.length > 0 && (
                  <div className="flex items-center gap-2 ml-2 sm:ml-4 text-white font-bold text-xs shrink-0">
                    <span className="text-red-400">تحميل الفصل...</span>
                  </div>
                )
              )}
            </div>

            <div className="flex items-center gap-1 shrink-0">
              {provider !== 'vk' && (
                <>
                  <div className="hidden sm:flex items-center gap-1 bg-black/20 p-1 rounded-full border border-white/5 mr-2">
                    {[0.5, 1, 1.5, 2].map((speed) => (
                      <motion.div
                        whileHover={{ scale: 1.1 }}
                        whileTap={{ scale: 0.9 }}
                        key={speed}
                      >
                        <Button
                          onClick={(e) => { e.stopPropagation(); setSpeed(speed); }}
                          variant="ghost"
                          className={cn(
                            "text-white hover:bg-[#111111d1] hover:text-white h-7 px-2.5 text-xs rounded-full cursor-pointer",
                            playbackSpeed === speed && "bg-white/20 font-bold"
                          )}
                        >
                          {speed}x
                        </Button>
                      </motion.div>
                    ))}
                  </div>

                  {/* Mobile speed indicator fallback */}
                  <div className="sm:hidden flex items-center justify-center mr-1">
                    <Button
                      onClick={(e) => {
                        e.stopPropagation();
                        const next = playbackSpeed === 1 ? 1.5 : playbackSpeed === 1.5 ? 2 : playbackSpeed === 2 ? 0.5 : 1;
                        setSpeed(next);
                      }}
                      variant="ghost"
                      className="text-white hover:bg-[#111111d1] hover:text-white h-8 px-2 text-xs font-bold rounded-full"
                    >
                      {playbackSpeed}x
                    </Button>
                  </div>
                </>
              )}


              {/* Fullscreen Button */}
              <motion.div whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.9 }}>
                <Button
                  onClick={(e) => { e.stopPropagation(); onToggleFullscreen(); }}
                  variant="ghost"
                  size="icon"
                  className="text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)] rounded-full"
                >
                  <Maximize className="h-5 w-5" />
                </Button>
              </motion.div>
            </div>

          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
