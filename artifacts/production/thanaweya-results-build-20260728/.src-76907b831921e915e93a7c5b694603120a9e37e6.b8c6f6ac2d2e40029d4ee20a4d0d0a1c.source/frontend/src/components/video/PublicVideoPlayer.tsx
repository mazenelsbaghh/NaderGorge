'use client';

import { Play } from 'lucide-react';
import { AnimatePresence, motion } from 'framer-motion';
import { useCallback, useEffect, useRef, useState } from 'react';

import PlayerControls from './PlayerControls';
import { SpinnerLoader } from '@/components/ui/loading-indicator';
import apiClient from '@/services/api-client';

type PublicVideoPlayerProps = {
  url: string;
  title: string;
};

type PlayerAppearance = {
  topOpacity: number;
  bottomOpacity: number;
  topCoverage: number;
  bottomCoverage: number;
  topSolid: number;
  bottomSolid: number;
  youtubeShadowDelayMs: number;
  bunnyShadowDelayMs: number;
  enabledProviders: string[];
};

const defaultAppearance: PlayerAppearance = {
  topOpacity: 0.7,
  bottomOpacity: 0.98,
  topCoverage: 40,
  bottomCoverage: 38,
  topSolid: 10,
  bottomSolid: 12,
  youtubeShadowDelayMs: 5000,
  bunnyShadowDelayMs: 5000,
  enabledProviders: ['youtube', 'bunny', 'vk', 'telegram', 'telegram-direct', 'rutube', 'google-drive'],
};

const clamp = (value: unknown, fallback: number, max: number) => {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? Math.min(max, Math.max(0, numberValue)) : fallback;
};

const formatTime = (seconds: number) => {
  if (!seconds || Number.isNaN(seconds)) return '0:00';
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.floor(seconds % 60);
  return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
};

/**
 * Public introductions use the same embed protocol and visual controls as lesson videos.
 * They omit only lesson-specific concerns such as watch limits and chapters.
 */
