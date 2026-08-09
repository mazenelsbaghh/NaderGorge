import { Suspense } from 'react';
import AssistantContentPageClient from './AssistantContentPageClient';

export default function AssistantContentPage() {
  return (
    <Suspense fallback={null}>
      <AssistantContentPageClient />
    </Suspense>
  );
}
