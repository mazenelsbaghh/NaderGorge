import assert from 'node:assert/strict';
import test from 'node:test';
import { renderWhatsAppTemplatePreview, whatsAppTemplateParameterCount } from './whatsapp-template.ts';
import type { LiveSupportWhatsAppTemplate } from '@/services/live-support-service';

const resultTemplate: LiveSupportWhatsAppTemplate = {
  id: 'template-1',
  name: 'student_result_2',
  language: 'ar_EG',
  category: 'UTILITY',
  status: 'APPROVED',
  lastSyncedAt: '2026-08-24T00:00:00Z',
  components: [
    { type: 'HEADER', text: 'ولي الأمر {{1}}' },
    { type: 'BODY', text: 'الطالب {{1}} حصل على {{2}} من {{3}} في {{4}}، {{5}}' },
  ],
};

test('header_and_body_placeholders_preserve_meta_parameter_order', () => {
  const parameters = ['أحمد', 'محمد', '26', '60', 'التاريخ', 'المحاضرة السابعة'];
  assert.equal(whatsAppTemplateParameterCount(resultTemplate), 6);
  assert.equal(
    renderWhatsAppTemplatePreview(resultTemplate, parameters),
    'ولي الأمر أحمد\nالطالب محمد حصل على 26 من 60 في التاريخ، المحاضرة السابعة',
  );
});
