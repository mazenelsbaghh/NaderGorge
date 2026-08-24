'use client';

import dynamic from 'next/dynamic';
import { useEffect, useState } from 'react';

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

export function DeferredStudentOverlays() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const timerId = globalThis.setTimeout(() => setReady(true), 250);
    return () => globalThis.clearTimeout(timerId);
  }, []);

  if (!ready) return null;
  return (
    <>
      <StudentBirthdayCelebration />
      <PlatformPopup />
    </>
  );
}
