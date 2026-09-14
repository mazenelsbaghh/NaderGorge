'use client';

import { type RefObject, useEffect, useRef } from 'react';

import {
  installManualHistoryScrollRestoration,
  readNavigationState,
  rememberNavigationState,
  restoreNavigationScroll,
  type NavigationSurface,
} from '@/lib/navigation-state';


interface ShellNavigationStateOptions {
  surface: NavigationSurface;
  pathname: string;
  scrollRef: RefObject<HTMLElement | null>;
  sidebarCollapsed?: boolean;
  setSidebarCollapsed?: (collapsed: boolean) => void;
  expandedGroups?: Record<string, boolean>;
  setExpandedGroups?: (groups: Record<string, boolean>) => void;
}

export function useShellNavigationState({
  surface,
  pathname,
  scrollRef,
  sidebarCollapsed,
  setSidebarCollapsed,
  expandedGroups,
  setExpandedGroups,
}: ShellNavigationStateOptions): void {
  const uiState = useRef({ sidebarCollapsed, expandedGroups });

  useEffect(() => {
    uiState.current = { sidebarCollapsed, expandedGroups };
  }, [expandedGroups, sidebarCollapsed]);

  useEffect(() => installManualHistoryScrollRestoration(), []);

  useEffect(() => {
    const element = scrollRef.current;
    if (!element) return;
    const stored = readNavigationState(surface, pathname);
    if (
      stored &&
      typeof stored.sidebarCollapsed === 'boolean' &&
      setSidebarCollapsed
    ) {
      setSidebarCollapsed(stored.sidebarCollapsed);
    }
    if (stored?.expandedGroups && setExpandedGroups) {
      setExpandedGroups(
        Object.fromEntries(stored.expandedGroups.map((group) => [group, true]))
      );
    }
    const cancelRestore = restoreNavigationScroll(element, surface, pathname);

    return () => {
      cancelRestore();
      const current = uiState.current;
      rememberNavigationState(surface, pathname, {
        scrollTop: element.scrollTop,
        sidebarCollapsed: current.sidebarCollapsed,
        expandedGroups: current.expandedGroups
          ? Object.entries(current.expandedGroups)
              .filter(([, expanded]) => expanded)
              .map(([group]) => group)
          : undefined,
      });
    };
  }, [
    pathname,
    scrollRef,
    setExpandedGroups,
    setSidebarCollapsed,
    surface,
  ]);
}
