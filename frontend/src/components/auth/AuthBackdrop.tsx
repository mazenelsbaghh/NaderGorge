'use client';

import dynamic from 'next/dynamic';

import { useConstrainedMotion } from '@/hooks/useConstrainedMotion';

const RippleGrid = dynamic(
  () =>
    import('@/components/ui/ripple-grid').then((module) => ({
      default: module.RippleGrid,
    })),
  { ssr: false },
);

export function AuthBackdrop({ isDark }: { isDark: boolean }) {
  const { allowEnhancedMotion, isPageVisible } = useConstrainedMotion();
  const gridColor = isDark ? '#36d6d6' : '#0e8f8f'; // design-token-allow: WebGL uniform requires a concrete color value.

  return (
    <>
      <div className="auth-shell__static-grid pointer-events-none absolute inset-0 z-0">
        {allowEnhancedMotion ? (
          <RippleGrid
            active={isPageVisible}
            gridColor={gridColor}
            rippleIntensity={0.05}
            gridSize={10}
            gridThickness={isDark ? 15 : 12}
            mouseInteraction
            mouseInteractionRadius={1.2}
            opacity={isDark ? 0.45 : 0.25}
          />
        ) : null}
      </div>

      <div className="auth-shell__glow pointer-events-none">
        <div className="auth-shell__glow-top" />
        <div className="auth-shell__glow-bottom" />
      </div>
    </>
  );
}
