import axios from 'axios';
import toast from 'react-hot-toast';

import {
  clearStoredAuth,
  getStoredAccessToken,
  replaceStoredTokens,
} from '@/lib/auth-storage';

import { getSurfaceName } from '@/packages/surface-runtime/config';
import { useAuthStore } from '@/stores/auth-store';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 20_000,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

const AUTH_BYPASS_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/parent/reports'];
const RATE_LIMIT_TOAST_COOLDOWN_MS = 4_000;
let lastRateLimitToastAt = 0;

let refreshPromise: Promise<string> | null = null;

// Request interceptor: attach JWT token and dynamic surface header
apiClient.interceptors.request.use(
  (config) => {
    config.headers = config.headers || {};
    config.headers['X-App-Surface'] = getSurfaceName();

    const token = getStoredAccessToken();
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
    const originalRequest = error.config;
    const requestUrl = originalRequest?.url || '';
    const shouldBypassAuthRefresh = AUTH_BYPASS_PATHS.some((path) =>
      requestUrl.includes(path)
    );

    if (
      error.response?.status === 401 &&
      !originalRequest?._retry &&
      !shouldBypassAuthRefresh
    ) {
      originalRequest._retry = true;

      if (!refreshPromise) {
        refreshPromise = (async () => {
          try {
            const { data } = await axios.post(
              `${API_BASE_URL}/auth/refresh`,
              {},
              { withCredentials: true }
            );

            const token = data.data.accessToken;
            replaceStoredTokens(token);

            // Update Zustand store atomically
            useAuthStore.setState({
              user: data.data.user,
              accessToken: token,
              isAuthenticated: true,
              isLoading: false,
            });

            return token;
          } catch (refreshError) {
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

      try {
        const token = await refreshPromise;
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
    }

    if (status === 429) {
      const now = Date.now();
      if (now - lastRateLimitToastAt > RATE_LIMIT_TOAST_COOLDOWN_MS) {
        lastRateLimitToastAt = now;
        toast.error(error.response?.data?.message || 'طلبات كثيرة في وقت قصير. انتظر لحظات ثم حاول مرة أخرى.', { id: 'rate-limit' });
      }
      return Promise.reject(error);
    }

    if (status !== 401) {
      toast.error(errorMsg, { id: errorMsg });
    }
    return Promise.reject(error);
  }
);

export default apiClient;
