import { expect, test, type Page, type Route } from '@playwright/test';
import { readFile } from 'node:fs/promises';

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
    } else if (url.pathname.startsWith(endpoint) || url.pathname.startsWith('/api/center-students')) {
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

test('student search uses the newest captured host snapshot across pages and shows joined lessons with Cairo check-in time', async ({ page, baseURL }) => {
  const latest = { ...receipt(uploadC, firstReceipt.centerId), createdAt: '2026-11-22T10:00:00Z', receivedAt: '2026-11-22T10:01:00Z' };
  const student = { id: 'synthetic-student', name: 'طالب التجربة', code: '00701', barcode: '', phone: '01000000701', guardianPhone: '01100000701', notes: '', suspended: false, discountPercent: 0, groups: ['مجموعة الجمعة'], centers: ['سنتر التجربة'] };
  const payment = { coverage: 'unknown', packageName: '', centerFeePaid: 0, receipts: [] };
  const lessons = [
    { id: 'lesson-one', groupId: 'g1', group: 'مجموعة الجمعة', center: 'سنتر التجربة', month: 1, monthName: 'الشهر الأول', number: 1, name: '', date: '2026-11-01T09:00:00Z', attendance: { status: 'present', recordedAt: '2026-11-01T10:13:00Z', makeupSourceGroup: '', paymentPending: false }, payment: { ...payment, coverage: 'package', packageName: 'الشهر', receipts: [{ id: 'p1', description: '', amount: 21000, expected: 21000, settled: 0, refunded: 0, online: 0 }] }, exams: [{ id: 'e1', name: 'ورقي', score: 0, maxScore: 10, absent: false }, { id: 'e2', name: 'إلكتروني', score: 9, maxScore: 12, absent: false }], homework: 'complete' },
    { id: 'lesson-two', groupId: 'g1', group: 'مجموعة الجمعة', center: 'سنتر التجربة', month: 1, monthName: 'الشهر الأول', number: 2, name: '', date: '', attendance: null, payment, exams: [], homework: 'notReviewed' },
    { id: 'lesson-three', groupId: 'g1', group: 'مجموعة الجمعة', center: 'سنتر التجربة', month: 2, monthName: 'الشهر الثاني', number: 1, name: '', date: '', attendance: { status: 'absent', recordedAt: '', makeupSourceGroup: '', paymentPending: false }, payment, exams: [{ id: 'e3', name: 'الامتحان الثالث', score: null, maxScore: 10, absent: true }], homework: 'missing' },
  ];
  await installApi(page, async (route, url) => {
    if (url.pathname === `${endpoint}/uploads` && url.searchParams.get('limit') === '500') {
      await reply(route, url.searchParams.has('after') ? { uploads: [latest], nextCursor: '' } : { uploads: [firstReceipt, secondReceipt], nextCursor: uploadB });
    } else if (url.pathname.endsWith('/students')) {
      expect(url.pathname).toBe(`${endpoint}/uploads/${uploadC}/students`);
      const selected = url.searchParams.get('studentId');
      await reply(route, { students: [student], total: 1, groups: [{ id: 'g1', name: 'مجموعة الجمعة', centerId: 'c1', center: 'سنتر التجربة', gradeId: 'r1', grade: 'الصف الأول' }], profile: selected ? { present: 1, absent: 1, attendanceTotal: 2, examTotal: 3, attendances: [], exams: [], lessons } : null });
    } else await availableApi(route, url);
  });
  await openPage(page, baseURL!);
  await page.getByRole('button', { name: 'طلاب السنتر', exact: true }).click();
  const search = page.getByRole('region', { name: 'بحث طلاب السنتر' });
  await expect(search.getByLabel('آخر تحديث لبيانات الطلاب').locator('time')).toHaveAttribute('datetime', latest.createdAt);
  await page.getByRole('searchbox', { name: 'البحث عن طالب', exact: true }).fill('٠١١٠٠٠٠٠٧٠١');
  await expect(search.getByRole('cell', { name: 'سنتر التجربة', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'عرض سجل طالب التجربة', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'شهر الحصص' })).toHaveValue('all');
  await expect(page.getByText('غائب عن الامتحان', { exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'شهر الحصص' }).selectOption('1');
  const first = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: /الحصة 1/ }) });
  await expect(first).toContainText('0 من 10');
  await expect(first).toContainText('9 من 12');
  await expect(first.locator('time[datetime="2026-11-01T10:13:00Z"]')).toContainText('12:13');
  await expect(first).toContainText('مشمولة في باقة الشهر');
  await expect(page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: /الحصة 2/ }) })).toContainText('لم يُسجل');
  const downloading = page.waitForEvent('download');
  await page.getByRole('button', { name: 'تنزيل PDF للطالب', exact: true }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe('student-report-00701.pdf');
  const pdfPath = test.info().outputPath('student-report.pdf');
  await download.saveAs(pdfPath);
  expect((await readFile(pdfPath)).subarray(0, 5).toString()).toBe('%PDF-');
  if (process.env.CAPTURE_UI === '1') {
    await page.getByRole('region', { name: 'سجل الحصص', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: '/tmp/massar-student-search-desktop.png', fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByRole('main')).toHaveCSS('margin-inline-start', '0px');
    await expect.poll(async () => (await page.getByRole('main').boundingBox())?.width ?? 0).toBeGreaterThan(350);
    await page.getByRole('region', { name: 'سجل الحصص', exact: true }).scrollIntoViewIfNeeded();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: '/tmp/massar-student-search-mobile.png', fullPage: true });
  }
});

