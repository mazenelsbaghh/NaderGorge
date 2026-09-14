'use client';

import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import { useStudentShellStore } from '@/stores/student-shell-store';

export function StudentRealtimeBridge() {
  usePlatformEvents({
    onBalanceChanged: (payload) => {
      useStudentShellStore.getState().setBalance(payload.newBalance);
    },
    onNotificationCreated: () => {
      const current = useStudentShellStore.getState().unreadNotificationsCount;
      useStudentShellStore.getState().setUnreadCount(current + 1);
    },
    onNotificationRead: () => {
      const current = useStudentShellStore.getState().unreadNotificationsCount;
      useStudentShellStore.getState().setUnreadCount(Math.max(0, current - 1));
    },
    onNotificationsCleared: () => {
      useStudentShellStore.getState().setUnreadCount(0);
    },
  });

  return null;
}
