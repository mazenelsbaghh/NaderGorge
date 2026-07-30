import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), 'utf8');

const [teachers, testimonials, featureCarousel, studentBottomNav, assistant, teacher, admin] =
  await Promise.all([
    read('src/components/landing/CircularGallerySection.tsx'),
    read('src/components/landing/TestimonialsSection.tsx'),
    read('src/components/ui/feature-carousel.tsx'),
    read('src/components/layout/StudentBottomNav.tsx'),
    read('src/components/assistant/AssistantShellChrome.tsx'),
    read('src/components/teacher/TeacherShellChrome.tsx'),
    read('src/components/admin/AdminShellChrome.tsx'),
  ]);

for (const token of [
  'visibilitychange',
  'onMouseEnter',
  'onFocusCapture',
  'onKeyDown',
  'aria-live',
  'prefersReducedMotion',
]) {
  assert.ok(teachers.includes(token), `teacher carousel is missing ${token}`);
}

for (const token of ['onClick={() => paginate(-1)}', 'onClick={() => paginate(1)}', 'aria-current']) {
  assert.ok(testimonials.includes(token), `testimonial carousel is missing ${token}`);
}

for (const token of [
  'prefersReducedMotion',
  'visibilitychange',
  'onMouseEnter',
  'onFocusCapture',
  'aria-label="الخطوة السابقة"',
  'aria-label="الخطوة التالية"',
]) {
  assert.ok(featureCarousel.includes(token), `registration carousel is missing ${token}`);
}

assert.ok(
  studentBottomNav.includes('primaryItems.slice(0, 3)'),
  'student bottom navigation must expose three primary items plus home and menu'
);
assert.ok(studentBottomNav.includes('aria-current'), 'student bottom navigation needs current-page semantics');

for (const [surface, source] of [
  ['assistant', assistant],
  ['teacher', teacher],
  ['admin', admin],
]) {
  assert.ok(source.includes('.slice(0, 3)'), `${surface} mobile navigation must be bounded`);
  assert.ok(source.includes('aria-current={isMoreActive'), `${surface} more control needs current-page semantics`);
}

console.log('carousel and mobile-navigation accessibility checks passed');