for (const role of ['Staff', 'حضور والانصراف']) {
  test(`${role} student reader searches and downloads a student PDF without program administration`, async ({ page, baseURL }) => {
    const user: User = { ...administrator, roles: [role, 'متابعة طلاب السنتر'], permissions: ['center_students.view'], allowedDomains: ['assistant'], allowedNavbarItems: ['/assistant/dashboard', '/assistant/center-students'] };
    const student = { id: 'reader-student', name: 'طالب خدمة السنتر', code: '00901', barcode: '', phone: '01000000901', guardianPhone: '01100000901', notes: '', suspended: false, discountPercent: 0, groups: ['مجموعة الجمعة'], centers: ['سنتر التجربة'] };
    const requests: string[] = [];
    await installApi(page, async (route, url) => {
      requests.push(url.pathname);
      if (url.pathname === '/api/center-students/sources') {
        const { uploadId, centerId, createdAt, receivedAt } = firstReceipt;
        await reply(route, [{ uploadId, centerId, createdAt, receivedAt }]);
      } else if (url.pathname.endsWith('/students')) {
        await reply(route, { students: [student], total: 1, groups: [], profile: url.searchParams.has('studentId') ? {
          present: 1, absent: 0, attendanceTotal: 1, examTotal: 1, attendances: [], exams: [],
          lessons: [{ id: 'reader-lesson', groupId: 'g1', group: 'مجموعة الجمعة', center: 'سنتر التجربة', number: 1, month: 1, monthName: 'الشهر الأول', name: '', date: '', attendance: { status: 'present', recordedAt: '2026-11-01T10:13:00Z', makeupSourceGroup: '', paymentPending: false }, payment: { coverage: 'unknown', packageName: '', centerFeePaid: 0, receipts: [] }, exams: [{ id: 'reader-exam', name: 'امتحان التجربة', score: 9, maxScore: 12, absent: false }], homework: 'complete' }],
        } : null });
      } else await reply(route, null, 403);
    }, user);
    const staffURL = baseURL!.replace('admin.lvh.me', 'staff.lvh.me');
    await installAuthAndGoto(page, 'synthetic-student-reader-token', user, `${staffURL}/assistant/center-students`);
    await expect(page.locator('a[href="/assistant/center-students"]').first()).toBeVisible();
    await expect(page.getByRole('searchbox')).toBeVisible();
    await page.getByRole('searchbox').fill('00901');
    await page.getByRole('button', { name: 'عرض سجل طالب خدمة السنتر', exact: true }).click();
    await expect(page.getByRole('region', { name: 'سجل الحصص', exact: true })).toContainText('9 من 12');
    const downloading = page.waitForEvent('download');
    await page.getByRole('button', { name: 'تنزيل PDF للطالب', exact: true }).click();
    const download = await downloading;
    const pdf = test.info().outputPath('staff-student-report.pdf');
    await download.saveAs(pdf);
    expect((await readFile(pdf)).subarray(0, 5).toString()).toBe('%PDF-');
    await expect(page.getByText('تفاصيل النسخة', { exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'النسخ المرفوعة', exact: true })).toHaveCount(0);
    await expect(page.locator('a[href="/admin/center-desktop"]')).toHaveCount(0);
    expect(requests.some(path => path.startsWith(endpoint))).toBe(false);
    await page.screenshot({ path: test.info().outputPath('staff-student-view.png'), fullPage: true });
  });
}

