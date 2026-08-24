import { test } from 'node:test';
import assert from 'node:assert/strict';
import { publicJobFailureReason } from './jobStatus.js';

test('job status never returns a raw provider or process failure', () => {
  const raw = 'yt-dlp failed for https://private.test/?token=SENSITIVE_SENTINEL';
  const result = publicJobFailureReason(raw, 'failed');
  assert.equal(result, 'تعذر إكمال المهمة. أعد المحاولة أو تواصل مع الدعم.');
  assert.equal(result?.includes('SENSITIVE_SENTINEL'), false);
});

test('job status preserves only reviewed remediation messages for failed jobs', () => {
  const reviewed = 'تعذر قراءة فيديو YouTube. تأكد أنه عام ومتاح وليس خاصًا أو غير مدرج.';
  assert.equal(publicJobFailureReason(reviewed, 'failed'), reviewed);
  assert.equal(publicJobFailureReason(reviewed, 'active'), null);
});
