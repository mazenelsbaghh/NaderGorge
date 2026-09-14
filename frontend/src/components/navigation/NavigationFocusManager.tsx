'use client';

import { useEffect, useRef } from 'react';
import { usePathname } from 'next/navigation';


export function SkipToContentLink({
  targetId = 'main-content',
}: {
  targetId?: string;
}) {
  return (
    <a
      href={`#${targetId}`}
      className="fixed start-3 top-3 z-[var(--z-modal)] -translate-y-24 rounded-md bg-[var(--admin-card)] px-4 py-2 font-bold text-[var(--admin-text)] shadow-lg transition-transform focus:translate-y-0"
    >
      تخطَّ إلى المحتوى الرئيسي
    </a>
  );
}

export function NavigationFocusManager({
  mainId = 'main-content',
  focusOnHistoryNavigation = false,
}: {
  mainId?: string;
  focusOnHistoryNavigation?: boolean;
}) {
  const pathname = usePathname();
  const initialPath = useRef(pathname);
  const historyNavigation = useRef(false);

  useEffect(() => {
    const markHistoryNavigation = () => {
      historyNavigation.current = true;
    };
    window.addEventListener('popstate', markHistoryNavigation);
    return () => window.removeEventListener('popstate', markHistoryNavigation);
  }, []);

  useEffect(() => {
    if (pathname === initialPath.current) return;
    initialPath.current = pathname;
    const shouldSkip = historyNavigation.current && !focusOnHistoryNavigation;
    historyNavigation.current = false;
    if (shouldSkip) return;

    const frame = window.requestAnimationFrame(() => {
      const main = document.getElementById(mainId);
      const target =
        main?.querySelector<HTMLElement>('[data-navigation-heading], h1') ??
        main;
      if (!target) return;
      if (!target.hasAttribute('tabindex')) {
        target.setAttribute('tabindex', '-1');
        target.addEventListener(
          'blur',
          () => target.removeAttribute('tabindex'),
          { once: true }
        );
      }
      target.focus({ preventScroll: true });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [focusOnHistoryNavigation, mainId, pathname]);

  return null;
}
