'use client';

import { useCallback, useEffect, useRef, useState, type ComponentPropsWithoutRef } from 'react';
import { Moon, Sun } from 'lucide-react';
import { flushSync } from 'react-dom';

import { cn } from '@/lib/utils';

type ViewTransitionDocument = Document & {
  startViewTransition?: (callback: () => void | Promise<void>) => {
    ready?: Promise<void>;
  };
};

type AnimatedThemeTogglerProps = Omit<ComponentPropsWithoutRef<'button'>, 'onToggle'> & {
  checked?: boolean;
  onToggle?: () => void;
  duration?: number;
};

export function AnimatedThemeToggler({
  checked,
  onToggle,
  className,
  duration = 400,
  ...props
}: AnimatedThemeTogglerProps) {
  const [internalIsDark, setInternalIsDark] = useState(false);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const isControlled = typeof checked === 'boolean' && typeof onToggle === 'function';
  const isDark = isControlled ? checked : internalIsDark;

  useEffect(() => {
    if (isControlled || typeof document === 'undefined') return;

    const updateTheme = () => {
      setInternalIsDark(document.documentElement.classList.contains('dark'));
    };

    updateTheme();

    const observer = new MutationObserver(updateTheme);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => observer.disconnect();
  }, [isControlled]);

  const toggleTheme = useCallback(() => {
    const doc = document as ViewTransitionDocument;
    const button = buttonRef.current;
    if (!button) return;

    const { top, left, width, height } = button.getBoundingClientRect();
    const x = left + width / 2;
    const y = top + height / 2;
    const viewportWidth = window.visualViewport?.width ?? window.innerWidth;
    const viewportHeight = window.visualViewport?.height ?? window.innerHeight;
    const maxRadius = Math.hypot(
      Math.max(x, viewportWidth - x),
      Math.max(y, viewportHeight - y),
    );

    const applyTheme = () => {
      if (isControlled && onToggle) {
        onToggle();
        return;
      }

      const newTheme = !internalIsDark;
      setInternalIsDark(newTheme);
      doc.documentElement.classList.toggle('dark', newTheme);
      localStorage.setItem('theme', newTheme ? 'dark' : 'light');
    };

    if (typeof doc.startViewTransition !== 'function') {
      applyTheme();
      return;
    }

    const transition = doc.startViewTransition(() => {
      flushSync(applyTheme);
    });

    const ready = transition?.ready;
    if (ready && typeof ready.then === 'function') {
      ready.then(() => {
        doc.documentElement.animate(
          {
            clipPath: [
              `circle(0px at ${x}px ${y}px)`,
              `circle(${maxRadius}px at ${x}px ${y}px)`,
            ],
          },
          {
            duration,
            easing: 'ease-in-out',
            pseudoElement: '::view-transition-new(root)',
          },
        );
      });
    }
  }, [duration, internalIsDark, isControlled, onToggle]);

  return (
    <button
      type="button"
      ref={buttonRef}
      onClick={toggleTheme}
      className={cn(className)}
      {...props}
    >
      {isDark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
      <span className="sr-only">Toggle theme</span>
    </button>
  );
}
