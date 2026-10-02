import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

for (const [width, mode] of [[390, 'bulk'], [1280, 'single']]) {
  test(`exam WhatsApp retry controls on ${width}px (${mode}, synthetic API)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      page.setDefaultTimeout(20000);
      const user = { id: 'test-admin', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['settings.manage', 'exams.manage'],
        allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'synthetic-test-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const states = [
        { attemptId: 'failed', status: 'Failed', failureCode: 'WHATSAPP_CLOUD_131042', canRetry: true },
        { attemptId: 'unsent', status: 'NotSent', failureCode: null, canRetry: true },
        { attemptId: 'read', status: 'Read', failureCode: null, canRetry: false },
        { attemptId: 'uncertain', status: 'Uncertain', failureCode: null, canRetry: false },
        { attemptId: 'not-ready', status: 'NotReady', failureCode: null, canRetry: false },
      ];
      const retryRequests = [];
      let summaryFailed = mode === 'bulk';
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path.endsWith('/dashboard')) data = { examId: 'test-exam', title: 'امتحان المحاضرة الخامسة', description: '',
          questions: [], questionCount: 0, totalScore: 40, passingScore: 20, durationMinutes: 30, isActive: true,
          attempts: states.map((state, index) => ({ attemptId: state.attemptId, studentId: `student-${index}`,
            studentName: ['أحمد محمد محمود', 'مريم علي حسن', 'عمر إبراهيم محمد', 'نور عبد الله', 'فاطمة محمود'][index],
            studentPhone: '01000000000', scoreAchieved: 35, totalScore: 40, evaluation: state.status === 'NotReady' ? 'قيد التصحيح' : 'ممتاز',
            isPassed: true, isTimeExpired: false, startedAt: null, submittedAt: null })) };
        if (path.endsWith('/parent-messages/retry')) {
          const body = route.request().postDataJSON();
          retryRequests.push(body);
          const targets = states.filter(state => state.canRetry && (!body.attemptId || state.attemptId === body.attemptId));
          for (const target of targets) { target.status = 'Pending'; target.canRetry = false; target.failureCode = null; }
          return route.fulfill({ json: { operationId: body.operationId, queuedCount: targets.length, alreadyQueued: false } });
        }
        if (path.endsWith('/parent-messages')) {
          if (summaryFailed) {
            summaryFailed = false;
            return route.fulfill({ status: 503, json: { message: 'تعذر تحميل حالات رسائل النتائج.' } });
          }
          return route.fulfill({ json: { enabled: true, configurationError: null, attempts: states,
            retryableCount: states.filter(state => state.canRetry).length, pendingCount: states.filter(state => state.status === 'Pending').length,
            deliveredCount: 1, failedCount: states.filter(state => state.status === 'Failed').length,
            notSentCount: states.filter(state => state.status === 'NotSent').length, awaitingDeliveryCount: 0, uncertainCount: 1 } });
        }
        await route.fulfill({ json: { success: true, data } });
      });
      await page.goto('http://admin.lvh.me:3000/admin/content/exams/test-exam/dashboard');
      const bulk = page.getByRole('button', { name: /إعادة إرسال الرسائل المتوقفة/ });
      if (mode === 'bulk') {
        await page.getByRole('alert').filter({ hasText: 'تعذر تحميل حالات' }).waitFor();
        assert.equal(await bulk.isDisabled(), true);
        await page.getByRole('button', { name: 'تحديث الحالات', exact: true }).click();
      }
      await page.getByRole('button', { name: 'إعادة إرسال الرسائل المتوقفة (2)', exact: true }).waitFor();
      const rows = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'إعادة إرسال واتساب', exact: true }) });
      const readRow = rows.filter({ hasText: 'عمر إبراهيم محمد' });
      assert.equal(await readRow.getByRole('button', { name: 'إعادة إرسال واتساب', exact: true }).isDisabled(), true);
      assert.ok(await page.getByText('تمت قراءتها', { exact: true }).isVisible());
      assert.ok(await page.getByText(/بعض الرسائل فشلت بسبب أهلية حساب واتساب/).isVisible());
      await mkdir('../artifacts/production/exam-whatsapp-diagnosis', { recursive: true });
      await page.evaluate(async () => {
        await Promise.all(document.getAnimations().filter(animation => animation.effect?.getTiming().iterations !== Infinity)
          .map(animation => animation.finished.catch(() => undefined)));
      });
      await page.screenshot({ path: `../artifacts/production/exam-whatsapp-diagnosis/retry-${width}.png`, fullPage: true });
      if (mode === 'bulk') await bulk.click();
      else await rows.filter({ hasText: 'أحمد محمد محمود' }).getByRole('button', { name: 'إعادة إرسال واتساب', exact: true }).click();
      try {
        await page.getByRole('status').filter({ hasText: 'الإرسال مستمر في الخلفية' }).waitFor();
      } catch (error) {
        await page.screenshot({ path: `../artifacts/production/exam-whatsapp-diagnosis/retry-failed-${width}.png`, fullPage: true });
        console.error({ retryRequests, page: await page.locator('body').innerText() });
        throw error;
      }
      assert.equal(retryRequests.length, 1);
      assert.match(retryRequests[0].operationId, /^[0-9a-f-]{36}$/);
      assert.equal(retryRequests[0].attemptId, mode === 'single' ? 'failed' : undefined);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      if (mode === 'bulk') assert.equal(await bulk.isDisabled(), true);
    } finally { await browser.close(); }
  });
}
