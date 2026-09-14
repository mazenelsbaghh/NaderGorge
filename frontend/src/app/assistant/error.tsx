'use client';

import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function AssistantError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return <AsyncRegionState status="error" onRetry={reset} homeHref="/assistant/dashboard" />;
}
