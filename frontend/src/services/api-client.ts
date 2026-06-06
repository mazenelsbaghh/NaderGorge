import axios from 'axios';
import toast from 'react-hot-toast';

import {
  clearStoredAuth,
  getStoredAccessToken,
} from '@/lib/auth-storage';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

const AUTH_BYPASS_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/parent/reports'];
const RATE_LIMIT_TOAST_COOLDOWN_MS = 4_000;
let lastRateLimitToastAt = 0;

// Request interceptor: attach JWT token
apiClient.interceptors.request.use(
  (config) => {
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
      !shouldBypassAuthRefresh
    ) {
      clearStoredAuth();
      if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
      return Promise.reject(error);
    }

    const status = error.response?.status;
    const errorMsg = error.response?.data?.message || error.message || 'An error occurred';

    if (status === 429) {
      const now = Date.now();
      if (now - lastRateLimitToastAt > RATE_LIMIT_TOAST_COOLDOWN_MS) {
        lastRateLimitToastAt = now;
        toast.error(error.response?.data?.message || 'طلبات كثيرة في وقت قصير. انتظر لحظات ثم حاول مرة أخرى.');
      }
      return Promise.reject(error);
    }

    if (status !== 401) {
      toast.error(errorMsg);
    }
    return Promise.reject(error);
  }
);

export default apiClient;
