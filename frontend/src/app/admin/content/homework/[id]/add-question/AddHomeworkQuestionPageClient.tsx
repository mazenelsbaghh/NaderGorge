'use client';

import { AssessmentDefinitionEditor } from '@/components/admin/AssessmentDefinitionEditor';

export default function AddHomeworkQuestionPageClient({ params, surface, initialQuestionId }: {
  params: { id: string }; surface?: 'admin' | 'teacher'; initialQuestionId?: string;
}) {
  return <AssessmentDefinitionEditor id={params.id} kind="homework" surface={surface} initialQuestionId={initialQuestionId} />;
}
