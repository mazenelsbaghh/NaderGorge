'use client';

import dynamic from 'next/dynamic';
import { useEffect, useState } from 'react';

type DeferredLiveSupportLauncherProps = {
  avoidMobileBottomNav?: boolean;
};

const LiveSupportLauncher = dynamic(
  () => import('./LiveSupportLauncher').then((module) => module.LiveSupportLauncher),
  { ssr: false },
);

export function DeferredLiveSupportLauncher(props: DeferredLiveSupportLauncherProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const timerId = globalThis.setTimeout(() => setReady(true), 250);
    return () => globalThis.clearTimeout(timerId);
  }, []);

  return ready ? <LiveSupportLauncher {...props} /> : null;
}
