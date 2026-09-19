import { expect, test, type Page, type Route } from '@playwright/test';

type WatchLesson = {
  startedVideos?: number;
  completedVideos?: number;
  watchedVideos: number;
};

function corsHeaders(route: Route) {
  return {
    'Access-Control-Allow-Origin': route.request().headers().origin || 'http://app.lvh.me:3000',
    'Access-Control-Allow-Credentials': 'true',
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type, Authorization',
  };
}

async function fulfillJson(route: Route, body: unknown) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: corsHeaders(route) });
    return;
  }

  await route.fulfill({ contentType: 'application/json', headers: corsHeaders(route), body: JSON.stringify(body) });
}

async function mockParentProgress(page: Page, watchLesson: WatchLesson) {
  await page.route('**/parent/verify-code', (route) => fulfillJson(route, { success: true, data: { token: 'parent-token', studentName: 'طالب الاختبار' } }));
  await page.route('**/parent/student-details', (route) => fulfillJson(route,
    {
      success: true,
      data: {
        studentName: 'طالب الاختبار', grade: 'SecondSecondary', attendance: { totalLessons: 1, watchedLessons: 0, completionRate: 0 },
        exams: [], homeworks: [], warnings: [], teachers: [], balance: { currentBalance: 0 }, courses: [],
        watchLessons: [{ lessonId: '00000000-0000-0000-0000-000000000001', lessonTitle: 'الحصة التجريبية', packageName: 'الكورس', termTitle: 'الترم الأول', totalVideos: 4, isCompleted: false, ...watchLesson }],
      },
    }));
}

async function openParentPortal(page: Page, watchLesson: WatchLesson) {
  await mockParentProgress(page, watchLesson);
  await page.goto('/parent');
  await page.getByLabel('رمز متابعة ولي الأمر').fill('ABC123');
  await page.getByRole('button', { name: 'عرض متابعة الطالب' }).click();
}

test('parent portal renders started and completed values returned by the API', async ({ page }) => {
  await openParentPortal(page, { startedVideos: 3, completedVideos: 1, watchedVideos: 1 });

  await expect(page.getByText('بدأ مشاهدة 3 من 4 فيديو')).toBeVisible();
  await expect(page.getByText('أكمل 1 من 4')).toBeVisible();
});

test('parent portal keeps legacy payloads explicit during a rolling deployment', async ({ page }) => {
  await openParentPortal(page, { watchedVideos: 1 });

  await expect(page.getByText('أكمل 1 من 4')).toBeVisible();
  await expect(page.getByText(/بدأ مشاهدة/)).toHaveCount(0);
});
