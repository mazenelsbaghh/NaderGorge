import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildMessengerSettingsPayload,
  canReplaceMessengerPageToken,
  messengerErrorMessage,
  messengerPageOperationState,
  messengerSetupState,
  pageConnectionState,
  validateMessengerPageLink,
  validateMessengerSettings,
} from './facebook-messenger-settings.ts';

test('keeps uncertain page operations directional while allowing credential repair', () => {
  assert.equal(messengerPageOperationState('LinkUncertain'), 'linkPending');
  assert.equal(messengerPageOperationState('UnlinkSettling'), 'unlinkPending');
  assert.equal(
    messengerPageOperationState('RemoteUnsubscribeConfirmed'),
    'unlinkPending'
  );
  assert.equal(messengerPageOperationState('Connected'), 'idle');
  assert.equal(canReplaceMessengerPageToken('LinkUncertain'), true);
  assert.equal(canReplaceMessengerPageToken('UnlinkUncertain'), true);
  assert.equal(canReplaceMessengerPageToken('UnlinkSettling'), false);
  assert.equal(canReplaceMessengerPageToken('Linking'), false);
  assert.equal(
    canReplaceMessengerPageToken('RemoteUnsubscribeConfirmed'),
    false
  );
});

test('write-only App Secret is omitted when an existing secret is preserved', () => {
  assert.deepEqual(
    buildMessengerSettingsPayload(
      { appId: ' 123456789 ', appSecret: '   ', apiVersion: 'v26.0' },
      'revision-1'
    ),
    {
      appId: '123456789',
      apiVersion: 'v26.0',
      expectedRevision: 'revision-1',
    }
  );
});

test('new configuration requires a secret and a supported API version', () => {
  assert.equal(
    validateMessengerSettings(
      { appId: '123456789', appSecret: '', apiVersion: 'v26.0' },
      false,
      ['v26.0']
    ),
    'أدخل App Secret لإكمال إعداد تطبيق Meta.'
  );
  assert.equal(
    validateMessengerSettings(
      {
        appId: '123456789',
        appSecret: '1234567890123456',
        apiVersion: 'v99.0',
      },
      false,
      ['v26.0']
    ),
    'اختر إصدار Graph API متاحًا من القائمة.'
  );
});

test('changing App ID requires the new application secret', () => {
  assert.equal(
    validateMessengerSettings(
      { appId: '222222', appSecret: '', apiVersion: 'v26.0' },
      true,
      ['v26.0'],
      '111111'
    ),
    'أدخل App Secret الخاص بالتطبيق الجديد عند تغيير App ID.'
  );
});

test('page token validation rejects blanks and whitespace', () => {
  assert.equal(
    validateMessengerPageLink({ accessToken: '', humanAgentEnabled: false }),
    'ألصق Page Access Token أولًا.'
  );
  assert.equal(
    validateMessengerPageLink({
      accessToken: 'x'.repeat(20) + ' token',
      humanAgentEnabled: false,
    }),
    'Page Access Token لا يجب أن يحتوي على مسافات.'
  );
});

test('page state requires both a valid token and an active subscription', () => {
  assert.equal(
    pageConnectionState({ tokenValid: true, subscribed: true }),
    'connected'
  );
  assert.equal(
    pageConnectionState({ tokenValid: true, subscribed: null }),
    'notChecked'
  );
  assert.equal(
    pageConnectionState({ tokenValid: false, subscribed: true }),
    'attention'
  );
});

test('overall setup remains incomplete until app, verify token, and a page exist', () => {
  assert.equal(
    messengerSetupState({
      appId: '123',
      appSecretConfigured: true,
      verifyTokenConfigured: false,
      pages: [],
    }),
    'incomplete'
  );
  assert.equal(
    messengerSetupState({
      appId: '123',
      appSecretConfigured: true,
      verifyTokenConfigured: true,
      pages: [{ tokenValid: true, subscribed: true }],
    }),
    'ready'
  );
});

test('provider error codes are translated without echoing provider payloads', () => {
  assert.equal(
    messengerErrorMessage('MESSENGER_GRAPH_190'),
    'Page Access Token غير صالح أو منتهي.'
  );
  assert.equal(
    messengerErrorMessage('UNKNOWN_PROVIDER_TEXT'),
    'يحتاج الربط إلى مراجعة.'
  );
});
