import { create } from 'zustand';
import { studentService } from '@/services/student-service';
import { registerCacheStore } from '@/lib/cache-invalidation';

interface StudentShellState {
  unreadNotificationsCount: number;
  currentBalance: number;
  gamificationPoints: number;
  gamificationLevel: string;
  avatarSlug: string | null;
  isLoading: boolean;
  lastFetchedAt: number | null;
  parentTrackingCode: string;
  hasSeenTrackingCodePopup: boolean;

  fetchBootstrap: (force?: boolean) => Promise<void>;
  setUnreadCount: (count: number) => void;
  setBalance: (balance: number) => void;
  acknowledgeTrackingPopup: () => Promise<void>;
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
  parentTrackingCode: '',
  hasSeenTrackingCodePopup: true, // Default to true to prevent screen flash before bootstrap load

  fetchBootstrap: async (force = false) => {
    console.log('fetchBootstrap CALLED in store, force:', force);
    const { lastFetchedAt, isLoading } = get();
    const now = Date.now();

    if (isLoading) {
      console.log('fetchBootstrap: isLoading is true, skipping');
      return;
    }
    if (!force && lastFetchedAt && now - lastFetchedAt < CACHE_TTL) {
      return; // Use cache
    }

    set({ isLoading: true });
    try {
      const data = await studentService.getShellBootstrap();
      console.log('BOOTSTRAP DATA FETCHED CLIENT SIDE:', JSON.stringify(data));
      set({
        unreadNotificationsCount: data.unreadNotificationsCount,
        currentBalance: Number(data.currentBalance),
        gamificationPoints: data.gamification.totalPoints,
        gamificationLevel: data.gamification.levelName,
        avatarSlug: data.avatarSlug,
        parentTrackingCode: data.parentTrackingCode || '',
        hasSeenTrackingCodePopup: data.hasSeenTrackingCodePopup ?? true,
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

  acknowledgeTrackingPopup: async () => {
    try {
      await studentService.acknowledgeTrackingPopup();
      set({ hasSeenTrackingCodePopup: true });
    } catch (err) {
      console.error('Failed to acknowledge tracking popup:', err);
      // Even if network fails, dismiss the popup on client to not annoy user
      set({ hasSeenTrackingCodePopup: true });
    }
  },
}));

registerCacheStore(
  'student:shell',
  () => {},
  () => {
    void useStudentShellStore.getState().fetchBootstrap(true);
  }
);
