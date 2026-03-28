'use client';

import React, { useEffect, useRef, useState, useCallback } from 'react';
import { videoSessionService } from '@/services/video-session-service';
import { AlertCircle, Play } from 'lucide-react';
import { InlineLoader } from '@/components/ui/loading-indicator';
import PlayerControls from './PlayerControls';
import { applyDomShields } from '@/utils/dom-shield';

interface SecureVideoPlayerProps {
  lessonVideoId: string;
  onWatchProgress?: (secondsWatched: number) => void;
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
export default function SecureVideoPlayer({ 
  lessonVideoId, 
  onWatchProgress,
  className = '',
  onSessionError
}: SecureVideoPlayerProps) {
  const iframeRef = useRef<HTMLIFrameElement | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error' | 'locked'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [watchInfo, setWatchInfo] = useState<{current: number, max: number, isLocked?: boolean} | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  
  const [showControls, setShowControls] = useState(true);
  const controlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  const handlePlayerInteraction = useCallback(() => {
    setShowControls(true);
    if (controlsTimeoutRef.current) {
      clearTimeout(controlsTimeoutRef.current);
    }
    if (isPlaying) {
      controlsTimeoutRef.current = setTimeout(() => {
        setShowControls(false);
      }, 3000);
    }
  }, [isPlaying]);

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

  const [progress, setProgress] = useState(0);
  const [volume, setVolume] = useState(100);
  const [isMuted, setIsMuted] = useState(false);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);

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
          break;
        case 'stateChange':
          setIsPlaying(msg.data.isPlaying);
          if (msg.data.isPlaying) {
            setShowControls(false);
          }
          break;
        case 'timeUpdate':
          if (msg.data.currentTime !== undefined) {
            setCurrentTime(msg.data.currentTime);
            setDuration(msg.data.duration || duration);
            if (msg.data.duration > 0) {
              setProgress((msg.data.currentTime / msg.data.duration) * 100);
            }
            if (onWatchProgress) {
              onWatchProgress(msg.data.currentTime);
            }
          }
          break;
        case 'error':
          setStatus('error');
          setErrorMessage('حدث خطأ أثناء تشغيل الفيديو');
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
  const [displayedWatched, setDisplayedWatched] = useState(0);
  const lastReportedTime = useRef(0);
  const trackingInterval = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    if (status !== 'ready') return;

    if (trackingInterval.current) clearInterval(trackingInterval.current);

    trackingInterval.current = setInterval(() => {
      if (isPlaying) {
        actualWatchedSeconds.current += 1;
        setDisplayedWatched(actualWatchedSeconds.current);
        
        const crossedThreshold = actualWatchedSeconds.current >= 60 && !viewTrackedRef.current;
        if (actualWatchedSeconds.current - lastReportedTime.current >= 10 || crossedThreshold) {
          lastReportedTime.current = actualWatchedSeconds.current;
          
          if (crossedThreshold) {
            viewTrackedRef.current = true;
          }
          
          videoSessionService.trackProgress(lessonVideoId, actualWatchedSeconds.current, crossedThreshold)
            .then(res => {
              const data = res.data.data;
              if (data) {
                setWatchInfo(prev => ({ ...prev, current: data.currentCount, max: prev?.max || data.maxCount, isLocked: data.isLocked }));
                if (data.viewRegistered) {
                  setViewTracked(true);
                }
              }
            })
            .catch(err => {
              console.error("Failed to sync progress:", err);
              if (crossedThreshold) {
                viewTrackedRef.current = false;
              }
            });
        }
      }
    }, 1000);