export function PublicVideoPlayer({ url, title }: PublicVideoPlayerProps) {
  const playerRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const controlsTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const shadowTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [provider, setProvider] = useState('youtube');
  const [isPlaying, setIsPlaying] = useState(false);
  const [isBuffering, setIsBuffering] = useState(true);
  const [isReady, setIsReady] = useState(false);
  const [showControls, setShowControls] = useState(true);
  const [showShadows, setShowShadows] = useState(true);
  const [progress, setProgress] = useState(0);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [volume, setVolume] = useState(100);
  const [isMuted, setIsMuted] = useState(false);
  const [appearance, setAppearance] = useState(defaultAppearance);

  const sendCommand = useCallback((type: string, data?: Record<string, unknown>) => {
    iframeRef.current?.contentWindow?.postMessage({ type, ...data }, window.location.origin);
  }, []);

  const showTimedShadows = useCallback(() => {
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
    setShowShadows(true);
    const delay = provider === 'bunny' ? appearance.bunnyShadowDelayMs : appearance.youtubeShadowDelayMs;
    shadowTimeoutRef.current = setTimeout(() => setShowShadows(false), delay);
  }, [appearance.bunnyShadowDelayMs, appearance.youtubeShadowDelayMs, provider]);

  const revealControls = useCallback(() => {
    setShowControls(true);
    if (controlsTimeoutRef.current) clearTimeout(controlsTimeoutRef.current);
    if (isPlaying) {
      controlsTimeoutRef.current = setTimeout(() => setShowControls(false), 3000);
    }
  }, [isPlaying]);

  useEffect(() => {
    let active = true;
    apiClient.get('/public/settings').then(({ data }) => {
      if (!active) return;
      const providers = data?.enabledPlayerShadowProviders ?? data?.EnabledPlayerShadowProviders;
      setAppearance({
        topOpacity: clamp(data?.playerShadowTopOpacity ?? data?.PlayerShadowTopOpacity, defaultAppearance.topOpacity, 1),
        bottomOpacity: clamp(data?.playerShadowBottomOpacity ?? data?.PlayerShadowBottomOpacity, defaultAppearance.bottomOpacity, 1),
        topCoverage: clamp(data?.playerShadowTopCoverage ?? data?.PlayerShadowTopCoverage, defaultAppearance.topCoverage, 100),
        bottomCoverage: clamp(data?.playerShadowBottomCoverage ?? data?.PlayerShadowBottomCoverage, defaultAppearance.bottomCoverage, 100),
        topSolid: clamp(data?.playerShadowTopSolid ?? data?.PlayerShadowTopSolid, defaultAppearance.topSolid, 100),
        bottomSolid: clamp(data?.playerShadowBottomSolid ?? data?.PlayerShadowBottomSolid, defaultAppearance.bottomSolid, 100),
        youtubeShadowDelayMs: clamp(data?.youTubePlayerShadowHideDelaySeconds ?? data?.YouTubePlayerShadowHideDelaySeconds, 5, 60) * 1000,
        bunnyShadowDelayMs: clamp(data?.bunnyPlayerShadowHideDelaySeconds ?? data?.BunnyPlayerShadowHideDelaySeconds, 5, 60) * 1000,
        enabledProviders: typeof providers === 'string'
          ? providers.toLowerCase().split(',').map((item: string) => item.trim()).filter(Boolean)
          : defaultAppearance.enabledProviders,
      });
    }).catch(() => {});
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin || event.data?.source !== 'video-embed') return;
      const { type, data = {} } = event.data;
      if (type === 'ready') {
        const nextProvider = String(data.provider || 'youtube').toLowerCase();
        setProvider(nextProvider);
        setDuration(data.duration || 0);
        setVolume(data.volume || 100);
        setIsMuted(Boolean(data.isMuted));
        setIsReady(true);
        setIsBuffering(false);
        setShowShadows(true);
      }
      if (type === 'stateChange') {
        const playing = Boolean(data.isPlaying);
        setIsPlaying(playing);
        setIsBuffering(data.state === 3 || data.state === 'buffering');
        if (playing) {
          setShowControls(false);
          showTimedShadows();
        } else {
          setShowControls(true);
          setShowShadows(true);
        }
      }
      if (type === 'timeUpdate') {
        const nextDuration = Number(data.duration) || duration;
        const nextTime = Number(data.currentTime) || 0;
        setDuration(nextDuration);
        setCurrentTime(nextTime);
        if (nextDuration > 0) setProgress((nextTime / nextDuration) * 100);
        setIsBuffering(false);
      }
      if (type === 'overlayClick') {
        sendCommand(data.isPlaying ? 'pause' : 'play');
        setIsBuffering(!data.isPlaying);
      }
      if (type === 'autoplayBlocked') {
        setIsPlaying(false);
        setIsBuffering(false);
        setShowControls(true);
        setShowShadows(true);
      }
    };
    window.addEventListener('message', onMessage);
    return () => window.removeEventListener('message', onMessage);
  }, [duration, sendCommand, showTimedShadows]);

  useEffect(() => () => {
    if (controlsTimeoutRef.current) clearTimeout(controlsTimeoutRef.current);
    if (shadowTimeoutRef.current) clearTimeout(shadowTimeoutRef.current);
  }, []);

  const toggleFullscreen = async () => {
    const element = playerRef.current;
    if (!element) return;
    if (!document.fullscreenElement) await element.requestFullscreen?.();
    else await document.exitFullscreen?.();
  };

  const togglePlay = () => {
    sendCommand(isPlaying ? 'pause' : 'play');
    if (!isPlaying) setIsBuffering(true);
  };

  const seek = (percent: number) => {
    const time = (percent / 100) * duration;
    setProgress(percent);
    setCurrentTime(time);
    sendCommand('seekTo', { time });
    sendCommand('play');
  };

  const shadowGradient = `linear-gradient(to bottom, rgba(0,0,0,${appearance.topOpacity}) 0%, rgba(0,0,0,${appearance.topOpacity}) ${Math.min(appearance.topSolid, appearance.topCoverage)}%, transparent ${appearance.topCoverage}%, transparent ${100 - appearance.bottomCoverage}%, rgba(0,0,0,${appearance.bottomOpacity}) ${100 - Math.min(appearance.bottomSolid, appearance.bottomCoverage)}%, rgba(0,0,0,${appearance.bottomOpacity}) 100%)`;

  return (
    <div ref={playerRef} className="group relative aspect-video w-full overflow-hidden rounded-xl border border-[var(--secondary)]/30 bg-black shadow-lg">
      <iframe
        ref={iframeRef}
        title={title}
        src={`/api/video/public-embed?url=${encodeURIComponent(url)}`}
        className="absolute inset-0 h-full w-full border-0"
        allow="autoplay; encrypted-media; picture-in-picture; fullscreen"
        allowFullScreen
        referrerPolicy="strict-origin-when-cross-origin"
        onLoad={() => setIsBuffering(true)}
      />

      <div
        className="absolute inset-0 z-[85]"
        onMouseMove={revealControls}
        onTouchStart={revealControls}
        onClick={togglePlay}
        role="presentation"
      />

      <AnimatePresence>
        {isReady && showShadows && appearance.enabledProviders.includes(provider) && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.3 }} className="pointer-events-none absolute inset-0 z-[80]" style={{ background: shadowGradient }} />
        )}
      </AnimatePresence>

      {isBuffering && <div className="pointer-events-none absolute inset-0 z-[90] flex items-center justify-center bg-black/40 backdrop-blur-sm"><SpinnerLoader /></div>}

      {isReady && !isPlaying && !isBuffering && (
        <button type="button" className="absolute inset-0 z-[91] flex items-center justify-center bg-black/40 backdrop-blur-sm" onClick={togglePlay} aria-label="تشغيل الفيديو">
          <span className="flex h-20 w-20 items-center justify-center rounded-full border border-white/50 bg-white/20 shadow-[0_0_30px_rgba(255,255,255,0.4)] backdrop-blur-md transition hover:scale-110"><Play className="ml-1 h-8 w-8 text-white" fill="currentColor" /></span>
        </button>
      )}

      {isReady && <PlayerControls
        isPlaying={isPlaying}
        onTogglePlay={togglePlay}
        progress={progress}
        onSeek={seek}
        volume={volume}
        isMuted={isMuted}
        onVolumeChange={(value) => { setVolume(value); sendCommand('setVolume', { volume: value }); }}
        onToggleMute={() => { setIsMuted((value) => !value); sendCommand(isMuted ? 'unmute' : 'mute'); }}
        onToggleFullscreen={toggleFullscreen}
        durationFormatted={formatTime(duration)}
        durationSeconds={duration}
        currentTimeFormatted={formatTime(currentTime)}
        onPlaybackRateChange={(rate) => sendCommand('setPlaybackRate', { rate })}
        visible={showControls}
        compact
        provider={provider}
      />}
    </div>
  );
}
