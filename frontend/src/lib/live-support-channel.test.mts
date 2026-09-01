import assert from 'node:assert/strict';
import test from 'node:test';

import {
  getLiveSupportChannelPresentation,
  isExternalChannel,
  normalizeLiveSupportChannel,
  resolveLiveSupportChannelCapabilities,
} from './live-support-channel.ts';

test('normalizes missing and unknown legacy channel values to Web', () => {
  for (const candidate of [undefined, null, '', 'Email']) {
    assert.equal(normalizeLiveSupportChannel(candidate), 'Web');
  }
});

test('classifies external channels without treating Web as external', () => {
  const cases = [
    { channel: 'Web', expected: false },
    { channel: 'WhatsApp', expected: true },
    { channel: 'Messenger', expected: true },
  ] as const;

  for (const scenario of cases) {
    assert.equal(isExternalChannel(scenario.channel), scenario.expected);
  }
});

test('presents Messenger with its page name and WhatsApp with its phone number', () => {
  assert.deepEqual(
    getLiveSupportChannelPresentation({
      channel: 'Messenger',
      externalPageId: 'page-technical-id',
      externalPageName: '  أكاديمية مسار  ',
    }),
    {
      channel: 'Messenger',
      label: 'ماسنجر',
      detail: 'أكاديمية مسار',
    }
  );

  assert.equal(
    getLiveSupportChannelPresentation({
      channel: 'WhatsApp',
      externalPhoneNumber: ' +201000000000 ',
    }).detail,
    '+201000000000'
  );
});

test('uses calm fallback presentation when external account details are absent', () => {
  assert.deepEqual(
    getLiveSupportChannelPresentation({ channel: 'Messenger' }),
    {
      channel: 'Messenger',
      label: 'ماسنجر',
      detail: 'محادثة صفحة فيسبوك',
    }
  );
  assert.deepEqual(getLiveSupportChannelPresentation({}), {
    channel: 'Web',
    label: 'الموقع',
    detail: 'محادثة داخل الموقع',
  });
});

test('keeps Messenger human-only and honors the backend-issued reply window', () => {
  const currentTime = Date.parse('2026-08-30T10:00:00.000Z');
  const open = resolveLiveSupportChannelCapabilities(
    {
      channel: 'Messenger',
      canSend: true,
      externalPageId: 'page-1',
      externalPageName: 'الصفحة الأولى',
      customerServiceWindowExpiresAt: '2026-09-06T10:00:00.000Z',
    },
    currentTime
  );

  assert.equal(open.isHumanOnly, true);
  assert.equal(open.usesExternalThread, true);
  assert.equal(open.supportsTemplates, false);
  assert.equal(open.canSendFreeform, true);
  assert.equal(open.canSendAttachments, false);

  const closed = resolveLiveSupportChannelCapabilities(
    {
      channel: 'Messenger',
      canSend: true,
      customerServiceWindowExpiresAt: 'invalid-date',
    },
    currentTime
  );
  assert.equal(closed.customerServiceWindowOpen, false);
  assert.equal(closed.canSendFreeform, false);
  assert.equal(closed.canSendAttachments, false);
});

test('keeps WhatsApp templates available after freeform reply window closes', () => {
  const capabilities = resolveLiveSupportChannelCapabilities(
    {
      channel: 'WhatsApp',
      canSend: true,
      customerServiceWindowExpiresAt: '2026-08-30T09:59:59.000Z',
    },
    Date.parse('2026-08-30T10:00:00.000Z')
  );

  assert.equal(capabilities.customerServiceWindowOpen, false);
  assert.equal(capabilities.canSendFreeform, false);
  assert.equal(capabilities.canSendTemplate, true);
});

test('honors the current DTO canSend flag for Web conversations', () => {
  const capabilities = resolveLiveSupportChannelCapabilities({
    channel: 'Web',
    canSend: false,
  });

  assert.equal(capabilities.customerServiceWindowOpen, null);
  assert.equal(capabilities.canSendFreeform, false);
  assert.equal(capabilities.canSendAttachments, false);
  assert.equal(capabilities.supportsMessageReply, true);
  assert.equal(capabilities.supportsParticipantTypingPreview, true);
});
