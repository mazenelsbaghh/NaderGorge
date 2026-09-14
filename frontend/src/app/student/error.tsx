'use client';

import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function StudentError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return <AsyncRegionState status="error" onRetry={reset} homeHref="/student" />;
}
