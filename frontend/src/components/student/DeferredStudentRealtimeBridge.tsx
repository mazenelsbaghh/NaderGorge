'use client';

import dynamic from 'next/dynamic';
import { useEffect, useState } from 'react';

const StudentRealtimeBridge = dynamic(
  () => import('./StudentRealtimeBridge').then((module) => module.StudentRealtimeBridge),
  { ssr: false },
);

export function DeferredStudentRealtimeBridge() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const idleWindow = window as unknown as {
      requestIdleCallback?: (callback: IdleRequestCallback, options?: IdleRequestOptions) => number;
      cancelIdleCallback?: (id: number) => void;
    };
    if (idleWindow.requestIdleCallback) {
      const idleCallbackId = idleWindow.requestIdleCallback(() => setReady(true), { timeout: 1_000 });
      return () => idleWindow.cancelIdleCallback?.(idleCallbackId);
    }

    const timerId = globalThis.setTimeout(() => setReady(true), 250);
    return () => globalThis.clearTimeout(timerId);
  }, []);

  return ready ? <StudentRealtimeBridge /> : null;
}