test('student PDF retries a failed font download and paginates long history', async ({ page, baseURL }) => {
  const student = { id: 'pdf-student', name: 'طالبة تقرير طويل', code: '00802', barcode: '', phone: '01000000802', guardianPhone: '01100000802', notes: 'ملاحظات الطالب '.repeat(150), suspended: false, discountPercent: 0, groups: ['مجموعة التقرير'], centers: ['سنتر التقرير'] };
  const lessons = Array.from({ length: 45 }, (_, i) => ({
    id: `pdf-lesson-${i}`, groupId: 'g1', group: 'مجموعة التقرير', center: 'سنتر التقرير', month: Math.floor(i / 4) + 1, monthName: '', number: i % 4 + 1, name: '', date: '', attendance: null,
    payment: { coverage: 'unknown', packageName: '', centerFeePaid: 0, receipts: [] },
    exams: [{ id: `exam-${i}`, name: 'امتحان طويل الاسم مع درجات مختلفة', score: i % 12, maxScore: 12, absent: false }], homework: 'notReviewed',
  }));
  let failFont = true;
  await page.route('**/fonts/MassarReportArabic.ttf', route => failFont ? route.fulfill({ status: 503, body: 'unavailable' }) : route.continue());
  await installApi(page, async (route, url) => {
    if (url.pathname.endsWith('/students')) await reply(route, { students: [student], total: 1, groups: [], profile: url.searchParams.has('studentId') ? { present: 0, absent: 0, attendanceTotal: 0, examTotal: 45, attendances: [], exams: [], lessons } : null });
    else await availableApi(route, url);
  });
  await openPage(page, baseURL!);
  await page.getByRole('button', { name: 'بحث الطلاب', exact: true }).click();
  await page.getByRole('button', { name: 'عرض سجل طالبة تقرير طويل', exact: true }).click();
  const button = page.getByRole('button', { name: 'تنزيل PDF للطالب', exact: true });
  await button.click();
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تجهيز التقرير' })).toBeVisible();
  failFont = false;
  const downloading = page.waitForEvent('download');
  await button.click();
  const download = await downloading;
  const pdfPath = test.info().outputPath('long-student-report.pdf');
  await download.saveAs(pdfPath);
  const bytes = await readFile(pdfPath);
  expect(bytes.subarray(0, 5).toString()).toBe('%PDF-');
  expect((bytes.toString('latin1').match(/\/Type \/Page\b/g) ?? []).length).toBeGreaterThan(1);
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تجهيز التقرير' })).toHaveCount(0);
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
    const student = { id: 'student-one', name: 'أحمد اختبار', code: '00123', barcode: '100123', phone: '01000000123', guardianPhone: '', notes: 'ملاحظة اختبار', discountPercent: 25, suspended: false, groups: ['الجمعة'], centers: ['سنتر التجربة'] };
    const payment = { coverage: 'unknown', packageName: '', centerFeePaid: 0, receipts: [] };
    await reply(route, { students: [student], total: 1, groups: [], profile: url.searchParams.has('studentId') ? {
      present: 1, absent: 1, attendanceTotal: 2, examTotal: 1,
      attendances: [{ id: 'a1', status: 'present', lesson: { group: 'الجمعة', number: 1, month: 2, date: '' } }, { id: 'a2', status: 'absent', lesson: { group: 'الجمعة', number: 2, month: 2, date: '' } }],
      exams: [{ id: 'e1', score: 0, maxScore: 10, absent: false, homework: 'complete', lesson: { group: 'الجمعة', number: 1, month: 2, date: '' } }],
      lessons: [
        { id: 'l1', groupId: 'g1', group: 'الجمعة', center: 'سنتر التجربة', month: 2, monthName: '', number: 1, name: '', date: '', attendance: { status: 'present', recordedAt: '', makeupSourceGroup: '', paymentPending: false }, payment, exams: [{ id: 'e1', name: 'امتحان', score: 0, maxScore: 10, absent: false }], homework: 'complete' },
        { id: 'l2', groupId: 'g1', group: 'الجمعة', center: 'سنتر التجربة', month: 2, monthName: '', number: 2, name: '', date: '', attendance: { status: 'absent', recordedAt: '', makeupSourceGroup: '', paymentPending: false }, payment, exams: [], homework: 'notReviewed' },
      ],
    } : null });
  });
  await openPage(page, baseURL!);
  await page.getByRole('button', { name: 'بحث الطلاب', exact: true }).click();
  await page.getByRole('searchbox', { name: 'البحث عن طالب', exact: true }).fill('00123');
  await page.getByRole('button', { name: 'عرض سجل أحمد اختبار', exact: true }).click();
  await expect(page.getByRole('heading', { name: /أحمد اختبار/ })).toBeVisible();
  await expect(page.getByRole('cell').filter({ hasText: /^غائبوقت التسجيل غير متوفر$/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'امتحان 0 من 10', exact: true })).toBeVisible();
  await expect(page.getByText('ملاحظة اختبار', { exact: true })).toBeVisible();
  if (process.env.CAPTURE_UI === '1') await page.screenshot({ path: '/tmp/massar-student-snapshot-preview.png', fullPage: true });
});


