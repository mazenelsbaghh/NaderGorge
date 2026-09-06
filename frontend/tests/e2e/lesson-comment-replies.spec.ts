import { expect, test, type Page, type Route } from '@playwright/test';
import { json, lessonId, openLesson } from '../fixtures/lesson-playback';
import { installAuthAndGoto } from './e2e-contract-helpers';

for (const role of ['admin', 'teacher'] as const) {
  test(`${role} can send a linked reply from comment moderation`, async ({ page, baseURL }) => {
    const user = { id: '96000000-0000-0000-0000-000000000299', fullName: 'صاحب الرد',
      roles: [role === 'admin' ? 'Admin' : 'Teacher'], permissions: ['comments.manage'],
      profileComplete: true, allowedDomains: [role], allowedNavbarItems: [], authorizationVersion: 1 };
    const comments = [{ id: '96000000-0000-0000-0000-000000000298', lessonId,
      lessonTitle: 'الحصة', studentName: 'صاحب السؤال', body: 'السؤال الأصلي للمراجعة',
      status: 'Approved', createdAt: '2026-09-06T10:00:00Z' }];
    let sent: unknown;
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/auth/session')) return json(route, { user, authorizationVersion: 1 });
      if (path.endsWith(`/${role}/comments`)) return json(route, comments);
      if (path.endsWith(`/${role}/comments/${comments[0].id}/reply`)) {
        sent = route.request().postDataJSON();
        return json(route, { id: 'saved-reply', parentCommentId: comments[0].id, status: 'Approved' });
      }
      if (path.endsWith('/teacher/context')) return json(route, { permissions: ['comments'], isOwner: true });
      if (path.endsWith('/public/settings')) return route.fulfill({ json: { maintenanceMode: false } });
      return json(route, []);
    });
    const url = new URL(`/${role}/comments`, baseURL);
    if (role === 'admin') url.hostname = 'admin.lvh.me';
    if (role === 'teacher') url.hostname = 'teacher.lvh.me';
    await installAuthAndGoto(page, `synthetic-${role}-token`, user, url.toString());
    const comment = page.locator('article').filter({ hasText: comments[0].body });
    await comment.getByRole('button', { name: 'رد على التعليق', exact: true }).click();
    await comment.getByLabel('رد على صاحب السؤال', { exact: true }).fill('الرد على السؤال المحدد');
    await comment.getByRole('button', { name: 'إرسال الرد', exact: true }).click();
    await expect(page.getByText('تم حفظ الرد تحت التعليق الأصلي.', { exact: true })).toBeVisible();
    expect(sent).toEqual({ body: 'الرد على السؤال المحدد' });
    await expect(comment.getByRole('button', { name: 'رد على التعليق', exact: true })).toBeVisible();
  });
}

const parentId = '96000000-0000-0000-0000-000000000201';
const root = {
  id: parentId,
  lessonId,
  body: 'سؤال الطالب الأصلي',
  authorName: 'صاحب السؤال',
  status: 'Approved',
  createdAt: '2026-09-06T10:00:00Z',
  isOwnComment: false,
  replyCount: 1,
};
const published = {
  ...root,
  id: '96000000-0000-0000-0000-000000000202',
  parentCommentId: parentId,
  body: 'شرح المدرس للسؤال',
  authorName: 'المدرس',
  replyCount: 0,
};

