import type { InlineExamQuestionDto } from './QuestionEditor';
import type { RevisionQuestion } from '@/services/assessment-revision-service';
import { createClientId } from '@/lib/client-id';

const questionTypes = ['MCQ', 'Essay', 'FindTheMistake'] as const;
export function toQuestionEditor(question: RevisionQuestion): InlineExamQuestionDto {
  return { ...question, type: questionTypes[question.type], audioUrl: question.audioUrl ?? undefined,
    imageUrl: question.imageUrl ?? undefined, writtenCorrection: question.writtenCorrection ?? undefined,
    hintText: question.hintText ?? undefined, baseText: question.baseText ?? undefined };
}
export function fromQuestionEditor(previous: RevisionQuestion, edited: InlineExamQuestionDto): RevisionQuestion {
  const type = questionTypes.indexOf(edited.type);
  const replaced = type !== previous.type;
  return { ...previous, id: replaced ? createClientId() : previous.id,
    bankQuestionId: replaced ? createClientId() : previous.bankQuestionId,
    text: edited.text, type, points: edited.points, order: edited.order,
    audioUrl: edited.audioUrl ?? null, imageUrl: edited.imageUrl ?? null,
    writtenCorrection: edited.writtenCorrection ?? null, hintText: edited.hintText ?? null,
    baseText: edited.baseText ?? null, mistakeStartIndex: edited.mistakeStartIndex ?? null,
    mistakeEndIndex: edited.mistakeEndIndex ?? null,
    options: type === 0 ? edited.options.map(option => ({ ...option,
      id: replaced || !option.id ? createClientId() : option.id })) : [],
    correctAnswerKey: type === 0 ? edited.options.find(option => option.isCorrect)?.text ?? null : previous.correctAnswerKey };
}
export function newRevisionQuestion(order: number): RevisionQuestion {
  return { id: createClientId(), bankQuestionId: createClientId(), order, type: 1, text: '', points: 1,
    audioUrl: null, imageUrl: null, writtenCorrection: null, hintText: null, baseText: null,
    mistakeStartIndex: null, mistakeEndIndex: null, options: [], correctAnswerKey: null };
}
