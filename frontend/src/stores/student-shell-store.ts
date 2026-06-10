import { create } from 'zustand';
import { studentService } from '@/services/student-service';

interface StudentShellState {
  unreadNotificationsCount: number;
  currentBalance: number;
  gamificationPoints: number;
  gamificationLevel: string;
  avatarSlug: string | null;
  isLoading: boolean;
  lastFetchedAt: number | null;

  fetchBootstrap: (force?: boolean) => Promise<void>;
  setUnreadCount: (count: number) => void;
  setBalance: (balance: number) => void;
}

const CACHE_TTL = 30000; // 30 seconds cache

export const useStudentShellStore = create<StudentShellState>((set, get) => ({
  unreadNotificationsCount: 0,
  currentBalance: 0,
  gamificationPoints: 0,
  gamificationLevel: 'طالب',
  avatarSlug: null,
  isLoading: false,
  lastFetchedAt: null,

  fetchBootstrap: async (force = false) => {
    const { lastFetchedAt, isLoading } = get();
    const now = Date.now();

    if (isLoading) return;
    if (!force && lastFetchedAt && now - lastFetchedAt < CACHE_TTL) {
      return; // Use cache
    }

    set({ isLoading: true });
    try {
      const data = await studentService.getShellBootstrap();
      set({
        unreadNotificationsCount: data.unreadNotificationsCount,
        currentBalance: Number(data.currentBalance),
        gamificationPoints: data.gamification.totalPoints,
        gamificationLevel: data.gamification.levelName,
        avatarSlug: data.avatarSlug,
        lastFetchedAt: now,
        isLoading: false,
      });
    } catch (err) {
      console.error('Failed to fetch student shell bootstrap:', err);
      set({ isLoading: false });
    }
  },

  setUnreadCount: (count) => set({ unreadNotificationsCount: count }),
  setBalance: (balance) => set({ currentBalance: balance }),
}));
