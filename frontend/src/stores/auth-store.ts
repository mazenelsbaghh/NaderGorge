import { create } from 'zustand';

import {
  clearStoredAuth,
  persistAuthSession,
  readStoredAuth,
  updateStoredUser,
} from '@/lib/auth-storage';
import { clearAccessToken, setAccessToken } from '@/lib/auth-memory';
import { getSurfaceName } from '@/packages/surface-runtime/config';
import { platformQueryClient } from '@/lib/query-client';

interface User {
  id: string;
  fullName: string;
  phone: string;
  roles: string[];
  permissions: string[];
  profileComplete: boolean;
  avatarSlug?: string | null;
  allowedDomains?: string[];
  allowedNavbarItems?: string[];
  authorizationVersion?: number;
}

interface AuthState {
  user: User | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  setAuth: (
    user: User,
    accessToken: string,
    rememberMe: boolean
  ) => void;
  clearAuth: () => void;
  logout: () => Promise<void>;
  setLoading: (loading: boolean) => void;
  updateProfile: (profileComplete: boolean) => void;
  updateAvatar: (avatarSlug: string | null) => void;
  refreshCurrentSession: () => Promise<void>;
  loadFromStorage: () => void;
}

function loadInitialAuth(): Pick<AuthState, 'user' | 'accessToken' | 'isAuthenticated' | 'isLoading'> {
  // Server and browser must share the same first snapshot; AuthBootstrap reads
  // browser storage after hydration and resolves this loading state.
  return { user: null, accessToken: null, isAuthenticated: false, isLoading: true };
}

const initialAuth = loadInitialAuth();

function authorizationBoundary(user: User | null | undefined) {
  if (!user) return null;
  return JSON.stringify({
    id: user.id,
    authorizationVersion: user.authorizationVersion ?? 0,
    roles: [...user.roles].sort(),
    permissions: [...user.permissions].sort(),
    allowedDomains: [...(user.allowedDomains ?? [])].sort(),
    allowedNavbarItems: [...(user.allowedNavbarItems ?? [])].sort(),
  });
}

function clearQueriesForBoundaryTransition(
  previous: User | null | undefined,
  next: User | null | undefined
) {
  if (authorizationBoundary(previous) !== authorizationBoundary(next)) {
    platformQueryClient.removeQueries();
  }
}

// A single rejected request can fan out into several 403 handlers (for example,
// while a page is mounting). Keep one in-flight read so those handlers do not
// stampede `/auth/session` and trigger the API rate limit.
let currentSessionRefreshPromise: Promise<void> | null = null;

function shouldAttemptCookieRefresh(hasStoredUser: boolean) {
  if (typeof window === 'undefined') return false;
  if (!hasStoredUser) return false;

  const publicPath = window.location.pathname;
  if (publicPath === '/' || ['/login', '/register', '/forgot-password'].includes(publicPath)) {
    return false;
  }

  const surface = getSurfaceName();
  if (surface === 'admin' || surface === 'teacher' || surface === 'assistant') {
    return true;
  }

  // Public/guest pages must not probe /auth/refresh on every visit. A guest
  // support session is authenticated by its own cookie and is unrelated to
  // the platform refresh-token cookie.
  return window.location.pathname.startsWith('/student');
}

export const useAuthStore = create<AuthState>((set, get) => ({
  ...initialAuth,

  setAuth: (user, accessToken, rememberMe) => {
    clearQueriesForBoundaryTransition(get().user, user);
    setAccessToken(accessToken);
    persistAuthSession({ user, accessToken }, rememberMe);
    set({ user, accessToken, isAuthenticated: true, isLoading: false });
  },

  clearAuth: () => {
    platformQueryClient.removeQueries();
    clearAccessToken();
    clearStoredAuth();
    set({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      isLoading: false,
    });
  },

  logout: async () => {
    try {
      const { authService } = await import('@/services/auth-service');
      await authService.logout();
    } catch (err) {
      console.error('Logout request failed:', err);
    } finally {
      get().clearAuth();
    }
  },

  setLoading: (loading) => set({ isLoading: loading }),

  updateProfile: (profileComplete) => {
    const { user } = get();
    if (user) {
      const updated = { ...user, profileComplete };
      updateStoredUser(updated);
      set({ user: updated });
    }
  },

  updateAvatar: (avatarSlug) => {
    const { user } = get();
    if (user) {
      const updated = { ...user, avatarSlug };
      updateStoredUser(updated);
      set({ user: updated });
    }
  },

  refreshCurrentSession: () => {
    if (currentSessionRefreshPromise) {
      return currentSessionRefreshPromise;
    }

    const { user, isAuthenticated } = get();
    if (!isAuthenticated || !user) return Promise.resolve();

    const refreshingUserId = user.id;

    currentSessionRefreshPromise = (async () => {
      try {
        const { authService } = await import('@/services/auth-service');
        const response = await authService.getCurrentSession();
        const snapshot = response.data.data;
        if (!snapshot?.user) return;

        // Do not restore a session that was cleared or replaced while this
        // request was in flight (such as a logout followed by another login).
        const latest = get();
        if (!latest.isAuthenticated || latest.user?.id !== refreshingUserId) return;

        clearQueriesForBoundaryTransition(latest.user, snapshot.user as User);
        updateStoredUser(snapshot.user);
        set({ user: snapshot.user as User });
      } catch (error) {
        console.warn('Current session refresh failed; backend remains authoritative.', error);
      }
    })().finally(() => {
      currentSessionRefreshPromise = null;
    });

    return currentSessionRefreshPromise;
  },

  loadFromStorage: () => {
    if (typeof window === 'undefined') {
      set({ isLoading: false });
      return;
    }
    const storedAuth = readStoredAuth();

    if (storedAuth?.accessToken) {
      clearQueriesForBoundaryTransition(get().user, storedAuth.user as User);
      setAccessToken(storedAuth.accessToken);
      set({
        user: storedAuth.user as User,
        accessToken: storedAuth.accessToken,
        isAuthenticated: true,
        isLoading: false,
      });
      return;
    }

    if (!shouldAttemptCookieRefresh(Boolean(storedAuth?.user))) {
      platformQueryClient.removeQueries();
      clearAccessToken();
      set({ user: null, accessToken: null, isAuthenticated: false, isLoading: false });
      return;
    }

    void (async () => {
      try {
        const { authService } = await import('@/services/auth-service');
        const response = await authService.refresh();
        const payload = response.data.data;
        clearQueriesForBoundaryTransition(get().user, payload.user);
        setAccessToken(payload.accessToken);
        persistAuthSession({ user: payload.user, accessToken: null }, true);
        set({
          user: payload.user,
          accessToken: payload.accessToken,
          isAuthenticated: true,
          isLoading: false,
        });
      } catch {
        if (storedAuth?.user) {
          clearStoredAuth();
        }
        platformQueryClient.removeQueries();
        clearAccessToken();
        set({ user: null, accessToken: null, isAuthenticated: false, isLoading: false });
      }
    })();
  },
}));