async function openDiscussion(page: Page) {
  const replies = [published];
  let failPost = false;
  const requests: unknown[] = [];
  const playback = await openLesson(page, async () => {
    await page.route(
      `**/api/content/lessons/${lessonId}/comments*`,
      async (route) => {
        if (route.request().method() === 'POST') {
          const request = route.request().postDataJSON();
          requests.push(request);
          if (failPost) return json(route, null, 503);
          replies.push({
            ...published,
            id: '96000000-0000-0000-0000-000000000203',
            body: request.body,
            status: 'Pending',
            authorName: 'أنت',
            isOwnComment: true,
          });
          return json(route, {
            id: replies.at(-1)!.id,
            status: 'Pending',
            createdAt: published.createdAt,
            message: 'تم إرسال الرد للمراجعة',
          });
        }
        const parent = new URL(route.request().url()).searchParams.get(
          'parentCommentId'
        );
        return json(route, parent ? replies : [root]);
      }
    );
  });
  const discussion = page
    .locator('section')
    .filter({
      has: page.getByRole('heading', { name: 'التعليقات تحت الفيديو' }),
    });
  const thread = discussion.locator('article').filter({ hasText: root.body });
  await expect(thread).toBeVisible();
  return {
    discussion,
    thread,
    requests,
    playback,
    failPosts: () => {
      failPost = true;
    },
  };
}

test('student reply is sent with its parent and appears only inside that thread', async ({
  page,
}) => {
  const { thread, discussion, requests, playback } = await openDiscussion(page);
  await thread.getByRole('button', { name: 'رد', exact: true }).click();
  await expect(thread.getByText(published.body, { exact: true })).toBeVisible();
  await thread
    .getByLabel('رد على صاحب السؤال', { exact: true })
    .fill('  رد مرتبط بالسؤال  ');
  await thread.getByRole('button', { name: 'إرسال الرد', exact: true }).click();
  await expect(
    thread.getByText('رد مرتبط بالسؤال', { exact: true })
  ).toBeVisible();
  await expect(thread.getByText('قيد المراجعة، ظاهر لك فقط')).toBeVisible();
  expect(requests).toEqual([
    { body: 'رد مرتبط بالسؤال', parentCommentId: parentId },
  ]);
  await expect(
    discussion.getByText('رد مرتبط بالسؤال', { exact: true })
  ).toHaveCount(1);
  expect(
    await playback.originalFrame.evaluate((frame) => frame.isConnected)
  ).toBe(true);
});

test('background comment refresh preserves an in-progress reply and the playing video', async ({
  page,
}) => {
  const { thread, playback } = await openDiscussion(page);
  await thread.getByRole('button', { name: 'رد', exact: true }).click();
  const draft = thread.getByLabel('رد على صاحب السؤال', { exact: true });
  await draft.fill('مسودة لم أرسلها');
  const pending: Route[] = [];
  await page.route(`**/api/content/lessons/${lessonId}/comments*`, (route) => {
    pending.push(route);
  });
  playback.notify('LessonCommentApproved');
  await expect.poll(() => pending.length).toBeGreaterThanOrEqual(2);
  await expect(draft).toHaveValue('مسودة لم أرسلها');
  for (const request of pending)
    await json(
      request,
      new URL(request.request().url()).searchParams.has('parentCommentId')
        ? [published]
        : [root]
    );
  await expect(draft).toHaveValue('مسودة لم أرسلها');
  expect(
    await playback.originalFrame.evaluate((frame) => frame.isConnected)
  ).toBe(true);
  expect(playback.sessions.length).toBe(playback.originalSessionCount);
});

test('failed reply keeps its text and never inserts a fake success', async ({
  page,
}) => {
  const { thread, failPosts, requests } = await openDiscussion(page);
  failPosts();
  await thread.getByRole('button', { name: 'رد', exact: true }).click();
  const draft = thread.getByLabel('رد على صاحب السؤال', { exact: true });
  await draft.fill('رد لم يتأكد إرساله');
  await thread.getByRole('button', { name: 'إرسال الرد', exact: true }).click();
  await expect(thread.getByRole('alert')).toContainText(
    'تعذر تأكيد إرسال الرد'
  );
  await expect(draft).toHaveValue('رد لم يتأكد إرساله');
  await expect(thread.getByText('قيد المراجعة، ظاهر لك فقط')).toHaveCount(0);
  expect(requests).toHaveLength(1);
});
