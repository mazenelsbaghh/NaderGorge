'use client';

import { CSSProperties, useEffect } from 'react';

export function useRootOverscrollBackground(themeVars: CSSProperties) {
  useEffect(() => {
    if (typeof document === 'undefined') return;

    const html = document.documentElement;
    const body = document.body;
    const themeMap = themeVars as Record<string, string | number | undefined>;
    const backgroundColor = String(themeMap['--admin-bg'] ?? '');
    const backgroundOverlay = String(themeMap['--admin-bg-overlay'] ?? '');
    const backgroundDot = String(themeMap['--admin-dot'] ?? '');
    const backgroundImage =
      backgroundDot && backgroundOverlay
        ? `radial-gradient(circle at 2px 2px, ${backgroundDot} 1px, transparent 0), linear-gradient(${backgroundOverlay}, ${backgroundOverlay})`
        : '';
    const backgroundSize = backgroundImage ? '40px 40px, 100% 100%' : '';

    const previousHtmlBackground = html.style.backgroundColor;
    const previousHtmlBackgroundImage = html.style.backgroundImage;
    const previousHtmlBackgroundSize = html.style.backgroundSize;
    const previousHtmlBackgroundRepeat = html.style.backgroundRepeat;
    const previousHtmlOverflow = html.style.overflow;
    const previousHtmlOverscrollBehavior = html.style.overscrollBehavior;
    const previousBodyBackground = body.style.backgroundColor;
    const previousBodyBackgroundImage = body.style.backgroundImage;
    const previousBodyBackgroundSize = body.style.backgroundSize;
    const previousBodyBackgroundRepeat = body.style.backgroundRepeat;
    const previousBodyOverflow = body.style.overflow;
    const previousBodyOverscrollBehavior = body.style.overscrollBehavior;

    html.style.backgroundColor = backgroundColor;
    html.style.backgroundImage = backgroundImage;
    html.style.backgroundSize = backgroundSize;
    html.style.backgroundRepeat = 'repeat, no-repeat';
    html.style.overflow = 'hidden';
    html.style.overscrollBehavior = 'none';
    body.style.backgroundColor = backgroundColor;
    body.style.backgroundImage = backgroundImage;
    body.style.backgroundSize = backgroundSize;
    body.style.backgroundRepeat = 'repeat, no-repeat';
    body.style.overflow = 'hidden';
    body.style.overscrollBehavior = 'none';

    return () => {
      html.style.backgroundColor = previousHtmlBackground;
      html.style.backgroundImage = previousHtmlBackgroundImage;
      html.style.backgroundSize = previousHtmlBackgroundSize;
      html.style.backgroundRepeat = previousHtmlBackgroundRepeat;
      html.style.overflow = previousHtmlOverflow;
      html.style.overscrollBehavior = previousHtmlOverscrollBehavior;
      body.style.backgroundColor = previousBodyBackground;
      body.style.backgroundImage = previousBodyBackgroundImage;
      body.style.backgroundSize = previousBodyBackgroundSize;
      body.style.backgroundRepeat = previousBodyBackgroundRepeat;
      body.style.overflow = previousBodyOverflow;
      body.style.overscrollBehavior = previousBodyOverscrollBehavior;
    };
  }, [themeVars]);
}
