import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';

for (const width of [390, 1280]) {
  test(`Settings → exams → send missing grades on ${width}px (synthetic API)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      page.setDefaultTimeout(20000);
      const user = { id: 'test-admin', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['settings.manage'],
        allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'synthetic-test-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const exams = [
        { examId: 'latest', title: 'امتحان المحاضرة السادسة', teacherName: 'نادر', finalResultCount: 0, createdAt: '2026-10-01T10:00:00Z' },
        { examId: 'fifth', title: 'امتحان المحاضرة الخامسة', teacherName: 'نادر', finalResultCount: 8, createdAt: '2026-09-29T10:00:00Z' },
      ];
      let catalogFails = width === 390;
      const states = [
        { attemptId: 'failed', status: 'Failed', failureCode: 'WHATSAPP_CLOUD_131026', canRetry: true },
        { attemptId: 'unsent', status: 'NotSent', failureCode: null, canRetry: true },
      ];
      const requests = [];
      await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        const path = url.pathname.replace(/^\/api/, '');
        if (path === '/auth/session') return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
        if (path === '/whatsapp/admin/exams/parent-messages') {
          if (catalogFails) { catalogFails = false; return route.fulfill({ status: 503, json: { message: 'تعذر تحميل الامتحانات.' } }); }
          const search = url.searchParams.get('search') ?? '';
          const items = exams.filter(exam => exam.title.includes(search) || exam.teacherName.includes(search));
          return route.fulfill({ json: { page: 1, pageSize: 20, totalCount: items.length, items } });
        }
        if (path.endsWith('/parent-messages/retry')) {
          const body = route.request().postDataJSON();
          requests.push({ path, body });
          const targets = states.filter(state => state.canRetry && (!body.failedOnly || state.status === 'Failed'));
          for (const target of targets) { target.status = 'Pending'; target.canRetry = false; target.failureCode = null; }
          return route.fulfill({ json: { queuedCount: targets.length, alreadyQueued: false, operationId: body.operationId } });
        }
        if (path.endsWith('/parent-messages')) {
          const empty = path.includes('/latest/');
          return route.fulfill({ json: { enabled: true, configurationError: null,
            retryableCount: empty ? 0 : states.filter(state => state.canRetry).length,
            pendingCount: empty ? 0 : states.filter(state => state.status === 'Pending').length,
            deliveredCount: empty ? 0 : 5, failedCount: empty ? 0 : states.filter(state => state.status === 'Failed').length,
            notSentCount: empty ? 0 : states.filter(state => state.status === 'NotSent').length,
            awaitingDeliveryCount: empty ? 0 : 1, uncertainCount: 0, attempts: empty ? [] : states } });
        }
        return route.fulfill({ json: { success: true, data: [] } });
      });
      await page.goto('http://admin.lvh.me:3000/admin/settings');
      await page.getByRole('button', { name: 'الامتحانات', exact: true }).click();
      if (width === 390) {
        await page.getByRole('alert').filter({ hasText: 'تعذر تحميل الامتحانات' }).waitFor();
        await page.getByRole('button', { name: 'إعادة المحاولة', exact: true }).click();
      }
      await page.getByRole('button', { name: 'إرسال الدرجات (0)', exact: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: 'إرسال الدرجات (0)', exact: true }).isDisabled(), true);
      await page.getByRole('button', { name: 'عرض رسائل امتحان المحاضرة الخامسة — نادر', exact: true }).click();
      const send = page.getByRole('button', { name: 'إرسال الدرجات (2)', exact: true });
      await send.waitFor();
      assert.ok(await page.getByText('الرسائل الناقصة: 2', { exact: false }).isVisible());
      assert.match(await page.getByRole('link', { name: 'تفاصيل الطلاب والدرجات', exact: true }).getAttribute('href'), /\/fifth\/dashboard$/);
      await page.getByRole('textbox', { name: 'اسم الامتحان أو المدرس', exact: true }).fill('الخامسة');
      await page.getByRole('button', { name: 'بحث', exact: true }).click();
      await page.getByText('1 امتحان · صفحة 1 من 1', { exact: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: 'التالي', exact: true }).isDisabled(), true);
      await send.scrollIntoViewIfNeeded();
      await page.evaluate(async () => {
        await Promise.all(document.getAnimations().filter(animation => animation.effect?.getTiming().iterations !== Infinity)
          .map(animation => animation.finished.catch(() => undefined)));
      });
      await page.screenshot({ path: `../artifacts/production/exam-whatsapp-diagnosis/settings-exams-${width}.png`, fullPage: true });
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      if (width === 1280) await page.getByRole('button', { name: 'إعادة إرسال الفاشلة فقط (1)', exact: true }).click();
      else await send.click();
      await page.getByRole('status').filter({ hasText: 'الإرسال مستمر في الخلفية' }).waitFor();
      assert.equal(requests.length, 1);
      assert.equal(requests[0].path, '/whatsapp/admin/exams/fifth/parent-messages/retry');
      assert.match(requests[0].body.operationId, /^[0-9a-f-]{36}$/);
      assert.equal(requests[0].body.attemptId, undefined);
      assert.equal(requests[0].body.failedOnly, width === 1280);
      if (width === 1280) {
        assert.ok(await page.getByText('الرسائل الناقصة: 1', { exact: false }).isVisible());
        assert.equal(await page.getByRole('button', { name: 'إعادة إرسال الفاشلة فقط (0)', exact: true }).isDisabled(), true);
        await page.getByRole('button', { name: 'إرسال الدرجات (1)', exact: true }).click();
        await page.getByRole('button', { name: 'إرسال الدرجات (0)', exact: true }).waitFor();
        assert.equal(requests.length, 2);
        assert.equal(requests[1].body.failedOnly, false);
        assert.notEqual(requests[0].body.operationId, requests[1].body.operationId);
      }
      assert.equal(await page.getByRole('button', { name: 'إرسال الدرجات (0)', exact: true }).isDisabled(), true);
      await page.getByRole('textbox', { name: 'اسم الامتحان أو المدرس', exact: true }).fill('لا يوجد');
      await page.getByRole('button', { name: 'بحث', exact: true }).click();
      await page.getByText('لا توجد امتحانات تطابق البحث.', { exact: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: /إرسال الدرجات/ }).count(), 0);
    } finally { await browser.close(); }
  });
}
