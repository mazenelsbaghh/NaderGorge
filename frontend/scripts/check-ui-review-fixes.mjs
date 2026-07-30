import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), 'utf8');

const [
  studentShell,
  investigation,
  imageZoom,
  recharge,
  staffWorkspace,
  staffLayout,
  staffSettings,
  staffQueue,
  staffStatus,
  staffHandoff,
  staffReplies,
  lessonCarousel,
  featureCarousel,
] = await Promise.all([
  read('src/components/layout/StudentShellChrome.tsx'),
  read('src/components/live-support/admin/ConversationInvestigation.tsx'),
  read('src/components/admin/ImageZoomModal.tsx'),
  read('src/app/admin/recharge-verification/RechargeVerificationPageClient.tsx'),
  read('src/components/live-support/staff/StaffConversationWorkspace.tsx'),
  read('src/components/live-support/staff/StaffConversationLayout.tsx'),
  read('src/components/live-support/staff/StaffChatSettings.tsx'),
  read('src/components/live-support/staff/ConversationQueueList.tsx'),
  read('src/components/live-support/staff/StaffStatusHeader.tsx'),
  read('src/components/live-support/staff/AIHandoffSummary.tsx'),
  read('src/components/live-support/staff/StaffCannedRepliesDialog.tsx'),
  read('src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx'),
  read('src/components/ui/feature-carousel.tsx'),
]);

for (const token of [
  'overflow-y-auto overscroll-contain',
  'group-focus-within/sidebar:block',
  'group-focus-within/sidebar:flex',
  'غير مقروءة',
  'aria-hidden="true"',
]) {
  assert.ok(studentShell.includes(token), `student shell is missing ${token}`);
}

for (const [name, source] of [
  ['conversation investigation', investigation],
  ['image zoom', imageZoom],
]) {
  assert.ok(source.includes('<AccessibleOverlay'), `${name} must use AccessibleOverlay`);
}
assert.ok(!investigation.includes('role="dialog"'), 'investigation must not create a parallel dialog');
assert.ok(!imageZoom.includes('document.addEventListener'), 'image zoom must delegate focus and Escape behavior');

assert.match(recharge, /<button[\s\S]*فتح صورة إثبات معاملة/);
assert.ok(recharge.includes('alt=""'), 'decorative proof thumbnail must use empty alt text');

for (const token of [
  'accessibleColorPair',
  'sm:grid-cols-[auto_auto_minmax(0,1fr)_auto]',
  'col-span-3 sm:col-span-1',
]) {
  assert.ok(staffWorkspace.includes(token), `staff workspace is missing ${token}`);
}
assert.ok(!staffWorkspace.includes('contrastColor'), 'legacy YIQ contrast selection must be removed');

for (const [name, source] of [
  ['staff workspace', staffWorkspace],
  ['staff layout', staffLayout],
  ['staff settings', staffSettings],
  ['staff queue', staffQueue],
  ['staff status', staffStatus],
  ['staff handoff', staffHandoff],
  ['staff canned replies', staffReplies],
]) {
  assert.doesNotMatch(
    source,
    /\b(?:bg-white|text-slate-|bg-slate-|border-slate-|text-cyan-|bg-cyan-|border-cyan-)/,
    `${name} still contains hard-coded light-theme utilities`,
  );
}

for (const forbidden of ['animated-cards', 'bg-gradient-to-b', 'backdrop-blur-md', 'shadow-2xl ring-4']) {
  assert.ok(!lessonCarousel.includes(forbidden), `lesson carousel still contains ${forbidden}`);
}
assert.ok(lessonCarousel.includes('aria-label="فيديوهات الدرس"'));
assert.ok(featureCarousel.includes('aria-label="مراحل العرض"'));

console.log('phase 9 UI review fix contracts passed');
