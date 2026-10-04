import { expect, test, type Page, type Route } from '@playwright/test';

import { installAuthAndGoto } from './e2e-contract-helpers';

const endpoint = '/api/admin/center-desktop';
const uploadA = '11111111-1111-4111-8111-111111111111';
const uploadB = '22222222-2222-4222-8222-222222222222';
const uploadC = '33333333-3333-4333-8333-333333333333';

type User = {
  id: string;
  fullName: string;
  phone: string;
  roles: string[];
  permissions: string[];
  profileComplete: boolean;
  allowedDomains: string[];
  allowedNavbarItems: string[];
  authorizationVersion: number;
};

const administrator: User = {
  id: 'desktop-support-admin',
  fullName: 'مسؤول الاختبار',
  phone: '20000000991',
  roles: ['Admin'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['admin'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};

function receipt(uploadId: string, centerId: string, role = 'host') {
  return {
    receiptId: uploadId,
    uploadId,
    centerId,
    sha256: 'a'.repeat(64),
    bundleSha256: 'b'.repeat(64),
    receivedAt: '2026-10-03T12:00:00Z',
    createdAt: '2026-10-03T11:59:00Z',
    size: 2048,
    app: { version: '3.4.0+9', build: '1234567890abcdef', role, os: 'windows' },
  };
}

const firstReceipt = receipt(uploadA, 'synthetic-center-alpha');
const secondReceipt = receipt(uploadB, 'synthetic-center-beta', 'client');
const thirdReceipt = receipt(uploadC, 'synthetic-center-gamma');

function diagnostics(value = firstReceipt) {
  return {
    receipt: value,
    kind: value.app.role === 'client' ? 'diagnostics' : 'database',
    total: 1,
    truncated: false,
    events: [{
      schema: 1,
      kind: 'error',
      id: '44444444-4444-4444-8444-444444444444',
      session: '55555555-5555-4555-8555-555555555555',
      time: '2026-10-03T11:58:00Z',
      platform: 'windows',
      version: value.app.version,
      build: value.app.build,
      role: value.app.role,
      operation: 'entry',
      errors: [{ type: 'CenterException' }],
      frames: [{ frame: 0, file: 'application/center_store.dart', line: 100, column: 2 }],
    }],
  };
}

async function reply(route: Route, data: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify({ success: status < 400, data }),
  });
}

type SupportHandler = (route: Route, url: URL) => Promise<void>;

async function installApi(page: Page, handle: SupportHandler, user: User = administrator) {
  // Realtime is a separate service: mock its network boundary, not the page state.
  await page.routeWebSocket('**/hubs/**', socket => {
    socket.onMessage(message => {
      if (typeof message === 'string' && message.includes('"protocol":"json"')) {
        socket.send('{}\x1e');
      }
    });
  });
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/auth/session')) {
      await reply(route, { user, authorizationVersion: user.authorizationVersion });
    } else if (url.pathname.startsWith(endpoint)) {
      await handle(route, url);
    } else {
      await reply(route, []);
    }
  });
}

async function openPage(page: Page, baseURL: string, user: User = administrator) {
  await installAuthAndGoto(page, 'synthetic-desktop-admin-token', user, `${baseURL}/admin/center-desktop`);
}

async function availableApi(route: Route, url: URL) {
  if (url.pathname === `${endpoint}/status`) {
    await reply(route, { configured: true, available: true, message: 'خدمة الدعم متصلة' });
  } else if (url.pathname === `${endpoint}/uploads`) {
    await reply(route, {
      uploads: url.searchParams.has('after') ? [thirdReceipt] : [firstReceipt, secondReceipt],
      nextCursor: url.searchParams.has('after') ? '' : uploadB,
    });
  } else if (url.pathname.endsWith('/diagnostics')) {
    await reply(route, diagnostics(url.pathname.includes(uploadB) ? secondReceipt : firstReceipt));
  } else if (url.pathname === `${endpoint}/releases`) {
    await reply(route, { releases: [{
      platform: 'windows-x64',
      role: 'host',
      status: 'available',
      manifest: {
        releaseId: 'synthetic-release-9',
        version: '3.4.0+9',
        build: '1234567890abcdef',
        platform: 'windows-x64',
        role: 'host',
        size: 10240,
        sha256: 'c'.repeat(64),
        downloadPath: '/v1/releases/synthetic-release-9.zip',
        notes: 'نسخة اختبار خاصة',
      },
    },
    { platform: 'windows-x64', role: 'client', status: 'missing', manifest: null },
    { platform: 'macos-arm64', role: 'host', status: 'missing', manifest: null },
    { platform: 'macos-arm64', role: 'client', status: 'missing', manifest: null },
    ] });
  } else {
    await reply(route, null, 404);
  }
}

