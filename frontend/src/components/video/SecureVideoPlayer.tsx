'use client';

import { devConsole } from '@/utils/dev-console';
import Image from 'next/image';
import React, { useEffect, useRef, useState, useCallback } from 'react';
import { videoSessionService, type ExtraWatchRequestStatus, type WatchProgressResponse } from '@/services/video-session-service';
import { AlertCircle, Play, Info, X, Map } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { SpinnerLoader } from '@/components/ui/loading-indicator';
import dynamic from 'next/dynamic';
import PlayerControls from './PlayerControls';

const SplitText = dynamic(() => import('@/components/ui/SplitText'), { ssr: false });
import { applyDomShields } from '@/utils/dom-shield';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { useRouter, useParams } from 'next/navigation';
import toast from 'react-hot-toast';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

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
  videoExamId?: string;
  chapters?: import("@/services/content-service").VideoChapterDto[];
  onWatchProgress?: (secondsWatched: number) => void;
  onWatchStatusChange?: (status: WatchStatus) => void;
  onEnded?: () => void;
  className?: string;
  onSessionError?: (error: string) => void;
  lessonPrice?: number;
  lessonId?: string;
}

/**
 * SecureVideoPlayer — Server-Side Embed Approach
 * 
 * Instead of loading the YouTube IFrame API directly (which exposes the video URL
 * in DevTools), we load an iframe pointing to our own `/api/video/embed` route.
 * That route decrypts the video ID server-side and returns an HTML page with YouTube
 * embedded. Communication happens via postMessage.
 * 
 * DevTools shows only an opaque session id in the iframe URL.
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
  videoExamId,
  chapters,
  onWatchProgress,
  onWatchStatusChange,
  onEnded,
  className = '',
  onSessionError,
  lessonPrice,
  lessonId
}, ref) => {
  const iframeRef = useRef<HTMLIFrameElement | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const params = useParams();
  const packageId = params?.packageId as string;
  const onEndedRef = useRef(onEnded);
  const hasEndedRef = useRef(false);

  useEffect(() => {
    onEndedRef.current = onEnded;
  }, [onEnded]);
  
  React.useImperativeHandle(ref, () => ({
    seekTo: (seconds: number) => {
      sendCommand('seekTo', { time: seconds, seconds, allowSeekAhead: true });
    },
    play: () => sendCommand('play'),
    pause: () => sendCommand('pause')
  }));

  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error' | 'locked' | 'superseded'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [watchInfo, setWatchInfo] = useState<{current: number, max: number, isLocked?: boolean} | null>(null);
  const [extraWatchReqStatus, setExtraWatchReqStatus] = useState<ExtraWatchRequestStatus | null>(null);
  const [extraWatchRejectionReason, setExtraWatchRejectionReason] = useState<string | null>(null);
  const [extraWatchStatusError, setExtraWatchStatusError] = useState<string | null>(null);
  const [requestingExtra, setRequestingExtra] = useState(false);
  const [isBuyingAgain, setIsBuyingAgain] = useState(false);
  const [showConfirmRepurchase, setShowConfirmRepurchase] = useState(false);

  const handleRepurchaseLesson = () => {
    if (!lessonId || isBuyingAgain) return;
    setShowConfirmRepurchase(true);
  };

  const executeRepurchase = async () => {
    if (!lessonId) return;
    setShowConfirmRepurchase(false);
    setIsBuyingAgain(true);
    try {
      const { balanceService } = await import('@/services/balance-service');
      const success = await balanceService.purchaseContent('Lesson', lessonId);
      if (success) {
        toast.success('تم إعادة شراء الحصة بنجاح!');
        window.location.reload();
      } else {
        toast.error('فشل في إعادة شراء الحصة');
      }
    } catch (err: any) {
      toast.error(err.message || 'فشل في إعادة شراء الحصة. تأكد من رصيدك.');
    } finally {
      setIsBuyingAgain(false);
    }
  };

  const [isPlaying, setIsPlaying] = useState(false);
  const [isBuffering, setIsBuffering] = useState(false);
  
  const [showControls, setShowControls] = useState(true);
  const [showPlayerShadows, setShowPlayerShadows] = useState(true);
  const [requiresDirectPlayback, setRequiresDirectPlayback] = useState(false);
  const controlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const shadowTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const embedReadyTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const isIOSDeviceRef = useRef(false);
  const watchThresholdPercentageRef = useRef<number>(30);
  const loadingSessionRef = useRef(false);
  const loadingExtraWatchStatusRef = useRef(false);
  const requestingExtraRef = useRef(false);
  const approvedLoadAttemptedRef = useRef(false);

  const [isHoveringControls, setIsHoveringControls] = useState(false);
  const [isChapterInfoOpen, setIsChapterInfoOpen] = useState(false);
  const [isMindmapOpen, setIsMindmapOpen] = useState(false);

  useEffect(() => {
    isIOSDeviceRef.current = /iPad|iPhone|iPod/.test(navigator.userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  }, []);

  const showPersistentPlayerShadows = useCallback(() => {
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
    shadowTimeoutRef.current = null;
    setShowPlayerShadows(true);
  }, []);

  const showTimedPlayerShadows = useCallback(() => {
    showPersistentPlayerShadows();
    shadowTimeoutRef.current = setTimeout(() => {
      setShowPlayerShadows(false);
      shadowTimeoutRef.current = null;
    }, 5000);
  }, [showPersistentPlayerShadows]);

  useEffect(() => () => {
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
  }, []);

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

  const loadExtraWatchStatus = useCallback(async () => {
    if (loadingExtraWatchStatusRef.current) return;
    loadingExtraWatchStatusRef.current = true;
    setExtraWatchStatusError(null);
    try {
      const response = await videoSessionService.getExtraWatchStatus(lessonVideoId);
      setExtraWatchReqStatus(response.data?.data?.requestStatus ?? null);
      setExtraWatchRejectionReason(response.data?.data?.rejectionReason ?? null);
    } catch (error) {
      devConsole.error(error);
      setExtraWatchStatusError('تعذر التحقق من حالة طلب المشاهدة الإضافية.');
    } finally {
      loadingExtraWatchStatusRef.current = false;
    }
  }, [lessonVideoId]);

  useEffect(() => {
    if (status === 'locked') void loadExtraWatchStatus();
  }, [loadExtraWatchStatus, status]);

  useEffect(() => {
    if (extraWatchReqStatus !== 'Approved') {
      approvedLoadAttemptedRef.current = false;
    }

    if (status === 'locked' && extraWatchReqStatus === 'Approved' && !approvedLoadAttemptedRef.current) {
      approvedLoadAttemptedRef.current = true;
      void loadVideo();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [extraWatchReqStatus, status]);

  const handleRequestExtra = async () => {
    if (requestingExtraRef.current) return;
    requestingExtraRef.current = true;
    setRequestingExtra(true);
    setExtraWatchStatusError(null);
    try {
      await videoSessionService.requestExtraWatch(lessonVideoId);
      setExtraWatchReqStatus('Pending');
      setExtraWatchRejectionReason(null);
    } catch(err: any) {
      devConsole.error(err);
      const errors = err.response?.data?.errors || [];
      if (errors.includes('REQUEST_LIMIT_REACHED')) {
        setExtraWatchStatusError('لقد استنفدت الحد الأقصى لطلبات المشاهدة الإضافية المسموح بها لهذه الحصة.');
      } else {
        setExtraWatchStatusError('تعذر إرسال طلب المشاهدة الإضافية. أعد المحاولة.');
      }
    } finally {
      setRequestingExtra(false);
      requestingExtraRef.current = false;
    }
  };

  const [progress, setProgress] = useState(0);
  const [volume, setVolume] = useState(100);
  const [isMuted, setIsMuted] = useState(false);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [provider, setProvider] = useState<string>('youtube');
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
      if (event.origin !== window.location.origin) return;
      const msg = event.data;
      if (!msg || msg.source !== 'video-embed') return;

      switch (msg.type) {
        case 'ready':
          if (embedReadyTimeoutRef.current) {
            clearTimeout(embedReadyTimeoutRef.current);
            embedReadyTimeoutRef.current = null;
          }
          setStatus('ready');
          setDuration(msg.data.duration || 0);
          setVolume(msg.data.volume || 100);
          setIsMuted(msg.data.isMuted || false);
          const embedProvider = (msg.data.provider || 'youtube').toLowerCase();
          setProvider(embedProvider);
          setRequiresDirectPlayback(isIOSDeviceRef.current && embedProvider === 'youtube');
          showPersistentPlayerShadows();

          setIsBuffering(true);
          
          // Fallback: If it doesn't play within 5 seconds (e.g. autoplay strictly blocked),
          // hide the spinner so the user sees the explicit play button if they haven't clicked the spinner yet.
          (window as any).__playFallbackTimeout = setTimeout(() => {
            setIsBuffering(false);
          }, 5000);

          // Debug: Log VK player available methods
          if (msg.data.vkMethods) {
            devConsole.log('[SecureVideoPlayer] VK Player methods:', msg.data.vkMethods);
          }
          break;
        case 'stateChange':
          setIsPlaying(msg.data.isPlaying);
          if (msg.data.isPlaying) {
            hasEndedRef.current = false;
            clearTimeout((window as any).__playFallbackTimeout);
            setRequiresDirectPlayback(false);
            setShowControls(false);
            setIsBuffering(false);
            showTimedPlayerShadows();
          } else {
            if ((msg.data.state === 0 || msg.data.state === 'ended') && !hasEndedRef.current) {
              hasEndedRef.current = true;
              onEndedRef.current?.();
            }
            // Check for actual buffering statuses (like YT state === 3 or VK string states)
            if (msg.data.state === 3 || msg.data.state === 'buffering') {
              setIsBuffering(true);
            } else {
              setIsBuffering(false);
              showPersistentPlayerShadows();
            }
          }
          break;
        case 'autoplayBlocked':
          clearTimeout((window as any).__playFallbackTimeout);
          setRequiresDirectPlayback(isIOSDeviceRef.current && (msg.data?.provider || provider) === 'youtube');
          setIsPlaying(false);
          setIsBuffering(false);
          setShowControls(true);
          showPersistentPlayerShadows();
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
          if (embedReadyTimeoutRef.current) {
            clearTimeout(embedReadyTimeoutRef.current);
            embedReadyTimeoutRef.current = null;
          }
          setStatus('error');
          setErrorMessage(msg.data?.message || 'حدث خطأ أثناء تشغيل الفيديو');
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
      iframeRef.current.contentWindow.postMessage({ type, ...data }, window.location.origin);
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
  const activeSessionIdRef = useRef<string | null>(null);
  const nextProgressSequenceRef = useRef(1);
  const activeProgressRequestRef = useRef<{ sequence: number; seconds: number } | null>(null);
  const trackingInterval = useRef<NodeJS.Timeout | null>(null);
  const [thresholdSeconds, setThresholdSeconds] = useState(60);

  const capWatchCount = useCallback((current: number, max: number) => {
    return max > 0 ? Math.min(current, max) : current;
  }, []);

  const stopSessionTracking = useCallback((nextStatus: 'error' | 'superseded', message?: string) => {
    activeProgressRequestRef.current = null;
    pendingTrackedSeconds.current = 0;
    if (trackingInterval.current) clearInterval(trackingInterval.current);
    sendCommand('pause');
    setIsPlaying(false);
    if (message) setErrorMessage(message);
    setStatus(nextStatus);
  }, [sendCommand]);

  const applyProgressResponse = useCallback((progressResponse: WatchProgressResponse) => {
    const newThreshold = progressResponse.thresholdSeconds || 60;
    setThresholdSeconds(newThreshold);
    const maxCount = progressResponse.maxCount ?? watchInfo?.max ?? 0;
    const cappedCurrent = capWatchCount(progressResponse.currentCount, maxCount);
    watchCountRef.current = cappedCurrent;
    setWatchInfo(previous => ({
      current: cappedCurrent,
      max: maxCount || previous?.max || 0,
      isLocked: progressResponse.isLocked,
    }));
    actualWatchedSeconds.current = progressResponse.totalTrackedSeconds;
    setDisplayedWatched(progressResponse.totalTrackedSeconds % Math.max(1, newThreshold));
    if (progressResponse.isLocked) pendingTrackedSeconds.current = 0;
    if (progressResponse.viewRegistered) {
      setViewTracked(true);
      viewTrackedRef.current = true;
    }
  }, [capWatchCount, watchInfo?.max]);

  useEffect(() => {
    if (duration > 0) {
      setThresholdSeconds(
        Math.max(1, Math.ceil(duration * (watchThresholdPercentageRef.current / 100)))
      );
    }
  }, [duration]);

  const flushTrackedProgress = useCallback(async () => {
    const sessionId = activeSessionIdRef.current;
    if (flushInFlight.current || !sessionId) {
      return;
    }

    if (!activeProgressRequestRef.current) {
      if (pendingTrackedSeconds.current <= 0) return;
      activeProgressRequestRef.current = {
        sequence: nextProgressSequenceRef.current,
        seconds: pendingTrackedSeconds.current,
      };
      pendingTrackedSeconds.current = 0;
    }

    const progressRequest = activeProgressRequestRef.current;
    flushInFlight.current = true;

    try {
      const res = await videoSessionService.trackProgress({
        lessonVideoId,
        sessionId,
        progressSequence: progressRequest.sequence,
        secondsWatched: progressRequest.seconds,
        totalDurationSeconds: Math.round(duration || 0),
      });
      activeProgressRequestRef.current = null;
      nextProgressSequenceRef.current += 1;
      applyProgressResponse(res.data.data);
    } catch (err) {
      const apiError = err as { response?: { data?: { errors?: string[] } } };
      const errors = apiError.response?.data?.errors ?? [];
      if (errors.includes('SESSION_SUPERSEDED')) {
        stopSessionTracking('superseded');
      } else if (errors.includes('SESSION_EXPIRED') || errors.includes('SESSION_INVALID')) {
        stopSessionTracking('error', 'انتهت جلسة تشغيل الفيديو. أعد تحميل الفيديو للمتابعة.');
      } else if (errors.includes('DURATION_REQUIRED')) {
        setStatus('error');
        setErrorMessage('تعذر تتبع المشاهدة لأن مدة الفيديو غير متاحة.');
      }
      devConsole.error("Failed to sync progress:", err);
    } finally {
      flushInFlight.current = false;
    }
  }, [applyProgressResponse, duration, lessonVideoId, stopSessionTracking]);

  useEffect(() => {
    if (status !== 'ready') return;

    if (trackingInterval.current) clearInterval(trackingInterval.current);

    trackingInterval.current = setInterval(() => {
      if (isPlaying) {
        pendingTrackedSeconds.current += 1;

        if (!viewTrackedRef.current) {
          actualWatchedSeconds.current += 1;
          setDisplayedWatched(actualWatchedSeconds.current % Math.max(1, thresholdSeconds));

          const targetSeconds = (watchCountRef.current + 1) * thresholdSeconds;
          if (actualWatchedSeconds.current >= targetSeconds) {
            viewTrackedRef.current = true;
            setViewTracked(true);
          }
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
      summaryText: ch.summaryText,
      mindmapImageUrl: ch.mindmapImageUrl,
      startTime: ch.startTime,
      endTime: ch.endTime,
      startPercent: (Math.max(0, ch.startTime) / duration) * 100,
      endPercent: (Math.min(duration, ch.endTime) / duration) * 100
    }));
  }, [chapters, duration]);

  // ── Load video ──
  const loadVideo = async () => {
    if (loadingSessionRef.current) return;
    loadingSessionRef.current = true;
    try {
      setStatus('loading');
      
      const response = await videoSessionService.createSession(lessonVideoId);
      const session = response.data.data;
      activeSessionIdRef.current = session.sessionId;
      nextProgressSequenceRef.current = 1;
      activeProgressRequestRef.current = null;
      pendingTrackedSeconds.current = 0;
      if (session.thresholdPercentage) {
        watchThresholdPercentageRef.current = session.thresholdPercentage;
      }
      const sessionMaxCount = session.watchInfo.maxCount ?? 0;
      const sessionCurrentCount = capWatchCount(session.watchInfo.currentCount ?? 0, sessionMaxCount);
      watchCountRef.current = sessionCurrentCount;
      setWatchInfo({
        current: sessionCurrentCount,
        max: sessionMaxCount,
        isLocked: session.watchInfo.isLocked
      });
      actualWatchedSeconds.current = session.watchInfo.totalTrackedSeconds ?? 0;
      setDisplayedWatched((session.watchInfo.totalTrackedSeconds ?? 0) % Math.max(1, thresholdSeconds));
      setViewTracked(false);
      viewTrackedRef.current = false;

      if (session.watchInfo.isLocked) {
        setStatus('locked');
        return;
      }

      if (embedReadyTimeoutRef.current) {
        clearTimeout(embedReadyTimeoutRef.current);
      }
      embedReadyTimeoutRef.current = setTimeout(() => {
        setStatus('error');
        setErrorMessage('تعذر تحميل مشغل الفيديو. تأكد من إعدادات الاتصال الداخلي بين الواجهة والباك اند.');
      }, 12000);

      const consumeAfterIframeLoad = () => {
        void videoSessionService.consumeSession(session.sessionId).catch((err) => {
          devConsole.error('Failed to consume video session after iframe load:', err);
        });
      };

      // 2. Render appropriately based on provider
      const providerName = session.provider?.toLowerCase() || 'youtube';
      setProvider(providerName);
      const embedUrl = `/api/video/embed?s=${encodeURIComponent(session.sessionId)}`;

      if (containerRef.current) {
        containerRef.current.innerHTML = '';
        
        const iframe = document.createElement('iframe');
        iframe.src = embedUrl;
        iframe.onload = consumeAfterIframeLoad;
        iframe.style.position = 'absolute';
        iframe.style.top = '0';
        iframe.style.left = '0';
        iframe.style.width = '100%';
        iframe.style.height = '100%';
        iframe.style.border = 'none';
        iframe.setAttribute('allow', 'autoplay; encrypted-media; picture-in-picture; fullscreen');
        iframe.setAttribute('allowfullscreen', '');
        iframe.setAttribute('playsinline', '');
        
        iframeRef.current = iframe;
        containerRef.current.appendChild(iframe);

        applyDomShields(containerRef.current, () => {
           setStatus('error');
           setErrorMessage('تم اكتشاف محاولة تعديل المشغل. لإعادة المشاهدة، قم بتحديث الصفحة.');
        });
      }

    } catch (err: any) {
      const errors = err.response?.data?.errors || [];
      if (errors.includes('WATCH_LIMIT_REACHED')) {
        setStatus('locked');
        // Use real watchInfo from the error response data (backend includes it even when locked)
        const lockData = err.response?.data?.data?.watchInfo;
        setWatchInfo(prev => ({
          current: capWatchCount(lockData?.currentCount ?? prev?.current ?? 0, lockData?.maxCount ?? prev?.max ?? 0),
          max: lockData?.maxCount ?? prev?.max ?? 0,
          isLocked: true
        }));
        return;
      }
      
      devConsole.error(err);
      setStatus('error');
      const msg = err.response?.data?.message || err.message || 'فشل في تحميل الفيديو';
      setErrorMessage(msg);
      if (onSessionError) onSessionError(msg);
    } finally {
      loadingSessionRef.current = false;
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

  const activeChapterDesktop = React.useMemo(() => {
    if (!normalizedChapters || normalizedChapters.length === 0 || duration <= 0) return null;
    const activeChapter = normalizedChapters.find((chapter, index) => (
      currentTime >= chapter.startTime
      && (currentTime < chapter.endTime || (index === normalizedChapters.length - 1 && currentTime <= chapter.endTime))
    ));
    if (activeChapter) return activeChapter;
    return currentTime < normalizedChapters[0].startTime
      ? normalizedChapters[0]
      : normalizedChapters[normalizedChapters.length - 1];
  }, [normalizedChapters, currentTime, duration]);

  // ── Render States ──
  if (isExamLocked) {
    const isSelfLocked = blockingExamId && videoExamId && blockingExamId.toLowerCase() === videoExamId.toLowerCase();
    return (
      <div className={`relative w-full aspect-video bg-black rounded-xl overflow-hidden flex flex-col items-center justify-center border border-[var(--admin-primary)]/30 p-8 text-center ${className}`}>
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] shadow-inner">
          <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
        </div>
        <h3 className="text-xl font-bold text-white mb-2">هذا الفيديو مغلق</h3>
        <p className="text-gray-300 mb-6 max-w-md">
          {isSelfLocked 
            ? "الفيديو مغلق. يرجى اجتياز امتحان هذا الفيديو أولاً لفتح المشاهدة."
            : "الفيديو مغلق. يرجى اجتياز امتحان الفيديو السابق أولاً."}
        </p>
        
        {blockingExamId && (
          <div className="flex flex-wrap gap-3 justify-center">
            <button 
              type="button"
              onClick={() => router.push(`/student/exams/${blockingExamId}?packageId=${packageId}&lessonId=${lessonId}`)}
              className="px-6 py-3 bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] border border-[var(--admin-primary)] text-[var(--admin-primary-contrast)] font-bold rounded-lg transition-all hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-black min-w-[200px]"
            >
              اذهب للامتحان
            </button>
            <button 
              type="button"
              onClick={() => router.push(`/student/exams/${blockingExamId}?packageId=${packageId}&lessonId=${lessonId}`)}
              className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 text-white font-bold rounded-lg transition-all hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-black min-w-[200px]"
            >
              عرض النتيجة
            </button>
          </div>
        )}
      </div>
    );
  }

  if (status === 'idle') {
    return (
      <button
        type="button"
        className={`relative flex aspect-video w-full items-center justify-center overflow-hidden rounded-xl border border-[var(--secondary)]/30 bg-black text-white group focus-visible:ring-2 focus-visible:ring-[var(--secondary)] focus-visible:ring-offset-2 focus-visible:ring-offset-black ${className}`}
        onClick={loadVideo}
        aria-label="تحميل وتشغيل الفيديو"
      >
        <div className="absolute inset-0 bg-cover bg-center opacity-40 group-hover:opacity-30 transition-opacity" style={{ backgroundImage: "url('/images/lesson-placeholder.webp')" }}></div>
        <div className="absolute inset-0 bg-black/40 backdrop-blur-sm z-30 flex items-center justify-center transition-all duration-300 pointer-events-auto">
          <div className="w-20 h-20 bg-white/20 backdrop-blur-md border border-white/50 rounded-full flex items-center justify-center transform group-hover:scale-110 transition-all shadow-[0_0_30px_rgba(255,255,255,0.4)] cursor-pointer">
            <Play className="w-8 h-8 text-white ml-1" fill="currentColor" />
          </div>
        </div>
      </button>
    );
  }

  if (status === 'locked') {
    return (
      <>
        <div className={`relative w-full aspect-video bg-black rounded-xl overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`}>
          <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
          <h3 className="text-xl font-bold text-white mb-2">تم الوصول للحد الأقصى للمشاهدات</h3>
          <p className="text-gray-300 mb-6">لقد استنفدت الحد المسموح به لمشاهدة هذا الفيديو ({watchInfo?.max} مرات).</p>
          
          <div className="flex flex-col gap-4 items-center justify-center">
            {lessonPrice !== undefined && lessonId && (
              <button
                type="button"
                onClick={handleRepurchaseLesson}
                disabled={isBuyingAgain}
                className="px-6 py-3 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg transition-all duration-200 flex items-center justify-center min-w-[200px] shadow-lg shadow-emerald-600/20 hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50"
              >
                {isBuyingAgain ? 'جاري الشراء...' : `شراء الحصة مجدداً (${lessonPrice} ج.م)`}
              </button>
            )}

            {extraWatchStatusError ? (
               <div role="alert" className="flex max-w-md flex-col items-center gap-3 rounded-xl border border-red-500/50 bg-red-500/20 px-5 py-4 text-red-100">
                  <span>{extraWatchStatusError}</span>
                  <button
                    type="button"
                    onClick={() => void loadExtraWatchStatus()}
                    className="min-h-11 rounded-lg border border-red-200/50 px-4 font-bold"
                  >
                    إعادة التحقق
                  </button>
               </div>
            ) : extraWatchReqStatus === 'Pending' ? (
               <div className="flex flex-col items-center gap-3">
                 <div className="px-6 py-3 bg-yellow-500/20 text-yellow-500 border border-yellow-500/50 rounded-lg text-sm">
                    جاري مراجعة طلبك للمشاهدة الإضافية من قبل الدعم الفني
                 </div>
                 <button
                   type="button"
                   onClick={() => void loadExtraWatchStatus()}
                   className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white text-xs font-bold rounded-lg border border-white/20 transition-colors"
                 >
                   تحديث الحالة
                 </button>
               </div>
            ) : extraWatchReqStatus === 'Rejected' ? (
               <div className="px-6 py-3 bg-red-500/20 text-red-500 border border-red-500/50 rounded-lg flex flex-col items-center gap-2 text-sm">
                  <span>تم رفض طلبك للمشاهدة الإضافية</span>
                  {extraWatchRejectionReason ? (
                    <span className="text-sm text-red-200 mb-2">{extraWatchRejectionReason}</span>
                  ) : null}
                  <button
                    type="button"
                    onClick={() => void loadExtraWatchStatus()}
                    className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white text-xs font-bold rounded-lg border border-white/20 transition-colors"
                  >
                    تحديث الحالة
                  </button>
               </div>
            ) : (
               <button 
                  type="button"
                  onClick={handleRequestExtra}
                  disabled={requestingExtra}
                  className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 text-white font-bold rounded-lg transition-colors flex items-center justify-center min-w-[200px] disabled:opacity-50"
               >
                  {requestingExtra ? 'جاري الطلب...' : 'طلب مشاهدة إضافية'}
               </button>
            )}
          </div>
        </div>

        <ConfirmDialog
          open={showConfirmRepurchase}
          title="تأكيد إعادة الشراء"
          description={`هل أنت متأكد من رغبتك في شراء هذه الحصة مجدداً بسعر (${lessonPrice} ج.م)؟ سيتم خصم هذا المبلغ من محفظتك وسيعيد تعيين عدد المشاهدات للفيديوهات إلى الصفر.`}
          confirmLabel="نعم، اشترِ مجدداً"
          cancelLabel="إلغاء"
          variant="primary"
          onConfirm={executeRepurchase}
          onCancel={() => setShowConfirmRepurchase(false)}
        />
      </>
    );
  }

  if (status === 'error') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-lg overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`}>
        <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
        <h3 className="text-xl font-bold text-white mb-2">عذراً، حدث خطأ</h3>
        <p className="text-gray-300">{errorMessage}</p>
        <button type="button" onClick={loadVideo} className="mt-6 min-h-11 rounded-md bg-red-600 px-6 font-medium text-white shadow-md transition-colors hover:bg-red-500">
          حاول مرة أخرى
        </button>
      </div>
    );
  }

  if (status === 'superseded') {
    return (
      <div className={`relative flex aspect-video w-full flex-col items-center justify-center overflow-hidden rounded-lg bg-black p-8 text-center ${className}`} role="alert">
        <AlertCircle className="mb-4 h-12 w-12 text-amber-400" />
        <h3 className="mb-2 text-xl font-bold text-white">توقفت المشاهدة هنا</h3>
        <p className="max-w-md text-gray-300">تم فتح الفيديو في تبويب أو جهاز أحدث. أعد تحميل الفيديو للمتابعة هنا.</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-6 min-h-11 rounded-md bg-[var(--admin-primary)] px-6 font-bold text-[var(--admin-primary-contrast)] transition-opacity hover:opacity-90"
        >
          إعادة تحميل الفيديو
        </button>
      </div>
    );
  }

  return (
    <div className={`flex flex-col w-full rounded-xl overflow-hidden border border-[var(--secondary)]/30 bg-black shadow-lg group ${className} ${isPseudoFullscreen ? '!fixed !inset-0 !z-[100] !rounded-none' : ''}`}>
      
      {/* Video Container */}
      <div 
        className="relative min-h-[200px] w-full aspect-video cursor-pointer overflow-hidden rounded-xl bg-black sm:min-h-0"
        role="region"
        aria-label="مشغل الفيديو"
        tabIndex={0}
        onMouseMove={handlePlayerInteraction}
        onTouchStart={handlePlayerInteraction}
        onFocus={handlePlayerInteraction}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            handlePlayerInteraction();
          }
        }}
        onClick={() => handlePlayerInteraction()}
        onMouseLeave={() => { if(isPlaying) setShowControls(false) }}
      >
        <div ref={containerRef} className="absolute inset-0 w-full h-full" />

        {/* Shadow Gradient Overlay */}
        <AnimatePresence>
          {status === 'ready' && showPlayerShadows && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.3 }}
              className="pointer-events-none absolute inset-0 z-[80] bg-[linear-gradient(to_bottom,rgba(0,0,0,0.62)_0%,rgba(0,0,0,0.24)_15%,rgba(0,0,0,0)_32%,rgba(0,0,0,0)_68%,rgba(0,0,0,0.72)_88%,rgba(0,0,0,0.94)_100%)]"
            />
          )}
        </AnimatePresence>
        
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
                   type="button"
                   key="btn"
                   initial={{ opacity: 0, scale: 0.8 }}
                   animate={{ opacity: 1, scale: 1 }}
                   exit={{ opacity: 0, scale: 0.8 }}
                   onClick={() => setIsChapterInfoOpen(true)} 
                   className="pointer-events-auto flex min-h-11 min-w-11 shrink-0 cursor-pointer items-center justify-center rounded-xl border border-white/10 bg-black/60 text-white shadow-[0_4px_20px_rgba(0,0,0,0.5)] backdrop-blur transition hover:bg-[var(--admin-primary)]"
                   aria-label="فتح معلومات الفصل الحالي"
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
                      type="button"
                      onClick={() => setIsChapterInfoOpen(false)} 
                      className="absolute left-2 top-2 z-10 flex min-h-11 min-w-11 items-center justify-center rounded-full bg-white/5 text-white/50 transition hover:bg-white/10 hover:text-red-400"
                      aria-label="إغلاق معلومات الفصل"
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
        {activeChapterDesktop && status === 'ready' && (showControls || isMindmapOpen) && (
          <div 
            className="pointer-events-none absolute left-3 top-3 z-[90] flex flex-col items-start sm:left-4 sm:top-4"
            onMouseEnter={() => setIsHoveringControls(true)}
            onMouseLeave={() => setIsHoveringControls(false)}
            onClick={(e) => e.stopPropagation()}
            dir="ltr"
          >
             <AnimatePresence mode="wait">
               {!isMindmapOpen ? (
                 <motion.button 
                   type="button"
                   key="btn-mindmap"
                   initial={{ opacity: 0, scale: 0.8 }}
                   animate={{ opacity: 1, scale: 1 }}
                   exit={{ opacity: 0, scale: 0.8 }}
                   onClick={() => setIsMindmapOpen(true)} 
                   disabled={!activeChapterDesktop.mindmapImageUrl}
                   className="pointer-events-auto flex min-h-11 min-w-11 shrink-0 cursor-pointer items-center justify-center rounded-xl border border-white/10 bg-black/60 px-3 text-white shadow-[0_4px_20px_rgba(0,0,0,0.5)] backdrop-blur transition hover:bg-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-60 sm:px-4"
                   aria-label={activeChapterDesktop.mindmapImageUrl ? 'فتح الخريطة الذهنية للفصل' : 'الخريطة الذهنية غير متاحة لهذا الفصل'}
                 >
                    <Map className="h-5 w-5 sm:mr-2" />
                    <span className="hidden text-sm font-bold sm:inline">الخريطة الذهنية</span>
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
                      type="button"
                      onClick={() => setIsMindmapOpen(false)} 
                      className="absolute right-2 top-2 z-10 flex min-h-11 min-w-11 items-center justify-center rounded-full bg-white/5 text-white/50 transition hover:bg-white/10 hover:text-red-400"
                      aria-label="إغلاق الخريطة الذهنية"
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
                      <Image
                        src={resolveMediaUrl(activeChapterDesktop.mindmapImageUrl)}
                        alt={`الخريطة الذهنية: ${activeChapterDesktop.title}`}
                        fill
                        sizes="(max-width: 640px) 280px, 500px"
                        className="object-contain"
                        unoptimized
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
          <button
            type="button"
            className={`absolute inset-0 z-10 flex items-center justify-center rounded-xl bg-black/40 backdrop-blur-sm transition-all duration-300 ${requiresDirectPlayback ? 'pointer-events-none' : 'pointer-events-auto'}`}
            aria-label="تشغيل الفيديو"
            tabIndex={requiresDirectPlayback ? -1 : 0}
            onClick={(e) => {
              e.stopPropagation();
              togglePlay();
            }}
          >
            <div className="w-20 h-20 bg-white/20 backdrop-blur-md border border-white/50 rounded-full flex items-center justify-center transform hover:scale-110 transition-all shadow-[0_0_30px_rgba(255,255,255,0.4)] cursor-pointer">
              <Play className="w-8 h-8 text-white ml-1" fill="currentColor" />
            </div>
          </button>
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
