"use client";

import React, { useState, useCallback } from "react";
import { Play, Pause, Volume2, Volume1, VolumeX, Maximize, Settings2 } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

const CustomSlider = ({
  value,
  onChange,
  className,
  chapters,
  keyboardStepPercent,
  ariaLabel = "شريط التقدم",
}: {
  value: number;
  onChange: (value: number) => void;
  className?: string;
  chapters?: { id?: string; title?: string; startPercent: number; endPercent: number }[];
  keyboardStepPercent?: number;
  ariaLabel?: string;
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

  const pointerPercentage = useCallback((clientX: number, clientY: number) => {
    const slider = containerRef.current;
    if (!slider) return 0;
    const rect = slider.getBoundingClientRect();
    const rotated = Boolean(slider.closest('.secure-video-force-landscape')) && window.matchMedia('(orientation: portrait)').matches;
    const distance = rotated ? clientY - rect.top : clientX - rect.left;
    const length = rotated ? rect.height : rect.width;
    return length > 0 ? Math.min(Math.max(distance / length * 100, 0), 100) : 0;
  }, []);

  const updateProgressLocally = useCallback((clientX: number, clientY: number) => {
    if (!containerRef.current) return undefined;
    let percentage = pointerPercentage(clientX, clientY);
    if (chapters) percentage = snapToChapter(percentage);
    setLocalValue(percentage);
    return percentage;
  }, [chapters, snapToChapter, pointerPercentage]);

  const handlePointerDown = (e: React.PointerEvent) => {
    if (!containerRef.current) return;
    setIsDragging(true);
    containerRef.current.setPointerCapture(e.pointerId);
    updateProgressLocally(e.clientX, e.clientY);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    if (!containerRef.current) return;
    const percentage = pointerPercentage(e.clientX, e.clientY);
    setHoverPercent(chapters ? snapToChapter(percentage) : percentage);

    if (isDragging) {
      updateProgressLocally(e.clientX, e.clientY);
    }
  };

  const handlePointerLeave = () => setHoverPercent(null);

  const handlePointerUp = (e: React.PointerEvent) => {
    if (!containerRef.current?.hasPointerCapture(e.pointerId)) return;
    setIsDragging(false);
    containerRef.current.releasePointerCapture(e.pointerId);
    const finalPercent = updateProgressLocally(e.clientX, e.clientY);
    if (finalPercent !== undefined) {
      onChange(finalPercent);
    }
  };

  const handlePointerCancel = (e: React.PointerEvent) => {
    setIsDragging(false);
    setHoverPercent(null);
    setLocalValue(value);
    if (containerRef.current?.hasPointerCapture(e.pointerId)) {
      containerRef.current.releasePointerCapture(e.pointerId);
    }
  };

  const commitValue = useCallback((nextValue: number) => {
    const clamped = Math.min(Math.max(nextValue, 0), 100);
    const snapped = chapters ? snapToChapter(clamped) : clamped;
    setLocalValue(snapped);
    onChange(snapped);
  }, [chapters, onChange, snapToChapter]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    const step = keyboardStepPercent ?? (e.shiftKey ? 10 : 5);
    let nextValue: number | null = null;

    switch (e.key) {
      case "ArrowRight":
      case "ArrowUp":
        nextValue = localValue + step;
        break;
      case "ArrowLeft":
      case "ArrowDown":
        nextValue = localValue - step;
        break;
      case "PageUp":
        nextValue = localValue + 10;
        break;
      case "PageDown":
        nextValue = localValue - 10;
        break;
      case "Home":
        nextValue = 0;
        break;
      case "End":
        nextValue = 100;
        break;
      default:
        return;
    }

    e.preventDefault();
    e.stopPropagation();
    commitValue(nextValue);
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
      role="slider"
      tabIndex={0}
      aria-label={ariaLabel}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(localValue)}
      aria-valuetext={`${Math.round(localValue)}%`}
      className={cn(
        "relative flex h-11 w-full cursor-pointer touch-none items-center rounded-full bg-transparent focus-visible:ring-2 focus-visible:ring-[var(--secondary)] focus-visible:ring-offset-2 focus-visible:ring-offset-black",
        className
      )}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerLeave={handlePointerLeave}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerCancel}
      onKeyDown={handleKeyDown}
    >
      {/* Tooltip */}
      {hoverPercent !== null && hoveredChapter && hoveredChapter.title && (
        <div 
          className="absolute bottom-full left-0 right-0 z-50 mb-1 rounded-lg bg-black/90 p-2 text-center [overflow-wrap:anywhere] pointer-events-none"
        >
          <div className="text-[#EBE2D4] text-xs font-bold">{hoveredChapter.title}</div>
        </div>
      )}

      {/* Background Track with Chapter Gaps */}
      <div className="absolute inset-x-0 top-1/2 flex h-2 -translate-y-1/2 gap-[3px] overflow-hidden rounded-full">
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
              className="h-full bg-white/20 relative overflow-hidden backdrop-blur-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300"
              style={{ width: `${widthPercent}%` }}
            >
              {/* Filled progress bar */}
              <div
                className="absolute top-0 left-0 h-full bg-[#0E8F8F] transition-none origin-left shadow-[0_0_10px_rgba(14,143,143,0.45)]"
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
  compact?: boolean;
  provider?: string;
  onControlHover?: (hovering: boolean) => void;
  chapters?: { id?: string; title?: string; startPercent: number; endPercent: number }[];
  durationSeconds?: number;
  qualityLevels?: { id: string; label: string; height?: number; bitrate?: number }[];
  currentQuality?: string;
  onHide?: () => void;
  onQualityChange?: (quality: string) => void;
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
  compact = false,
  provider,
  onControlHover,
  chapters,
  durationSeconds,
  qualityLevels = [],
  currentQuality = 'auto',
  onQualityChange,
  onHide,
}: PlayerControlsProps) {

  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [qualityMenuOpen, setQualityMenuOpen] = useState(false);
  const setSpeed = (speed: number) => {
    setPlaybackSpeed(speed);
    if (onPlaybackRateChange) onPlaybackRateChange(speed);
  };

  const currentChapter = React.useMemo(() => {
    if (!chapters || chapters.length === 0) return null;
    return chapters.find(ch => progress >= ch.startPercent && progress <= ch.endPercent) || chapters[chapters.length - 1];
  }, [chapters, progress]);

  if (compact) {
    if (!visible) return null;
    const seekBy = (seconds: number) => {
      if (durationSeconds && Number.isFinite(durationSeconds) && durationSeconds > 0) {
        onSeek(Math.max(0, Math.min(100, progress + seconds / durationSeconds * 100)));
      }
    };
    const action = "flex size-11 shrink-0 items-center justify-center rounded-full text-white hover:bg-white/15 focus-visible:outline focus-visible:outline-2 focus-visible:outline-white";
    return <div className="pointer-events-none absolute inset-0 z-[var(--z-modal)]" dir="ltr">
      <div className="pointer-events-auto absolute left-1/2 top-1/2 flex -translate-x-1/2 -translate-y-1/2 items-center gap-5" onClick={e => e.stopPropagation()}>
        <button type="button" className={`${action} bg-black/65`} aria-label="ترجيع 10 ثوانٍ" disabled={!durationSeconds} onClick={() => seekBy(-10)}>↶10</button>
        <button type="button" className={`${action} size-14 bg-black/65`} aria-label={isPlaying ? 'إيقاف الفيديو مؤقتًا' : 'تشغيل الفيديو'} onClick={onTogglePlay}>{isPlaying ? <Pause className="size-6" /> : <Play className="size-6" fill="currentColor" />}</button>
        <button type="button" className={`${action} bg-black/65`} aria-label="تقديم 10 ثوانٍ" disabled={!durationSeconds} onClick={() => seekBy(10)}>10↷</button>
      </div>
      {onHide && <button type="button" className={`pointer-events-auto absolute left-2 top-2 ${action} bg-black/65`} aria-label="إخفاء عناصر التحكم" onClick={e => { e.stopPropagation(); onHide(); }}>×</button>}
      <div className="secure-player-controls pointer-events-auto absolute inset-x-0 bottom-0 bg-black/80 px-2 text-white" onClick={e => e.stopPropagation()}>
        <div className="flex min-w-0 items-center gap-2 text-[11px] tabular-nums">
          <span className="shrink-0">{currentTimeFormatted}</span>
          <CustomSlider value={Number.isFinite(progress) ? progress : 0} onChange={onSeek} className="min-w-0 flex-1" chapters={chapters} ariaLabel="تقدم الفيديو" keyboardStepPercent={durationSeconds ? 1000 / durationSeconds : undefined} />
          <span className="shrink-0">{durationFormatted}</span>
        </div>
        <div className="flex h-11 items-center justify-end gap-1">
          <button type="button" className={action} aria-label={isMuted ? 'تشغيل الصوت' : 'كتم الصوت'} onClick={onToggleMute}>{isMuted ? <VolumeX className="size-4" /> : <Volume2 className="size-4" />}</button>
          {provider !== 'vk' && <select aria-label="سرعة التشغيل" value={playbackSpeed} onChange={e => setSpeed(Number(e.target.value))} className="h-11 w-16 bg-black text-xs text-white">{[0.5, 1, 1.5, 2].map(rate => <option key={rate} value={rate}>{rate}x</option>)}</select>}
          {qualityLevels.length > 0 && onQualityChange && <select aria-label="جودة الفيديو" value={currentQuality} onChange={e => onQualityChange(e.target.value)} className="h-11 max-w-24 bg-black text-xs text-white">{[{ id: 'auto', label: 'تلقائي' }, ...qualityLevels.filter(level => level.id !== 'auto')].map(level => <option key={level.id} value={level.id}>{level.label}</option>)}</select>}
          <button type="button" className={action} aria-label="ملء الشاشة" onClick={onToggleFullscreen}><Maximize className="size-4" /></button>
        </div>
      </div>
    </div>;
  }

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className={cn(
            "secure-player-controls absolute bottom-0 left-0 right-0 z-[var(--z-modal)] mx-auto bg-black/80",
            compact
              ? "mb-0 max-w-full rounded-none px-2 py-1 sm:mb-2 sm:max-w-2xl sm:rounded-xl sm:px-3 sm:py-2"
              : "mb-4 max-w-[90%] rounded-2xl p-4 md:max-w-xl"
          )}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
          dir="ltr"
          onClick={(e) => e.stopPropagation()} // Prevent toggling the video player behind
          onMouseEnter={() => { if (onControlHover) onControlHover(true); }}
          onMouseLeave={() => { if (onControlHover) onControlHover(false); }}
        >
          <div className="flex items-center gap-2 px-1">
            <span className="min-w-9 shrink-0 text-center text-xs font-medium tabular-nums text-white">
              {currentTimeFormatted}
            </span>
            <CustomSlider
              value={isFinite(progress) ? progress : 0}
              onChange={onSeek}
              className="flex-1"
              chapters={chapters}
              keyboardStepPercent={durationSeconds && durationSeconds > 0 ? (10 / durationSeconds) * 100 : undefined}
              ariaLabel="تقدم الفيديو"
            />
            <span className="min-w-9 shrink-0 text-center text-xs font-medium tabular-nums text-white">
              {durationFormatted}
            </span>
          </div>

          <div className={cn("flex items-center justify-between", compact ? "gap-1" : "gap-2")}>
            <div className={cn("flex min-w-0 flex-1 items-center text-white", compact ? "gap-1" : "gap-2")}>
              <motion.div whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.9 }}>
                <Button
                  onClick={(e) => { e.stopPropagation(); onTogglePlay(); }}
                  variant="ghost"
                  size="icon"
                  aria-label={isPlaying ? "إيقاف الفيديو مؤقتًا" : "تشغيل الفيديو"}
                  aria-pressed={isPlaying}
                  className={cn("rounded-full text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)]", compact && "size-11")}
                >
                  {isPlaying ? (
                    <Pause className={cn(compact ? "size-4" : "h-5 w-5")} fill="currentColor" />
                  ) : (
                    <Play className={cn(compact ? "size-4" : "h-5 w-5")} fill="currentColor" />
                  )}
                </Button>
              </motion.div>

              <div className="flex shrink-0 items-center gap-1 sm:w-28">
                <motion.div whileHover={{ scale: 1.1 }} whileTap={{ scale: 0.9 }}>
                  <Button
                    onClick={(e) => { e.stopPropagation(); onToggleMute(); }}
                    variant="ghost"
                    size="icon"
                    aria-label={isMuted || volume === 0 ? "تشغيل الصوت" : "كتم الصوت"}
                    aria-pressed={isMuted || volume === 0}
                    className={cn("shrink-0 rounded-full text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)]", compact && "size-11")}
                  >
                    {isMuted || volume === 0 ? (
                      <VolumeX className={cn(compact ? "size-4" : "h-5 w-5")} />
                    ) : volume > 50 ? (
                      <Volume2 className={cn(compact ? "size-4" : "h-5 w-5")} />
                    ) : (
                      <Volume1 className={cn(compact ? "size-4" : "h-5 w-5")} />
                    )}
                  </Button>
                </motion.div>

                <div className="hidden w-full sm:block">
                  <CustomSlider
                    value={isMuted ? 0 : volume}
                    onChange={onVolumeChange}
                    ariaLabel="مستوى الصوت"
                  />
                </div>
              </div>

              {currentChapter ? (
                <div className="hidden sm:flex items-center gap-2 ml-2 text-white font-bold text-xs whitespace-nowrap overflow-hidden min-w-0">
                  <span className="w-2 h-2 rounded-full bg-[#0E8F8F] shadow-[0_0_8px_rgba(14,143,143,0.75)] shrink-0"></span>
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

            <div className="relative flex items-center gap-1 shrink-0">
              {qualityLevels.length > 0 && onQualityChange && (
                <div className="relative">
                  <Button
                    type="button"
                    onClick={(event) => { event.stopPropagation(); setQualityMenuOpen((open) => !open); }}
                    variant="ghost"
                    aria-label="اختيار جودة الفيديو"
                    aria-haspopup="listbox"
                    aria-expanded={qualityMenuOpen}
                    className="min-h-11 min-w-11 rounded-full px-2 text-xs font-bold text-white hover:bg-[#111111d1] hover:text-white"
                  >
                    <Settings2 className="size-4 sm:me-1" />
                    <span className="hidden sm:inline">{currentQuality === 'auto' ? 'تلقائي' : qualityLevels.find((level) => level.id === currentQuality)?.label ?? 'الجودة'}</span>
                  </Button>
                  {qualityMenuOpen && (
                    <div role="listbox" aria-label="جودة الفيديو" className="absolute bottom-full right-0 z-50 mb-2 min-w-32 overflow-hidden rounded-xl border border-white/15 bg-[#111]/95 p-1.5 text-right shadow-2xl backdrop-blur-xl">
                      {[{ id: 'auto', label: 'تلقائي' }, ...qualityLevels].map((level) => (
                        <button key={level.id} type="button" role="option" aria-selected={currentQuality === level.id} onClick={(event) => { event.stopPropagation(); onQualityChange(level.id); setQualityMenuOpen(false); }} className={cn("flex min-h-11 w-full items-center justify-between rounded-lg px-3 text-sm font-bold text-white hover:bg-white/10", currentQuality === level.id && "bg-white/15 text-[#57d4d4]")}>
                          <span>{level.label}</span><span aria-hidden>{currentQuality === level.id ? '✓' : ''}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
              {provider !== 'vk' && (
                <>
                  <div
                    className="hidden sm:flex items-center gap-1 bg-black/20 p-1 rounded-full border border-white/5 mr-2"
                    role="group"
                    aria-label="سرعة تشغيل الفيديو"
                  >
                    {[0.5, 1, 1.5, 2].map((speed) => (
                      <motion.div
                        whileHover={{ scale: 1.1 }}
                        whileTap={{ scale: 0.9 }}
                        key={speed}
                      >
                        <Button
                          onClick={(e) => { e.stopPropagation(); setSpeed(speed); }}
                          variant="ghost"
                          aria-label={`ضبط سرعة التشغيل على ${speed}x`}
                          aria-pressed={playbackSpeed === speed}
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
                      aria-label={`سرعة التشغيل الحالية ${playbackSpeed}x. اضغط لتغيير السرعة`}
                      className={cn("min-h-11 min-w-11 rounded-full px-2 text-xs font-bold text-white hover:bg-[#111111d1] hover:text-white", compact && "text-sm")}
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
                  aria-label="تبديل وضع ملء الشاشة"
                  className={cn("rounded-full text-white hover:bg-[#111111d1] hover:text-[var(--admin-primary)]", compact && "size-11")}
                >
                  <Maximize className={cn(compact ? "size-4" : "h-5 w-5")} />
                </Button>
              </motion.div>
            </div>

          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
