import assert from 'node:assert/strict';
import test from 'node:test';

import {
  createEmptyWhatsAppAudienceFilters,
  inspectCampaignTemplate,
  isWhatsAppCampaignPreviewCurrent,
  maskWhatsAppDestination,
  validateWhatsAppAudienceFilters,
  validateWhatsAppVariableMappings,
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
  fingerprint: 'template-v4',
};

test('template variables remain scoped by component and index', () => {
  const support = inspectCampaignTemplate(approvedTextTemplate);
  assert.equal(support.supported, true);
  assert.deepEqual(
    support.parameters.map((parameter) => parameter.key),
    ['HEADER:0:1', 'BODY:1:1', 'BODY:1:2'],
  );
});

test('media and button templates are disabled instead of being partially sent', () => {
  assert.equal(inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'HEADER', format: 'IMAGE' }, { type: 'BODY', text: 'خبر جديد' }],
  }).supported, false);
  assert.equal(inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'خبر جديد', buttons: [{ type: 'URL', text: 'افتح' }] }],
  }).supported, false);
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
    { componentType: 'HEADER', position: 1, source: 'Literal', literalValue: '  ' },
  ])[0], /النص الثابت/);
});

test('purchase date mapping stays bound to one paid package and complete range', () => {
  const requirements = inspectCampaignTemplate({
    ...approvedTextTemplate,
    components: [{ type: 'BODY', text: 'تاريخ شرائك {{1}}' }],
  }).parameters;
  const mappings = [{
    componentType: 'BODY' as const,
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
    templateFingerprint: 'template-v4',
    eligibleCount: 1,
    excludedCount: 0,
    excludedByReason: {},
    samples: [],
    expiresAt: '2999-01-01T00:00:00Z',
  };
  assert.equal(isWhatsAppCampaignPreviewCurrent(preview, approvedTextTemplate.id, approvedTextTemplate), true);
  assert.equal(isWhatsAppCampaignPreviewCurrent(preview, approvedTextTemplate.id, { ...approvedTextTemplate, fingerprint: 'template-v5' }), false);
  assert.equal(isWhatsAppCampaignPreviewCurrent({ ...preview, expiresAt: '2000-01-01T00:00:00Z' }, approvedTextTemplate.id, approvedTextTemplate), false);
});
