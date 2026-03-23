'use client';

import { useState, useEffect, useRef } from 'react';
import { VideoDto, contentService } from '@/services/content-service';
import { motion } from 'framer-motion';

export function VideoPlayer({ video, onLockInfo }: { video: VideoDto; onLockInfo: (msg: string) => void }) {
  const [isPlaying, setIsPlaying] = useState(false);
  const [isLocked, setIsLocked] = useState(video.isLocked);
  const playTimerRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    setIsLocked(video.isLocked);
  }, [video]);

  // Handle Heartbeat
  useEffect(() => {
    if (isPlaying && !isLocked) {
      playTimerRef.current = setInterval(async () => {
        try {
          const res = await contentService.recordVideoEvent(video.id, 10); // report every 10 seconds playing
          if (res.data.data.isLocked) {
            setIsLocked(true);
            setIsPlaying(false);
            onLockInfo('Maximum watch limit reached for this video.');
          }
        } catch (e) {
          console.error("Failed to track video event");
        }
      }, 10000);
    } else {
      if (playTimerRef.current) clearInterval(playTimerRef.current);
    }

    return () => {
      if (playTimerRef.current) clearInterval(playTimerRef.current);
    };
  }, [isPlaying, isLocked, video.id, onLockInfo]);

  if (isLocked) {
    return (
      <div className="flex h-64 w-full flex-col items-center justify-center rounded-2xl bg-gray-900 text-white shadow-xl outline-1 outline outline-gray-800">
        <svg className="mb-4 h-12 w-12 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
        </svg>
        <h3 className="text-lg font-bold">Video Locked</h3>
        <p className="mt-2 text-sm text-gray-400">You have reached the maximum watch limit.</p>
      </div>
    );
  }

  // To truly track playback from an iframe, we need to inject the Youtube IFrame API.
  // For demonstration, simulating play state with mouse enters/clicks
  return (
    <div className="relative overflow-hidden rounded-2xl border border-gray-200 shadow-lg dark:border-gray-800">
      <div className="aspect-video w-full bg-black" onMouseEnter={() => setIsPlaying(true)} onMouseLeave={() => setIsPlaying(false)}>
        {/* We would use youtube iframe API in real scenario to capture true play/pause event */}
        <iframe
          src={video.embedUrl}
          className="h-full w-full border-none"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
        ></iframe>
      </div>
      <div className="absolute top-2 right-2 rounded-lg bg-black/70 px-3 py-1 text-xs font-semibold text-white backdrop-blur-sm">
        Views: {video.watched} / {video.limit}
      </div>
    </div>
  );
}
