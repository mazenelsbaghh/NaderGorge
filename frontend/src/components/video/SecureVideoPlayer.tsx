'use client';

import { devConsole } from '@/utils/dev-console';
import Image from 'next/image';
import React, { useEffect, useRef, useState, useCallback } from 'react';
import { videoSessionService, type ExtraWatchRequestStatus, type WatchProgressResponse } from '@/services/video-session-service';
import { AlertCircle, Play, Info, X, Map, Maximize2, Minimize2 } from 'lucide-react';
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
import apiClient from '@/services/api-client';
import {
  resolveProgressReportDurationSeconds,
  resolveStableVideoDuration,
  resolveTrackableDurationSeconds,
  resolveWatchThresholdSeconds,
} from '@/lib/video-tracking-duration';
import {
  canRetryBunnyPlayback,
  isBunnyPlaybackError,
  isBunnyPlaybackStable,
  isCurrentVideoSession,
} from '@/lib/video-playback-recovery';
import { usesNativeProviderControls } from '@/lib/video-player-provider';
import {
  exitVideoFullscreen,
  getFullscreenElement,
  lockVideoToLandscape,
  requestVideoFullscreen,
  unlockVideoOrientation,
  waitForVideoFullscreen,
} from '@/lib/video-fullscreen';
import {
  DOUBLE_TAP_SEEK_SECONDS,
  DOUBLE_TAP_WINDOW_MS,
  isDoubleTapSeek,
  resolveSeekTarget,
  type SeekDirection,
} from '@/lib/video-seek';
import {
  acknowledgeSequencedVideoProgressRequests,
  appendVideoProgressSegment,
  materializeVideoProgressRequests,
  sumVideoProgressMediaSeconds,
  sumVideoProgressWallSeconds,
  type SequencedVideoProgressSegment,
  type VideoProgressSegment,
} from '@/lib/video-progress-segments';
import {
  createBunnyBridgeReadinessWatchdog,
  type BunnyBridgeReadinessWatchdog,
} from '@/lib/bunny-bridge-readiness';

const SUPPORTED_PLAYBACK_RATES = new Set([0.5, 0.75, 1, 1.25, 1.5, 1.75, 2]);
export type VideoQualityLevel = { id: string; label: string; height?: number; bitrate?: number };

function isSupportedVideoPlaybackRate(playbackRate: number): boolean {
  return SUPPORTED_PLAYBACK_RATES.has(playbackRate);
}

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
 * The outer iframe URL contains only an opaque session id. The nested provider
 * still receives its playback identifier, so the embed uses a best-effort
 * inspection guard rather than treating browser DevTools as a security boundary.
 */
export interface SecureVideoPlayerRef {
  seekTo: (seconds: number) => void;
  play: () => void;
  pause: () => void;
}

const SESSION_START_MAX_ATTEMPTS = 3;
const TRACKING_FLUSH_INTERVAL_SECONDS = 30;
const TRACKING_RETRY_MAX_ATTEMPTS = 3;
const TRACKING_BATCH_MAX_SEGMENTS = 30;
const RECENT_MEDIA_PROGRESS_WINDOW_MS = 3_000;
const MAX_TRACKING_TICK_SECONDS = 1.5;

type ProgressFlushOptions = {
  keepalive?: boolean;
  drain?: boolean;
};

type ActiveProgressRequest = SequencedVideoProgressSegment;

async function createVideoSessionWithRetry(lessonVideoId: string) {
  let lastFailure: unknown;
  for (let attempt = 1; attempt <= SESSION_START_MAX_ATTEMPTS; attempt += 1) {
    try {
      return await videoSessionService.createSession(lessonVideoId);
    } catch (error) {
      lastFailure = error;
      const status = (error as { response?: { status?: number } })?.response?.status;
      const isTransient = status === undefined || status >= 500;
      if (!isTransient || attempt === SESSION_START_MAX_ATTEMPTS) {
        throw error;
      }

      await new Promise<void>((resolve) => window.setTimeout(resolve, attempt * 250));
    }
  }

  throw lastFailure;
}

