'use client';

import React, { useEffect, useRef, useState, useCallback } from 'react';
import { videoSessionService, type ExtraWatchRequestStatus } from '@/services/video-session-service';
import { AlertCircle, Play, Info, X, Map, ImageIcon } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { InlineLoader, SpinnerLoader } from '@/components/ui/loading-indicator';
import { useAuthStore } from '@/stores/auth-store';
import PlayerControls from './PlayerControls';
import SplitText from '@/components/ui/SplitText';
import { applyDomShields } from '@/utils/dom-shield';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { useRouter, useParams } from 'next/navigation';

export interface WatchStatus {
  current: number;
  max: number;
  isLocked?: boolean;
  viewTracked: boolean;
  displayedWatched: number;
  thresholdSeconds: number;
}

interface SecureVideoPlayerProps {
  lessonVideoId: string;
  isExamLocked?: boolean;
  blockingExamId?: string;
  chapters?: import("@/services/content-service").VideoChapterDto[];
  onWatchProgress?: (secondsWatched: number) => void;
  onWatchStatusChange?: (status: WatchStatus) => void;
  onEnded?: () => void;
  className?: string;
  onSessionError?: (error: string) => void;
}

/**
 * SecureVideoPlayer — Server-Side Embed Approach
 * 
 * Instead of loading the YouTube IFrame API directly (which exposes the video URL
 * in DevTools), we load an iframe pointing to our own `/api/video/embed` route.
 * That route decrypts the video ID server-side and returns an HTML page with YouTube
 * embedded. Communication happens via postMessage.
 * 
 * DevTools shows: <iframe src="/api/video/embed?t=ENCRYPTED..."> (no YouTube URL)
 */
export interface SecureVideoPlayerRef {
  seekTo: (seconds: number) => void;
  play: () => void;
  pause: () => void;
}

