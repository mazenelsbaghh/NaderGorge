import { expect, test } from '@playwright/test';
import { json, lessonApi, lessonId, openLesson } from '../fixtures/lesson-playback';

const packageId = '96000000-0000-0000-0000-000000000002';
const lessonPath = `/student/packages/${packageId}/lessons/${lessonId}`;

test.describe('2026-10-12 pending essay access regression (synthetic HTTP and media)', () => {
  for (const locked of [false, true]) {
    test(`lesson exam ${locked ? 'stays blocked by' : 'remains available after submitting'} a preceding video exam`, async ({ page }) => {
      await openLesson(page, async () => {
        await page.route(lessonApi, route => json(route, {
          id: lessonId, packageId, title: 'Playback regression lesson', summary: '', hasAccess: true,
          examId: 'lesson-exam', examPassed: false,
          videos: [1, 2, 3].map(index => ({
            id: `96000000-0000-0000-0000-00000000001${index}`, title: `Part ${index}`,
            provider: 'bunny', order: index, limit: 5, watched: 0, watchedSeconds: 0,
            isLocked: false, hasAccess: true, examId: index === 1 ? 'video-exam' : undefined,
            examPassed: false, isExamLocked: index === 1 && locked,
          })),
        }));
      });
      const examButton = page.getByRole('button', { name: 'اختبار الدرس', exact: true });
      if (locked) await expect(examButton).toBeDisabled();
      else await expect(examButton).toBeEnabled();
    });
  }

  test('pending manual review offers a usable return to the lesson without requiring a final grade', async ({ page }) => {
    await openLesson(page);
    await page.route('**/api/exams/lesson-exam/latest-result', route => json(route, {
      attemptId: 'pending-attempt', scoreAchieved: 0, totalScore: 10, isPassed: false,
      blocksNextLesson: false, evaluation: 'قيد التصحيح', isTimeExpired: false,
      resultState: 'PartiallyGraded', lessonId, packageId, questions: [],
    }));
    await page.route('**/api/exams/attempts/pending-attempt/grading-status', route => json(route, {
      attemptId: 'pending-attempt', resultState: 'PartiallyGraded',
      essays: [{ essaySubmissionId: 'pending-essay', questionId: 'question', status: 'WaitTeacher' }],
    }));
    await page.goto(`/student/exams/lesson-exam?packageId=${packageId}&lessonId=${lessonId}`);
    await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'إجاباتك المقالية محفوظة للمراجعة' })).toBeVisible();
    await expect(page.getByText('تقدر ترجع للحصة وتكمل التعلم أثناء انتظار التصحيح. إجاباتك محفوظة.')).toBeVisible();
    await page.getByRole('button', { name: 'العودة للحصة', exact: true }).click();
    await expect(page).toHaveURL(lessonPath);
    await expect(page.getByRole('heading', { name: 'Playback regression lesson', exact: true })).toBeVisible();
  });
});