    return () => {
      if (trackingInterval.current) clearInterval(trackingInterval.current);
    };
  }, [status, isPlaying, lessonVideoId]);

  // ── Load video ──
  const loadVideo = async () => {
    try {
      setStatus('loading');
      
      // 1. Fetch encrypted session token
      const response = await videoSessionService.createSession(lessonVideoId);
      const session = response.data.data;

      setWatchInfo({
        current: session.watchInfo.currentCount,
        max: session.watchInfo.maxCount
      });

      if (session.watchInfo.isLocked) {
        setStatus('locked');
        return;
      }

      // 2. Mark session consumed
      await videoSessionService.consumeSession(session.sessionId);

      // 3. Build the embed URL pointing to our own API route
      //    The encrypted token + key are passed as query params.
      //    Our route decrypts SERVER-SIDE — the YouTube URL never reaches the browser JS.
      const embedUrl = `/api/video/embed?t=${encodeURIComponent(session.token)}&k=${encodeURIComponent(session.key)}`;

      // 4. Create the iframe pointing to our embed route
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

        // Apply DOM shields to prevent tampering
        applyDomShields(containerRef.current, () => {
          setStatus('error');
          setErrorMessage('تم اكتشاف محاولة تعديل المشغل. لإعادة المشاهدة، قم بتحديث الصفحة.');
        });
      }

    } catch (err: any) {
      console.error(err);
      
      const errors = err.response?.data?.errors || [];
      if (errors.includes('WATCH_LIMIT_REACHED')) {
        setStatus('locked');
        setWatchInfo(prev => ({ current: prev?.current || 3, max: prev?.max || 3, isLocked: true }));
        return;
      }

      setStatus('error');
      const msg = err.response?.data?.message || err.message || 'فشل في تحميل الفيديو';
      setErrorMessage(msg);
      if (onSessionError) onSessionError(msg);
    }
  };

  // ── Player controls (send commands to iframe via postMessage) ──
  const togglePlay = () => {
    sendCommand(isPlaying ? 'pause' : 'play');
  };

  const handleSeek = (percent: number) => {
    if (duration === 0) return;
    const targetTime = (percent / 100) * duration;
    sendCommand('seekTo', { time: targetTime });
    setCurrentTime(targetTime);
    setProgress(percent);
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
  };

  // ── Render States ──
  if (status === 'idle') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-lg overflow-hidden flex items-center justify-center border border-pharaoh-gold/30 cursor-pointer group ${className}`} onClick={loadVideo}>
        <div className="absolute inset-0 bg-cover bg-center opacity-40 group-hover:opacity-30 transition-opacity" style={{ backgroundImage: "url('/images/lesson-placeholder.jpg')" }}></div>
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-4">
          <div className="w-16 h-16 rounded-full bg-pharaoh-gold/20 flex items-center justify-center backdrop-blur-md border border-pharaoh-gold/50 group-hover:scale-110 transition-transform shadow-lg">
            <Play className="w-8 h-8 text-pharaoh-gold ml-1" />
          </div>
          <span className="text-white font-bold text-lg drop-shadow-md">انقر لبدء مشاهدة الدرس بأمان</span>
        </div>
      </div>
    );
  }

  if (status === 'locked') {
    return (
      <div className={`relative w-full aspect-video bg-black rounded-lg overflow-hidden flex flex-col items-center justify-center border border-red-500/30 p-8 text-center ${className}`}>
        <AlertCircle className="w-12 h-12 text-red-500 mb-4 drop-shadow-lg" />
        <h3 className="text-xl font-bold text-white mb-2">تم الوصول للحد الأقصى للمشاهدات</h3>
        <p className="text-gray-300">لقد استنفدت الحد المسموح به لمشاهدة هذا الفيديو ({watchInfo?.max} مرات). يرجى التواصل مع الإدارة لطلب مشاهدة إضافية.</p>
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
    <div className={`flex flex-col w-full rounded-lg overflow-hidden border border-pharaoh-gold/30 bg-black shadow-lg group ${className} ${isPseudoFullscreen ? '!fixed !inset-0 !z-[100] !rounded-none' : ''}`}>
      
      {/* Top Tracking Bar */}
      <div className="flex items-center justify-between px-4 py-2 bg-zinc-900 border-b border-pharaoh-gold/20 flex-wrap gap-2 z-10 transition-colors">
           <div className="flex items-center gap-4">
             <div className="flex flex-col">
               <span className="text-white text-sm font-semibold">المشاهدات</span>
               <span className="text-xs text-gray-400">
                 {watchInfo ? `${watchInfo.current} مشاهدة من أصل ${watchInfo.max}` : 'جاري التجهيز...'}
               </span>
             </div>
           </div>

           <div className="flex items-center gap-2">
             {viewTracked ? (
               <span className="text-green-400 font-medium text-sm flex items-center gap-1">
                 <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                   <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                 </svg>
                 تم احتساب المشاهدة
               </span>
             ) : (
               <div className="flex items-center gap-2 text-white text-sm font-medium bg-zinc-800 px-3 py-1.5 rounded-full border border-zinc-700">
                 <InlineLoader className="text-pharaoh-gold" />
                 <span>سيتم احتساب المشاهدة بعد 60 ثانية (شاهدت: {displayedWatched})</span>
               </div>
             )}
           </div>
        </div>

      {/* Video Container */}
      <div 
        className="relative w-full aspect-video bg-black cursor-pointer"
        onMouseMove={handlePlayerInteraction}
        onTouchStart={handlePlayerInteraction}
        onClick={handlePlayerInteraction}
        onMouseLeave={() => { if(isPlaying) setShowControls(false) }}
      >
        <div ref={containerRef} className="absolute inset-0 w-full h-full" />
        
         {status === 'loading' && (
          <div className="absolute inset-0 flex items-center justify-center bg-black/80 backdrop-blur-sm z-20">
            <InlineLoader className="text-pharaoh-gold scale-150" />
          </div>
        )}

        {/* Anti-Suggestions Blur (Shows when paused to hide "More Videos") */}
        {!isPlaying && currentTime > 0 && status === 'ready' && (
          <div className="absolute bottom-0 left-0 right-0 h-[40%] bg-black/60 backdrop-blur-[12px] border-t border-pharaoh-gold/20 pointer-events-none z-10 flex flex-col items-center justify-center animate-in fade-in duration-300">
             <div className="p-3 bg-black/40 rounded-full border border-pharaoh-gold/30 mb-2">
               <svg className="w-8 h-8 text-pharaoh-gold" fill="currentColor" viewBox="0 0 24 24"><path d="M8 5v14l11-7z" /></svg>
             </div>
             <span className="text-pharaoh-sand font-bold text-sm tracking-widest bg-black/50 px-4 py-1 rounded-full drop-shadow-lg">تم الإيقاف</span>
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
            onQualityChange={handleQualityChange}
            visible={showControls}
          />
        )}
      </div>
    </div>
  );
}
