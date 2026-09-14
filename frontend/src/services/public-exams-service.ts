import apiClient from './api-client';
import type { PublicExamProductDto } from './admin-sales-service';

function unwrap<T>(response: { data?: { data?: T } }): T {
  const data = response.data?.data;
  if (data === undefined) throw new Error('استجابة الخادم غير مكتملة.');
  return data;
}

export const publicExamsService = {
  async list() {
    return unwrap<PublicExamProductDto[]>(await apiClient.get('/public-exams'));
  },
};