test('legacy warnings remain available without inflating errors or showing null codes', async ({ page, baseURL }) => {
  await installApi(page, async (route, url) => {
    if (url.pathname.endsWith('/diagnostics')) {
      const detail = diagnostics();
      const event = { ...detail.events[0], frames: [], errors: [{ type: 'CenterException', code: null }] };
      await reply(route, { ...detail, total: 75, events: [
        ...Array.from({ length: 59 }, (_, i) => ({ ...event, id: `warning-${i}`, operation: 'ui.warning' })),
        ...Array.from({ length: 16 }, (_, i) => ({ ...event, id: `failure-${i}`, operation: 'lan.command' })),
      ] });
    } else await availableApi(route, url);
  });
  await openPage(page, baseURL!);
  await page.getByRole('button', { name: `عرض مشاكل synthetic-center-alpha ${uploadA}` }).click();
  const detail = page.getByRole('region', { name: 'synthetic-center-alpha · الرئيسي' });
  await expect(detail.getByText('16 خطأ مسجل · 59 تنبيه · 16 حدث ظاهر', { exact: false })).toBeVisible();
  await expect(detail.locator('article')).toHaveCount(16);
  await expect(detail.getByText('كود الخطأ: null', { exact: false })).toHaveCount(0);
  await expect(detail.getByText('هذا السجل القديم لا يحتوي', { exact: false })).toHaveCount(16);
  await detail.getByLabel('الأخطاء فقط').uncheck();
  await expect(detail.locator('article')).toHaveCount(75);
  await expect(detail.getByText('تنبيه · تنبيه أثناء العمل', { exact: true })).toHaveCount(59);
});
