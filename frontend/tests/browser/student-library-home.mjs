import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { webkit } from '@playwright/test';

const baseUrl = process.env.STUDENT_HOME_TEST_URL || 'http://app.lvh.me:8738';
const user = { id: 'student-home-test', fullName: 'أحمد محمد', roles: ['Student'], permissions: [], profileComplete: true, allowedDomains: ['student'], allowedNavbarItems: [], authorizationVersion: 1 };
const lessons = [
  { id: 'lesson-history', title: 'الحصة الأولى', packageId: 'history', packageName: 'التاريخ', teacherName: 'مستر نادر جورج', termTitle: 'الصف الأول الثانوي', isCompleted: false, videoCount: 4, watchedVideoCount: 2, watchProgressPercent: 60, recordedWatchSeconds: 1080, totalVideoSeconds: 1800, lastWatchedAt: '2026-09-09T10:00:00Z' },
  { id: 'lesson-chemistry', title: 'التفاعلات الكيميائية', packageId: 'chemistry', packageName: 'الكيمياء', teacherName: 'مدرس الكيمياء', termTitle: 'الترم الأول', isCompleted: true, videoCount: 1, watchedVideoCount: 1, watchProgressPercent: 100, recordedWatchSeconds: 600, totalVideoSeconds: 600 },
];
const videos = [
  { id: 'v1', title: 'الجزء الأول: مقدمة التاريخ', durationSeconds: 300, learningWatchedSeconds: 300, hasAccess: true },
  { id: 'v2', title: 'الجزء الثاني: مصادر التاريخ', durationSeconds: 600, learningWatchedSeconds: 600, hasAccess: true },
  { id: 'v3', title: 'الجزء الثالث: الحضارات القديمة', durationSeconds: 600, learningWatchedSeconds: 180, hasAccess: true },
  { id: 'v4', title: 'الجزء الرابع: مصر القديمة', durationSeconds: 300, learningWatchedSeconds: 0, hasAccess: true },
];

async function installApi(page, { failLessons = false, empty = false } = {}) {
  const calls = new Map();
  await page.addInitScript(authUser => {
    localStorage.setItem('accessToken', 'student-home-test-token');
    localStorage.setItem('user', JSON.stringify(authUser));
    localStorage.setItem(`onboarding_ack_${authUser.id}`, '1');
  }, user);
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    calls.set(path, (calls.get(path) || 0) + 1);
    if (path === '/student/lessons' && failLessons) return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Test unavailable' }) });
    let data = null;
    if (path === '/auth/session') data = { user, authorizationVersion: 1 };
    if (path === '/student/dashboard') data = { studentName: user.fullName, activePackages: [], upcomingExams: [], upcomingHomeworks: [], overallProgressPercent: 50, totalLessonsCompleted: 1, totalLessons: 2, codesRedeemed: 0 };
    if (path === '/student/dashboard/quick-access') data = [];
    if (path === '/student/lessons') data = empty ? [] : lessons;
    if (path === '/content/lessons/lesson-history') data = { id: 'lesson-history', title: 'الحصة الأولى', videos };
    if (path === '/content/packages') data = [];
    if (path === '/student/exams' || path.includes('public-exams')) data = [];
    if (path === '/student/shell-bootstrap') data = { unreadNotificationsCount: 2, currentBalance: 0, gamification: { totalPoints: 0, currentStreakCount: 0, longestStreakCount: 0, levelName: '' }, themePreferences: {}, hasSeenTrackingCodePopup: true };
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data }) });
  });
  return calls;
}

test('library home uses live progress, filters, shared mobile chrome and fits all viewports', { timeout: 120000 }, async () => {
  const browser = await webkit.launch();
  try {
    await mkdir('../artifacts/student-home', { recursive: true });
    for (const width of [390, 768, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: width === 390 ? 844 : 1024 }, hasTouch: width < 1024 });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      const calls = await installApi(page);
      await page.goto(`${baseUrl}/student`);
      const overview = page.getByTestId('student-learning-overview');
      await overview.waitFor();
      await page.getByRole('progressbar', { name: videos[2].title }).waitFor();
      assert.equal(await page.getByRole('progressbar', { name: videos[2].title }).getAttribute('aria-valuenow'), '30');
      assert.equal(await page.getByRole('progressbar', { name: 'تقدّم الحصة', exact: true }).getAttribute('aria-valuenow'), '60');
      assert.equal(await page.getByRole('progressbar', { name: 'إجمالي تقدّم المشاهدة', exact: true }).getAttribute('aria-valuenow'), '70');
      await page.screenshot({ path: `../artifacts/student-home/library-${width}.png`, fullPage: true });
      await page.getByRole('heading', { name: 'آخر حصة فتحتها' }).scrollIntoViewIfNeeded();
      await page.screenshot({ path: `../artifacts/student-home/progress-${width}.png` });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await page.getByRole('button', { name: 'مكتملة', exact: true }).click();
      assert.equal(await page.getByRole('link', { name: 'افتح الكورس', exact: true }).getAttribute('href'), '/student/packages/chemistry');
      await page.getByRole('button', { name: 'الكل', exact: true }).click();
      await page.getByRole('textbox', { name: 'ابحث عن كورس أو درس' }).fill('مستر نادر');
      assert.equal(await page.getByRole('link', { name: 'افتح الكورس', exact: true }).getAttribute('href'), '/student/packages/history');
      assert.equal(calls.get('/student/lessons'), 1, 'rendering must not repeatedly request progress');
      if (width < 1024) {
        const nav = page.getByRole('navigation', { name: 'القائمة السفلية للطالب' });
        assert.equal(await nav.isVisible(), true);
        for (const [name, suffix] of [['دروسي', '/student/lessons'], ['باقاتي', '/student/packages'], ['امتحاناتي', '/student/public-exams']]) {
          await nav.getByRole('link', { name, exact: true }).click();
          await page.waitForURL(`${baseUrl}${suffix}`);
          assert.equal(await page.getByTestId('student-mobile-header').isVisible(), true);
          assert.equal(await nav.getByRole('link', { name, exact: true }).getAttribute('aria-current'), 'page');
        }
        await nav.getByRole('button', { name: 'القائمة', exact: true }).click();
        assert.equal(await page.getByRole('button', { name: 'إغلاق القائمة' }).isVisible(), true);
        await page.getByRole('button', { name: 'إغلاق القائمة' }).click();
      }
      assert.deepEqual(errors, []);
      await context.close();
    }
  } finally { await browser.close(); }
});

test('unavailable progress is an error, not a zero or a repeated request loop', { timeout: 60000 }, async () => {
  const browser = await webkit.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const calls = await installApi(page, { failLessons: true });
    await page.goto(`${baseUrl}/student`);
    await page.getByRole('button', { name: 'إعادة تحميل التقدّم' }).waitFor();
    const attempts = calls.get('/student/lessons');
    await page.getByRole('navigation', { name: 'القائمة السفلية للطالب' }).getByRole('button', { name: 'القائمة' }).click();
    await page.getByRole('button', { name: 'إغلاق القائمة' }).click();
    assert.equal(calls.get('/student/lessons'), attempts);
    assert.equal(await page.getByTestId('student-learning-overview').count(), 0);
  } finally { await browser.close(); }
});
