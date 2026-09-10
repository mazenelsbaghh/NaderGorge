import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { webkit } from '@playwright/test';

test('assessment review saves grades, confirms deletion and downloads all missing students', { timeout: 90000 }, async () => {
  const browser = await webkit.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    page.setDefaultTimeout(15000);
    const user = { id: 'assessment-admin', fullName: 'مدير الاختبار', roles: ['Admin'], permissions: ['content.manage', 'exams.manage'], allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
    await page.addInitScript(user => { localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
    let saved; let deletes = 0; let failSave = true;
    const review = { attemptId: 'attempt', studentName: 'طالب اختبار', title: 'واجب الاختبار', score: 0, total: 20, status: 'PendingReview', canGrade: true, questions: [{ questionId: 'question', order: 1, text: 'اشرح السبب', answer: 'إجابة الطالب الأصلية', maximum: 4, score: null }] };
    await page.route('**/api/**', async route => {
      const url = new URL(route.request().url()); const path = url.pathname.replace(/^\/api/, '');
      let data = [];
      if (path === '/auth/session') data = { user, authorizationVersion: 1 };
      if (path.endsWith('/dashboard')) data = { title: 'واجب الاختبار', questionCount: 1, totalScore: 20, passingScore: 10, isActive: true, questions: [], submissions: deletes ? [] : [{ submissionId: 'attempt', studentId: 'student', studentName: 'طالب اختبار', status: 'PendingReview', scoreAchieved: 0, submittedAt: '2026-09-09T12:00:00Z' }] };
      if (path.endsWith('/review')) data = review;
      if (path.endsWith('/grade')) {
        saved = route.request().postDataJSON();
        if (failSave) return route.fulfill({ status: 503, json: { success: false, message: 'Temporary test failure' } });
        review.score = 15; review.questions[0].score = 3; data = true;
      }
      if (route.request().method() === 'DELETE') { deletes++; data = true; }
      if (path.endsWith('/missing-students')) data = url.searchParams.get('page') === '1'
        ? { students: [{ studentId: 'one', name: '=formula', phone: '01000000001' }], hasMore: true }
        : { students: [{ studentId: 'two', name: 'طالب ثانٍ', phone: '01000000002' }], hasMore: false };
      await route.fulfill({ json: { success: true, data } });
    });
    await page.goto('http://admin.lvh.me:8738/admin/content/homework/homework');
    await page.getByRole('button', { name: 'الإجابات والتصحيح', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByText('إجابة الطالب الأصلية', { exact: true }).waitFor();
    await dialog.getByLabel('درجة السؤال 1', { exact: true }).fill('3');
    await dialog.getByRole('button', { name: 'حفظ التصحيح اليدوي' }).click();
    await dialog.getByRole('alert').waitFor();
    assert.equal(await dialog.getByLabel('درجة السؤال 1', { exact: true }).inputValue(), '3');
    failSave = false;
    await dialog.getByRole('button', { name: 'حفظ التصحيح اليدوي' }).click();
    await dialog.getByText('واجب الاختبار — الدرجة: 15 / 20', { exact: true }).waitFor();
    assert.deepEqual(saved.scores, [{ questionId: 'question', score: 3 }]);
    await dialog.getByRole('button', { name: 'حذف المحاولة وإتاحة الحل من جديد' }).click();
    assert.equal(deletes, 0);
    await dialog.getByRole('button', { name: 'إلغاء', exact: true }).click();
    assert.equal(deletes, 0);
    await dialog.getByRole('button', { name: 'حذف المحاولة وإتاحة الحل من جديد' }).click();
    await dialog.getByRole('button', { name: 'حذف وإتاحة الإعادة', exact: true }).click();
    await page.getByText('لا توجد تسليمات لهذا الواجب حتى الآن.', { exact: true }).waitFor();
    assert.equal(deletes, 1);
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'تنزيل الطلاب الذين لم يسلّموا' }).click();
    const download = await downloadPromise;
    const csv = await readFile(await download.path(), 'utf8');
    assert.ok(csv.includes('طالب ثانٍ'));
    assert.ok(csv.includes("'=formula"));
    assert.ok(csv.includes("'01000000001"));
  } finally { await browser.close(); }
});

for (const kind of ['exam', 'homework']) {
  test(`${kind} attempt can be deleted from its row without loading answers, with cancel and failure recovery`, { timeout: 60000 }, async () => {
    const browser = await webkit.launch();
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
      page.setDefaultTimeout(10000);
      const user = { id: 'assessment-admin', fullName: 'مدير الاختبار', roles: ['Admin'], permissions: ['content.manage', 'exams.manage'], allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      let reviewReads = 0;
      let deletes = 0;
      let removed = false;
      const deletePath = kind === 'exam' ? '/admin/exams/assessment/attempts/attempt' : '/admin/homework/assessment/submissions/attempt';
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path.endsWith('/dashboard')) {
          const attempt = { attemptId: 'attempt', submissionId: 'attempt', studentId: 'student', studentName: 'طالب الإعادة', studentPhone: '20000000001', status: 'Graded', scoreAchieved: 0, evaluation: 'ضعيف', isPassed: false, attemptedAt: '2026-09-10T12:00:00Z', submittedAt: '2026-09-10T12:00:00Z' };
          data = { examId: 'assessment', title: 'اختبار الإعادة', description: '', questionCount: 0, totalScore: 20, passingScore: 10, isActive: true, questions: [], attempts: removed ? [] : [attempt], submissions: removed ? [] : [attempt] };
        }
        if (path.endsWith('/review') || path.endsWith('/assessment-review')) reviewReads++;
        if (route.request().method() === 'DELETE') {
          assert.equal(path, deletePath);
          deletes++;
          if (deletes === 1) return route.fulfill({ status: 503, json: { success: false, message: 'تعذر حذف المحاولة الآن' } });
          removed = true;
          data = true;
        }
        return route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`http://admin.lvh.me:8738/admin/content/${kind === 'exam' ? 'exams' : 'homework'}/assessment`);
      if (kind === 'exam') await page.getByRole('button', { name: /محاولات الطلاب/ }).click();
      const shortcut = page.getByRole('button', { name: 'حذف محاولة طالب الإعادة وإتاحة الإعادة', exact: true });
      await shortcut.click();
      const dialog = page.getByRole('dialog');
      await dialog.getByText(/سيتم حذف إجابات طالب الإعادة/).waitFor();
      assert.equal(reviewReads, 0);
      assert.equal(deletes, 0);
      await dialog.getByRole('button', { name: 'إلغاء', exact: true }).click();
      assert.equal(await page.getByRole('dialog').count(), 0);
      await shortcut.click();
      await dialog.getByRole('button', { name: 'حذف وإتاحة الإعادة', exact: true }).click();
      await page.getByRole('alert').filter({ hasText: 'تعذر حذف المحاولة الآن' }).waitFor();
      await shortcut.click();
      await dialog.getByRole('button', { name: 'حذف وإتاحة الإعادة', exact: true }).click();
      await page.getByText(kind === 'exam' ? 'لا توجد محاولات مسجلة لهذا الامتحان حتى الآن.' : 'لا توجد تسليمات لهذا الواجب حتى الآن.', { exact: true }).waitFor();
      assert.equal(deletes, 2);
      assert.equal(reviewReads, 0);
    } finally { await browser.close(); }
  });
}
