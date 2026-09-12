import {
  expect,
  type Page,
  type Route,
  type WebSocketRoute,
} from '@playwright/test';
import type { LessonDetailDto } from '../../src/services/content-service';

export const lessonId = '96000000-0000-0000-0000-000000000001';
const packageId = '96000000-0000-0000-0000-000000000002';
const lessonPath = `/student/packages/${packageId}/lessons/${lessonId}`;
export const lessonApi = `**/api/content/lessons/${lessonId}`;
export const embedSelector = 'iframe[src*="/api/video/embed?s="]';

function lessonFixture(): LessonDetailDto {
  return {
    id: lessonId,
    packageId,
    title: 'Playback regression lesson',
    summary: '',
    hasAccess: true,
    videos: [1, 2, 3].map((index) => ({
      id: `96000000-0000-0000-0000-00000000001${index}`,
      title: `Part ${index}`,
      provider: 'bunny',
      order: index,
      limit: 5,
      watched: 0,
      watchedSeconds: 0,
      isLocked: false,
      hasAccess: true,
    })),
  };
}

export const json = (route: Route, data: unknown, status = 200) =>
  route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify({ success: status === 200, data }),
  });

export async function openLesson(
  page: Page,
  beforeNavigate?: () => Promise<void>,
  options: { trackProgress?: boolean } = {},
) {
  const user = {
    id: '96000000-0000-0000-0000-000000000099',
    fullName: 'Synthetic student',
    phone: '20000000001',
    roles: ['Student'],
    permissions: [],
    profileComplete: true,
    allowedDomains: ['student'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  };
  await page.addInitScript((authUser) => {
    localStorage.setItem('accessToken', 'synthetic-student-token');
    localStorage.setItem('user', JSON.stringify(authUser));
  }, user);
  await page.route('**/api/**', (route) => json(route, []));
  await page.route('**/api/auth/session', (route) =>
    json(route, { user, authorizationVersion: 1 })
  );
  await page.route('**/api/public/settings', (route) =>
    route.fulfill({ json: { maintenanceMode: false } })
  );
  await page.route('**/api/student/shell-bootstrap', (route) =>
    json(route, {
      hasSeenTrackingCodePopup: true,
      unreadNotificationsCount: 0,
      currentBalance: 0,
      gamification: { totalPoints: 0, levelName: 'طالب' },
    })
  );
  let lesson = lessonFixture();
  await page.route(lessonApi, (route) => json(route, lesson));

  const sessions: string[] = [];
  await page.route('**/api/student/video-session', (route) => {
    const videoId = route.request().postDataJSON().lessonVideoId as string;
    sessions.push(videoId);
    return json(route, {
      sessionId: `session-${sessions.length}`,
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      provider: 'bunny',
      videoTitle: videoId,
      durationSeconds: 600,
      thresholdPercentage: 80,
      isPreview: !options.trackProgress,
      watchInfo: {
        currentCount: 0,
        maxCount: 5,
        isLocked: false,
        totalTrackedSeconds: 0,
      },
    });
  });
  // Only HTTP/media and SignalR boundaries are synthetic; the actual page,
  // cache invalidation, selection, carousel and secure player run unchanged.
  await page.route('**/api/video/embed?*', (route) =>
    route.fulfill({
      contentType: 'text/html',
      body: `<body>Playing test video<script>
      parent.postMessage({source:'video-embed',type:'ready',data:{duration:600,provider:'bunny'}},location.origin);
      parent.postMessage({source:'video-embed',type:'stateChange',data:{isPlaying:true}},location.origin);
    </script></body>`,
    })
  );
  const sockets = new Set<WebSocketRoute>();
  await page.routeWebSocket('**/hubs/platform**', (socket) => {
    socket.onMessage((raw) => {
      for (const frame of String(raw).split('\x1e').filter(Boolean)) {
        const message = JSON.parse(frame);
        if (message.protocol) socket.send('{}\x1e');
        if (message.type === 1 && message.invocationId) {
          socket.send(
            `${JSON.stringify({ type: 3, invocationId: message.invocationId })}\x1e`
          );
          if (message.target === 'JoinLesson') sockets.add(socket);
        }
      }
    });
    socket.onClose(() => sockets.delete(socket));
  });
  await beforeNavigate?.();
  await page.goto(lessonPath);
  await expect(
    page.getByRole('heading', { name: lesson.title, exact: true })
  ).toBeVisible();
  await expect.poll(() => sockets.size).toBeGreaterThan(0);
  await page
    .getByRole('navigation', { name: 'فيديوهات الدرس' })
    .getByRole('button', { name: /Part 2/ })
    .click();
  await expect.poll(() => sessions.at(-1)).toBe(lesson.videos[1].id);
  await expect(page.locator(embedSelector)).toHaveCount(1);
  await expect(
    page.frameLocator(embedSelector).getByText('Playing test video')
  ).toBeVisible();
  const originalFrame = await page.locator(embedSelector).elementHandle();
  const originalSessionCount = sessions.length;
  return {
    lesson,
    sessions,
    originalSessionCount,
    originalFrame: originalFrame!,
    updateLesson: (next: LessonDetailDto) => {
      lesson = next;
    },
    notify: (event = 'VideoUpdated') => {
      for (const socket of sockets)
        socket.send(
          `${JSON.stringify({
            type: 1,
            target: event,
            arguments: [
              JSON.stringify({ lessonId, videoId: lesson.videos[0]?.id }),
            ],
          })}\x1e`
        );
    },
  };
}