test('admin can page through uploads, inspect sanitized diagnostics and see the actual release version', async ({ page, baseURL }) => {
  await installApi(page, availableApi);
  await openPage(page, baseURL!);

  await expect(page.getByRole('heading', { name: 'برنامج السنتر', exact: true })).toBeVisible();
  await expect(page.locator('a[href="/admin/center-desktop"]').first()).toBeVisible();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'تحميل المزيد', exact: true }).click();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${thirdReceipt.centerId} ${uploadC}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toBeVisible();
  await page.getByLabel('بحث في النسخ المحمّلة').fill('gamma');
  await expect(page.getByRole('button', { name: `عرض مشاكل ${thirdReceipt.centerId} ${uploadC}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toHaveCount(0);
  await page.getByLabel('بحث في النسخ المحمّلة').fill('');
  await page.getByRole('combobox', { name: 'نوع الجهاز', exact: true }).selectOption('client');
  await expect(page.getByRole('button', { name: `عرض مشاكل ${secondReceipt.centerId} ${uploadB}`, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toHaveCount(0);
  await page.getByRole('combobox', { name: 'نوع الجهاز', exact: true }).selectOption('');

  await page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true }).click();
  await expect(page.getByText('CenterException', { exact: false })).toBeVisible();
  await page.getByText('تفاصيل فنية للدعم', { exact: true }).click();
  await expect(page.getByText('application/center_store.dart:100:2', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'تنزيل النسخة', exact: true })).toBeVisible();
  await expect(page.locator('body')).not.toContainText('deviceToken');
  await expect(page.locator('body')).not.toContainText('credentials');

  if (process.env.CAPTURE_UI === '1') {
    await page.getByRole('heading', { name: 'برنامج السنتر', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: '/tmp/massar-center-desktop-desktop.png', fullPage: true });
    const desktopSize = page.viewportSize()!;
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByRole('main')).toHaveCSS('margin-inline-start', '0px');
    await page.getByRole('heading', { name: 'برنامج السنتر', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: '/tmp/massar-center-desktop-mobile.png', fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.setViewportSize(desktopSize);
  }

  await page.getByRole('button', { name: 'إصدارات البرنامج', exact: true }).click();
  await expect(page.getByText('نسخة اختبار خاصة', { exact: true })).toBeVisible();
  await expect(page.getByText('3.4.0+9', { exact: true })).toBeVisible();
});

for (const role of ['Assistant', 'Supervisor']) {
  test(`${role} cannot open desktop support even with settings permission and an explicit navbar grant`, async ({ page, baseURL }) => {
    const scopedUser: User = {
      ...administrator,
      roles: [role],
      permissions: ['settings.manage', 'users.manage'],
      allowedNavbarItems: ['/admin/center-desktop'],
    };
    const sensitiveRequests: string[] = [];
    await installApi(page, async (route, url) => {
      sensitiveRequests.push(url.pathname);
      await availableApi(route, url);
    }, scopedUser);
    await openPage(page, baseURL!, scopedUser);

    await expect(page).toHaveURL(`${baseURL}/admin/unauthorized`);
    await expect(page.getByRole('heading', { name: 'غير مصرح بالدخول', exact: true })).toBeVisible();
    await expect(page.locator('a[href="/admin/center-desktop"]')).toHaveCount(0);
    expect(sensitiveRequests).toEqual([]);
    await expect(page.locator('body')).not.toContainText(firstReceipt.centerId);
  });
}

test('an unavailable support service is not presented as an empty upload history and can recover', async ({ page, baseURL }) => {
  let unavailable = true;
  await installApi(page, async (route, url) => {
    if (url.pathname === `${endpoint}/status` && unavailable) {
      await reply(route, { configured: true, available: false, message: 'تعذر الاتصال بخدمة دعم برنامج السنتر.' });
    } else if (unavailable) {
      await route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'تعذر الاتصال بخدمة دعم برنامج السنتر.' }) });
    } else {
      await availableApi(route, url);
    }
  });
  await openPage(page, baseURL!);

  await expect(page.getByRole('heading', { name: 'تعذر الوصول لخدمة النسخ', exact: true })).toBeVisible();
  await expect(page.locator('body')).not.toContainText('لم تُرفع أي نسخة بعد');
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toHaveCount(0);
  unavailable = false;
  await page.getByRole('button', { name: 'تحديث', exact: true }).click();
  await expect(page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true })).toBeVisible();
  await expect(page.getByRole('main').getByRole('alert')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'تعذر الوصول لخدمة النسخ', exact: true })).toHaveCount(0);
});

test('switching the selected upload discards a delayed diagnostic response from the previous center', async ({ page, baseURL }) => {
  let releaseOld!: () => void;
  let oldStarted!: () => void;
  let oldFinished!: () => void;
  const heldResponse = new Promise<void>(resolve => { releaseOld = resolve; });
  const started = new Promise<void>(resolve => { oldStarted = resolve; });
  const finished = new Promise<void>(resolve => { oldFinished = resolve; });
  await installApi(page, async (route, url) => {
    if (url.pathname === `${endpoint}/uploads/${uploadA}/diagnostics`) {
      oldStarted();
      await heldResponse;
      try {
        await reply(route, diagnostics(firstReceipt));
      } catch {
        // Aborting the old HTTP request is a valid way to reject its response.
      } finally {
        oldFinished();
      }
    } else if (url.pathname === `${endpoint}/uploads/${uploadB}/diagnostics`) {
      const detail = diagnostics(secondReceipt);
      detail.events[0].errors = [{ type: 'SocketException' }];
      detail.events[0].frames = [{ frame: 0, file: 'application/center_store_remote.dart', line: 80, column: 1 }];
      await reply(route, detail);
    } else {
      await availableApi(route, url);
    }
  });
  await openPage(page, baseURL!);

  try {
    await page.getByRole('button', { name: `عرض مشاكل ${firstReceipt.centerId} ${uploadA}`, exact: true }).click();
    await started;
    await page.getByRole('button', { name: `عرض مشاكل ${secondReceipt.centerId} ${uploadB}`, exact: true }).click();
    await expect(page.getByText('SocketException', { exact: true })).toBeVisible();
    releaseOld();
    await finished;
    await expect(page.getByRole('heading', { name: `${secondReceipt.centerId} · الفرعي`, exact: true })).toBeVisible();
    await expect(page.getByText('CenterException', { exact: true })).toHaveCount(0);
    await page.getByText('تفاصيل فنية للدعم', { exact: true }).click();
    await expect(page.getByText('application/center_store_remote.dart:80:1', { exact: true })).toBeVisible();
    await expect(page.getByText('application/center_store.dart:100:2', { exact: true })).toHaveCount(0);
  } finally {
    releaseOld();
  }
});


test('admin searches snapshot students and opens attendance profile without leaving support', async ({ page, baseURL }) => {
  await installApi(page, async (route, url) => {
    if (!url.pathname.endsWith('/students')) return availableApi(route, url);
    const student = { id: 'student-one', name: 'أحمد اختبار', code: '00123', barcode: '100123', phone: '01000000123', guardianPhone: '', notes: 'ملاحظة اختبار', discountPercent: 25, suspended: false, groups: ['الجمعة'] };
    await reply(route, { students: [student], total: 1, profile: url.searchParams.has('studentId') ? {
      present: 1, absent: 1, attendanceTotal: 2, examTotal: 1,
      attendances: [{ id: 'a1', status: 'present', lesson: { group: 'الجمعة', number: 1, month: 2, date: '' } }, { id: 'a2', status: 'absent', lesson: { group: 'الجمعة', number: 2, month: 2, date: '' } }],
      exams: [{ id: 'e1', score: 0, maxScore: 10, absent: false, homework: 'complete', lesson: { group: 'الجمعة', number: 1, month: 2, date: '' } }],
    } : null });
  });
  await openPage(page, baseURL!);
  await page.getByRole('button', { name: 'بحث الطلاب', exact: true }).click();
  await page.getByRole('searchbox', { name: 'الاسم أو الكود أو الباركود أو رقم الهاتف' }).fill('00123');
  await page.getByRole('button', { name: 'عرض البروفايل' }).click();
  await expect(page.getByRole('heading', { name: /أحمد اختبار/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'غائب', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '0 / 10', exact: true })).toBeVisible();
  await expect(page.getByText('ملاحظة اختبار', { exact: true })).toBeVisible();
  await page.screenshot({ path: '../artifacts/private/windows-release-1.2.0/student-support-preview.png', fullPage: true });
});
