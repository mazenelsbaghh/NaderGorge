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
  profileComplete: boolean;
  avatarSlug?: string | null;
}

interface AuthState {
  user: User | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  setAuth: (
    user: User,
    accessToken: string,
    refreshToken: string,
    rememberMe: boolean
  ) => void;
  clearAuth: () => void;
  setLoading: (loading: boolean) => void;
  updateProfile: (profileComplete: boolean) => void;
  updateAvatar: (avatarSlug: string | null) => void;
  loadFromStorage: () => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
  isLoading: true,

  setAuth: (user, accessToken, refreshToken, rememberMe) => {
    persistAuthSession({ user, accessToken, refreshToken }, rememberMe);
    set({ user, accessToken, refreshToken, isAuthenticated: true, isLoading: false });
  },

  clearAuth: () => {
    clearStoredAuth();
    set({
      user: null,
      accessToken: null,
      refreshToken: null,
      isAuthenticated: false,
      isLoading: false,
    });
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
      refreshToken: storedAuth.refreshToken,
      isAuthenticated: true,
      isLoading: false,
    });
  },
}));
