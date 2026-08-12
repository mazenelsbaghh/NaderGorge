import axios from 'axios';
import toast from 'react-hot-toast';

import {
  clearStoredAuth,
  getStoredAccessToken,
  readStoredAuth,
} from '@/lib/auth-storage';
import { getAccessToken, setAccessToken } from '@/lib/auth-memory';
import { getApiErrorSummary } from '@/lib/api-errors';

import { getSurfaceName } from '@/packages/surface-runtime/config';
import { useAuthStore } from '@/stores/auth-store';

declare module 'axios' {
  interface AxiosRequestConfig {
    suppressErrorToast?: boolean;
  }
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 20_000,
  withCredentials: true,
});

const AUTH_BYPASS_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/parent/reports',
  '/metrics/web-vitals',
];
const RATE_LIMIT_TOAST_COOLDOWN_MS = 4_000;
let lastRateLimitToastAt = 0;

let refreshPromise: Promise<string> | null = null;

export function isRequestCancellation(error: unknown): boolean {
  return (
    axios.isCancel(error) ||
    (typeof error === 'object' &&
      error !== null &&
      ('code' in error || 'name' in error) &&
      ((error as { code?: string }).code === 'ERR_CANCELED' ||
        (error as { name?: string }).name === 'AbortError'))
  );
}

function shouldBypassAuthRefresh(requestUrl: string) {
  return AUTH_BYPASS_PATHS.some((path) => requestUrl.includes(path));
}

function hasStoredUserSession() {
  return Boolean(readStoredAuth()?.user);
}

function refreshAccessToken() {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const { data } = await axios.post(
          `${API_BASE_URL}/auth/refresh`,
          {},
          { withCredentials: true }
        );

        const token = data.data.accessToken;
        setAccessToken(token);

        // Update Zustand store atomically
        useAuthStore.setState({
          user: data.data.user,
          accessToken: token,
          isAuthenticated: true,
          isLoading: false,
        });

        return token;
      } catch (refreshError) {
        setAccessToken(null);
        clearStoredAuth();
        useAuthStore.getState().clearAuth();
        if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
          window.location.href = '/login';
        }
        throw refreshError;
      } finally {
        refreshPromise = null;
      }
    })();
  }

  return refreshPromise;
}

// Request interceptor: attach JWT token and dynamic surface header
apiClient.interceptors.request.use(
  async (config) => {
    config.headers = config.headers || {};
    config.headers['X-App-Surface'] = getSurfaceName();

    let token = getAccessToken() ?? getStoredAccessToken();
    const requestUrl = config.url || '';
    if (!token && !shouldBypassAuthRefresh(requestUrl) && hasStoredUserSession()) {
      try {
        token = await refreshAccessToken();
      } catch {
        // Let the response interceptor handle the final unauthenticated response.
      }
    }

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor: handle 401 and auto-refresh
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    // Requests aborted by navigation, polling replacement, or an AbortController are
    // expected control flow. They must never surface as a user-facing failure toast.
    if (isRequestCancellation(error)) {
      return Promise.reject(error);
    }

    const originalRequest = error.config;
    const requestUrl = originalRequest?.url || '';
    const bypassAuthRefresh = shouldBypassAuthRefresh(requestUrl);

    if (
      error.response?.status === 401 &&
      !originalRequest?._retry &&
      !bypassAuthRefresh &&
      hasStoredUserSession()
    ) {
      originalRequest._retry = true;

      try {
        const token = await refreshAccessToken();
        originalRequest.headers = originalRequest.headers ?? {};
        originalRequest.headers.Authorization = `Bearer ${token}`;
        return apiClient(originalRequest);
      } catch (refreshErr) {
        return Promise.reject(refreshErr);
      }
    }

    const status = error.response?.status;
    let errorMsg = error.response?.data?.message || error.message || 'An error occurred';
    const errors = error.response?.data?.errors || [];

    // Localize common English error messages/keys to Arabic
    if (errors.includes('REQUEST_LIMIT_REACHED') || errorMsg === 'Extra watch request limit reached.') {
      errorMsg = 'لقد تجاوزت الحد الأقصى لطلبات المشاهدة الإضافية المسموح بها.';
    } else if (errors.includes('WATCH_LIMIT_REACHED') || errorMsg === 'Watch limit reached for this video') {
      errorMsg = 'لقد استنفدت الحد الأقصى للمشاهدات المسموح بها لهذا الفيديو.';
    } else if (errors.includes('REQUEST_EXISTS')) {
      errorMsg = 'لديك طلب معلق بالفعل لمشاهدة هذا الفيديو.';
    } else if (errors.includes('ACADEMIC_SCOPE_DENIED') || errors.includes('ACADEMIC_SCOPE_TARGET_UNSCOPED')) {
      errorMsg = 'هذا المحتوى غير متاح لحسابك الدراسي الحالي.';
    } else {
      errorMsg = getApiErrorSummary(error);
    }

    if (status === 429) {
      const now = Date.now();
      if (now - lastRateLimitToastAt > RATE_LIMIT_TOAST_COOLDOWN_MS) {
        lastRateLimitToastAt = now;
        toast.error(errorMsg, { id: 'rate-limit' });
      }
      return Promise.reject(error);
    }

    if (status === 403 && !requestUrl.includes('/auth/session')) {
      useAuthStore.getState().refreshCurrentSession().catch(() => undefined);
    }

    if (
      status !== 401 &&
      status !== 403 &&
      !requestUrl.includes('/auth/register') &&
      !originalRequest?.suppressErrorToast
    ) {
      toast.error(errorMsg, { id: errorMsg });
    }
    return Promise.reject(error);
  }
);

export default apiClient;