function createVideoEmbedIframe(sessionId: string): HTMLIFrameElement {
  const iframe = document.createElement('iframe');
  iframe.src = `/api/video/embed?s=${encodeURIComponent(sessionId)}`;
  Object.assign(iframe.style, {
    position: 'absolute', top: '0', left: '0', width: '100%', height: '100%', border: 'none',
  });
  iframe.setAttribute('allow', 'autoplay; encrypted-media; picture-in-picture; fullscreen');
  iframe.setAttribute('allowfullscreen', '');
  iframe.setAttribute('playsinline', '');
  iframe.referrerPolicy = 'strict-origin-when-cross-origin';
  return iframe;
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

  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error' | 'locked' | 'superseded' | 'protected'>('idle');
  const statusRef = useRef(status);
  useEffect(() => { statusRef.current = status; }, [status]);
  const [errorMessage, setErrorMessage] = useState('');
  const [watchInfo, setWatchInfo] = useState<{current: number, max: number, isLocked?: boolean} | null>(null);
  const [extraWatchReqStatus, setExtraWatchReqStatus] = useState<ExtraWatchRequestStatus | null>(null);
  const [canWatchAfterStatusRefresh, setCanWatchAfterStatusRefresh] = useState(false);
  const [extraWatchRejectionReason, setExtraWatchRejectionReason] = useState<string | null>(null);
  const [extraWatchStatusError, setExtraWatchStatusError] = useState<string | null>(null);
  const [requestingExtra, setRequestingExtra] = useState(false);
  const [showExtraWatchRequestForm, setShowExtraWatchRequestForm] = useState(false);
  const [extraWatchRequestReason, setExtraWatchRequestReason] = useState('');
  const [extraWatchRequestValidationError, setExtraWatchRequestValidationError] = useState('');
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
  const isPlayingRef = useRef(false);
  const [isBuffering, setIsBuffering] = useState(false);
  const [nativeProviderSurfaceLoaded, setNativeProviderSurfaceLoaded] = useState(false);
  const [provider, setProvider] = useState<string>('youtube');
  const [qualityLevels, setQualityLevels] = useState<VideoQualityLevel[]>([]);
  const [currentQuality, setCurrentQuality] = useState('auto');
  const providerRef = useRef('youtube');
  const serverCanResolveDurationRef = useRef(false);
  
  const [showControls, setShowControls] = useState(true);
  const [showPlayerShadows, setShowPlayerShadows] = useState(true);
  const [requiresDirectPlayback, setRequiresDirectPlayback] = useState(false);
  const controlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const shadowTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const embedReadinessWatchdogRef = useRef<BunnyBridgeReadinessWatchdog | null>(null);
  const playFallbackTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const bunnyRecoveryTimerRef = useRef<NodeJS.Timeout | null>(null);
  const bunnyRecoveryAttemptsRef = useRef(0);
  const bunnyRecoveryVideoIdRef = useRef(lessonVideoId);
  const bunnyReadyAtRef = useRef(0);
  const bunnyRecoveryResumeTimeRef = useRef(0);
  const isIOSDeviceRef = useRef(false);
  const watchThresholdPercentageRef = useRef<number>(30);
  const youtubeShadowDelayMsRef = useRef(5000);
  const bunnyShadowDelayMsRef = useRef(5000);
  const [shadowOpacity, setShadowOpacity] = useState({ top: 0.70, bottom: 0.98 });
  const [shadowCoverage, setShadowCoverage] = useState({ top: 40, bottom: 38 });
  const [shadowSolid, setShadowSolid] = useState({ top: 10, bottom: 12 });
  const [enabledShadowProviders, setEnabledShadowProviders] = useState<string[]>(['youtube', 'bunny', 'vk', 'telegram', 'telegram-direct', 'rutube', 'google-drive']);
  const loadingSessionRef = useRef(false);
  const securitySuspendedRef = useRef(false);
  const domShieldsCleanupRef = useRef<(() => void) | null>(null);
  const reloadSessionRef = useRef<(() => void) | null>(null);
  const reloadActiveEmbedRef = useRef<(() => void) | null>(null);
  const activeSessionIdRef = useRef<string | null>(null);
  const sessionExpiresAtRef = useRef(0);
  const embedSessionRefreshCountRef = useRef(0);
  const loadingExtraWatchStatusRef = useRef(false);
  const requestingExtraRef = useRef(false);
  const approvedLoadAttemptedRef = useRef(false);
  const lastSeekTapRef = useRef<{ direction: SeekDirection; timestamp: number } | null>(null);
  const seekPointerStartRef = useRef<{ pointerId: number; x: number; y: number } | null>(null);
  const singleTapTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const seekFeedbackTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [seekFeedback, setSeekFeedback] = useState<SeekDirection | null>(null);

  const [isHoveringControls, setIsHoveringControls] = useState(false);
  const [isChapterInfoOpen, setIsChapterInfoOpen] = useState(false);
  const [isMindmapOpen, setIsMindmapOpen] = useState(false);

  useEffect(() => {
    isIOSDeviceRef.current = /iPad|iPhone|iPod/.test(navigator.userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  }, []);

  useEffect(() => {
    let active = true;
    apiClient.get('/public/settings').then(({ data }) => {
      if (!active) return;
      const top = Number(data?.playerShadowTopOpacity ?? data?.PlayerShadowTopOpacity ?? 0.70);
      const bottom = Number(data?.playerShadowBottomOpacity ?? data?.PlayerShadowBottomOpacity ?? 0.98);
      setShadowOpacity({ top: Math.min(1, Math.max(0, top)), bottom: Math.min(1, Math.max(0, bottom)) });
      
      const topCov = Number(data?.playerShadowTopCoverage ?? data?.PlayerShadowTopCoverage ?? 40);
      const bottomCov = Number(data?.playerShadowBottomCoverage ?? data?.PlayerShadowBottomCoverage ?? 38);
      setShadowCoverage({ top: Math.min(100, Math.max(0, topCov)), bottom: Math.min(100, Math.max(0, bottomCov)) });

      const topSol = Number(data?.playerShadowTopSolid ?? data?.PlayerShadowTopSolid ?? 10);
      const bottomSol = Number(data?.playerShadowBottomSolid ?? data?.PlayerShadowBottomSolid ?? 12);
      setShadowSolid({ top: Math.min(100, Math.max(0, topSol)), bottom: Math.min(100, Math.max(0, bottomSol)) });

      const providers = data?.enabledPlayerShadowProviders ?? data?.EnabledPlayerShadowProviders;
      if (typeof providers === 'string') {
        setEnabledShadowProviders(providers.toLowerCase().split(',').map(s => s.trim()).filter(Boolean));
      }

      youtubeShadowDelayMsRef.current = Math.min(60, Math.max(0, Number(data?.youTubePlayerShadowHideDelaySeconds ?? data?.YouTubePlayerShadowHideDelaySeconds ?? 5))) * 1000;
      bunnyShadowDelayMsRef.current = Math.min(60, Math.max(0, Number(data?.bunnyPlayerShadowHideDelaySeconds ?? data?.BunnyPlayerShadowHideDelaySeconds ?? 5))) * 1000;
    }).catch((error) => devConsole.error('Failed to load player appearance settings:', error));
    return () => { active = false; };
  }, []);

  const showPersistentPlayerShadows = useCallback(() => {
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
    shadowTimeoutRef.current = null;
    setShowPlayerShadows(true);
  }, []);

  const showTimedPlayerShadows = useCallback(() => {
    showPersistentPlayerShadows();
    const delay = providerRef.current === 'bunny' ? bunnyShadowDelayMsRef.current : youtubeShadowDelayMsRef.current;
    shadowTimeoutRef.current = setTimeout(() => {
      setShowPlayerShadows(false);
      shadowTimeoutRef.current = null;
    }, delay);
  }, [showPersistentPlayerShadows]);

  useEffect(() => () => {
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
  }, []);

  useEffect(() => () => {
    domShieldsCleanupRef.current?.();
    domShieldsCleanupRef.current = null;
    if (singleTapTimerRef.current) clearTimeout(singleTapTimerRef.current);
    if (seekFeedbackTimerRef.current) clearTimeout(seekFeedbackTimerRef.current);
    if (playFallbackTimeoutRef.current) clearTimeout(playFallbackTimeoutRef.current);
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
      setCanWatchAfterStatusRefresh(response.data?.data?.canWatch === true);
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
    if (extraWatchReqStatus !== 'Approved' && !canWatchAfterStatusRefresh) {
      approvedLoadAttemptedRef.current = false;
    }

    if (
      status === 'locked'
      && (extraWatchReqStatus === 'Approved' || canWatchAfterStatusRefresh)
      && !approvedLoadAttemptedRef.current
    ) {
      approvedLoadAttemptedRef.current = true;
      void loadVideo();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canWatchAfterStatusRefresh, extraWatchReqStatus, status]);

  const handleRequestExtra = async () => {
    const requestReason = extraWatchRequestReason.trim();
    if (!requestReason) {
      setExtraWatchRequestValidationError('اكتب سبب احتياجك لمشاهدة إضافية قبل إرسال الطلب.');
      return;
    }

    if (requestingExtraRef.current) return;
    requestingExtraRef.current = true;
    setRequestingExtra(true);
    setExtraWatchStatusError(null);
    try {
      await videoSessionService.requestExtraWatch(lessonVideoId, requestReason);
      setExtraWatchReqStatus('Pending');
      setExtraWatchRejectionReason(null);
      setShowExtraWatchRequestForm(false);
      setExtraWatchRequestReason('');
      setExtraWatchRequestValidationError('');
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
  const durationRef = useRef(0);
  const stableDurationRef = useRef<number | null>(null);
  const [currentTime, setCurrentTime] = useState(0);
  const currentTimeRef = useRef(0);
  const lastReportedMediaTimeRef = useRef(0);
  const lastMediaProgressAtRef = useRef(0);
  const consecutiveAdvancingMediaSamplesRef = useRef(0);
  const lastSeekCommandAtRef = useRef(0);
  const lastRenderedTimeUpdateAtRef = useRef(0);
  const flushTrackedProgressRef = useRef<(options?: ProgressFlushOptions) => Promise<void>>(async () => undefined);
  const pageExitProgressPromiseRef = useRef<Promise<void> | null>(null);
  const accrueTrackedPlaybackRef = useRef<(now?: number) => void>(() => undefined);
  const onWatchProgressRef = useRef(onWatchProgress);
  useEffect(() => { onWatchProgressRef.current = onWatchProgress; }, [onWatchProgress]);

  const consumeActiveSession = useCallback(() => {
    const sessionId = activeSessionIdRef.current;
    if (!sessionId || consumedSessionIdRef.current === sessionId) return;

    consumedSessionIdRef.current = sessionId;
    void videoSessionService.consumeSession(sessionId).catch((error) => {
      if (isCurrentVideoSession(sessionId, activeSessionIdRef.current)) {
        consumedSessionIdRef.current = null;
      }
      devConsole.error('Failed to consume video session after player became available:', error);
    });
  }, []);

  const applyStableDuration = useCallback((rawDuration: unknown) => {
    const stableDuration = resolveStableVideoDuration(stableDurationRef.current, rawDuration);
    if (stableDuration === null) return durationRef.current;

    // Bunny sessions carry the authoritative asset duration from our API. For
    // providers without server metadata, lock the first valid player duration.
    // Repeated HLS metadata callbacks can differ by a second and must not move
    // the watch threshold while the student is already watching.
    stableDurationRef.current = stableDuration;
    if (durationRef.current !== stableDuration) {
      durationRef.current = stableDuration;
      setDuration(stableDuration);
    }
    return stableDuration;
  }, []);

  const mountVideoEmbed = useCallback((sessionId: string) => {
    const playerContainer = containerRef.current;
    if (!playerContainer) return;

    domShieldsCleanupRef.current?.();
    playerContainer.replaceChildren();
    setNativeProviderSurfaceLoaded(false);

    const iframe = createVideoEmbedIframe(sessionId);
    iframeRef.current = iframe;
    playerContainer.appendChild(iframe);
    domShieldsCleanupRef.current = applyDomShields(playerContainer, () => {
      setStatus('error');
      setErrorMessage('تم اكتشاف محاولة تعديل المشغل. لإعادة المشاهدة، قم بتحديث الصفحة.');
    });
  }, []);

  const scheduleBunnyPlaybackRecovery = useCallback(() => {
    if (bunnyRecoveryTimerRef.current) return true;
    if (!canRetryBunnyPlayback(providerRef.current, bunnyRecoveryAttemptsRef.current)) {
      return false;
    }

    bunnyRecoveryAttemptsRef.current += 1;
    bunnyRecoveryResumeTimeRef.current = currentTimeRef.current;
    bunnyReadyAtRef.current = 0;
    embedReadinessWatchdogRef.current?.cancel();
    embedReadinessWatchdogRef.current = null;

    const failedIframe = iframeRef.current;
    iframeRef.current = null;
    domShieldsCleanupRef.current?.();
    domShieldsCleanupRef.current = null;
    if (failedIframe) {
      failedIframe.removeAttribute('src');
      failedIframe.src = 'about:blank';
      failedIframe.remove();
    }

    setErrorMessage('');
    accrueTrackedPlaybackRef.current();
    isPlayingRef.current = false;
    setIsPlaying(false);
    setIsBuffering(true);
    setStatus('loading');

    const retryDelayMs = bunnyRecoveryAttemptsRef.current * 750;
    bunnyRecoveryTimerRef.current = setTimeout(() => {
      bunnyRecoveryTimerRef.current = null;
      reloadActiveEmbedRef.current?.();
    }, retryDelayMs);
    return true;
  }, []);

  const loadActiveEmbed = useCallback((sessionId: string) => {
    embedReadinessWatchdogRef.current?.cancel();
    const watchdog = createBunnyBridgeReadinessWatchdog({
      schedule: (callback, delayMs) => window.setTimeout(callback, delayMs),
      cancelScheduled: (handle) => window.clearTimeout(handle),
      retryBridgeInPlace: () => {
        const embedWindow = iframeRef.current?.contentWindow;
        if (!embedWindow) return false;
        embedWindow.postMessage({ type: 'retryBridge' }, window.location.origin);
        return true;
      },
      recoverEmbed: () => {
        if (securitySuspendedRef.current) return;
        if (scheduleBunnyPlaybackRecovery()) return;
        setStatus('error');
        setErrorMessage('تعذر تحميل مشغل الفيديو بعد عدة محاولات. تحقق من الاتصال ثم اضغط «حاول مرة أخرى».');
      },
    });
    embedReadinessWatchdogRef.current = watchdog;
    watchdog.start();
    mountVideoEmbed(sessionId);
  }, [mountVideoEmbed, scheduleBunnyPlaybackRecovery]);

  useEffect(() => () => {
    embedReadinessWatchdogRef.current?.cancel();
    embedReadinessWatchdogRef.current = null;
    if (bunnyRecoveryTimerRef.current) clearTimeout(bunnyRecoveryTimerRef.current);
  }, []);

  const formatTime = (seconds: number) => {
    if (!seconds || isNaN(seconds)) return '0:00';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const sendCommand = useCallback((type: string, data?: Record<string, unknown>) => {
    if (securitySuspendedRef.current) return;
    if (iframeRef.current?.contentWindow) {
      iframeRef.current.contentWindow.postMessage({ type, ...data }, window.location.origin);
    }
  }, []);

  // ── PostMessage listener ──
  // Receives events from the embedded video page
  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return;
      if (event.source !== iframeRef.current?.contentWindow) return;
      const msg = event.data;
      if (!msg || msg.source !== 'video-embed') return;
      if (securitySuspendedRef.current && msg.type !== 'securityViolation') return;

      switch (msg.type) {
        case 'securityViolation': {
          securitySuspendedRef.current = true;
          domShieldsCleanupRef.current?.();
          domShieldsCleanupRef.current = null;
          trackingEnabledRef.current = false;
          progressSegmentsRef.current = [];
          fixedProgressRequestsRef.current = [];
          if (trackingInterval.current) {
            clearInterval(trackingInterval.current);
            trackingInterval.current = null;
          }
          embedReadinessWatchdogRef.current?.cancel();
          embedReadinessWatchdogRef.current = null;
          if (playFallbackTimeoutRef.current) {
            clearTimeout(playFallbackTimeoutRef.current);
            playFallbackTimeoutRef.current = null;
          }
          const playerIframe = iframeRef.current;
          iframeRef.current = null;
          if (playerIframe) {
            playerIframe.removeAttribute('src');
            playerIframe.src = 'about:blank';
            playerIframe.remove();
          }
          containerRef.current?.replaceChildren();
          isPlayingRef.current = false;
          setIsPlaying(false);
          setIsBuffering(false);
          setShowControls(true);
          showPersistentPlayerShadows();
          setStatus('protected');
          setErrorMessage('تم إيقاف تشغيل الفيديو لحماية المحتوى. أغلق أدوات المطوّر ثم أعد تحميل الصفحة للمتابعة.');
          break;
        }
        case 'providerLoaded': {
          const loadedProvider = String(msg.data?.provider || '').toLowerCase();
          if (loadedProvider === 'bunny') {
            // Treat iframe load only as a watchdog signal. A browser-generated
            // network error document can also fire load, so keep the platform
            // loader covering the nested frame until Bunny proves that its
            // media-clock bridge is ready.
            embedReadinessWatchdogRef.current?.markSurfaceLoaded();
            providerRef.current = loadedProvider;
            serverCanResolveDurationRef.current = true;
            setProvider(loadedProvider);
          } else if (loadedProvider === 'bunny-hls') {
            providerRef.current = loadedProvider;
            serverCanResolveDurationRef.current = true;
            setProvider(loadedProvider);
          }
          break;
        }
        case 'ready':
          embedSessionRefreshCountRef.current = 0;
          embedReadinessWatchdogRef.current?.markReady();
          embedReadinessWatchdogRef.current = null;
          consumeActiveSession();
          setStatus('ready');
          applyStableDuration(msg.data.duration);
          setVolume(msg.data.volume ?? 100);
          setIsMuted(msg.data.isMuted ?? false);
          const embedProvider = (msg.data.provider || 'youtube').toLowerCase();
          providerRef.current = embedProvider;
          serverCanResolveDurationRef.current = embedProvider === 'bunny' || embedProvider === 'bunny-hls';
          bunnyReadyAtRef.current = embedProvider === 'bunny' ? Date.now() : 0;
          setProvider(embedProvider);
          setNativeProviderSurfaceLoaded(embedProvider === 'bunny');
          setRequiresDirectPlayback(isIOSDeviceRef.current && embedProvider === 'youtube');
          showPersistentPlayerShadows();

          if (embedProvider === 'bunny') {
            const resumeTime = bunnyRecoveryResumeTimeRef.current;
            if (resumeTime > 0) {
              iframeRef.current?.contentWindow?.postMessage(
                { type: 'seekTo', time: resumeTime },
                window.location.origin,
              );
              iframeRef.current?.contentWindow?.postMessage(
                { type: 'play' },
                window.location.origin,
              );
              bunnyRecoveryResumeTimeRef.current = 0;
            }
            setIsBuffering(false);
            setShowControls(false);
          } else {
            setIsBuffering(true);
            // Browsers may reject autoplay. Stop covering the provider after a
            // bounded wait so the explicit play affordance remains reachable.
            if (playFallbackTimeoutRef.current) clearTimeout(playFallbackTimeoutRef.current);
            playFallbackTimeoutRef.current = setTimeout(() => {
              setIsBuffering(false);
              playFallbackTimeoutRef.current = null;
            }, 5000);
          }

          // Debug: Log VK player available methods
          if (msg.data.vkMethods) {
            devConsole.log('[SecureVideoPlayer] VK Player methods:', msg.data.vkMethods);
          }
          break;
        case 'qualityLevels': {
          const levels = Array.isArray(msg.data?.levels)
            ? msg.data.levels.filter((level: VideoQualityLevel) => level && typeof level.id === 'string' && typeof level.label === 'string')
            : [];
          setQualityLevels(levels);
          setCurrentQuality(typeof msg.data?.currentQuality === 'string' ? msg.data.currentQuality : 'auto');
          break;
        }
        case 'stateChange':
          if (msg.data.isPlaying) {
            isPlayingRef.current = true;
            consecutiveAdvancingMediaSamplesRef.current = 0;
            setIsPlaying(true);
            hasEndedRef.current = false;
            if (playFallbackTimeoutRef.current) {
              clearTimeout(playFallbackTimeoutRef.current);
              playFallbackTimeoutRef.current = null;
            }
            setRequiresDirectPlayback(false);
            setShowControls(false);
            setIsBuffering(false);
            showTimedPlayerShadows();
          } else {
            accrueTrackedPlaybackRef.current();
            isPlayingRef.current = false;
            setIsPlaying(false);
            if ((msg.data.state === 0 || msg.data.state === 'ended') && !hasEndedRef.current) {
              hasEndedRef.current = true;
              const endingSessionId = activeSessionIdRef.current;
              void flushTrackedProgressRef.current({ keepalive: true, drain: true }).then(() => {
                if (endingSessionId && isCurrentVideoSession(endingSessionId, activeSessionIdRef.current)) {
                  onEndedRef.current?.();
                }
              });
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
          if (playFallbackTimeoutRef.current) {
            clearTimeout(playFallbackTimeoutRef.current);
            playFallbackTimeoutRef.current = null;
          }
          setRequiresDirectPlayback(isIOSDeviceRef.current && (msg.data?.provider || providerRef.current) === 'youtube');
          isPlayingRef.current = false;
          setIsPlaying(false);
          setIsBuffering(false);
          setShowControls(true);
          showPersistentPlayerShadows();
          break;
        case 'timeUpdate':
          // Prevent rubber-banding: ignore stale time updates for 1.2 seconds after seeking
          if (Date.now() - lastSeekCommandAtRef.current < 1200) {
            break;
          }
          if (msg.data.currentTime !== undefined) {
            const reportedCurrentTime = Number(msg.data.currentTime);
            if (Number.isFinite(reportedCurrentTime)) {
              const nextMediaTime = Math.max(0, reportedCurrentTime);
              if (nextMediaTime > lastReportedMediaTimeRef.current + 0.01) {
                lastMediaProgressAtRef.current = Date.now();
                consecutiveAdvancingMediaSamplesRef.current += 1;
                // A native Bunny Play tap can happen before Player.js installs
                // its play listener on older WebViews. Two advancing media-clock
                // samples are stronger evidence than that missed event and let
                // tracking recover without crediting a single seek operation.
                if (
                  providerRef.current === 'bunny'
                  && !isPlayingRef.current
                  && consecutiveAdvancingMediaSamplesRef.current >= 2
                  && Date.now() - lastSeekCommandAtRef.current >= 1200
                ) {
                  isPlayingRef.current = true;
                  setIsPlaying(true);
                }
              } else if (nextMediaTime < lastReportedMediaTimeRef.current - 0.5) {
                consecutiveAdvancingMediaSamplesRef.current = 0;
              }
              lastReportedMediaTimeRef.current = nextMediaTime;
              currentTimeRef.current = nextMediaTime;
            }
            const reportedPlaybackRate = Number(msg.data.playbackRate);
            if (isSupportedVideoPlaybackRate(reportedPlaybackRate)) {
              if (reportedPlaybackRate !== playbackRateRef.current) {
                accrueTrackedPlaybackRef.current();
                void flushTrackedProgressRef.current();
              }
              playbackRateRef.current = reportedPlaybackRate;
            }
            if (
              providerRef.current === 'bunny'
              && isBunnyPlaybackStable(bunnyReadyAtRef.current, Date.now())
            ) {
              bunnyRecoveryAttemptsRef.current = 0;
            }
            const now = Date.now();
            if (now - lastRenderedTimeUpdateAtRef.current < 900) {
              break;
            }
            lastRenderedTimeUpdateAtRef.current = now;
            // Since time is confidently updating past the deadzone, we're definitely not buffering anymore!
            setIsBuffering(false);
            
            setCurrentTime(msg.data.currentTime);
            const stableDuration = applyStableDuration(msg.data.duration);
            if (stableDuration > 0) {
              setProgress((msg.data.currentTime / stableDuration) * 100);
            }
            if (typeof msg.data.volume === 'number') setVolume(msg.data.volume);
            if (typeof msg.data.isMuted === 'boolean') setIsMuted(msg.data.isMuted);
            if (onWatchProgressRef.current) {
              onWatchProgressRef.current(msg.data.currentTime);
            }
          }
          break;
        case 'playbackRateChange': {
          const nextPlaybackRate = Number(msg.data?.playbackRate);
          if (isSupportedVideoPlaybackRate(nextPlaybackRate) && nextPlaybackRate !== playbackRateRef.current) {
            accrueTrackedPlaybackRef.current();
            void flushTrackedProgressRef.current();
            playbackRateRef.current = nextPlaybackRate;
          }
          break;
        }
        case 'error':
          embedReadinessWatchdogRef.current?.cancel();
          embedReadinessWatchdogRef.current = null;
          if (msg.data?.message === 'Session expired or invalid' && embedSessionRefreshCountRef.current < 1) {
            embedSessionRefreshCountRef.current += 1;
            reloadSessionRef.current?.();
            break;
          }
          if (isBunnyPlaybackError(msg.data?.provider)) {
            providerRef.current = 'bunny';
            serverCanResolveDurationRef.current = true;
            if (scheduleBunnyPlaybackRecovery()) break;
            setStatus('error');
            setErrorMessage('تعذر تشغيل فيديو Bunny بعد عدة محاولات. تحقق من الاتصال ثم اضغط «حاول مرة أخرى».');
            break;
          }
          setStatus('error');
          setErrorMessage(msg.data?.message || 'تعذر تشغيل الفيديو. اضغط «حاول مرة أخرى» للمتابعة.');
          break;
        case 'overlayClick':
          if (statusRef.current === 'ready') {
            const willPlay = !msg.data?.isPlaying; // Or rely on togglePlay state
            sendCommand(willPlay ? 'play' : 'pause');
            if (willPlay) setIsBuffering(true);
          }
          break;
      }
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [applyStableDuration, consumeActiveSession, scheduleBunnyPlaybackRecovery, sendCommand, showPersistentPlayerShadows, showTimedPlayerShadows]);

  // ── Watch tracking ──
  const [viewTracked, setViewTracked] = useState(false);
  const viewTrackedRef = useRef(false);
  useEffect(() => { viewTrackedRef.current = viewTracked; }, [viewTracked]);

  const actualWatchedSeconds = useRef(0);
  const serverTrackedSecondsRef = useRef(0);
  const watchCountRef = useRef(0);
  const [displayedWatched, setDisplayedWatched] = useState(0);
  const progressSegmentsRef = useRef<VideoProgressSegment[]>([]);
  const fixedProgressRequestsRef = useRef<SequencedVideoProgressSegment[]>([]);
  const playbackRateRef = useRef(1);
  const progressDrainPromiseRef = useRef<Promise<void> | null>(null);
  const keepaliveProgressRequestedRef = useRef(false);
  const trackingEnabledRef = useRef(true);
  const consumedSessionIdRef = useRef<string | null>(null);
  const nextProgressSequenceRef = useRef(1);
  const activeProgressRequestRef = useRef<ActiveProgressRequest | null>(null);
  const trackingInterval = useRef<NodeJS.Timeout | null>(null);
  const lastTrackingTickAtRef = useRef(0);
  const [thresholdSeconds, setThresholdSeconds] = useState(60);
  const thresholdSecondsRef = useRef(60);

  const capWatchCount = useCallback((current: number, max: number) => {
    return max > 0 ? Math.min(current, max) : current;
  }, []);

  const stopSessionTracking = useCallback((nextStatus: 'error' | 'superseded', message?: string) => {
    activeProgressRequestRef.current = null;
    progressSegmentsRef.current = [];
    fixedProgressRequestsRef.current = [];
    keepaliveProgressRequestedRef.current = false;
    if (trackingInterval.current) clearInterval(trackingInterval.current);
    sendCommand('pause');
    isPlayingRef.current = false;
    setIsPlaying(false);
    if (message) setErrorMessage(message);
    setStatus(nextStatus);
  }, [sendCommand]);

  const getQueuedMediaSeconds = useCallback(
    () => sumVideoProgressMediaSeconds(progressSegmentsRef.current)
      + sumVideoProgressMediaSeconds(fixedProgressRequestsRef.current),
    [],
  );

  const resolveDisplayedProgress = useCallback((totalSeconds: number, currentCount: number, threshold: number) => {
    const safeThreshold = Math.max(1, threshold);
    return Math.min(safeThreshold, Math.max(0, totalSeconds - (currentCount * safeThreshold)));
  }, []);

  const applyProgressResponse = useCallback((progressResponse: WatchProgressResponse) => {
    const newThreshold = progressResponse.thresholdSeconds || 60;
    thresholdSecondsRef.current = newThreshold;
    setThresholdSeconds(newThreshold);
    const maxCount = progressResponse.maxCount ?? 0;
    const cappedCurrent = capWatchCount(progressResponse.currentCount, maxCount);
    watchCountRef.current = cappedCurrent;
    setWatchInfo(previous => ({
      current: cappedCurrent,
      max: maxCount || previous?.max || 0,
      isLocked: progressResponse.isLocked,
    }));
    const sessionHasRegisteredView = progressResponse.sessionHasRegisteredView
      ?? progressResponse.viewRegistered;
    if (progressResponse.isLocked || sessionHasRegisteredView) {
      progressSegmentsRef.current = [];
      fixedProgressRequestsRef.current = [];
      activeProgressRequestRef.current = null;
    }
    serverTrackedSecondsRef.current = progressResponse.totalTrackedSeconds;
    const refreshedExpiry = Date.parse(progressResponse.sessionExpiresAt);
    if (Number.isFinite(refreshedExpiry)) sessionExpiresAtRef.current = refreshedExpiry;
    actualWatchedSeconds.current = progressResponse.totalTrackedSeconds + getQueuedMediaSeconds();
    setDisplayedWatched(resolveDisplayedProgress(
      actualWatchedSeconds.current,
      cappedCurrent,
      newThreshold,
    ));
    if (sessionHasRegisteredView) {
      setViewTracked(true);
      viewTrackedRef.current = true;
    }
  }, [capWatchCount, getQueuedMediaSeconds, resolveDisplayedProgress]);

  const acknowledgeProgressResponse = useCallback((
    sessionId: string,
    sequence: number,
    progressResponse: WatchProgressResponse,
  ) => {
    if (!isCurrentVideoSession(sessionId, activeSessionIdRef.current)) return;
    if (activeProgressRequestRef.current?.sequence !== sequence) return;

    acknowledgeSequencedVideoProgressRequests(
      fixedProgressRequestsRef.current,
      new Set([sequence]),
    );
    activeProgressRequestRef.current = null;
    nextProgressSequenceRef.current = Math.max(nextProgressSequenceRef.current, sequence + 1);
    applyProgressResponse(progressResponse);
  }, [applyProgressResponse]);

  useEffect(() => {
    if (duration > 0) {
      const nextThreshold = resolveWatchThresholdSeconds(
        duration,
        watchThresholdPercentageRef.current,
      );
      thresholdSecondsRef.current = nextThreshold;
      setThresholdSeconds(nextThreshold);
      setDisplayedWatched(resolveDisplayedProgress(
        actualWatchedSeconds.current,
        watchCountRef.current,
        nextThreshold,
      ));
    }
  }, [duration, resolveDisplayedProgress]);

  const appendTrackedPlayback = useCallback((wallSeconds: number, playbackRate: number) => {
    if (
      viewTrackedRef.current
      || !Number.isFinite(wallSeconds)
      || wallSeconds <= 0
      || !isSupportedVideoPlaybackRate(playbackRate)
    ) {
      return;
    }

    appendVideoProgressSegment(progressSegmentsRef.current, wallSeconds, playbackRate);

    actualWatchedSeconds.current += wallSeconds * playbackRate;
    setDisplayedWatched(resolveDisplayedProgress(
      actualWatchedSeconds.current,
      watchCountRef.current,
      thresholdSecondsRef.current,
    ));
  }, [resolveDisplayedProgress]);

  const accrueTrackedPlayback = useCallback((now = performance.now()) => {
    const previousTick = lastTrackingTickAtRef.current;
    lastTrackingTickAtRef.current = now;
    if (previousTick <= 0 || !isPlayingRef.current) return;

    const mediaClockIsAdvancing = Date.now() - lastMediaProgressAtRef.current
      <= RECENT_MEDIA_PROGRESS_WINDOW_MS;
    if (!mediaClockIsAdvancing) return;

    const elapsedSeconds = Math.min(
      MAX_TRACKING_TICK_SECONDS,
      Math.max(0, (now - previousTick) / 1000),
    );
    appendTrackedPlayback(elapsedSeconds, playbackRateRef.current);
  }, [appendTrackedPlayback]);

  accrueTrackedPlaybackRef.current = accrueTrackedPlayback;

  const flushTrackedProgress = useCallback((options: ProgressFlushOptions = {}): Promise<void> => {
    if (!trackingEnabledRef.current || viewTrackedRef.current) return Promise.resolve();

    const sessionId = activeSessionIdRef.current;
    if (!sessionId) return Promise.resolve();

    const totalDurationSeconds = resolveProgressReportDurationSeconds(
      durationRef.current,
      serverCanResolveDurationRef.current,
    );
    if (totalDurationSeconds === null) return Promise.resolve();

    if (options.keepalive) keepaliveProgressRequestedRef.current = true;

    const pageExitDrain = pageExitProgressPromiseRef.current;
    if (pageExitDrain) {
      if (!options.drain) return pageExitDrain;
      return pageExitDrain.then(() => {
        if (
          !viewTrackedRef.current
          && isCurrentVideoSession(sessionId, activeSessionIdRef.current)
          && (
            activeProgressRequestRef.current
            || fixedProgressRequestsRef.current.length > 0
            || progressSegmentsRef.current.length > 0
          )
        ) {
          return flushTrackedProgressRef.current({ keepalive: options.keepalive, drain: true });
        }
      });
    }

    const existingDrain = progressDrainPromiseRef.current;
    if (existingDrain) {
      if (!options.drain) return existingDrain;
      return existingDrain.then(() => {
        if (
          !viewTrackedRef.current
          && isCurrentVideoSession(sessionId, activeSessionIdRef.current)
          && (
            activeProgressRequestRef.current
            || fixedProgressRequestsRef.current.length > 0
            || progressSegmentsRef.current.length > 0
          )
        ) {
          return flushTrackedProgressRef.current({ keepalive: options.keepalive, drain: true });
        }
      });
    }

    const drain = (async () => {
      while (
        trackingEnabledRef.current
        && !viewTrackedRef.current
        && isCurrentVideoSession(sessionId, activeSessionIdRef.current)
        && !pageExitProgressPromiseRef.current
      ) {
        if (!activeProgressRequestRef.current) {
          if (fixedProgressRequestsRef.current.length === 0) {
            nextProgressSequenceRef.current = materializeVideoProgressRequests(
              progressSegmentsRef.current,
              fixedProgressRequestsRef.current,
              nextProgressSequenceRef.current,
              TRACKING_FLUSH_INTERVAL_SECONDS,
              1,
            );
          }

          const firstRequest = fixedProgressRequestsRef.current[0];
          if (!firstRequest) break;
          activeProgressRequestRef.current = firstRequest;
        }

        const progressRequest = activeProgressRequestRef.current;
        try {
          let res;
          for (let attempt = 1; attempt <= TRACKING_RETRY_MAX_ATTEMPTS; attempt += 1) {
            try {
              res = await videoSessionService.trackProgress({
                lessonVideoId,
                sessionId,
                progressSequence: progressRequest.sequence,
                secondsWatched: progressRequest.seconds,
                playbackRate: progressRequest.playbackRate,
                totalDurationSeconds,
              }, { keepalive: keepaliveProgressRequestedRef.current });
              break;
            } catch (error) {
              const status = (error as { response?: { status?: number } }).response?.status;
              if ((status !== undefined && status < 500) || attempt === TRACKING_RETRY_MAX_ATTEMPTS) {
                throw error;
              }
              await new Promise<void>((resolve) => window.setTimeout(resolve, attempt * 250));
              if (
                pageExitProgressPromiseRef.current
                || activeProgressRequestRef.current?.sequence !== progressRequest.sequence
              ) {
                return;
              }
            }
          }
          if (!res) break;
          acknowledgeProgressResponse(sessionId, progressRequest.sequence, res.data.data);
        } catch (err) {
          if (!isCurrentVideoSession(sessionId, activeSessionIdRef.current)) return;
          const apiError = err as { response?: { data?: { errors?: string[] } } };
          const errors = apiError.response?.data?.errors ?? [];
          if (errors.includes('SESSION_SUPERSEDED')) {
            stopSessionTracking('superseded');
          } else if (errors.includes('SESSION_EXPIRED') || errors.includes('SESSION_INVALID')) {
            stopSessionTracking('error', 'انتهت جلسة تشغيل الفيديو. أعد تحميل الفيديو للمتابعة.');
          } else if (errors.includes('DURATION_REQUIRED')) {
            activeProgressRequestRef.current = null;
          }
          devConsole.error('Failed to sync progress:', err);
          break;
        }
      }
    })();

    progressDrainPromiseRef.current = drain;
    const releaseDrain = () => {
      if (progressDrainPromiseRef.current === drain) {
        progressDrainPromiseRef.current = null;
      }
      if (
        !activeProgressRequestRef.current
        && fixedProgressRequestsRef.current.length === 0
        && progressSegmentsRef.current.length === 0
      ) {
        keepaliveProgressRequestedRef.current = false;
      }
    };
    void drain.then(releaseDrain, releaseDrain);
    return drain;
  }, [acknowledgeProgressResponse, lessonVideoId, stopSessionTracking]);

  flushTrackedProgressRef.current = flushTrackedProgress;

  const flushProgressForPageExit = useCallback((): Promise<void> => {
    if (!trackingEnabledRef.current || viewTrackedRef.current) return Promise.resolve();

    const sessionId = activeSessionIdRef.current;
    if (!sessionId) return Promise.resolve();

    const existingBatch = pageExitProgressPromiseRef.current;
    if (existingBatch) return existingBatch;

    accrueTrackedPlaybackRef.current();
    const totalDurationSeconds = resolveProgressReportDurationSeconds(
      durationRef.current,
      serverCanResolveDurationRef.current,
    );
    if (totalDurationSeconds === null) return Promise.resolve();

    nextProgressSequenceRef.current = materializeVideoProgressRequests(
      progressSegmentsRef.current,
      fixedProgressRequestsRef.current,
      nextProgressSequenceRef.current,
      TRACKING_FLUSH_INTERVAL_SECONDS,
      TRACKING_BATCH_MAX_SEGMENTS,
    );

    const batchRequests = fixedProgressRequestsRef.current
      .slice(0, TRACKING_BATCH_MAX_SEGMENTS)
      .map((request) => ({ ...request }));
    if (batchRequests.length === 0) return Promise.resolve();

    keepaliveProgressRequestedRef.current = true;
    const acknowledgedSequences = new Set(batchRequests.map((request) => request.sequence));
    const delivery = videoSessionService.trackProgressBatch({
      lessonVideoId,
      sessionId,
      totalDurationSeconds,
      progressSegments: batchRequests.map((request) => ({
        progressSequence: request.sequence,
        secondsWatched: request.seconds,
        playbackRate: request.playbackRate,
      })),
    }, { keepalive: true });

    const batchPromise = delivery.then((response) => {
      if (!isCurrentVideoSession(sessionId, activeSessionIdRef.current)) return;

      acknowledgeSequencedVideoProgressRequests(
        fixedProgressRequestsRef.current,
        acknowledgedSequences,
      );
      if (
        activeProgressRequestRef.current
        && acknowledgedSequences.has(activeProgressRequestRef.current.sequence)
      ) {
        activeProgressRequestRef.current = null;
      }
      applyProgressResponse(response.data.data);
    }).catch((error) => {
      devConsole.error('Failed to sync page-exit progress batch:', error);
    });

    pageExitProgressPromiseRef.current = batchPromise;
    const releaseBatch = () => {
      if (pageExitProgressPromiseRef.current !== batchPromise) return;
      pageExitProgressPromiseRef.current = null;
      if (
        typeof document !== 'undefined'
        && document.visibilityState !== 'hidden'
        && isCurrentVideoSession(sessionId, activeSessionIdRef.current)
        && (
          activeProgressRequestRef.current
          || fixedProgressRequestsRef.current.length > 0
          || progressSegmentsRef.current.length > 0
        )
      ) {
        void flushTrackedProgressRef.current();
      }
    };
    void batchPromise.then(releaseBatch, releaseBatch);
    return batchPromise;
  }, [applyProgressResponse, lessonVideoId]);

  useEffect(() => {
    if (status !== 'ready' || !trackingEnabledRef.current) return;

    if (trackingInterval.current) clearInterval(trackingInterval.current);
    lastTrackingTickAtRef.current = performance.now();

    trackingInterval.current = setInterval(() => {
      accrueTrackedPlayback();
      const targetSeconds = (watchCountRef.current + 1) * thresholdSecondsRef.current;
      const queuedWallSeconds = sumVideoProgressWallSeconds(progressSegmentsRef.current)
        + sumVideoProgressWallSeconds(fixedProgressRequestsRef.current);
      if (
        actualWatchedSeconds.current >= targetSeconds
        || queuedWallSeconds >= TRACKING_FLUSH_INTERVAL_SECONDS
      ) {
        void flushTrackedProgress();
      }
    }, 250);

    return () => {
      accrueTrackedPlaybackRef.current();
      if (trackingInterval.current) clearInterval(trackingInterval.current);
      trackingInterval.current = null;
      lastTrackingTickAtRef.current = 0;
    };
  }, [accrueTrackedPlayback, flushTrackedProgress, status]);

  useEffect(() => {
    if (!isPlaying) {
      void flushTrackedProgress();
    }
  }, [flushTrackedProgress, isPlaying]);

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        void flushProgressForPageExit();
      } else {
        const pendingBatch = pageExitProgressPromiseRef.current;
        if (pendingBatch) {
          void pendingBatch.then(() => flushTrackedProgressRef.current());
        } else {
          void flushTrackedProgressRef.current();
        }
      }
    };

    const handlePageExit = () => {
      void flushProgressForPageExit();
    };

    const handlePageShow = (event: PageTransitionEvent) => {
      lastTrackingTickAtRef.current = performance.now();
      if (
        event.persisted
        && sessionExpiresAtRef.current > 0
        && sessionExpiresAtRef.current <= Date.now()
      ) {
        reloadSessionRef.current?.();
        return;
      }

      if (event.persisted) {
        const pendingBatch = pageExitProgressPromiseRef.current;
        if (pendingBatch) {
          void pendingBatch.then(() => flushTrackedProgressRef.current());
        } else {
          void flushTrackedProgressRef.current();
        }
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('pagehide', handlePageExit);
    window.addEventListener('pageshow', handlePageShow);
    window.addEventListener('beforeunload', handlePageExit);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('pagehide', handlePageExit);
      window.removeEventListener('pageshow', handlePageShow);
      window.removeEventListener('beforeunload', handlePageExit);
      void flushProgressForPageExit();
    };
  }, [flushProgressForPageExit]);

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
    if (!chapters || chapters.length === 0) return undefined;
    const timelineDuration = duration > 0
      ? duration
      : Math.max(1, ...chapters.map((chapter) => chapter.endTime));
    return chapters.map(ch => ({
      id: ch.id,
      title: ch.title,
      summaryText: ch.summaryText,
      mindmapImageUrl: ch.mindmapImageUrl,
      startTime: ch.startTime,
      endTime: ch.endTime,
      startPercent: (Math.max(0, ch.startTime) / timelineDuration) * 100,
      endPercent: (Math.min(timelineDuration, ch.endTime) / timelineDuration) * 100
    }));
  }, [chapters, duration]);

  // ── Load video ──
  const loadVideo = async () => {
    if (bunnyRecoveryVideoIdRef.current !== lessonVideoId) {
      bunnyRecoveryVideoIdRef.current = lessonVideoId;
      bunnyRecoveryAttemptsRef.current = 0;
      bunnyRecoveryResumeTimeRef.current = 0;
      currentTimeRef.current = 0;
      setCurrentTime(0);
      setProgress(0);
    }
    if (securitySuspendedRef.current) {
      setStatus('protected');
      return;
    }
    if (loadingSessionRef.current) return;
    loadingSessionRef.current = true;
    try {
      setStatus('loading');
      durationRef.current = 0;
      stableDurationRef.current = null;
      setDuration(0);
      thresholdSecondsRef.current = 60;
      setThresholdSeconds(60);
      lastReportedMediaTimeRef.current = 0;
      lastMediaProgressAtRef.current = 0;
      consecutiveAdvancingMediaSamplesRef.current = 0;
      lastTrackingTickAtRef.current = 0;
      playbackRateRef.current = 1;
      serverCanResolveDurationRef.current = false;
      isPlayingRef.current = false;
      setIsPlaying(false);

      activeSessionIdRef.current = null;
      const response = await createVideoSessionWithRetry(lessonVideoId);
      const session = response.data.data;
      trackingEnabledRef.current = !session.isPreview;
      activeSessionIdRef.current = session.sessionId;
      const sessionExpiry = Date.parse(session.expiresAt);
      sessionExpiresAtRef.current = Number.isFinite(sessionExpiry) ? sessionExpiry : 0;
      consumedSessionIdRef.current = null;
      nextProgressSequenceRef.current = 1;
      activeProgressRequestRef.current = null;
      progressSegmentsRef.current = [];
      fixedProgressRequestsRef.current = [];
      progressDrainPromiseRef.current = null;
      pageExitProgressPromiseRef.current = null;
      keepaliveProgressRequestedRef.current = false;
      const thresholdPercentage = session.thresholdPercentage || watchThresholdPercentageRef.current;
      watchThresholdPercentageRef.current = thresholdPercentage;
      const knownDurationSeconds = resolveTrackableDurationSeconds(Number(session.durationSeconds));
      const knownThresholdSeconds = knownDurationSeconds === null
        ? 60
        : resolveWatchThresholdSeconds(knownDurationSeconds, thresholdPercentage);
      if (knownDurationSeconds !== null) {
        stableDurationRef.current = knownDurationSeconds;
        durationRef.current = knownDurationSeconds;
        setDuration(knownDurationSeconds);
        thresholdSecondsRef.current = knownThresholdSeconds;
        setThresholdSeconds(knownThresholdSeconds);
      }
      const sessionMaxCount = session.watchInfo.maxCount ?? 0;
      const sessionCurrentCount = capWatchCount(session.watchInfo.currentCount ?? 0, sessionMaxCount);
      watchCountRef.current = sessionCurrentCount;
      setWatchInfo({
        current: sessionCurrentCount,
        max: sessionMaxCount,
        isLocked: session.watchInfo.isLocked
      });
      serverTrackedSecondsRef.current = session.watchInfo.totalTrackedSeconds ?? 0;
      actualWatchedSeconds.current = serverTrackedSecondsRef.current;
      setDisplayedWatched(resolveDisplayedProgress(
        actualWatchedSeconds.current,
        sessionCurrentCount,
        knownThresholdSeconds,
      ));
      setViewTracked(false);
      viewTrackedRef.current = false;

      if (session.watchInfo.isLocked) {
        setStatus('locked');
        return;
      }

      if (securitySuspendedRef.current) {
        setStatus('protected');
        return;
      }

      const providerName = session.provider?.toLowerCase() || 'youtube';
      providerRef.current = providerName;
      serverCanResolveDurationRef.current = providerName === 'bunny' || providerName === 'bunny-hls';
      setProvider(providerName);
      setQualityLevels([]);
      setCurrentQuality('auto');
      loadActiveEmbed(session.sessionId);

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
      if (errors.includes('BUNNY_VIDEO_NOT_READY')) {
        providerRef.current = 'bunny';
        serverCanResolveDurationRef.current = true;
        if (scheduleBunnyPlaybackRecovery()) return;
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

  reloadSessionRef.current = () => { void loadVideo(); };
  reloadActiveEmbedRef.current = () => {
    const sessionId = activeSessionIdRef.current;
    if (!sessionId) {
      reloadSessionRef.current?.();
      return;
    }
    setStatus('loading');
    loadActiveEmbed(sessionId);
  };

  useEffect(() => {
    if (status === 'idle' && !isExamLocked) void loadVideo();
    // A new video id creates a fresh secured session automatically.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isExamLocked, lessonVideoId, status]);

  // ── Player controls (send commands to iframe via postMessage) ──
  const togglePlay = () => {
    sendCommand(isPlaying ? 'pause' : 'play');
    if (!isPlaying) {
      setIsBuffering(true);
    }
  };

  const showSeekFeedback = (direction: SeekDirection) => {
    setSeekFeedback(direction);
    if (seekFeedbackTimerRef.current) clearTimeout(seekFeedbackTimerRef.current);
    seekFeedbackTimerRef.current = setTimeout(() => {
      setSeekFeedback(null);
      seekFeedbackTimerRef.current = null;
    }, 650);
  };

  const seekByDoubleTap = (direction: SeekDirection) => {
    accrueTrackedPlaybackRef.current();
    void flushTrackedProgressRef.current();
    const targetTime = resolveSeekTarget(currentTimeRef.current, durationRef.current, direction);
    lastSeekCommandAtRef.current = Date.now();
    lastTrackingTickAtRef.current = performance.now();
    consecutiveAdvancingMediaSamplesRef.current = 0;
    currentTimeRef.current = targetTime;
    lastReportedMediaTimeRef.current = targetTime;
    sendCommand('seekTo', { time: targetTime });
    setCurrentTime(targetTime);
    if (durationRef.current > 0) setProgress((targetTime / durationRef.current) * 100);
    showSeekFeedback(direction);
  };

  const cancelSingleTapAction = () => {
    if (!singleTapTimerRef.current) return;
    clearTimeout(singleTapTimerRef.current);
    singleTapTimerRef.current = null;
  };

  const queueSingleTapAction = () => {
    cancelSingleTapAction();
    singleTapTimerRef.current = setTimeout(() => {
      lastSeekTapRef.current = null;
      singleTapTimerRef.current = null;
      if (usesNativeProviderControls(providerRef.current)) togglePlay();
    }, DOUBLE_TAP_WINDOW_MS);
  };

  const handleSeekTap = (
    direction: SeekDirection,
    event: React.PointerEvent<HTMLDivElement>,
  ) => {
    if (!event.isPrimary) return;
    if (event.pointerType === 'mouse' && event.button !== 0) return;
    const pointerStart = seekPointerStartRef.current;
    seekPointerStartRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    if (!pointerStart || pointerStart.pointerId !== event.pointerId) return;
    if (Math.hypot(event.clientX - pointerStart.x, event.clientY - pointerStart.y) > 24) return;
    event.stopPropagation();
    handlePlayerInteraction();

    const currentTap = { direction, timestamp: Date.now() };
    if (isDoubleTapSeek(lastSeekTapRef.current, currentTap)) {
      cancelSingleTapAction();
      lastSeekTapRef.current = null;
      seekByDoubleTap(direction);
      return;
    }

    lastSeekTapRef.current = currentTap;
    queueSingleTapAction();
  };

  const cancelSeekTap = (event: React.PointerEvent<HTMLDivElement>) => {
    seekPointerStartRef.current = null;
    lastSeekTapRef.current = null;
    cancelSingleTapAction();
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  const handleSeek = (percent: number) => {
    if (duration === 0) return;
    accrueTrackedPlaybackRef.current();
    void flushTrackedProgressRef.current();
    const targetTime = (percent / 100) * duration;
    lastSeekCommandAtRef.current = Date.now();
    lastTrackingTickAtRef.current = performance.now();
    consecutiveAdvancingMediaSamplesRef.current = 0;
    currentTimeRef.current = targetTime;
    lastReportedMediaTimeRef.current = targetTime;
    sendCommand('seekTo', { time: targetTime });
    sendCommand('play');
    setCurrentTime(targetTime);
    setProgress(percent);
    setIsBuffering(true);
  };

  const handleVolumeChange = (vol: number) => {
    if (vol > 0 && isMuted) {
      sendCommand('unmute');
      setIsMuted(false);
    }
    sendCommand('setVolume', { volume: vol });
    setVolume(vol);
  };

  const toggleMute = () => {
    if (isMuted) {
      sendCommand('unmute');
      sendCommand('setVolume', { volume });
      setIsMuted(false);
    } else {
      sendCommand('mute');
      setIsMuted(true);
    }
  };

  const [isPseudoFullscreen, setIsPseudoFullscreen] = useState(false);
  const [isNativeFullscreen, setIsNativeFullscreen] = useState(false);
  const [rotateLandscapeFallback, setRotateLandscapeFallback] = useState(false);

  useEffect(() => {
    if (!isPseudoFullscreen) return;

    document.body.classList.add('secure-video-fullscreen-open');
    const fullscreenRoot = containerRef.current?.parentElement?.parentElement;
    const adjustedAncestors: HTMLElement[] = [];
    let ancestor = fullscreenRoot?.parentElement;
    while (ancestor && ancestor !== document.body) {
      ancestor.classList.add('secure-video-fullscreen-ancestor');
      adjustedAncestors.push(ancestor);
      ancestor = ancestor.parentElement;
    }

    return () => {
      document.body.classList.remove('secure-video-fullscreen-open');
      adjustedAncestors.forEach((element) => element.classList.remove('secure-video-fullscreen-ancestor'));
    };
  }, [isPseudoFullscreen]);

  const resetFullscreenState = useCallback(() => {
    setIsPseudoFullscreen(false);
    setIsNativeFullscreen(false);
    setRotateLandscapeFallback(false);
    unlockVideoOrientation(window.screen);
  }, []);

  useEffect(() => {
    const handleFullscreenChange = () => {
      const active = Boolean(getFullscreenElement(document));
      setIsNativeFullscreen(active);
      if (active) {
        // WebKit may resolve requestFullscreen before it exposes the active
        // element. If the fallback was already scheduled, native mode wins.
        setIsPseudoFullscreen(false);
        return;
      }
      if (!active && !isPseudoFullscreen) {
        setRotateLandscapeFallback(false);
        unlockVideoOrientation(window.screen);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && isPseudoFullscreen) resetFullscreenState();
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
      window.removeEventListener('keydown', handleKeyDown);
      unlockVideoOrientation(window.screen);
    };
  }, [isPseudoFullscreen, resetFullscreenState]);

  const enterFullscreen = useCallback(async (element: HTMLElement) => {
    const fullscreenRequestResolved = await requestVideoFullscreen(element);
    const enteredNativeFullscreen = fullscreenRequestResolved
      ? await waitForVideoFullscreen(document)
      : false;
    setIsNativeFullscreen(enteredNativeFullscreen);
    if (!enteredNativeFullscreen) setIsPseudoFullscreen(true);

    const landscapeLocked = enteredNativeFullscreen
      ? await lockVideoToLandscape(window.screen)
      : false;
    const viewportIsPortrait = window.matchMedia('(orientation: portrait)').matches;
    setRotateLandscapeFallback(viewportIsPortrait && !landscapeLocked);
  }, []);

  const toggleFullscreen = async () => {
    const el = containerRef.current?.parentElement;
    if (!el) return;

    if (isPseudoFullscreen) {
      resetFullscreenState();
      return;
    }

    if (!getFullscreenElement(document)) {
      await enterFullscreen(el);
    } else {
      const exitedFullscreen = await exitVideoFullscreen(document);
      if (exitedFullscreen) resetFullscreenState();
    }
  };

  const fullscreenActive = isPseudoFullscreen || isNativeFullscreen;

  const handlePlaybackRateChange = (rate: number) => {
    accrueTrackedPlaybackRef.current();
    void flushTrackedProgress();
    playbackRateRef.current = rate;
    sendCommand('setPlaybackRate', { rate });
  };

  const handleQualityChange = (quality: string) => {
    setCurrentQuality(quality);
    sendCommand('setQuality', { quality });
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

  // A mind map is a lesson aid, not a control that should disappear until the
  // playback clock reaches one specific chapter. Fall back to the first ready
  // map so students can always open it from the player.
  const activeMindmapChapter = React.useMemo(() => {
    if (!normalizedChapters?.length) return null;
    return activeChapterDesktop?.mindmapImageUrl
      ? activeChapterDesktop
      : normalizedChapters.find((chapter) => Boolean(chapter.mindmapImageUrl)) ?? null;
  }, [activeChapterDesktop, normalizedChapters]);

  const usesNativePlayerChrome = usesNativeProviderControls(provider);

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
              className="px-6 py-3 bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] border border-[var(--admin-primary)] text-[var(--admin-primary-contrast)] font-bold rounded-lg transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-black min-w-[200px]"
            >
              اذهب للامتحان
            </button>
            <button 
              type="button"
              onClick={() => router.push(`/student/exams/${blockingExamId}?packageId=${packageId}&lessonId=${lessonId}`)}
              className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 text-white font-bold rounded-lg transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-black min-w-[200px]"
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
        <div className="absolute inset-0 bg-black/40 z-30 flex items-center justify-center transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 pointer-events-auto">
          <div className="flex h-20 w-20 cursor-pointer items-center justify-center rounded-full bg-[#0A1D3D] text-white shadow-lg transition-transform duration-200 group-hover:scale-105 group-active:scale-95">
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
                className="px-6 py-3 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 flex items-center justify-center min-w-[200px] shadow-lg shadow-emerald-600/20 hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50"
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
                  onClick={() => {
                    setExtraWatchRequestValidationError('');
                    setShowExtraWatchRequestForm(true);
                  }}
                  disabled={requestingExtra}
                  className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 text-white font-bold rounded-lg transition-colors flex items-center justify-center min-w-[200px] disabled:opacity-50"
               >
                  {requestingExtra ? 'جاري الطلب...' : 'طلب مشاهدة إضافية'}
               </button>
            )}
          </div>
        </div>

        <AnimatePresence>
          {showExtraWatchRequestForm && (
            <motion.div
              className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              role="presentation"
              onMouseDown={() => !requestingExtra && setShowExtraWatchRequestForm(false)}
            >
              <motion.form
                dir="rtl"
                className="w-full max-w-md rounded-2xl bg-white p-6 text-right shadow-xl"
                initial={{ opacity: 0, scale: 0.98, y: 12 }}
                animate={{ opacity: 1, scale: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.98, y: 12 }}
                transition={{ duration: 0.2 }}
                onMouseDown={(event) => event.stopPropagation()}
                onSubmit={(event) => {
                  event.preventDefault();
                  void handleRequestExtra();
                }}
              >
                <h3 className="text-lg font-bold text-slate-900">سبب طلب المشاهدة الإضافية</h3>
                <p className="mt-2 text-sm leading-relaxed text-slate-600">
                  وضّح سبب احتياجك لمشاهدة إضافية، ليتمكن فريق الدعم من مراجعة طلبك.
                </p>
                <label htmlFor="extra-watch-request-reason" className="mt-5 block text-sm font-bold text-slate-800">
                  السبب <span className="text-rose-600">*</span>
                </label>
                <textarea
                  id="extra-watch-request-reason"
                  value={extraWatchRequestReason}
                  onChange={(event) => {
                    setExtraWatchRequestReason(event.target.value);
                    if (event.target.value.trim()) setExtraWatchRequestValidationError('');
                  }}
                  maxLength={1000}
                  rows={4}
                  required
                  autoFocus
                  placeholder="مثال: أحتاج مراجعة هذه الجزئية قبل الامتحان."
                  className={`mt-2 w-full resize-none rounded-xl border bg-slate-50 p-3 text-sm text-slate-900 outline-none transition focus:ring-2 ${extraWatchRequestValidationError ? 'border-rose-500 focus:ring-rose-200' : 'border-slate-300 focus:border-teal-700 focus:ring-teal-100'}`}
                />
                {extraWatchRequestValidationError && <p className="mt-2 text-xs font-semibold text-rose-600">{extraWatchRequestValidationError}</p>}
                <div className="mt-5 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setShowExtraWatchRequestForm(false)}
                    disabled={requestingExtra}
                    className="min-h-11 rounded-xl px-4 text-sm font-bold text-slate-700 transition hover:bg-slate-100 disabled:opacity-50"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={requestingExtra}
                    className="min-h-11 rounded-xl bg-teal-700 px-5 text-sm font-bold text-white transition hover:bg-teal-800 disabled:opacity-50"
                  >
                    {requestingExtra ? 'جاري الإرسال...' : 'إرسال الطلب'}
                  </button>
                </div>
              </motion.form>
            </motion.div>
          )}
        </AnimatePresence>

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

  if (status === 'protected') {
    return (
      <div className={`relative flex aspect-video w-full flex-col items-center justify-center overflow-hidden rounded-lg border border-amber-500/30 bg-black p-8 text-center ${className}`} role="alert">
        <AlertCircle className="mb-4 h-12 w-12 text-amber-400" />
        <h3 className="mb-2 text-xl font-bold text-white">تم إيقاف تشغيل الفيديو</h3>
        <p className="max-w-md text-gray-300">{errorMessage}</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-6 min-h-11 rounded-md bg-[var(--admin-primary)] px-6 font-bold text-[var(--admin-primary-contrast)] transition-opacity hover:opacity-90"
        >
          أعد تحميل الصفحة بعد إغلاق أدوات المطوّر
        </button>
      </div>
    );
  }

  if (status === 'error') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-lg overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`} role="alert">
        <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
        <h3 className="text-xl font-bold text-white mb-2">عذراً، حدث خطأ</h3>
        <p className="text-gray-300">{errorMessage}</p>
        <button
          type="button"
          onClick={() => {
            bunnyRecoveryAttemptsRef.current = 0;
            void loadVideo();
          }}
          className="mt-6 min-h-11 rounded-md bg-[var(--admin-primary)] px-6 font-bold text-[var(--admin-primary-contrast)] transition-opacity hover:opacity-90"
        >
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
    <div className={`group flex min-h-0 w-full flex-col overflow-hidden rounded-xl border border-[var(--secondary)]/30 bg-black shadow-lg ${className} ${isPseudoFullscreen ? 'secure-video-pseudo-fullscreen !fixed !inset-0 !z-[var(--z-modal)] !rounded-none' : ''} ${rotateLandscapeFallback ? 'secure-video-force-landscape' : ''}`}>
      
      {/* Video Container */}
      <div 
        className={`secure-video-fullscreen-surface relative min-h-0 w-full shrink aspect-video cursor-pointer overflow-hidden rounded-xl bg-black ${rotateLandscapeFallback ? 'secure-video-force-landscape' : ''}`}
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

        {status === 'ready' && (
          <div
            className="pointer-events-none absolute inset-x-0 bottom-[22%] top-0 z-[var(--z-overlay-content)] flex touch-manipulation"
            aria-hidden="true"
            dir="ltr"
          >
            <div
              className="pointer-events-auto h-full w-[12.5%] min-w-11 max-w-16 touch-manipulation select-none"
              onPointerDown={(event) => {
                if (!event.isPrimary) return;
                event.currentTarget.setPointerCapture(event.pointerId);
                seekPointerStartRef.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY };
              }}
              onPointerUp={(event) => handleSeekTap('backward', event)}
              onPointerCancel={cancelSeekTap}
              onClick={(event) => event.stopPropagation()}
            />
            <div className="h-full flex-1" />
            <div
              className="pointer-events-auto h-full w-[12.5%] min-w-11 max-w-16 touch-manipulation select-none"
              onPointerDown={(event) => {
                if (!event.isPrimary) return;
                event.currentTarget.setPointerCapture(event.pointerId);
                seekPointerStartRef.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY };
              }}
              onPointerUp={(event) => handleSeekTap('forward', event)}
              onPointerCancel={cancelSeekTap}
              onClick={(event) => event.stopPropagation()}
            />
          </div>
        )}

        {seekFeedback && (
          <div
            className={`pointer-events-none absolute top-1/2 z-[var(--z-floating)] -translate-y-1/2 rounded-full bg-black/70 px-4 py-3 text-center text-sm font-black text-white ${seekFeedback === 'forward' ? 'right-[12%]' : 'left-[12%]'}`}
            aria-hidden="true"
          >
            {seekFeedback === 'forward' ? '+' : '−'}{DOUBLE_TAP_SEEK_SECONDS} ث
          </div>
        )}

        {status === 'ready' && usesNativePlayerChrome && (
          <>
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                void toggleFullscreen();
              }}
              className="absolute left-3 top-3 z-[var(--z-modal)] flex size-11 items-center justify-center rounded-full border border-white/20 bg-black/70 text-white shadow-lg backdrop-blur-sm transition hover:bg-black/85 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white sm:left-4 sm:top-4"
              aria-label={fullscreenActive ? 'الخروج من ملء الشاشة' : 'عرض الفيديو بملء الشاشة أفقيًا'}
              title={fullscreenActive ? 'الخروج من ملء الشاشة' : 'ملء الشاشة أفقيًا'}
            >
              {fullscreenActive ? <Minimize2 className="size-5" /> : <Maximize2 className="size-5" />}
            </button>
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                void toggleFullscreen();
              }}
              className="absolute bottom-0 right-0 z-[var(--z-modal)] size-14 bg-transparent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-white"
              aria-label={fullscreenActive ? 'الخروج من ملء الشاشة' : 'عرض الفيديو بملء الشاشة أفقيًا'}
              title={fullscreenActive ? 'الخروج من ملء الشاشة' : 'ملء الشاشة أفقيًا'}
            />
          </>
        )}

        {/* Shadow Gradient Overlay */}
        <AnimatePresence>
          {status === 'ready' && !usesNativePlayerChrome && showPlayerShadows && enabledShadowProviders.includes(provider.toLowerCase()) && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.3 }}
              className="pointer-events-none absolute inset-0 z-[var(--z-overlay)]"
              style={{
                background: `linear-gradient(to bottom, rgba(0,0,0,${shadowOpacity.top}) 0%, rgba(0,0,0,${shadowOpacity.top}) ${Math.min(shadowSolid.top, shadowCoverage.top)}%, transparent ${shadowCoverage.top}%, transparent ${100 - shadowCoverage.bottom}%, rgba(0,0,0,${shadowOpacity.bottom}) ${100 - Math.min(shadowSolid.bottom, shadowCoverage.bottom)}%, rgba(0,0,0,${shadowOpacity.bottom}) 100%)`
              }}
            />
          )}
        </AnimatePresence>
        
        {/* Floating Chapter Info Overlay */}
        {activeChapterDesktop && activeChapterDesktop.summaryText && status === 'ready' && !usesNativePlayerChrome && (showControls || isChapterInfoOpen) && (
          <div 
            className="absolute top-4 right-4 bottom-16 z-[var(--z-floating)] flex flex-col items-end pointer-events-none"
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
                   className="pointer-events-auto flex min-h-11 min-w-11 shrink-0 cursor-pointer items-center justify-center rounded-xl border border-white/10 bg-black/60 text-white shadow-sm backdrop-blur transition hover:bg-[var(--admin-primary)]"
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
                   className="pointer-events-auto bg-black/70 backdrop-blur-md border border-[var(--admin-primary)]/30 rounded-2xl p-6 w-[280px] sm:w-[350px] h-full overflow-y-auto custom-scrollbar shadow-sm relative flex flex-col"
                 >
                    <button 
                      type="button"
                      onClick={() => setIsChapterInfoOpen(false)} 
                      className="absolute left-2 top-2 z-10 flex min-h-11 min-w-11 items-center justify-center rounded-full bg-white/5 text-white/50 transition hover:bg-white/10 hover:text-red-400"
                      aria-label="إغلاق معلومات الفصل"
                    >
                       <X className="w-4 h-4" />
                    </button>
                    <div className="w-full text-start" dir="auto">
                      <SplitText
                        key={`title-${activeChapterDesktop.id}`}
                        text={activeChapterDesktop.title}
                        tag="h4"
                        className="mb-2 ml-6 block text-sm font-black text-white"
                        textAlign="start"
                        splitType="words"
                      />
                      <SplitText
                        key={`summary-${activeChapterDesktop.id}`}
                        text={activeChapterDesktop.summaryText}
                        tag="p"
                        className="block text-xs leading-relaxed text-white/90 sm:text-sm"
                        textAlign="start"
                        splitType="words"
                        delay={20}
                      />
                    </div>
                 </motion.div>
               )}
             </AnimatePresence>
          </div>
        )}

        {/* Floating Mindmap Overlay */}
        {/* Keep the lesson aid reachable while the video is playing. Player controls
            intentionally auto-hide, but that must not hide the mind-map trigger. */}
        {activeMindmapChapter && status === 'ready' && !usesNativePlayerChrome && (
          <div 
            className="pointer-events-none absolute left-3 top-3 z-[var(--z-floating)] flex flex-col items-start sm:left-4 sm:top-4"
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
                   className="pointer-events-auto flex min-h-11 min-w-11 shrink-0 cursor-pointer items-center justify-center rounded-xl border border-white/10 bg-black/60 px-3 text-white shadow-sm backdrop-blur transition hover:bg-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-60 sm:px-4"
                   aria-label="فتح الخريطة الذهنية للفصل"
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
                   className="pointer-events-auto bg-black/70 backdrop-blur-md border border-[var(--admin-primary)]/30 rounded-2xl p-6 w-[280px] sm:w-[500px] h-full overflow-hidden shadow-sm relative flex flex-col"
                 >
                    <button 
                      type="button"
                      onClick={() => setIsMindmapOpen(false)} 
                      className="absolute right-2 top-2 z-10 flex min-h-11 min-w-11 items-center justify-center rounded-full bg-white/5 text-white/50 transition hover:bg-white/10 hover:text-red-400"
                      aria-label="إغلاق الخريطة الذهنية"
                    >
                       <X className="w-4 h-4" />
                    </button>
                    <h4 className="mb-4 block ps-6 text-right text-sm font-black text-white" dir="rtl">
                      <span>الخريطة الذهنية:</span>{' '}
                      <bdi dir="auto">{activeMindmapChapter.title}</bdi>
                    </h4>
                    <div className="flex-grow w-full relative rounded-lg overflow-hidden border border-white/10 bg-black/50">
                      <Image
                        src={resolveMediaUrl(activeMindmapChapter.mindmapImageUrl)}
                        alt={`الخريطة الذهنية: ${activeMindmapChapter.title}`}
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
        
        {(status === 'loading' || isBuffering) && !(provider === 'bunny' && nativeProviderSurfaceLoaded) && (
          <div
            className={`absolute inset-0 z-20 flex flex-col items-center justify-center rounded-xl bg-black/40 backdrop-blur-sm ${
              status === 'loading' ? 'pointer-events-auto' : 'pointer-events-none'
            }`}
            aria-busy={status === 'loading'}
          >
            <SpinnerLoader />
          </div>
        )}

        {status === 'ready' && !usesNativePlayerChrome && !isPlaying && !isBuffering && (
          <button
            type="button"
            className={`absolute inset-0 z-10 flex items-center justify-center rounded-xl bg-black/35 transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-200 ${requiresDirectPlayback ? 'pointer-events-none' : 'pointer-events-auto'}`}
            aria-label="تشغيل الفيديو"
            tabIndex={requiresDirectPlayback ? -1 : 0}
            onClick={(e) => {
              e.stopPropagation();
              togglePlay();
            }}
          >
            <div className="flex h-20 w-20 cursor-pointer items-center justify-center rounded-full bg-[#0A1D3D] text-white shadow-lg transition-transform duration-200 hover:scale-105 active:scale-95">
              <Play className="w-8 h-8 text-white ml-1" fill="currentColor" />
            </div>
          </button>
        )}

        {status === 'ready' && !usesNativePlayerChrome && (
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
            durationSeconds={duration}
            currentTimeFormatted={formatTime(currentTime)}
            onPlaybackRateChange={handlePlaybackRateChange}
            qualityLevels={qualityLevels}
            currentQuality={currentQuality}
            onQualityChange={handleQualityChange}
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
