import dotenv from 'dotenv';
import { evaluateEssayWithAI } from '../services/geminiService.js';

dotenv.config({ quiet: true });

// Synthetic variants of the November 2026 consistency incident; no student records or saved marks are touched.
const question = 'ما النتائج المترتبة على التجانس الثقافي للشعب المصري؟';
const reference = 'أصبح الشعب كتلة واحدة متماسكة، وحافظ على هويته، ورفض ثقافة المحتل.';
const variants: Array<[string, string, number]> = [
  ['partial-identity-first', 'حافظ الشعب على هويته، ورفض ثقافة المحتل.', 0.67],
  ['partial-reordered', 'رفض الشعب ثقافة المحتل واستمر متمسكًا بهويته وتراثه عبر العصور.', 0.67],
  ['complete', 'أصبح الشعب كتلة واحدة متماسكة، وحافظ على هويته ورفض ثقافة المحتل.', 1],
  ['equivalent-wording', 'اتحد أبناء الشعب وتماسكوا، وصانوا هويتهم المصرية ولم يذوبوا في ثقافة المحتل.', 1],
  ['contradiction', 'تفكك المجتمع المصري وضاعت هويته وتبنى ثقافة المحتل.', 0],
];
const results = [];
for (const [id, answer, expected] of variants) {
  try {
    const grade = await evaluateEssayWithAI(answer, reference, question);
    results.push({ id, expected, actual: grade.score, rubric: grade.rubricFingerprint, passed: grade.score === expected });
  } catch (error) { results.push({ id, passed: false, error: error instanceof Error ? error.name : 'UnknownError' }); }
}
const fingerprints = new Set(results.flatMap(result => 'rubric' in result ? [result.rubric] : []));
const passed = results.every(result => result.passed) && fingerprints.size === 1;
console.log(JSON.stringify({ usesMocks: false, mutatesStudentData: false, sharedRubric: fingerprints.size === 1, passed, results }, null, 2));
process.exitCode = passed ? 0 : 1;
