import apiClient from './api-client';

export interface WhatsAppCheckResponse {
  exists: boolean | null;
  number: string;
}

/**
 * Check if a phone number is registered on WhatsApp.
 * Calls our backend proxy which communicates with Evolution API.
 *
 * @param phoneNumber - Egyptian phone number (e.g. 01012345678)
 */
export async function checkWhatsApp(phoneNumber: string): Promise<WhatsAppCheckResponse> {
  const { data } = await apiClient.post<WhatsAppCheckResponse>('/whatsapp/check', {
    phoneNumber,
  });
  return data;
}
