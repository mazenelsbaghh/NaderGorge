import { create } from 'zustand';

import {
  clearStoredAuth,
  persistAuthSession,
  readStoredAuth,
  updateStoredUser,
} from '@/lib/auth-storage';

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
  loadFromStorage: () => void;
}

function loadInitialAuth(): Pick<AuthState, 'user' | 'accessToken' | 'isAuthenticated' | 'isLoading'> {
  if (typeof window === 'undefined') {
    return { user: null, accessToken: null, isAuthenticated: false, isLoading: true };
  }
  const storedAuth = readStoredAuth();
  if (storedAuth) {
    return {
      user: storedAuth.user as User,
      accessToken: storedAuth.accessToken,
      isAuthenticated: true,
      isLoading: false,
    };
  }
  return { user: null, accessToken: null, isAuthenticated: false, isLoading: false };
}

const initialAuth = loadInitialAuth();

export const useAuthStore = create<AuthState>((set, get) => ({
  ...initialAuth,

  setAuth: (user, accessToken, rememberMe) => {
    persistAuthSession({ user, accessToken }, rememberMe);
    set({ user, accessToken, isAuthenticated: true, isLoading: false });
  },

  clearAuth: () => {
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

  loadFromStorage: () => {
    if (typeof window === 'undefined') {
      set({ isLoading: false });
      return;
    }
    const storedAuth = readStoredAuth();

    if (!storedAuth) {
      set({ isLoading: false });
      return;
    }

    set({
      user: storedAuth.user as User,
      accessToken: storedAuth.accessToken,
      isAuthenticated: true,
      isLoading: false,
    });
  },
}));
