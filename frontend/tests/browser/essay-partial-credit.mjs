import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';

test('2026-10-03 student sees partial essay marks and the specific deduction reason (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    page.setDefaultTimeout(20000);
    const user = { id: 'essay-test-student', fullName: 'طالب اختبار', roles: ['Student'], permissions: [],
      allowedDomains: ['student'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
    await page.addInitScript(user => {
      localStorage.setItem('accessToken', 'synthetic-test-token');
      localStorage.setItem('user', JSON.stringify(user));
    }, user);
    const feedback = 'وضحت مصالح ثلاثة أطراف صح؛ ناقص مصلحة الموردين، وهي السداد في الموعد.';
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname;
      let data = {};
      if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
      if (path.endsWith('/latest-result')) data = {
        attemptId: 'test-attempt', scoreAchieved: 0.75, totalScore: 1, isPassed: true,
        blocksNextLesson: false, evaluation: 'جيد', isTimeExpired: false, resultState: 'Completed',
        questions: [{ examQuestionId: 'test-question', order: 1, questionText: 'وضح مصالح أربعة أطراف.',
          selectedOptionText: 'مصالح العملاء والعاملين والمالك.', isAnswered: true, isCorrect: false,
          pointsAwarded: 0.75, maximumPoints: 1, gradingFeedback: feedback,
          correctOptionText: 'مصالح العملاء والعاملين والمالك والموردين.' }],
      };
      await route.fulfill({ json: { success: true, data } });
    });
    const base = process.env.ESSAY_BROWSER_BASE_URL || 'http://app.lvh.me:8738';
    await page.goto(`${base}/student/exams/test-exam`);
    await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
    await page.getByText('صحيحة جزئيًا', { exact: true }).waitFor();
    const review = page.locator('section').filter({ has: page.getByRole('heading', { name: 'مراجعة الورقة كاملة' }) });
    assert.equal(await review.getByText('0.75 من 1 نقطة', { exact: true }).count(), 1);
    assert.equal(await review.getByText(feedback, { exact: true }).count(), 1);
    assert.equal(await review.getByText('خاطئة ✗', { exact: true }).count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  } finally {
    await browser.close();
  }
});
