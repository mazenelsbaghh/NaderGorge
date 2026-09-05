import assert from 'node:assert/strict';
import test from 'node:test';

import {
  availableWhatsAppCampaignVariableSources,
  createEmptyWhatsAppAudienceFilters,
  inspectCampaignTemplate,
  isWhatsAppCampaignPreviewCurrent,
  maskWhatsAppDestination,
  validateWhatsAppAudienceFilters,
  validateWhatsAppVariableMappings,
  WHATSAPP_CAMPAIGN_VARIABLE_SOURCES,
  whatsAppCampaignVariableSourceLabel,
} from './whatsapp-campaign.ts';

const approvedTextTemplate = {
  id: 'template-1',
  name: 'lesson_reminder',
  language: 'ar',
  category: 'UTILITY',
  status: 'APPROVED',
  components: [
    { type: 'HEADER', format: 'TEXT', text: 'مرحبًا {{1}}' },
    { type: 'BODY', text: 'موعد حصة {{1}} هو {{2}}' },
  ],
  lastSyncedAt: '2026-08-25T10:00:00Z',
  fingerprint: 'a'.repeat(64),
};

test('template variables remain scoped by component and index', () => {
  const support = inspectCampaignTemplate(approvedTextTemplate);
  assert.equal(support.supported, true);
  assert.deepEqual(
    support.parameters.map((parameter) => parameter.key),
    ['HEADER:0:1', 'BODY:1:1', 'BODY:1:2'],
  );
});

test('2026-08-26 production template with two static URL buttons needs only body mappings', () => {
  const support = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [
      { type: 'HEADER', format: 'TEXT', text: 'متابعة تقدم الطالب' },
      { type: 'BODY', text: 'مرحبًا {{1}}، النتيجة {{2}}' },
      {
        type: 'BUTTONS',
        buttons: [
          { type: 'URL', text: 'فتح المنصة', url: 'https://massar-academy.net/student' },
          { type: 'URL', text: 'الدعم', url: 'https://massar-academy.net/support' },
        ],
      },
    ],
  });

  assert.equal(support.supported, true);
  assert.deepEqual(support.parameters.map((parameter) => parameter.key), ['BODY:1:1', 'BODY:1:2']);
});

test('a static phone button needs no mapping', () => {
  const support = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [
      { type: 'BODY', text: 'تواصل مع الدعم' },
      { type: 'BUTTONS', buttons: [{ type: 'PHONE_NUMBER', text: 'اتصل بنا', phone_number: '+201000000000' }] },
    ],
  });

  assert.equal(support.supported, true);
  assert.deepEqual(support.parameters, []);
});

test('media headers and button types without a safe send contract fail closed', () => {
  assert.equal(inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'HEADER', format: 'IMAGE' }, { type: 'BODY', text: 'خبر جديد' }],
  }).supported, false);
  for (const buttonType of ['QUICK_REPLY', 'FLOW', 'COPY_CODE']) {
    assert.equal(inspectCampaignTemplate({
      ...approvedTextTemplate,
      components: [
        { type: 'BODY', text: 'خبر جديد' },
        { type: 'BUTTONS', buttons: [{ type: buttonType, text: 'افتح' }] },
      ],
    }).supported, false);
  }
});

test('dynamic URL variables retain their exact component and button identity', () => {
  const support = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [
      { type: 'BODY', text: 'تفاصيل الطالب {{1}}' },
      {
        type: 'BUTTONS',
        buttons: [
          { type: 'URL', text: 'الملف', url: 'https://massar-academy.net/student/{{1}}' },
          { type: 'URL', text: 'التقرير', url: 'https://massar-academy.net/report/{{1}}' },
        ],
      },
    ],
  });

  assert.equal(support.supported, true);
  assert.deepEqual(
    support.parameters.map((parameter) => parameter.key),
    ['BODY:0:1', 'BUTTON:1:0:1', 'BUTTON:1:1:1'],
  );
  assert.equal(support.parameters[1].parameterType, 'URL_SUFFIX');
});

test('a dynamic URL mapping cannot satisfy another button at the same position', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [
      { type: 'BODY', text: 'تفاصيل' },
      {
        type: 'BUTTONS',
        buttons: [
          { type: 'URL', text: 'الملف', url: 'https://massar-academy.net/student/{{1}}' },
          { type: 'URL', text: 'التقرير', url: 'https://massar-academy.net/report/{{1}}' },
        ],
      },
    ],
  }).parameters;
  const firstButtonMapping = {
    componentType: 'BUTTON' as const,
    componentIndex: 1,
    buttonIndex: 0,
    position: 1,
    source: 'Literal' as const,
    literalValue: 'student-1',
  };

  assert.match(validateWhatsAppVariableMappings(requirements, [
    { ...firstButtonMapping, source: 'StudentFirstName' as const, literalValue: null },
  ])[0], /نصًا ثابتًا/);
  assert.equal(validateWhatsAppVariableMappings(requirements, [firstButtonMapping]).length, 1);
  assert.deepEqual(validateWhatsAppVariableMappings(requirements, [
    firstButtonMapping,
    { ...firstButtonMapping, buttonIndex: 1, literalValue: 'report-1' },
  ]), []);
});

