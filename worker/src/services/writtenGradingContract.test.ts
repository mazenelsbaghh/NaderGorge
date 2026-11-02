import test from 'node:test';
import assert from 'node:assert/strict';
import { parseRubric, parseWrittenGrade } from './writtenGradingContract.js';

const rubric = parseRubric(JSON.stringify({ criteria: [
  { meaning: 'تماسك المجتمع', weight: 1 }, { meaning: 'الحفاظ على الهوية', weight: 1 }, { meaning: 'رفض ثقافة المحتل', weight: 1 },
] }), 'synthetic-fingerprint');
const answer = 'حافظ المجتمع على هويته، ورفض ثقافة المحتل.';
const judgments = [
  { criterionId: 'c1', met: false, evidence: '' },
  { criterionId: 'c2', met: true, evidence: 'حافظ المجتمع على هويته' },
  { criterionId: 'c3', met: true, evidence: 'رفض ثقافة المحتل' },
];

test('2026-11-01 equivalent two-of-three judgments cannot drift across the 70 percent full-credit threshold', () => {
  for (const inventedScore of [0.67, 0.7, 0.75, 1]) {
    const grade = parseWrittenGrade(JSON.stringify({ score: inventedScore, judgments, feedback: 'ذكرت الهوية ورفض ثقافة المحتل، وينقص توضيح التماسك.' }), rubric, answer);
    assert.equal(grade.score, 0.67);
    assert.equal(grade.isCorrect, false);
    assert.match(grade.feedback, /67٪/);
    assert.equal(grade.rubricFingerprint, rubric.fingerprint);
  }
});

test('fixed explicit weights and reversed judgment order produce the same calculated grade', () => {
  const weighted = parseRubric(JSON.stringify({ criteria: rubric.criteria.map((criterion, index) => ({ ...criterion, weight: index === 0 ? 2 : 1 })) }), 'weighted');
  assert.equal(parseWrittenGrade(JSON.stringify({ judgments: [...judgments].reverse(), feedback: 'ناقص معنى التماسك.' }), weighted, answer).score, 0.5);
});

for (const [name, invalid] of [
  ['omitted criterion', judgments.slice(1)],
  ['duplicate criterion', [judgments[0], judgments[1], judgments[1]]],
  ['unknown criterion', [{ ...judgments[0], criterionId: 'c4' }, ...judgments.slice(1)]],
  ['invented evidence', [judgments[0], { ...judgments[1], evidence: 'نص غير موجود عند الطالب' }, judgments[2]]],
  ['missing evidence', [judgments[0], { ...judgments[1], evidence: '' }, judgments[2]]],
  ['nonboolean judgment', [judgments[0], { ...judgments[1], met: 'true' }, judgments[2]]],
] as const) test(`${name} cannot award a grade`, () => {
  assert.throws(() => parseWrittenGrade(JSON.stringify({ judgments: invalid, feedback: 'تقييم' }), rubric, answer), /invalid result/);
});

for (const [weights, met, expected] of [
  [[1, 1000], [true, false], 0.01], [[1000, 1], [true, false], 0.99],
  [[1, 1], [true, true], 1], [[1, 1], [false, false], 0],
] as const) test(`partial-credit precision preserves ${expected} for fixed weights ${weights}`, () => {
  const weighted = parseRubric(JSON.stringify({ criteria: weights.map((weight, index) => ({ meaning: `meaning ${index}`, weight })) }), 'precision');
  const grade = parseWrittenGrade(JSON.stringify({ judgments: met.map((matched, index) => ({ criterionId: `c${index + 1}`, met: matched, evidence: matched ? 'answer' : '' })), feedback: 'تقييم' }), weighted, 'answer');
  assert.equal(grade.score, expected);
  assert.equal(grade.isCorrect, expected === 1);
});
