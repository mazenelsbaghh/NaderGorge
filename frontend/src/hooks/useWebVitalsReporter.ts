import { useEffect, useCallback } from 'react';
import { useReportWebVitals } from 'next/web-vitals';
import apiClient from '@/services/api-client';
import { useAuthStore } from '@/stores/auth-store';

const QUEUE_KEY = 'web_vitals_queue';

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
    localStorage.setItem(QUEUE_KEY, JSON.stringify(queue));
  } catch (e) {
    console.error('Failed to save web vitals queue:', e);
  }
}

export function useWebVitalsReporter() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

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
    if (isAuthenticated) {
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
  }, [isAuthenticated, flushQueue]);

  useReportWebVitals((metric) => {
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
    } else {
      // Queue it locally
      const queue = getQueue();
      queue.push(body);
      saveQueue(queue);
    }
  });
}
