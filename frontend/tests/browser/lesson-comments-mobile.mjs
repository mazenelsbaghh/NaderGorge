import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { webkit } from '@playwright/test';

for (const width of [320, 390, 768, 1280]) {
  test(
    `lesson discussion fits ${width}px with long text, pending comments and threaded replies`,
    { timeout: 90000 },
    async () => {
      const browser = await webkit.launch();
      try {
        const page = await browser.newPage({
          viewport: { width, height: 844 },
          hasTouch: width < 1024,
        });
        page.setDefaultTimeout(15000);
        const user = {
          id: 'student',
          fullName: 'أحمد محمد',
          roles: ['Student'],
          permissions: [],
          allowedDomains: ['student'],
          allowedNavbarItems: [],
          profileComplete: true,
          authorizationVersion: 1,
        };
        await page.addInitScript((user) => {
          localStorage.setItem('accessToken', 'test-token');
          localStorage.setItem('user', JSON.stringify(user));
          localStorage.setItem(`onboarding_ack_${user.id}`, '1');
        }, user);
        const root = {
          id: 'root',
          lessonId: 'discussion',
          authorName: 'مستر نادر جورج',
          status: 'Approved',
          createdAt: '2026-09-09T12:00:00Z',
          body:
            'أرقام الاستفسارات العلمية '.repeat(6) +
            'https://example.test/' +
            'a'.repeat(180),
          replyCount: 1,
        };
        const own = {
          ...root,
          id: 'own',
          authorName: user.fullName,
          body: 'تعليقي المنشور',
          isOwnComment: true,
          replyCount: 0,
        };
        const replies = [
          {
            ...root,
            id: 'reply',
            parentCommentId: root.id,
            authorName: 'زميلك',
            body: 'رد طويل ' + 'س'.repeat(120),
            replyCount: 0,
          },
        ];
        let failPost = true;
        await page.route('**/api/**', async (route) => {
          const url = new URL(route.request().url());
          const path = url.pathname.replace(/^\/api/, '');
          let data = [];
          if (path === '/auth/session')
            data = { user, authorizationVersion: 1 };
          if (path === '/student/shell-bootstrap')
            data = {
              unreadNotificationsCount: 0,
              currentBalance: 0,
              gamification: {},
              themePreferences: {},
              hasSeenTrackingCodePopup: true,
            };
          if (path === '/content/lessons/discussion')
            data = {
              id: 'discussion',
              title: 'الحصة الأولى',
              packageId: 'package',
              isLocked: false,
              videos: [],
              resources: [],
            };
          if (path.includes('/comments')) {
            data = url.searchParams.has('parentCommentId')
              ? replies.filter(
                  (reply) =>
                    reply.parentCommentId ===
                    url.searchParams.get('parentCommentId')
                )
              : path.endsWith('/mine')
                ? [own]
                : [root, own];
            if (route.request().method() === 'POST') {
              if (failPost)
                return route.fulfill({
                  status: 503,
                  json: { success: false, message: 'Test failure' },
                });
              const posted = route.request().postDataJSON();
              data = {
                id: 'new',
                status: 'Pending',
                createdAt: '2026-09-09T13:00:00Z',
                message: 'تم إرسال الرد للمراجعة',
              };
              if (posted.parentCommentId)
                replies.push({
                  ...root,
                  ...data,
                  parentCommentId: posted.parentCommentId,
                  body: posted.body,
                  authorName: user.fullName,
                });
            }
          }
          return route.fulfill({ json: { success: true, data } });
        });
        await page.goto(
          'http://app.lvh.me:8738/student/packages/package/lessons/discussion'
        );
        const discussion = page.getByTestId('lesson-discussion');
        await discussion.getByText(own.body, { exact: true }).waitFor();
        assert.equal(
          await discussion.getByText(own.body, { exact: true }).count(),
          1
        );
        assert.equal(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth
          ),
          true,
          JSON.stringify(
            await page.locator('body *').evaluateAll((elements) =>
              elements
                .filter((el) => {
                  const r = el.getBoundingClientRect();
                  return (
                    r.width > 0 && (r.right > innerWidth + 1 || r.left < -1)
                  );
                })
                .slice(-12)
                .map((el) => ({
                  tag: el.tagName,
                  class: el.className,
                  width: el.getBoundingClientRect().width,
                  text: el.textContent?.slice(0, 80),
                }))
            )
          )
        );
        const thread = discussion.locator('[data-comment-id="root"]');
        await thread.scrollIntoViewIfNeeded();
        await thread.getByText(replies[0].body, { exact: true }).waitFor();
        assert.equal(
          await thread.getByRole('button', { name: /^عرض الردود/ }).count(),
          0
        );
        assert.equal(
          await thread
            .getByText(`ردًا على ${root.authorName}`, { exact: true })
            .isVisible(),
          true
        );
        await thread.getByRole('button', { name: 'رد', exact: true }).click();
        const draft = thread.getByLabel(`رد على ${root.authorName}`, {
          exact: true,
        });
        await draft.fill('الرد الخاص بتجربة الموبايل');
        await thread
          .getByRole('button', { name: 'إرسال الرد', exact: true })
          .click();
        await thread.getByRole('alert').waitFor();
        assert.equal(await draft.inputValue(), 'الرد الخاص بتجربة الموبايل');
        failPost = false;
        await thread
          .getByRole('button', { name: 'إرسال الرد', exact: true })
          .click();
        await thread
          .getByText('الرد الخاص بتجربة الموبايل', { exact: true })
          .waitFor();
        await thread
          .getByText('قيد المراجعة، ظاهر لك فقط', { exact: true })
          .waitFor();
        assert.equal(
          await thread
            .getByText('قيد المراجعة، ظاهر لك فقط', { exact: true })
            .count(),
          1
        );
        const emptyThread = discussion.locator('[data-comment-id="own"]');
        await emptyThread
          .getByRole('button', { name: 'رد', exact: true })
          .click();
        await emptyThread
          .getByLabel(`رد على ${own.authorName}`, { exact: true })
          .fill('أول رد على تعليقي');
        await emptyThread
          .getByRole('button', { name: 'إرسال الرد', exact: true })
          .click();
        await emptyThread
          .getByText('قيد المراجعة، ظاهر لك فقط', { exact: true })
          .waitFor();
        assert.equal(
          await emptyThread
            .getByText('أول رد على تعليقي', { exact: true })
            .isVisible(),
          true
        );
        assert.equal(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth
          ),
          true
        );
        await discussion
          .getByLabel('أضف تعليقًا جديدًا')
          .fill('سؤال جديد قيد المراجعة');
        await discussion
          .getByRole('button', { name: 'إرسال التعليق', exact: true })
          .click();
        await discussion
          .getByText('سؤال جديد قيد المراجعة', { exact: true })
          .waitFor();
        await page.waitForFunction(
          () => document.querySelector('#lesson-comment-body')?.value === ''
        );
        await discussion
          .getByRole('button', { name: 'تعليقاتي', exact: true })
          .click();
        assert.equal(
          await discussion.locator('[data-comment-id="root"]').count(),
          0
        );
        await discussion
          .getByRole('button', { name: 'كل التعليقات', exact: true })
          .click();
        await discussion.scrollIntoViewIfNeeded();
        await mkdir('../artifacts/comments-mobile', { recursive: true });
        await page.screenshot({
          path: `../artifacts/comments-mobile/discussion-${width}.png`,
        });
      } finally {
        await browser.close();
      }
    }
  );
}
