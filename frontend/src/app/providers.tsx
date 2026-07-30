'use client';

import { MotionConfig } from 'framer-motion';
import { Toaster } from 'react-hot-toast';

import { AuthBootstrap } from '@/components/layout/AuthBootstrap';
import { QueryProvider } from '@/components/providers/QueryProvider';
import { WebVitalsReporter } from '@/components/providers/WebVitalsReporter';

export function AppProviders({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <MotionConfig reducedMotion="user">
        <AuthBootstrap />
        <WebVitalsReporter />
        {children}
        <Toaster position="bottom-left" />
      </MotionConfig>
    </QueryProvider>
  );
}
