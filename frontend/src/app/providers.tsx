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
        <Toaster
          position="top-center"
          containerStyle={{
            insetInline: '0.75rem',
            top: 'max(0.75rem, env(safe-area-inset-top))',
          }}
          toastOptions={{
            duration: 4000,
            style: {
              maxWidth: 'min(28rem, calc(100vw - 1.5rem))',
            },
          }}
        />
      </MotionConfig>
    </QueryProvider>
  );
}
