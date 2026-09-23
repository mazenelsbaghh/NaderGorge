import apiClient from './api-client';

export type WelcomeClaim = { token: string; kind: 'first' | 'returning'; expiresAt: string };
type Response<T> = { success: boolean; data: T };
const config = { suppressErrorToast: true };

export const studentWelcomeService = {
  async claim(): Promise<WelcomeClaim | null> {
    const { data } = await apiClient.post<Response<WelcomeClaim | null>>('/student/welcome/claim', {}, config);
    return data.success ? data.data : null;
  },
  async complete(token: string): Promise<boolean> {
    const { data } = await apiClient.post<Response<boolean>>('/student/welcome/complete', { token }, config);
    return data.success && data.data;
  },
  async release(token: string): Promise<void> {
    await apiClient.post('/student/welcome/release', { token }, config);
  },
};
