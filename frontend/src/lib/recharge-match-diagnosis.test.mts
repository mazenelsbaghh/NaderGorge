import assert from 'node:assert/strict';
import test from 'node:test';

import type {
  AdminRechargeRequestDto,
  RechargeMatchDiagnosisCode,
} from '../services/wallet-service.ts';
import { describeRechargeMatchDiagnosis } from './recharge-match-diagnosis.ts';

const request = (code: RechargeMatchDiagnosisCode): AdminRechargeRequestDto => ({
  id: 'request-1',
  userId: 'student-1',
  studentName: 'طالب',
  studentPhoneNumber: '01000000000',
  studentBalance: 0,
  teacherBalance: 0,
  hasPreviousRequest: false,
  walletId: 'wallet-1',
  walletLabel: 'المحفظة الأولى',
  walletPhoneNumber: '01000000001',
  amount: 200,
  teacherId: 'teacher-1',
  teacherName: 'مدرس',
  senderPhoneNumber: '01000000002',
  requiresSenderPhoneConfirmation: false,
  screenshotUrl: '/proof.webp',
  status: 0,
  createdAt: '2026-08-09T19:39:00Z',
  matchDiagnosis: {
    code,
    exactSmsCount: 0,
    competingRequestCount: 0,
    candidate: {
      smsLogId: 'sms-1',
      walletId: 'wallet-2',
      walletLabel: 'المحفظة الثانية',
      amount: 200,
      senderPhoneNumber: '01000000002',
      receivedAt: '2026-08-09T10:44:00Z',
      timeOffsetMinutes: -536,
      outsideWindowByMinutes: 416,
      matchingDigits: 11,
      hasSingleDigitMismatchPattern: false,
      matchingDigitsBeforeMismatch: 0,
      matchingDigitsAfterMismatch: 0,
      amountMatches: true,
      phoneMatches: true,
      withinWindow: false,
      sameWallet: false,
      isMatched: false,
    },
  },
});

test('explains an exact transfer outside the automatic matching window', () => {
  const presentation = describeRechargeMatchDiagnosis(request('OutsideWindow'));

  assert.equal(presentation?.code, 'OutsideWindow');
  assert.equal(presentation?.tone, 'amber');
  assert.match(presentation?.detail ?? '', /8 س و56 د/);
  assert.match(presentation?.detail ?? '', /ساعتان/);
});

test('uses a non-final teal state while an eligible transfer waits for reconciliation', () => {
  const eligible = request('EligibleWaiting');
  eligible.matchDiagnosis!.candidate!.withinWindow = true;
  eligible.matchDiagnosis!.candidate!.timeOffsetMinutes = 5;

  const presentation = describeRechargeMatchDiagnosis(eligible);

  assert.equal(presentation?.tone, 'teal');
  assert.match(presentation?.detail ?? '', /كل 10 دقائق/);
});

test('explains a single-digit phone mismatch using the matching digits on both sides', () => {
  const nearPhone = request('PhoneMismatch');
  nearPhone.matchDiagnosis!.candidate!.hasSingleDigitMismatchPattern = true;
  nearPhone.matchDiagnosis!.candidate!.matchingDigits = 5;
  nearPhone.matchDiagnosis!.candidate!.matchingDigitsBeforeMismatch = 4;
  nearPhone.matchDiagnosis!.candidate!.matchingDigitsAfterMismatch = 5;

  const presentation = describeRechargeMatchDiagnosis(nearPhone);

  assert.match(presentation?.title ?? '', /خطأ محتمل في رقم واحد/);
  assert.match(presentation?.detail ?? '', /4 أرقام صحيحة، ثم رقم مختلف، ثم 5 أرقام صحيحة/);
});

test('does not describe a pending request as processed when diagnosis data is unavailable', () => {
  const pending = request('NoCandidate');
  pending.matchDiagnosis = null;

  const presentation = describeRechargeMatchDiagnosis(pending);

  assert.equal(presentation?.code, 'Unavailable');
  assert.match(presentation?.title ?? '', /التشخيص غير متاح/);
});

test('does not treat a missing candidate as proof that the transfer reached the wallet', () => {
  const presentation = describeRechargeMatchDiagnosis(request('NoCandidate'));

  assert.equal(presentation?.code, 'NoCandidate');
  assert.match(presentation?.title ?? '', /لا توجد رسالة مطابقة حتى الآن/);
  assert.match(presentation?.detail ?? '', /إذا كان هاتف المحفظة متصلًا/);
  assert.match(presentation?.detail ?? '', /لا تثبت وصول التحويل/);
});