const SecureVideoPlayerComponent = React.forwardRef<SecureVideoPlayerRef, SecureVideoPlayerProps>(({ 
  lessonVideoId, 
  isExamLocked = false,
  blockingExamId,
  chapters,
  onWatchProgress,
  onWatchStatusChange,
  onEnded,
  className = '',
  onSessionError
}, ref) => {
  const iframeRef = useRef<HTMLIFrameElement | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const params = useParams();
  const packageId = params?.packageId as string;
  
  React.useImperativeHandle(ref, () => ({
    seekTo: (seconds: number) => {
      sendCommand('seekTo', { time: seconds, seconds, allowSeekAhead: true });
    },
    play: () => sendCommand('play'),
    pause: () => sendCommand('pause')
  }));

  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error' | 'locked'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [watchInfo, setWatchInfo] = useState<{current: number, max: number, isLocked?: boolean} | null>(null);
  const [extraWatchReqStatus, setExtraWatchReqStatus] = useState<ExtraWatchRequestStatus | null>(null);
  const [extraWatchRejectionReason, setExtraWatchRejectionReason] = useState<string | null>(null);
  const [requestingExtra, setRequestingExtra] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [isBuffering, setIsBuffering] = useState(false);
  
  const [showControls, setShowControls] = useState(true);
  const controlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const watchThresholdPercentageRef = useRef<number>(30);

  const [isHoveringControls, setIsHoveringControls] = useState(false);
  const [isChapterInfoOpen, setIsChapterInfoOpen] = useState(false);
  const [isMindmapOpen, setIsMindmapOpen] = useState(false);

  const handlePlayerInteraction = useCallback(() => {
    setShowControls(true);
    if (controlsTimeoutRef.current) {
      clearTimeout(controlsTimeoutRef.current);
    }
    // Only set timeout if we are playing and not actively hovering the controls overlay
    if (isPlaying && !isHoveringControls) {
      controlsTimeoutRef.current = setTimeout(() => {
        setShowControls(false);
      }, 3000);
    }
  }, [isPlaying, isHoveringControls]);

  useEffect(() => {
    if (!isPlaying) {
      setShowControls(true);
      if (controlsTimeoutRef.current) clearTimeout(controlsTimeoutRef.current);
    } else {
      handlePlayerInteraction();
    }
    return () => {
      if (controlsTimeoutRef.current) clearTimeout(controlsTimeoutRef.current);
    };
  }, [isPlaying, handlePlayerInteraction]);

  useEffect(() => {
    if (status === 'locked') {
      videoSessionService.getExtraWatchStatus(lessonVideoId).then(res => {
         if (res.data?.data) {
          setExtraWatchReqStatus(res.data.data.requestStatus ?? null);
          setExtraWatchRejectionReason(res.data.data.rejectionReason ?? null);
         }
      }).catch(() => {});
    }
  }, [status, lessonVideoId]);

  const handleRequestExtra = async () => {
    setRequestingExtra(true);
    try {
      await videoSessionService.requestExtraWatch(lessonVideoId);
      setExtraWatchReqStatus('Pending');
      setExtraWatchRejectionReason(null);
    } catch(err) {
      console.error(err);
    } finally {
      setRequestingExtra(false);
    }
  };

  const [progress, setProgress] = useState(0);
  const [volume, setVolume] = useState(100);
  const [isMuted, setIsMuted] = useState(false);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [provider, setProvider] = useState<string>('youtube');
  const [qualityLevels, setQualityLevels] = useState<string[]>([]);
  const [currentQuality, setCurrentQuality] = useState<string>('auto');
  const { user } = useAuthStore();
  const onWatchProgressRef = useRef(onWatchProgress);
  useEffect(() => { onWatchProgressRef.current = onWatchProgress; }, [onWatchProgress]);

  const formatTime = (seconds: number) => {
    if (!seconds || isNaN(seconds)) return '0:00';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  // ── PostMessage listener ──
  // Receives events from the embedded video page
  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (!msg || msg.source !== 'video-embed') return;

      switch (msg.type) {
        case 'ready':
          setStatus('ready');
          setDuration(msg.data.duration || 0);
          setVolume(msg.data.volume || 100);
          setIsMuted(msg.data.isMuted || false);
          if (msg.data.provider) setProvider(msg.data.provider);

          setIsBuffering(true);
          
          // Fallback: If it doesn't play within 5 seconds (e.g. autoplay strictly blocked),
          // hide the spinner so the user sees the explicit play button if they haven't clicked the spinner yet.
          (window as any).__playFallbackTimeout = setTimeout(() => {
            setIsBuffering(false);
          }, 5000);

          // Debug: Log VK player available methods
          if (msg.data.vkMethods) {
            console.log('[SecureVideoPlayer] VK Player methods:', msg.data.vkMethods);
          }
          // Request quality levels for YouTube
          if (msg.data.provider === 'youtube') {
            setTimeout(() => sendCommand('getQualities'), 3000);
          }
          break;
        case 'stateChange':
          setIsPlaying(msg.data.isPlaying);
          if (msg.data.isPlaying) {
            clearTimeout((window as any).__playFallbackTimeout);
            setShowControls(false);
            setIsBuffering(false);
          } else {
            // Check for actual buffering statuses (like YT state === 3 or VK string states)
            if (msg.data.state === 3 || msg.data.state === 'buffering') {
              setIsBuffering(true);
            } else {
              setIsBuffering(false);
            }
          }
          break;
        case 'timeUpdate':
          // Prevent rubber-banding: ignore stale time updates for 1.2 seconds after seeking
          if (Date.now() - (window as any).__lastSeekTime < 1200) {
            break;
          }
          if (msg.data.currentTime !== undefined) {
            // Since time is confidently updating past the deadzone, we're definitely not buffering anymore!
            setIsBuffering(false);
            
            setCurrentTime(msg.data.currentTime);
            setDuration(msg.data.duration || duration);
            if (msg.data.duration > 0) {
              setProgress((msg.data.currentTime / msg.data.duration) * 100);
            }
            if (onWatchProgressRef.current) {
              onWatchProgressRef.current(msg.data.currentTime);
            }
          }
          break;
        case 'error':
          setStatus('error');
          setErrorMessage('حدث خطأ أثناء تشغيل الفيديو');
          break;
        case 'qualityLevels':
          if (msg.data.levels && Array.isArray(msg.data.levels)) {
            setQualityLevels(msg.data.levels.filter((l: string) => l !== 'auto'));
          }
          if (msg.data.current) {
            setCurrentQuality(msg.data.current);
          }
          break;
        case 'overlayClick':
          if (status === 'ready') {
            const willPlay = !msg.data?.isPlaying; // Or rely on togglePlay state
            sendCommand(willPlay ? 'play' : 'pause');
            if (willPlay) setIsBuffering(true);
          }
          break;
      }
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [duration, onWatchProgress]);

  // ── Send command to embedded player ──
  const sendCommand = useCallback((type: string, data?: any) => {
    if (iframeRef.current?.contentWindow) {
      iframeRef.current.contentWindow.postMessage({ type, ...data }, '*');
    }
  }, []);

  // ── Watch tracking ──
  const [viewTracked, setViewTracked] = useState(false);
  const viewTrackedRef = useRef(false);
  useEffect(() => { viewTrackedRef.current = viewTracked; }, [viewTracked]);

  const actualWatchedSeconds = useRef(0);
  const watchCountRef = useRef(0);
  const [displayedWatched, setDisplayedWatched] = useState(0);
  const pendingTrackedSeconds = useRef(0);
  const flushInFlight = useRef(false);
  const trackingInterval = useRef<NodeJS.Timeout | null>(null);
  const [thresholdSeconds, setThresholdSeconds] = useState(60);

  useEffect(() => {
    if (duration > 0) {
      setThresholdSeconds(
        Math.max(1, Math.ceil(duration * (watchThresholdPercentageRef.current / 100)))
      );
    }
  }, [duration]);

  const flushTrackedProgress = useCallback(async () => {
    if (flushInFlight.current || pendingTrackedSeconds.current <= 0) {
      return;
    }

    const secondsToFlush = pendingTrackedSeconds.current;
    pendingTrackedSeconds.current = 0;
    flushInFlight.current = true;

    try {
      const res = await videoSessionService.trackProgress(
        lessonVideoId,
        secondsToFlush,
        Math.round(duration || 0),
        false
      );
      const data = res.data.data;
      if (data) {
        const newThreshold = data.thresholdSeconds || 60;
        setThresholdSeconds(newThreshold);
        watchCountRef.current = data.currentCount;
        setWatchInfo(prev => ({
          current: data.currentCount,
          max: prev?.max || data.maxCount,
          isLocked: data.isLocked
        }));
        const total = data.totalTrackedSeconds ?? actualWatchedSeconds.current;
        actualWatchedSeconds.current = total;
        setDisplayedWatched(total % Math.max(1, newThreshold));
        if (data.viewRegistered) {
          setViewTracked(true);
          viewTrackedRef.current = true;
        }
      }
    } catch (err) {
      const apiError = err as { response?: { data?: { errors?: string[] } } };
      if (apiError.response?.data?.errors?.includes('DURATION_REQUIRED')) {
        setStatus('error');
        setErrorMessage('تعذر تتبع المشاهدة لأن مدة الفيديو غير متاحة.');
      }
      pendingTrackedSeconds.current += secondsToFlush;
      console.error("Failed to sync progress:", err);
    } finally {
      flushInFlight.current = false;
    }
  }, [duration, lessonVideoId]);

  useEffect(() => {
    if (status !== 'ready') return;

    if (trackingInterval.current) clearInterval(trackingInterval.current);

    trackingInterval.current = setInterval(() => {
      if (isPlaying) {
        actualWatchedSeconds.current += 1;
        setDisplayedWatched(actualWatchedSeconds.current % Math.max(1, thresholdSeconds));
        pendingTrackedSeconds.current += 1;

        const targetSeconds = (watchCountRef.current + 1) * thresholdSeconds;
        if (!viewTrackedRef.current && actualWatchedSeconds.current >= targetSeconds) {
          viewTrackedRef.current = true;
          setViewTracked(true);
        }

        if (pendingTrackedSeconds.current >= 10) {
          void flushTrackedProgress();
        }
      }
    }, 1000);

    return () => {
      if (trackingInterval.current) clearInterval(trackingInterval.current);
    };
  }, [flushTrackedProgress, status, isPlaying, thresholdSeconds]);

  useEffect(() => {
    if (!isPlaying) {
      void flushTrackedProgress();
    }
  }, [flushTrackedProgress, isPlaying]);

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        void flushTrackedProgress();
      }
    };

    const handleBeforeUnload = () => {
      void flushTrackedProgress();
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('beforeunload', handleBeforeUnload);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('beforeunload', handleBeforeUnload);
      void flushTrackedProgress();
    };
  }, [flushTrackedProgress]);

  const onWatchStatusChangeRef = useRef(onWatchStatusChange);
  useEffect(() => {
    onWatchStatusChangeRef.current = onWatchStatusChange;
  }, [onWatchStatusChange]);

  // Sync internal watch state to the parent via onWatchStatusChange
  useEffect(() => {
    if (onWatchStatusChangeRef.current && watchInfo) {
      onWatchStatusChangeRef.current({
        current: watchInfo.current,
        max: watchInfo.max,
        isLocked: watchInfo.isLocked,
        viewTracked,
        displayedWatched,
        thresholdSeconds
      });
    }
  }, [watchInfo, viewTracked, displayedWatched, thresholdSeconds]);

  const normalizedChapters = React.useMemo(() => {
    if (!chapters || chapters.length === 0 || duration <= 0) return undefined;
    return chapters.map(ch => ({
      id: ch.id,
      title: ch.title,
      summaryText: (ch as any).summaryText,
      mindmapImageUrl: (ch as any).mindmapImageUrl,
      startPercent: (Math.max(0, ch.startTime) / duration) * 100,
      endPercent: (Math.min(duration, ch.endTime) / duration) * 100
    }));
  }, [chapters, duration]);

  // ── Load video ──
  const loadVideo = async () => {
    try {
      setStatus('loading');
      
      const response = await videoSessionService.createSession(lessonVideoId);
      const session = response.data.data;
      if (session.thresholdPercentage) {
        watchThresholdPercentageRef.current = session.thresholdPercentage;
      }
      watchCountRef.current = session.watchInfo.currentCount ?? 0;
      setWatchInfo({
        current: session.watchInfo.currentCount,
        max: session.watchInfo.maxCount
      });
      actualWatchedSeconds.current = session.watchInfo.totalTrackedSeconds ?? 0;
      setDisplayedWatched((session.watchInfo.totalTrackedSeconds ?? 0) % Math.max(1, thresholdSeconds));
      setViewTracked(false);
      viewTrackedRef.current = false;

      if (session.watchInfo.isLocked) {
        setStatus('locked');
        return;
      }

      // 2. Mark session consumed
      await videoSessionService.consumeSession(session.sessionId);

      // 3. Render appropriately based on provider
      if (session.provider?.toLowerCase() === 'vk') {
        setProvider('vk');
        // Build the embed URL pointing to our own API route for VK
        const embedUrl = `/api/video/embed?t=${encodeURIComponent(session.token)}&k=${encodeURIComponent(session.key)}`;

        if (containerRef.current) {
          containerRef.current.innerHTML = '';
          
          const iframe = document.createElement('iframe');
          iframe.src = embedUrl;
          iframe.style.position = 'absolute';
          iframe.style.top = '0';
          iframe.style.left = '0';
          iframe.style.width = '100%';
          iframe.style.height = '100%';
          iframe.style.border = 'none';
          iframe.setAttribute('allow', 'autoplay; encrypted-media');
          iframe.setAttribute('allowfullscreen', '');
          
          iframeRef.current = iframe;
          containerRef.current.appendChild(iframe);

          applyDomShields(containerRef.current, () => {
             setStatus('error');
             setErrorMessage('تم اكتشاف محاولة تعديل المشغل. لإعادة المشاهدة، قم بتحديث الصفحة.');
          });
        }
      } else {
        // Fallback or explicit youtube
        setProvider('youtube');
        // Build the embed URL pointing to our own API route for YouTube
        const embedUrl = `/api/video/embed?t=${encodeURIComponent(session.token)}&k=${encodeURIComponent(session.key)}`;

        if (containerRef.current) {
          containerRef.current.innerHTML = '';
          
          const iframe = document.createElement('iframe');
          iframe.src = embedUrl;
          iframe.style.position = 'absolute';
          iframe.style.top = '0';
          iframe.style.left = '0';
          iframe.style.width = '100%';
          iframe.style.height = '100%';
          iframe.style.border = 'none';
          iframe.setAttribute('allow', 'autoplay; encrypted-media');
          iframe.setAttribute('allowfullscreen', '');
          
          iframeRef.current = iframe;
          containerRef.current.appendChild(iframe);

          applyDomShields(containerRef.current, () => {
            setStatus('error');
            setErrorMessage('تم اكتشاف محاولة تعديل المشغل. لإعادة المشاهدة، قم بتحديث الصفحة.');
          });
        }
      }

    } catch (err: any) {
      const errors = err.response?.data?.errors || [];
      if (errors.includes('WATCH_LIMIT_REACHED')) {
        setStatus('locked');
        setWatchInfo(prev => ({ current: prev?.current || 3, max: prev?.max || 3, isLocked: true }));
        return;
      }
      
      console.error(err);
      setStatus('error');
      const msg = err.response?.data?.message || err.message || 'فشل في تحميل الفيديو';
      setErrorMessage(msg);
      if (onSessionError) onSessionError(msg);
    }
  };

  // ── Player controls (send commands to iframe via postMessage) ──
  const togglePlay = () => {
    sendCommand(isPlaying ? 'pause' : 'play');
    if (!isPlaying) {
      setIsBuffering(true);
    }
  };

  const handleSeek = (percent: number) => {
    if (duration === 0) return;
    const targetTime = (percent / 100) * duration;
    (window as any).__lastSeekTime = Date.now();
    sendCommand('seekTo', { time: targetTime });
    sendCommand('play');
    setCurrentTime(targetTime);
    setProgress(percent);
    setIsBuffering(true);
  };

  const handleVolumeChange = (vol: number) => {
    sendCommand('setVolume', { volume: vol });
    setVolume(vol);
    if (vol > 0 && isMuted) {
      setIsMuted(false);
      sendCommand('unmute');
    }
  };

  const toggleMute = () => {
    if (isMuted) {
      sendCommand('unmute');
      setIsMuted(false);
    } else {
      sendCommand('mute');
      setIsMuted(true);
    }
  };

  const [isPseudoFullscreen, setIsPseudoFullscreen] = useState(false);

  const toggleFullscreen = () => {
    const el = containerRef.current?.parentElement;
    if (!el) return;
    
    if (!document.fullscreenElement && !(document as any).webkitFullscreenElement) {
      if (el.requestFullscreen) {
        el.requestFullscreen().catch(() => setIsPseudoFullscreen(true));
      } else if ((el as any).webkitRequestFullscreen) {
        (el as any).webkitRequestFullscreen();
      } else {
        setIsPseudoFullscreen(true);
      }
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      } else if ((document as any).webkitExitFullscreen) {
        (document as any).webkitExitFullscreen();
      }
      setIsPseudoFullscreen(false);
    }
  };

  const handlePlaybackRateChange = (rate: number) => {
    sendCommand('setPlaybackRate', { rate });
  };

  const handleQualityChange = (quality: string) => {
    sendCommand('setQuality', { quality });
    setCurrentQuality(quality);
  };

  const activeChapterDesktop = React.useMemo(() => {
    if (!normalizedChapters || normalizedChapters.length === 0 || duration <= 0) return null;
    const currentPercent = (currentTime / duration) * 100;
    return normalizedChapters.find(ch => currentPercent >= ch.startPercent && Math.floor(currentPercent) < Math.ceil(ch.endPercent)) || normalizedChapters[normalizedChapters.length - 1];
  }, [normalizedChapters, currentTime, duration]);

  // ── Render States ──
  if (isExamLocked) {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-xl overflow-hidden flex flex-col items-center justify-center border border-[var(--admin-primary)]/30 p-8 text-center ${className}`}>
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] shadow-inner">
          <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
        </div>
        <h3 className="text-xl font-bold text-white mb-2">هذا الفيديو مغلق</h3>
        <p className="text-gray-300 mb-6 max-w-md">الفيديو مغلق. يرجى اجتياز امتحان الفيديو السابق أولاً.</p>
        
        {blockingExamId && (
          <button 
            type="button"
            onClick={() => router.push(`/student/exams/${blockingExamId}?packageId=${packageId}`)}
            className="px-6 py-3 bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] border border-[var(--admin-primary)] text-[var(--admin-primary-contrast)] font-bold rounded-lg transition-all hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-black min-w-[200px]"
          >
            اذهب للامتحان
          </button>
        )}
      </div>
    );
  }

  if (status === 'idle') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-xl overflow-hidden flex items-center justify-center border border-pharaoh-gold/30 cursor-pointer group ${className}`} onClick={loadVideo}>
        <div className="absolute inset-0 bg-cover bg-center opacity-40 group-hover:opacity-30 transition-opacity" style={{ backgroundImage: "url('/images/lesson-placeholder.jpg')" }}></div>
        <div className="absolute inset-0 bg-black/40 backdrop-blur-sm z-30 flex items-center justify-center transition-all duration-300 pointer-events-auto">
          <div className="w-20 h-20 bg-white/20 backdrop-blur-md border border-white/50 rounded-full flex items-center justify-center transform group-hover:scale-110 transition-all shadow-[0_0_30px_rgba(255,255,255,0.4)] cursor-pointer">
            <Play className="w-8 h-8 text-white ml-1" fill="currentColor" />
          </div>
        </div>
      </div>
    );
  }

  if (status === 'locked') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-xl overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`}>
        <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
        <h3 className="text-xl font-bold text-white mb-2">تم الوصول للحد الأقصى للمشاهدات</h3>
        <p className="text-gray-300 mb-6">لقد استنفدت الحد المسموح به لمشاهدة هذا الفيديو ({watchInfo?.max} مرات).</p>
        
        {extraWatchReqStatus === 'Pending' ? (
           <div className="px-6 py-3 bg-yellow-500/20 text-yellow-500 border border-yellow-500/50 rounded-lg">
              جاري مراجعة طلبك للمشاهدة الإضافية من قبل الدعم الفني
           </div>
        ) : extraWatchReqStatus === 'Rejected' ? (
           <div className="px-6 py-3 bg-red-500/20 text-red-500 border border-red-500/50 rounded-lg flex flex-col items-center gap-2">
              <span>تم رفض طلبك للمشاهدة الإضافية</span>
              {extraWatchRejectionReason ? (
                <span className="text-sm text-red-200">{extraWatchRejectionReason}</span>
              ) : null}
           </div>
        ) : (
           <button 
              onClick={handleRequestExtra}
              disabled={requestingExtra}
              className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 text-white font-bold rounded-lg transition-colors flex items-center justify-center min-w-[200px]"
           >
              {requestingExtra ? 'جاري الطلب...' : 'طلب مشاهدة إضافية'}
           </button>
        )}
      </div>
    );
  }

  if (status === 'error') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-lg overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`}>
        <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
        <h3 className="text-xl font-bold text-white mb-2">عذراً، حدث خطأ</h3>
        <p className="text-gray-300">{errorMessage}</p>
        <button onClick={loadVideo} className="mt-6 px-6 py-2 bg-red-600 hover:bg-red-500 text-white font-medium rounded-md transition-colors shadow-md">
          حاول مرة أخرى
        </button>
      </div>
    );
  }

  return (
    <div className={`flex flex-col w-full rounded-xl overflow-hidden border border-pharaoh-gold/30 bg-black shadow-lg group ${className} ${isPseudoFullscreen ? '!fixed !inset-0 !z-[100] !rounded-none' : ''}`}>
      
      {/* Video Container */}
      <div 
        className="relative w-full aspect-video bg-black cursor-pointer rounded-xl overflow-hidden"
        onMouseMove={handlePlayerInteraction}
        onTouchStart={handlePlayerInteraction}
        onClick={() => handlePlayerInteraction()}
        onMouseLeave={() => { if(isPlaying) setShowControls(false) }}
      >
        <div ref={containerRef} className="absolute inset-0 w-full h-full" />
        
        {/* Floating Chapter Info Overlay */}
        {activeChapterDesktop && activeChapterDesktop.summaryText && status === 'ready' && (showControls || isChapterInfoOpen) && (
          <div 
            className="absolute top-4 right-4 bottom-16 z-[90] flex flex-col items-end pointer-events-none"
            onMouseEnter={() => setIsHoveringControls(true)}
            onMouseLeave={() => setIsHoveringControls(false)}
            onClick={(e) => e.stopPropagation()}
            dir="rtl"
          >
             <AnimatePresence mode="wait">
               {!isChapterInfoOpen ? (
                 <motion.button 
                   key="btn"
                   initial={{ opacity: 0, scale: 0.8 }}
                   animate={{ opacity: 1, scale: 1 }}
                   exit={{ opacity: 0, scale: 0.8 }}
                   onClick={() => setIsChapterInfoOpen(true)} 
                   className="pointer-events-auto w-10 h-10 shrink-0 rounded-xl bg-black/60 backdrop-blur border border-white/10 flex items-center justify-center text-white hover:bg-[var(--admin-primary)] transition shadow-[0_4px_20px_rgba(0,0,0,0.5)] cursor-pointer"
                   title="معلومات الفصل الحالى"
                 >
                    <Info className="w-5 h-5" />
                 </motion.button>
               ) : (
                 <motion.div 
                   key="panel"
                   initial={{ opacity: 0, y: -20 }}
                   animate={{ opacity: 1, y: 0 }}
                   exit={{ opacity: 0, y: -20 }}
                   transition={{ type: 'spring', damping: 25, stiffness: 300 }}
                   className="pointer-events-auto bg-black/70 backdrop-blur-xl border border-[var(--admin-primary)]/30 rounded-2xl p-6 w-[280px] sm:w-[350px] h-full overflow-y-auto custom-scrollbar shadow-[0_10px_40px_rgba(0,0,0,0.6)] relative flex flex-col"
                 >
                    <button 
                      onClick={() => setIsChapterInfoOpen(false)} 
                      className="absolute top-2 left-2 text-white/50 hover:text-red-400 bg-white/5 hover:bg-white/10 rounded-full p-1.5 transition z-10"
                      title="إغلاق"
                    >
                       <X className="w-4 h-4" />
                    </button>
                    <SplitText 
                      key={`title-${activeChapterDesktop.id}`}
                      text={activeChapterDesktop.title} 
                      tag="h4" 
                      className="text-white font-black text-sm mb-2 ml-6 block" 
                      textAlign="right"
                      splitType="words"
                    />
                    <SplitText 
                      key={`summary-${activeChapterDesktop.id}`}
                      text={activeChapterDesktop.summaryText} 
                      tag="p" 
                      className="text-white/90 text-xs sm:text-sm leading-relaxed block" 
                      textAlign="right"
                      splitType="words"
                      delay={20}
                    />
                 </motion.div>
               )}
             </AnimatePresence>
          </div>
        )}

        {/* Floating Mindmap Overlay */}
        {activeChapterDesktop && activeChapterDesktop.mindmapImageUrl && status === 'ready' && (showControls || isMindmapOpen) && (
          <div 
            className="absolute top-4 left-4 bottom-16 z-[90] flex flex-col items-start pointer-events-none"
            onMouseEnter={() => setIsHoveringControls(true)}
            onMouseLeave={() => setIsHoveringControls(false)}
            onClick={(e) => e.stopPropagation()}
            dir="ltr"
          >
             <AnimatePresence mode="wait">
               {!isMindmapOpen ? (
                 <motion.button 
                   key="btn-mindmap"
                   initial={{ opacity: 0, scale: 0.8 }}
                   animate={{ opacity: 1, scale: 1 }}
                   exit={{ opacity: 0, scale: 0.8 }}
                   onClick={() => setIsMindmapOpen(true)} 
                   className="pointer-events-auto shrink-0 rounded-xl bg-black/60 backdrop-blur border border-white/10 flex items-center justify-center text-white hover:bg-[var(--admin-primary)] transition shadow-[0_4px_20px_rgba(0,0,0,0.5)] cursor-pointer px-4 h-10"
                   title="الخريطة الذهنية"
                 >
                    <Map className="w-5 h-5 mr-2" />
                    <span className="font-bold text-sm tracking-wide">Mindmap</span>
                 </motion.button>
               ) : (
                 <motion.div 
                   key="panel-mindmap"
                   initial={{ opacity: 0, y: -20 }}
                   animate={{ opacity: 1, y: 0 }}
                   exit={{ opacity: 0, y: -20 }}
                   transition={{ type: 'spring', damping: 25, stiffness: 300 }}
                   className="pointer-events-auto bg-black/70 backdrop-blur-xl border border-[var(--admin-primary)]/30 rounded-2xl p-6 w-[280px] sm:w-[500px] h-full overflow-hidden shadow-[0_10px_40px_rgba(0,0,0,0.6)] relative flex flex-col"
                 >
                    <button 
                      onClick={() => setIsMindmapOpen(false)} 
                      className="absolute top-2 right-2 text-white/50 hover:text-red-400 bg-white/5 hover:bg-white/10 rounded-full p-1.5 transition z-10"
                      title="إغلاق"
                    >
                       <X className="w-4 h-4" />
                    </button>
                    <SplitText 
                      key={`mindmap-title-${activeChapterDesktop.id}`}
                      text={`الخريطة الذهنية: ${activeChapterDesktop.title}`} 
                      tag="h4" 
                      className="text-white font-black text-sm mb-4 pr-6 block" 
                      textAlign="right"
                      splitType="words"
                    />
                    <div className="flex-grow w-full relative rounded-lg overflow-hidden border border-white/10 bg-black/50">
                        {/* eslint-disable-next-line @next/next/no-img-element */}
                        <img 
                          src={resolveMediaUrl(activeChapterDesktop.mindmapImageUrl)} 
                          alt="Mindmap" 
                          className="w-full h-full object-contain absolute top-0 left-0"
                        />
                    </div>
                 </motion.div>
               )}
             </AnimatePresence>
          </div>
        )}
        
        {(status === 'loading' || isBuffering) && (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/40 backdrop-blur-sm z-20 pointer-events-none rounded-xl">
            <SpinnerLoader />
          </div>
        )}

        {status === 'ready' && !isPlaying && !isBuffering && (
          <div 
            className="absolute inset-0 bg-black/40 backdrop-blur-sm z-10 flex items-center justify-center transition-all duration-300 pointer-events-auto rounded-xl"
            onClick={(e) => {
              e.stopPropagation();
              togglePlay();
            }}
          >
            <div className="w-20 h-20 bg-white/20 backdrop-blur-md border border-white/50 rounded-full flex items-center justify-center transform hover:scale-110 transition-all shadow-[0_0_30px_rgba(255,255,255,0.4)] cursor-pointer">
              <Play className="w-8 h-8 text-white ml-1" fill="currentColor" />
            </div>
          </div>
        )}

        {status === 'ready' && (
          <PlayerControls 
            isPlaying={isPlaying}
            onTogglePlay={togglePlay}
            progress={progress}
            onSeek={handleSeek}
            volume={volume}
            isMuted={isMuted}
            onVolumeChange={handleVolumeChange}
            onToggleMute={toggleMute}
            onToggleFullscreen={toggleFullscreen}
            durationFormatted={formatTime(duration)}
            currentTimeFormatted={formatTime(currentTime)}
            onPlaybackRateChange={handlePlaybackRateChange}
            visible={showControls}
            provider={provider}
            onControlHover={setIsHoveringControls}
            chapters={normalizedChapters}
          />
        )}
      </div>
    </div>
  );
});

SecureVideoPlayerComponent.displayName = 'SecureVideoPlayer';

export default SecureVideoPlayerComponent;