test('dynamic URL accepts one final numbered suffix only', () => {
  for (const url of [
    'https://massar-academy.net/{{name}}',
    'https://massar-academy.net/{{2}}',
    'https://massar-academy.net/{{1}}/details',
    'http://massar-academy.net/{{1}}',
    'javascript:alert({{1}})',
  ]) {
    assert.equal(inspectCampaignTemplate({
      ...approvedTextTemplate,
      components: [
        { type: 'BODY', text: 'تفاصيل' },
        { type: 'BUTTONS', buttons: [{ type: 'URL', text: 'افتح', url }] },
      ],
    }).supported, false);
  }
});

test('named, mixed, and duplicate text components fail closed', () => {
  assert.equal(inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'مرحبًا {{name}} ورقمك {{1}}' }],
  }).supported, false);
  assert.equal(inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'الأول' }, { type: 'BODY', text: 'الثاني' }],
  }).supported, false);
});

test('a repeated numeric placeholder is one component-scoped requirement', () => {
  const support = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: '{{1}} — أهلاً مرة أخرى {{1}}' }],
  });
  assert.equal(support.supported, true);
  assert.deepEqual(support.parameters.map((parameter) => parameter.key), ['BODY:0:1']);
});

test('template category, body, footer, and numbering follow the server policy', () => {
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, fingerprint: 'stale' }).supported, false);
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, category: 'AUTHENTICATION' }).supported, false);
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, components: [{ type: 'HEADER', format: 'TEXT', text: 'تنبيه' }] }).supported, false);
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, components: [{ type: 'BODY', text: 'تنبيه' }, { type: 'FOOTER', text: '{{1}}' }] }).supported, false);
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, components: [{ type: 'BODY', text: '{{1}} ثم {{3}}' }] }).supported, false);
  assert.equal(inspectCampaignTemplate({ ...approvedTextTemplate, components: [{ type: 'BODY', text: '{{0}}' }] }).supported, false);
});

test('fixed variables require a non-empty audited value', () => {
  const requirements = inspectCampaignTemplate(approvedTextTemplate).parameters;
  assert.equal(validateWhatsAppVariableMappings(requirements, []).length, 3);
  assert.match(validateWhatsAppVariableMappings(requirements, [
    { componentType: 'HEADER', componentIndex: 0, position: 1, source: 'Literal', literalValue: '  ' },
  ])[0], /النص الثابت/);
  assert.match(validateWhatsAppVariableMappings(requirements, [
    { componentType: 'HEADER', componentIndex: 0, position: 1, source: 'Literal', literalValue: 'https://example.com' },
  ])[0], /رابطًا/);
  assert.match(validateWhatsAppVariableMappings(requirements, [
    { componentType: 'HEADER', componentIndex: 0, buttonIndex: 0, position: 1, source: 'Literal', literalValue: 'ولي الأمر' },
  ]).join(' '), /لا يطابق/);
});

test('utility templates offer parent tracking code with a generic preview label', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'رقم متابعة الطالب {{1}}' }],
  }).parameters;
  const mapping = {
    componentType: 'BODY' as const,
    componentIndex: 0,
    position: 1,
    source: 'ParentTrackingCode' as const,
  };

  assert.deepEqual(validateWhatsAppVariableMappings(requirements, [mapping], undefined, 'UTILITY'), []);
  assert.deepEqual(
    WHATSAPP_CAMPAIGN_VARIABLE_SOURCES.find((source) => source.value === mapping.source),
    { value: 'ParentTrackingCode', label: 'رقم متابعة الطالب' },
  );
  assert.equal(whatsAppCampaignVariableSourceLabel(mapping.source), 'رقم متابعة الطالب');
  assert.equal(
    availableWhatsAppCampaignVariableSources('UTILITY', 'TEXT').some((source) => source.value === mapping.source),
    true,
  );
});

test('parent tracking code rejects literal and reference configuration', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'رقم متابعة الطالب {{1}}' }],
  }).parameters;
  const mapping = {
    componentType: 'BODY' as const,
    componentIndex: 0,
    position: 1,
    source: 'ParentTrackingCode' as const,
  };

  assert.match(validateWhatsAppVariableMappings(requirements, [
    { ...mapping, literalValue: 'لا يُسمح', referenceId: 'student-1' },
  ], undefined, 'UTILITY')[0], /لا يقبل/);
});

test('parent tracking code is unavailable to marketing campaign templates', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    category: 'MARKETING',
    components: [{ type: 'BODY', text: 'رقم متابعة الطالب {{1}}' }],
  }).parameters;
  const mapping = {
    componentType: 'BODY' as const,
    componentIndex: 0,
    position: 1,
    source: 'ParentTrackingCode' as const,
  };

  assert.equal(
    availableWhatsAppCampaignVariableSources('MARKETING', 'TEXT').some((source) => source.value === mapping.source),
    false,
  );
  assert.match(
    validateWhatsAppVariableMappings(requirements, [mapping], undefined, 'MARKETING')[0],
    /UTILITY/,
  );
});

