'use client';

import { useCallback, useEffect, useState } from 'react';

import { useAuthStore } from '@/stores/auth-store';

export type LiveSupportSound = 'soft' | 'bell' | 'chime';

export type LiveSupportPreferences = {
  staffBubbleColor: string;
  studentBubbleColor: string;
  fontScale: 'small' | 'medium' | 'large';
  notificationsEnabled: boolean;
  soundEnabled: boolean;
  sound: LiveSupportSound;
};

const defaults: LiveSupportPreferences = {
  staffBubbleColor: '#0E7490',
  studentBubbleColor: '#E2E8F0',
  fontScale: 'medium',
  notificationsEnabled: true,
  soundEnabled: true,
  sound: 'soft',
};

function storageKey(userId: string) {
  return `massar:live-support-preferences:${userId}`;
}

export function useLiveSupportPreferences() {
  const userId = useAuthStore((state) => state.user?.id);
  const [preferences, setPreferences] = useState<LiveSupportPreferences>(defaults);

  useEffect(() => {
    if (!userId) return;
    try {
      const stored = window.localStorage.getItem(storageKey(userId));
      setPreferences(stored ? { ...defaults, ...JSON.parse(stored) } : defaults);
    } catch {
      setPreferences(defaults);
    }
  }, [userId]);

  const updatePreferences = useCallback((change: Partial<LiveSupportPreferences>) => {
    setPreferences((current) => {
      const next = { ...current, ...change };
      if (userId) window.localStorage.setItem(storageKey(userId), JSON.stringify(next));
      return next;
    });
  }, [userId]);

  return { preferences, updatePreferences };
}

export function playLiveSupportSound(sound: LiveSupportSound) {
  if (typeof window === 'undefined') return;
  const AudioContextConstructor = window.AudioContext || window.webkitAudioContext;
  if (!AudioContextConstructor) return;

  const context = new AudioContextConstructor();
  const notes: Record<LiveSupportSound, number[]> = {
    soft: [660],
    bell: [784, 1047],
    chime: [523, 659, 784],
  };

  notes[sound].forEach((frequency, index) => {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = sound === 'bell' ? 'sine' : 'triangle';
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.0001, context.currentTime + index * 0.12);
    gain.gain.exponentialRampToValueAtTime(0.08, context.currentTime + index * 0.12 + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + index * 0.12 + 0.35);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(context.currentTime + index * 0.12);
    oscillator.stop(context.currentTime + index * 0.12 + 0.36);
  });

  window.setTimeout(() => void context.close(), 900);
}

declare global {
  interface Window {
    webkitAudioContext?: typeof AudioContext;
  }
}
