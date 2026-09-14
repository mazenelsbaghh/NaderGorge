'use client';

import { AssessmentDefinitionEditor } from '@/components/admin/AssessmentDefinitionEditor';

export default function AddExamQuestionPageClient({ params, surface, initialQuestionId }: {
  params: { id: string }; surface?: 'admin' | 'teacher'; initialQuestionId?: string;
}) {
  return <AssessmentDefinitionEditor id={params.id} kind="exam" surface={surface} initialQuestionId={initialQuestionId} />;
}
