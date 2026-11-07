import type { ExamQuestionDto } from '@/services/exam-service';
import type { StartHomeworkQuestionDto } from '@/services/homework-service';

type AssessmentQuestion =
  | Pick<ExamQuestionDto, 'type'>
  | Pick<StartHomeworkQuestionDto, 'questionType'>;

export function AssessmentQuestionSummary({
  questions,
}: {
  questions: readonly AssessmentQuestion[];
}) {
  if (questions.length === 0) return null;

  let essayCount = 0;
  let mistakeCount = 0;
  for (const question of questions) {
    const type = 'questionType' in question ? question.questionType : question.type;
    if (type === 'Essay' || type === 1) essayCount++;
    if (type === 'FindTheMistake' || type === 2) mistakeCount++;
  }
  const choiceCount = questions.length - essayCount - mistakeCount;

  return (
    <p className="mt-3 text-sm font-bold leading-6 text-foreground">
      عدد الأسئلة: {questions.length}
      {essayCount > 0 && ` · ${essayCount} مقالي`}
      {choiceCount > 0 && ` · ${choiceCount} اختياري`}
      {mistakeCount > 0 && ` · ${mistakeCount} اكتشف الخطأ`}
    </p>
  );
}
