'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  type ComponentProps,
  type FocusEvent,
  type MouseEvent,
  type TouchEvent,
  useCallback,
  useRef,
} from 'react';


type EffectiveConnectionType = 'slow-2g' | '2g' | '3g' | '4g' | string;
const EXPENSIVE_ROUTE_PREFIXES = [
  '/admin/ai-monitor',
  '/admin/finance',
  '/admin/hr',
  '/admin/live-support',
  '/admin/reports',
  '/assistant/live-support',
  '/assistant/reports',
  '/student/public-exams',
  '/teacher/finance',
  '/teacher/reports',
] as const;

export function isExpensivePrefetchRoute(href: string): boolean {
  return EXPENSIVE_ROUTE_PREFIXES.some(
    (prefix) => href === prefix || href.startsWith(`${prefix}/`)
  );
}

export interface PrefetchEligibility {
  href: string;
  currentOrigin: string;
  canPrefetch: boolean;
  expensive: boolean;
  saveData: boolean;
  effectiveType?: EffectiveConnectionType;
}

export function shouldPrefetchDestination({
  href,
  currentOrigin,
  canPrefetch,
  expensive,
  saveData,
  effectiveType,
}: PrefetchEligibility): boolean {
  if (
    !canPrefetch ||
    expensive ||
    saveData ||
    effectiveType === 'slow-2g' ||
    effectiveType === '2g' ||
    href.startsWith('#') ||
    href.startsWith('//')
  ) {
    return false;
  }
  try {
    const destination = new URL(href, currentOrigin);
    return (
      destination.origin === currentOrigin &&
      destination.protocol === new URL(currentOrigin).protocol &&
      destination.pathname.startsWith('/')
    );
  } catch {
    return false;
  }
}

type BaseLinkProps = Omit<
  ComponentProps<typeof Link>,
  'href' | 'prefetch' | 'onMouseEnter' | 'onFocus' | 'onTouchStart'
>;

export interface IntentLinkProps extends BaseLinkProps {
  href: string;
  canPrefetch?: boolean;
  expensive?: boolean;
  intentPrefetch?: boolean;
  onMouseEnter?: (event: MouseEvent<HTMLAnchorElement>) => void;
  onFocus?: (event: FocusEvent<HTMLAnchorElement>) => void;
  onTouchStart?: (event: TouchEvent<HTMLAnchorElement>) => void;
}

interface NetworkInformation {
  saveData?: boolean;
  effectiveType?: EffectiveConnectionType;
}

function networkInformation(): NetworkInformation {
  if (typeof navigator === 'undefined') return {};
  return (
    navigator as Navigator & {
      connection?: NetworkInformation;
    }
  ).connection ?? {};
}

export function IntentLink({
  href,
  canPrefetch = true,
  expensive = isExpensivePrefetchRoute(href),
  intentPrefetch = true,
  onMouseEnter,
  onFocus,
  onTouchStart,
  ...props
}: IntentLinkProps) {
  const router = useRouter();
  const prepared = useRef(false);

  const prepare = useCallback(() => {
    if (prepared.current || !intentPrefetch || typeof window === 'undefined') {
      return;
    }
    const connection = networkInformation();
    if (
      !shouldPrefetchDestination({
        href,
        currentOrigin: window.location.origin,
        canPrefetch,
        expensive,
        saveData: connection.saveData === true,
        effectiveType: connection.effectiveType,
      })
    ) {
      return;
    }
    prepared.current = true;
    router.prefetch(href);
  }, [canPrefetch, expensive, href, intentPrefetch, router]);

  return (
    <Link
      {...props}
      href={href}
      prefetch={false}
      onMouseEnter={(event) => {
        onMouseEnter?.(event);
        prepare();
      }}
      onFocus={(event) => {
        onFocus?.(event);
        prepare();
      }}
      onTouchStart={(event) => {
        onTouchStart?.(event);
        prepare();
      }}
    />
  );
}
