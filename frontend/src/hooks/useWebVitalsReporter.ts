import { useEffect, useCallback } from 'react';
import { useReportWebVitals } from 'next/web-vitals';
import apiClient from '@/services/api-client';
import { useAuthStore } from '@/stores/auth-store';

const QUEUE_KEY = 'web_vitals_queue';
const SAMPLE_KEY = 'web_vitals_sampled';
const QUEUE_LIMIT = 20;
const PRODUCTION_SAMPLE_RATE = 0.05;

interface QueuedMetric {
  metricName: string;
  value: number;
  rating: string;
  pageUrl: string;
  userAgent: string;
}

function getQueue(): QueuedMetric[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = localStorage.getItem(QUEUE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function saveQueue(queue: QueuedMetric[]) {
  if (typeof window === 'undefined') return;
  try {
    localStorage.setItem(QUEUE_KEY, JSON.stringify(queue.slice(-QUEUE_LIMIT)));
  } catch (e) {
    console.error('Failed to save web vitals queue:', e);
  }
}

function shouldReportWebVitals() {
  if (typeof window === 'undefined') return false;

  if (process.env.NODE_ENV !== 'production') {
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

export function useWebVitalsReporter() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const shouldReport = shouldReportWebVitals();

  // Function to send a single metric
  const sendMetric = useCallback(async (metric: QueuedMetric) => {
    await apiClient.post('/v1/metrics/web-vitals', metric);
  }, []);

  // Function to flush the queue
  const flushQueue = useCallback(async () => {
    if (!isAuthenticated || (typeof navigator !== 'undefined' && !navigator.onLine)) return;
    const queue = getQueue();
    if (queue.length === 0) return;

    // Clear queue first to prevent concurrent duplicate sends
    saveQueue([]);

    const failed: QueuedMetric[] = [];
    for (const metric of queue) {
      try {
        await sendMetric(metric);
      } catch (err) {
        console.warn('Failed to send queued web vitals metric, putting back in queue:', err);
        failed.push(metric);
      }
    }

    if (failed.length > 0) {
      const currentQueue = getQueue();
      saveQueue([...failed, ...currentQueue]);
    }
  }, [isAuthenticated, sendMetric]);

  // Flush queue on mount or when auth state changes or when online status changes
  useEffect(() => {
    if (shouldReport && isAuthenticated) {
      void flushQueue();

      const handleOnline = () => {
        void flushQueue();
      };

      if (typeof window !== 'undefined') {
        window.addEventListener('online', handleOnline);
        return () => {
          window.removeEventListener('online', handleOnline);
        };
      }
    }
  }, [isAuthenticated, flushQueue, shouldReport]);

  useReportWebVitals((metric) => {
    if (!shouldReport) return;

    const body: QueuedMetric = {
      metricName: metric.name,
      value: metric.value,
      rating: metric.rating,
      pageUrl: typeof window !== 'undefined' ? window.location.href : '',
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : '',
    };

    if (isAuthenticated && typeof navigator !== 'undefined' && navigator.onLine) {
      sendMetric(body).catch((err) => {
        console.warn('Failed to send web vitals metric, queueing for retry:', err);
        const queue = getQueue();
        queue.push(body);
        saveQueue(queue);
      });
    }
  });
}
