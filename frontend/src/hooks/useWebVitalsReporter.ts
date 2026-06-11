import { useReportWebVitals } from 'next/web-vitals';
import apiClient from '@/services/api-client';
import { useAuthStore } from '@/stores/auth-store';

export function useWebVitalsReporter() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

  useReportWebVitals((metric) => {
    if (!isAuthenticated) return;

    const body = {
      metricName: metric.name,
      value: metric.value,
      rating: metric.rating,
      pageUrl: typeof window !== 'undefined' ? window.location.href : '',
      userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : '',
    };

    apiClient.post('/v1/metrics/web-vitals', body).catch((err) => {
      console.warn('Failed to send web vitals metric:', err);
    });
  });
}
