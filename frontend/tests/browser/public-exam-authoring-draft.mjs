import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium, expect } from '@playwright/test';

test('a public exam draft keeps questions through reload and asks before restoring over a newer revision', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const user = {
      id: 'public-exam-author', fullName: 'محرر الامتحان', roles: ['Admin'],
      permissions: ['content.manage', 'public_exams.manage', 'exams.manage'],
      allowedDomains: ['admin'], profileComplete: true, authorizationVersion: 1,
    };
    await page.addInitScript(account => {
      localStorage.setItem('accessToken', 'public-exam-test-token');
      localStorage.setItem('user', JSON.stringify(account));
    }, user);

    const productId = 'public-product';
    const examId = 'public-draft-exam';
    let published = {
      assessmentId: examId, kind: 'exam', schemaVersion: 1, title: 'الامتحان الشامل',
      description: '', totalScore: 10, passingScore: 5, durationMinutes: 30,
      isActive: true, isMandatory: false, isRandomized: false, displayQuestionCount: null, questions: [],
    };
    const updates = [];
    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      let response = [];
      if (path.endsWith('/auth/session')) response = { user, authorizationVersion: 1 };
      if (path.endsWith('/admin/public-exams')) response = [{ id: productId, examId }];
      if (path.endsWith('/admin/subjects')) response = [{ id: 'subject-1', name: 'التاريخ' }];
      if (path.endsWith('/dashboard')) response = { ...published, questionCount: published.questions.length, attempts: [] };
      if (path.endsWith('/editor')) response = { definition: published, attemptCount: 0, revisionToken: 'editor-token' };
      if (path.endsWith('/ocr/questions')) response = [{
        text: 'سؤال من الصورة', type: 'MCQ', points: 1, confidence: 1,
        options: [{ text: 'الإجابة الأولى', isCorrect: true }, { text: 'الإجابة الثانية', isCorrect: false }],
      }];
      if (path.endsWith('/revision-preview')) response = {
        revisionToken: 'preview-token', attemptCount: 0, addedQuestions: 2, removedQuestions: 0, attempts: [],
      };
      if (path.endsWith('/definition')) {
        const update = request.postDataJSON();
        updates.push(update);
        published = update.definition;
        response = { definition: published, attemptCount: 0, revisionToken: 'saved-token' };
      }
      await route.fulfill({ json: { success: true, data: response } });
    });

    await page.goto('http://admin.lvh.me:8738/admin/public-exams/' + productId);
    await page.getByRole('button', { name: 'تعديل الإعدادات والأسئلة' }).click();
    const subject = page.getByLabel('مادة أسئلة الامتحان');
    await subject.selectOption('subject-1');
    await page.getByRole('button', { name: 'إضافة سؤال', exact: true }).click();
    await page.locator('#assessment-question-editor .ql-editor').fill('سؤال تمت كتابته');
    await page.getByPlaceholder(/اكتب عناصر الإجابة الصحيحة/).fill('نموذج الإجابة المحفوظ');
    await page.getByText('اختيار الصور أو PDF', { exact: true }).locator('input').setInputFiles({
      name: 'paper.png', mimeType: 'image/png', buffer: Buffer.from('synthetic image'),
    });
    await expect(page.getByText('مراجعة الأسئلة المستخرجة')).toBeVisible();
    await expect(page.getByText('تم حفظ المسودة في هذا التبويب.')).toBeVisible();
    assert.equal(updates.length, 0);

    await page.reload();
    await expect(page.getByText('تم استرجاع مسودتك في هذا التبويب.')).toBeVisible();
    await expect(page.getByRole('button', { name: '1. سؤال تمت كتابته' })).toBeVisible();
    await expect(subject).toHaveValue('subject-1');
    await expect(page.getByText('مراجعة الأسئلة المستخرجة')).toBeVisible();
    await page.getByRole('button', { name: 'إضافة الأسئلة للمحتوى' }).click();
    await expect(page.getByRole('button', { name: '2. سؤال من الصورة' })).toBeVisible();

    published = { ...published, title: 'عنوان محفوظ من محرر آخر' };
    await page.reload();
    const restore = page.getByRole('button', { name: 'استرجاع المسودة', exact: true });
    await expect(restore).toBeVisible();
    await expect(page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true })).toHaveCount(0);
    await restore.click();

    await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
    await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
    await page.waitForURL('**/admin/content/exams/' + examId);
    assert.equal(updates.length, 1);
    assert.equal(updates[0].subjectId, 'subject-1');
    assert.equal(updates[0].definition.questions.length, 2);
    assert.equal(updates[0].definition.questions[0].writtenCorrection, 'نموذج الإجابة المحفوظ');
    await page.getByRole('button', { name: 'تعديل الإعدادات والأسئلة' }).click();
    await expect(page.getByRole('heading', { name: 'الأسئلة (2)', exact: true })).toBeVisible();
    await expect(page.getByText('تم استرجاع مسودتك في هذا التبويب.')).toHaveCount(0);
  } finally {
    await browser.close();
  }
});
