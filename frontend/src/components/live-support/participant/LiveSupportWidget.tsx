'use client';
import type { ReactNode } from 'react';

export function LiveSupportWidget({ children, title = 'الدعم المباشر' }: { children: ReactNode; title?: string }) {
  return <section aria-labelledby="live-support-title" className="flex min-h-0 flex-1 flex-col"><h2 id="live-support-title" className="sr-only">{title}</h2>{children}</section>;
}
