import { useCallback, useEffect } from 'react';
import { useReportWebVitals } from 'next/web-vitals';

import apiClient from '@/services/api-client';

const QUEUE_KEY = 'web_vitals_queue_v2';
const SAMPLE_KEY = 'web_vitals_sampled';
const QUEUE_LIMIT = 20;
const PRODUCTION_SAMPLE_RATE = 0.05;
const SAFE_SEGMENT = /^[\p{L}\p{N}_-]+$/u;
const GUID_SEGMENT =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const DYNAMIC_PARENT_SEGMENTS = new Set([
  'assistants',
  'codes',
  'coupons',
  'packages',
  'lessons',
  'sections',
  'terms',
  'teachers',
  'students',
  'users',
  'exams',
  'forms',
  'gifts',
  'homework',
  'videos',
  'conversations',
  'groups',
]);

interface QueuedMetric {
  metricId: string;
  metricName: string;
  value: number;
  rating: string;
  routeTemplate: string;
  surface: string;
  deviceClass: string;
  connectionClass: string;
  navigationType: string;
  releaseId: string;
}

type NavigatorWithConnection = Navigator & {
  connection?: { effectiveType?: string };
};

function getQueue(): QueuedMetric[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = localStorage.getItem(QUEUE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.slice(-QUEUE_LIMIT) : [];
  } catch {
    return [];
  }
}

function saveQueue(queue: QueuedMetric[]) {
  if (typeof window === 'undefined') return;
  try {
    localStorage.setItem(QUEUE_KEY, JSON.stringify(queue.slice(-QUEUE_LIMIT)));
  } catch {
    // RUM is observational and must never interrupt the user journey.
  }
}

function shouldReportWebVitals() {
  if (typeof window === 'undefined' || process.env.NODE_ENV !== 'production') {
    return false;
  }
  try {
    const stored = sessionStorage.getItem(SAMPLE_KEY);
    if (stored) return stored === '1';
    const sampled = Math.random() < PRODUCTION_SAMPLE_RATE;
    sessionStorage.setItem(SAMPLE_KEY, sampled ? '1' : '0');
    return sampled;
  } catch {
    return Math.random() < PRODUCTION_SAMPLE_RATE;
  }
}

export function normalizeRouteTemplate(pathname: string): string {
  const segments = pathname
    .split('?', 1)[0]
    .split('/')
    .filter(Boolean)
    .map((segment, index, all) => {
      if (
        GUID_SEGMENT.test(segment) ||
        (index > 0 && DYNAMIC_PARENT_SEGMENTS.has(all[index - 1]))
      ) {
        return all[index - 1] === 'packages' ? '[packageId]' : '[id]';
      }
      if (/^\d+$/.test(segment)) return '[id]';
      return segment.length <= 64 && SAFE_SEGMENT.test(segment)
        ? segment
        : '[id]';
    });
  return segments.length === 0 ? '/' : `/${segments.join('/')}`;
}

export function getSurface(routeTemplate: string): string {
  const segment = routeTemplate.split('/').filter(Boolean)[0] ?? 'public';
  return [
    'student',
    'parent',
    'teacher',
    'assistant',
    'employee',
    'admin',
    'support',
  ].includes(segment)
    ? segment
    : 'public';
}

function getDeviceClass(): string {
  if (typeof window === 'undefined') return 'unknown';
  if (window.innerWidth < 640) return 'mobile';
  if (window.innerWidth < 1024) return 'tablet';
  return 'desktop';
}

function getConnectionClass(): string {
  if (typeof navigator === 'undefined') return 'unknown';
  if (!navigator.onLine) return 'offline';
  const effectiveType = (navigator as NavigatorWithConnection).connection
    ?.effectiveType;
  if (effectiveType === 'slow-2g' || effectiveType === '2g') return 'slow';
  if (effectiveType === '3g') return 'moderate';
  if (effectiveType === '4g') return 'fast';
  return 'unknown';
}

function normalizeNavigationType(value: string | undefined): string {
  if (value === 'back-forward' || value === 'back_forward') {
    return 'back-forward';
  }
  return ['navigate', 'client', 'reload', 'prerender'].includes(value ?? '')
    ? value!
    : 'unknown';
}

export function useWebVitalsReporter() {
  const shouldReport = shouldReportWebVitals();

  const sendMetric = useCallback(async (metric: QueuedMetric) => {
    await apiClient.post('/v1/metrics/web-vitals', metric);
  }, []);

  const flushQueue = useCallback(async () => {
    if (typeof navigator !== 'undefined' && !navigator.onLine) return;
    const queue = getQueue();
    if (queue.length === 0) return;
    saveQueue([]);
    const failed: QueuedMetric[] = [];
    for (const metric of queue) {
      try {
        await sendMetric(metric);
      } catch {
        failed.push(metric);
      }
    }
    if (failed.length > 0) saveQueue([...failed, ...getQueue()]);
  }, [sendMetric]);

  useEffect(() => {
    if (!shouldReport || typeof window === 'undefined') return;
    void flushQueue();
    const handleOnline = () => void flushQueue();
    window.addEventListener('online', handleOnline);
    return () => window.removeEventListener('online', handleOnline);
  }, [flushQueue, shouldReport]);

  useReportWebVitals((metric) => {
    if (!shouldReport || typeof window === 'undefined') return;
    const routeTemplate = normalizeRouteTemplate(window.location.pathname);
    const body: QueuedMetric = {
      metricId: metric.id.slice(0, 64),
      metricName: metric.name,
      value: metric.value,
      rating: metric.rating,
      routeTemplate,
      surface: getSurface(routeTemplate),
      deviceClass: getDeviceClass(),
      connectionClass: getConnectionClass(),
      navigationType: normalizeNavigationType(metric.navigationType),
      releaseId: (
        process.env.NEXT_PUBLIC_RELEASE_ID ?? 'unknown'
      ).slice(0, 96),
    };

    if (typeof navigator !== 'undefined' && navigator.onLine) {
      void sendMetric(body).catch(() => saveQueue([...getQueue(), body]));
    } else {
      saveQueue([...getQueue(), body]);
    }
  });
}
