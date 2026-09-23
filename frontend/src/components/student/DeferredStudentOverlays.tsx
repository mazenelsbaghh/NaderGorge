'use client';

import dynamic from 'next/dynamic';
import { useCallback, useEffect, useState } from 'react';

const PlatformPopup = dynamic(
  () => import('@/components/platform/PlatformPopup').then((module) => module.PlatformPopup),
  { ssr: false },
);
const StudentBirthdayCelebration = dynamic(
  () =>
    import('./StudentBirthdayCelebration').then(
      (module) => module.StudentBirthdayCelebration,
    ),
  { ssr: false },
);
const ParentCodePopup = dynamic(
  () => import('./ParentCodePopup').then((module) => module.ParentCodePopup),
  { ssr: false },
);

const StudentWelcome = dynamic(
  () => import('./welcome/StudentWelcome').then(module => module.StudentWelcome),
  { ssr: false },
);

export function DeferredStudentOverlays() {
  const [welcomeSettled, setWelcomeSettled] = useState(false);
  const settleWelcome = useCallback(() => setWelcomeSettled(true), []);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const timerId = globalThis.setTimeout(() => setReady(true), 250);
    return () => globalThis.clearTimeout(timerId);
  }, []);

  if (!ready) return null;
  return (
    <>
      <StudentWelcome onSettled={settleWelcome} />
      {welcomeSettled && (
        <>
          <StudentBirthdayCelebration />
          <PlatformPopup />
          <ParentCodePopup />
        </>
      )}
    </>
  );
}
