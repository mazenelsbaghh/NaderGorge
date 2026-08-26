import assert from 'node:assert/strict';
import test from 'node:test';
import {
  inspectDirectWhatsAppTemplate,
  renderWhatsAppTemplatePreview,
  whatsAppTemplateParameterCount,
} from './whatsapp-template.ts';
import type { LiveSupportWhatsAppTemplate } from '@/services/live-support-service';

const resultTemplate: LiveSupportWhatsAppTemplate = {
  id: 'template-1',
  name: 'student_result_2',
  language: 'ar_EG',
  category: 'UTILITY',
  status: 'APPROVED',
  lastSyncedAt: '2026-08-24T00:00:00Z',
  fingerprint: 'a'.repeat(64),
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

test('out-of-order body placeholders use numeric positions after the header offset', () => {
  const template: LiveSupportWhatsAppTemplate = {
    ...resultTemplate,
    components: [
      { type: 'HEADER', text: 'ولي الأمر {{1}}' },
      { type: 'BODY', text: 'القيمة الثانية {{2}} ثم الأولى {{1}}' },
    ],
  };

  assert.equal(
    renderWhatsAppTemplatePreview(template, ['أحمد', 'الأولى', 'الثانية']),
    'ولي الأمر أحمد\nالقيمة الثانية الثانية ثم الأولى الأولى',
  );
});

test('2026-08-26 direct picker keeps the production static URL template selectable', () => {
  const template: LiveSupportWhatsAppTemplate = {
    ...resultTemplate,
    name: 'student_progress_tracking22',
    components: [
      { type: 'HEADER', format: 'TEXT', text: 'متابعة الطالب' },
      { type: 'BODY', text: 'الطالب {{1}} أتم {{2}}' },
      {
        type: 'BUTTONS',
        buttons: [
          { type: 'URL', text: 'فتح المنصة', url: 'https://massar-academy.net/student' },
          { type: 'URL', text: 'عرض التقرير', url: 'https://massar-academy.net/report' },
        ],
      },
    ],
  };

  assert.equal(inspectDirectWhatsAppTemplate(template).supported, true);
  assert.equal(whatsAppTemplateParameterCount(template), 2);
});

test('direct picker accepts a static phone button without asking for another value', () => {
  const template: LiveSupportWhatsAppTemplate = {
    ...resultTemplate,
    components: [
      { type: 'BODY', text: 'تواصل مع الدعم يا {{1}}' },
      { type: 'BUTTONS', buttons: [{ type: 'PHONE_NUMBER', text: 'اتصل بنا', phone_number: '+201000000000' }] },
    ],
  };

  assert.equal(inspectDirectWhatsAppTemplate(template).supported, true);
  assert.equal(whatsAppTemplateParameterCount(template), 1);
});

test('direct picker rejects a dynamic URL before outbound delivery', () => {
  const template: LiveSupportWhatsAppTemplate = {
    ...resultTemplate,
    components: [
      { type: 'BODY', text: 'افتح التقرير' },
      { type: 'BUTTONS', buttons: [{ type: 'URL', text: 'عرض', url: 'https://massar-academy.net/report/{{1}}' }] },
    ],
  };

  assert.equal(inspectDirectWhatsAppTemplate(template).supported, false);
  assert.equal(whatsAppTemplateParameterCount(template), 0);
  assert.equal(renderWhatsAppTemplatePreview(template, ['student-token']), '');
});

test('direct picker rejects interactive and media components without a matching sender contract', () => {
  const unsafeComponents = [
    [{ type: 'HEADER', format: 'IMAGE' }, { type: 'BODY', text: 'خبر جديد' }],
    [{ type: 'BODY', text: 'خبر جديد' }, { type: 'BUTTONS', buttons: [{ type: 'QUICK_REPLY', text: 'رد' }] }],
  ];

  for (const components of unsafeComponents) {
    assert.equal(inspectDirectWhatsAppTemplate({ ...resultTemplate, components }).supported, false);
  }
});

test('direct picker rejects repeated or spaced placeholders that the flat sender cannot reproduce', () => {
  for (const text of ['مرحبًا {{1}} ثم {{1}}', 'مرحبًا {{ 1 }}']) {
    assert.equal(inspectDirectWhatsAppTemplate({
      ...resultTemplate,
      components: [{ type: 'BODY', text }],
    }).supported, false);
  }
});
