import apiClient from './api-client';

import type {
  MessengerPageLinkDraft,
  MessengerSettingsDraft,
} from '@/lib/facebook-messenger-settings';

interface ApiResponse<T> {
  data: T;
}

export interface FacebookMessengerAdminPage {
  id: string;
  pageId: string;
  displayName: string;
  accessTokenConfigured: boolean;
  humanAgentEnabled: boolean;
  connectionStatus: string;
  tokenValid: boolean | null;
  subscribed: boolean | null;
  lastCheckedAtUtc?: string | null;
  lastInboundAtUtc?: string | null;
  lastErrorCode?: string | null;
}

export interface FacebookMessengerAdminSettings {
  revision: string;
  appId: string;
  appSecretConfigured: boolean;
  verifyTokenConfigured: boolean;
  apiVersion: string;
  supportedApiVersions: string[];
  webhookUrl: string;
  pages: FacebookMessengerAdminPage[];
}

export interface RotateMessengerVerifyTokenResult {
  verifyToken: string;
  revision: string;
}

export interface CheckMessengerPageResult {
  page: FacebookMessengerAdminPage;
  revision: string;
}

type UpdateSettingsPayload = Pick<
  MessengerSettingsDraft,
  'appId' | 'apiVersion'
> & {
  appSecret?: string;
  expectedRevision: string;
};

type LinkPagePayload = MessengerPageLinkDraft & {
  expectedRevision: string;
  existingPageRecordId?: string;
};

export const facebookMessengerAdminService = {
  getSettings: async (signal?: AbortSignal) => {
    const response = await apiClient.get<
      ApiResponse<FacebookMessengerAdminSettings>
    >('/admin/live-support/messenger/settings', {
      signal,
      suppressErrorToast: true,
    });
    return response.data.data;
  },

  updateSettings: async (payload: UpdateSettingsPayload) => {
    const response = await apiClient.put<
      ApiResponse<FacebookMessengerAdminSettings>
    >('/admin/live-support/messenger/settings', payload, {
      suppressErrorToast: true,
    });
    return response.data.data;
  },

  rotateVerifyToken: async (expectedRevision: string) => {
    const response = await apiClient.post<
      ApiResponse<RotateMessengerVerifyTokenResult>
    >(
      '/admin/live-support/messenger/verify-token/rotate',
      { expectedRevision },
      { suppressErrorToast: true }
    );
    return response.data.data;
  },

  linkPage: async (payload: LinkPagePayload) => {
    const response = await apiClient.post<
      ApiResponse<FacebookMessengerAdminPage>
    >('/admin/live-support/messenger/pages/link', payload, {
      suppressErrorToast: true,
    });
    return response.data.data;
  },

  checkPage: async (pageRecordId: string) => {
    const response = await apiClient.post<
      ApiResponse<CheckMessengerPageResult>
    >(
      `/admin/live-support/messenger/pages/${encodeURIComponent(pageRecordId)}/check`,
      {},
      { suppressErrorToast: true }
    );
    return response.data.data;
  },

  unlinkPage: async (pageRecordId: string, expectedRevision: string) => {
    const response = await apiClient.delete<
      ApiResponse<FacebookMessengerAdminSettings>
    >(
      `/admin/live-support/messenger/pages/${encodeURIComponent(pageRecordId)}`,
      {
        data: { expectedRevision },
        suppressErrorToast: true,
      }
    );
    return response.data.data;
  },
};
