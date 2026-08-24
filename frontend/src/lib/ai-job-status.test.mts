import assert from 'node:assert/strict';
import test from 'node:test';

import {
  aiJobStatusFromProgressEvent,
  sanitizeAiJobStatus,
} from './ai-job-status.ts';

const rawDiagnostic =
  'ERROR yt-dlp --cookies /run/secrets/cookies.txt https://video.example/private?id=secret';

// Regression: 2026-08-24 admin lesson analysis exposed yt-dlp diagnostics.
test('failed worker statuses become safe retryable Arabic contracts', () => {
  const scenarios = [
    {
      jobId: 'video-id',
      code: 'AI_VIDEO_ANALYSIS_FAILED',
      message: 'تعذر إكمال تحليل الفيديو. تحقّق من رابط الفيديو وصلاحية الوصول، ثم أعد المحاولة.',
    },
    {
      jobId: 'video-id_mindmaps',
      code: 'AI_MINDMAP_GENERATION_FAILED',
      message: 'تعذر إكمال توليد الخرائط الذهنية. أعد المحاولة بعد قليل.',
    },
  ] as const;

  for (const scenario of scenarios) {
    const status = sanitizeAiJobStatus({
      id: scenario.jobId,
      state: 'failed',
      progress: { percentage: 0, stage: rawDiagnostic },
      failedReason: rawDiagnostic,
    });

    assert.equal(status.state, 'failed');
    assert.equal(status.failure?.code, scenario.code);
    assert.equal(status.failure?.retryable, true);
    assert.equal(status.failure?.message, scenario.message);
    assert.doesNotMatch(JSON.stringify(status), /yt-dlp|--cookies|run\/secrets|https?:\/\//i);
  }
});

test('SignalR status=failed is authoritative even when progress is zero', () => {
  const status = aiJobStatusFromProgressEvent({
    jobId: 'video-id',
    progress: 0,
    status: 'failed',
    message: rawDiagnostic,
  });

  assert.equal(status.state, 'failed');
  assert.equal(status.failure?.retryable, true);
  assert.doesNotMatch(JSON.stringify(status), /yt-dlp|video\.example|cookies\.txt/i);
});

test('active progress also ignores arbitrary worker stage text', () => {
  const status = sanitizeAiJobStatus({
    id: 'video-id',
    state: 'active',
    progress: { percentage: 40, stage: rawDiagnostic },
  });

  assert.equal(status.state, 'active');
  assert.equal(status.progress.percentage, 40);
  assert.match(status.progress.stage, /صوت المحاضرة/);
  assert.doesNotMatch(JSON.stringify(status), /yt-dlp|video\.example|cookies\.txt/i);
});
