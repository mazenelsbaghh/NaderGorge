import { studentActionFields } from './student-action-definitions';

export const expectedLiveSupportActionKeys = ['student.profile.update','student.password.reset','student.account.status.set','student.note.add','student.note.delete','student.device.disconnect','student.devices.disconnect-all','student.package.cancel','student.balance.adjust','student.gamification.adjust','student.video.override.add','student.watch.reset','student.watch.count.set','student.watch-request.approve','student.watch-request.reject','student.lesson.unlock','student.crm.assign','student.crm.call.add','student.create-and-link'] as const;

export function assertLiveSupportActionCatalogParity() {
  const actual = Object.keys(studentActionFields).sort();
  const expected = [...expectedLiveSupportActionKeys].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`Live-support action catalog mismatch: ${actual.join(',')}`);
}

assertLiveSupportActionCatalogParity();