test('purchase date mapping stays bound to one paid package and complete range', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'تاريخ شرائك {{1}}' }],
  }).parameters;
  const mappings = [{
    componentType: 'BODY' as const,
    componentIndex: 0,
    position: 1,
    source: 'PurchaseDate' as const,
    referenceId: 'package-1',
    format: 'dd/MM/yyyy',
  }];
  const filters = createEmptyWhatsAppAudienceFilters();
  assert.equal(validateWhatsAppVariableMappings(requirements, mappings, filters).length, 1);
  filters.hasPaidPurchase = true;
  filters.packageIds = ['package-1'];
  filters.purchaseFromUtc = '2026-08-01T00:00:00Z';
  filters.purchaseToUtc = '2026-09-01T00:00:00Z';
  assert.deepEqual(validateWhatsAppVariableMappings(requirements, mappings, filters), []);
  filters.packageIds = ['package-2'];
  assert.equal(validateWhatsAppVariableMappings(requirements, mappings, filters).length, 1);
});

test('spreadsheet variables require an explicit column name', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'مرحبًا {{1}}' }],
  }).parameters;
  const mapping = {
    componentType: 'BODY' as const,
    componentIndex: 0,
    position: 1,
    source: 'SpreadsheetColumn' as const,
  };

  assert.match(validateWhatsAppVariableMappings(requirements, [mapping])[0], /عمود الشيت/);
  assert.deepEqual(validateWhatsAppVariableMappings(
    requirements,
    [{ ...mapping, columnName: 'student_name' }],
  ), []);
});

test('negative watch targeting requires an exact lesson and bounded period', () => {
  const filters = createEmptyWhatsAppAudienceFilters();
  filters.hasWatched = false;
  assert.match(validateWhatsAppAudienceFilters(filters).join(' '), /حصة محددة/);
  assert.match(validateWhatsAppAudienceFilters(filters).join(' '), /نطاقًا دراسيًا/);
  filters.lessonIds = ['lesson-1'];
  filters.watchFromUtc = '2026-08-01';
  filters.watchToUtc = '2026-08-25';
  assert.deepEqual(validateWhatsAppAudienceFilters(filters), []);
});

test('activity scope cannot remain active without selecting its state', () => {
  const filters = createEmptyWhatsAppAudienceFilters();
  filters.lessonIds = ['lesson-1'];
  filters.watchFromUtc = '2026-08-01T00:00:00Z';
  filters.watchToUtc = '2026-08-02T00:00:00Z';
  assert.match(validateWhatsAppAudienceFilters(filters).join(' '), /نطاق المشاهدة/);
});

test('audience date ranges follow the server one-year bound', () => {
  const filters = createEmptyWhatsAppAudienceFilters();
  filters.hasPaidPurchase = true;
  filters.purchaseFromUtc = '2025-01-01T00:00:00Z';
  filters.purchaseToUtc = '2026-02-01T00:00:00Z';
  assert.match(validateWhatsAppAudienceFilters(filters).join(' '), /لا يمكن أن تتجاوز/);
});

test('active access alone cannot widen a negative purchase audience', () => {
  const filters = createEmptyWhatsAppAudienceFilters();
  filters.hasActiveAccess = true;
  filters.hasPaidPurchase = false;
  filters.purchaseFromUtc = '2026-08-01';
  filters.purchaseToUtc = '2026-09-01';
  assert.match(validateWhatsAppAudienceFilters(filters).join(' '), /نطاقًا دراسيًا/);
});

test('destinations are defensively masked even if an API returns a raw number', () => {
  const masked = maskWhatsAppDestination('+201279799432');
  assert.equal(masked, '•••• •••• 32');
  assert.equal(masked.includes('01279799432'), false);
});

test('template drift and expired previews invalidate launch review', () => {
  const preview = {
    audienceFingerprint: 'server-issued',
    templateFingerprint: approvedTextTemplate.fingerprint,
    eligibleCount: 1,
    excludedCount: 0,
    excludedByReason: {},
    samples: [],
    expiresAt: '2999-01-01T00:00:00Z',
  };
  assert.equal(isWhatsAppCampaignPreviewCurrent(preview, approvedTextTemplate.id, approvedTextTemplate), true);
  assert.equal(isWhatsAppCampaignPreviewCurrent(preview, approvedTextTemplate.id, { ...approvedTextTemplate, fingerprint: 'b'.repeat(64) }), false);
  assert.equal(isWhatsAppCampaignPreviewCurrent({ ...preview, expiresAt: '2000-01-01T00:00:00Z' }, approvedTextTemplate.id, approvedTextTemplate), false);
});
