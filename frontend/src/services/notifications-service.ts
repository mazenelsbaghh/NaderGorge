import apiClient from '@/services/api-client';

export type PlatformNotificationDto = {
  id: string;
  title: string;
  body: string;
  readAt?: string | null;
  createdAt: string;
};

export const notificationsService = {
  list: async (): Promise<PlatformNotificationDto[]> =>
    (await apiClient.get<PlatformNotificationDto[]>('/notifications')).data ?? [],
  markRead: async (id: string): Promise<void> => {
    await apiClient.post(`/notifications/${id}/read`);
  },
};
