export type NavigationSurface =
  | 'student'
  | 'admin'
  | 'teacher'
  | 'assistant';

export interface NavigationState {
  scrollTop: number;
  sidebarCollapsed?: boolean;
  expandedGroups?: readonly string[];
}

interface StoredNavigationState extends NavigationState {
  updatedAt: number;
}

interface NavigationStateEnvelope {
  version: 1;
  entries: Record<string, StoredNavigationState>;
}

const STORAGE_KEY = 'massar:navigation-state:v1';
const DEFAULT_ENTRY_LIMIT = 24;
const memoryState: NavigationStateEnvelope = { version: 1, entries: {} };

function safePathname(pathname: string): string | null {
  if (
    !pathname.startsWith('/') ||
    pathname.startsWith('//') ||
    pathname.includes('\\') ||
    /[\u0000-\u001f\u007f]/.test(pathname)
  ) {
    return null;
  }
  return pathname.split(/[?#]/, 1)[0] || '/';
}

function stateKey(surface: NavigationSurface, pathname: string): string | null {
  const safePath = safePathname(pathname);
  return safePath ? `${surface}:${safePath}` : null;
}

function sanitizeState(state: NavigationState): NavigationState {
  return {
    scrollTop: Math.max(0, Math.round(Number.isFinite(state.scrollTop) ? state.scrollTop : 0)),
    sidebarCollapsed:
      typeof state.sidebarCollapsed === 'boolean'
        ? state.sidebarCollapsed
        : undefined,
    expandedGroups: state.expandedGroups
      ?.filter((value) => /^[A-Za-z0-9:_-]{1,80}$/.test(value))
      .slice(0, 20),
  };
}

function readEnvelope(): NavigationStateEnvelope {
  if (typeof window === 'undefined') return memoryState;
  try {
    const parsed = JSON.parse(
      window.sessionStorage.getItem(STORAGE_KEY) || ''
    ) as NavigationStateEnvelope;
    if (
      parsed?.version === 1 &&
      parsed.entries &&
      typeof parsed.entries === 'object'
    ) {
      return parsed;
    }
  } catch {
    // Corrupt or unavailable session storage falls back to bounded memory.
  }
  return { version: 1, entries: { ...memoryState.entries } };
}

function writeEnvelope(envelope: NavigationStateEnvelope): void {
  memoryState.entries = { ...envelope.entries };
  if (typeof window === 'undefined') return;
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(envelope));
  } catch {
    // Navigation restoration is an enhancement; memory remains authoritative.
  }
}

export function rememberNavigationState(
  surface: NavigationSurface,
  pathname: string,
  state: NavigationState,
  entryLimit = DEFAULT_ENTRY_LIMIT
): void {
  const key = stateKey(surface, pathname);
  if (!key) return;
  const envelope = readEnvelope();
  envelope.entries[key] = {
    ...sanitizeState(state),
    updatedAt: Date.now(),
  };

  const retained = Object.entries(envelope.entries)
    .sort((left, right) => right[1].updatedAt - left[1].updatedAt)
    .slice(0, Math.max(1, entryLimit));
  envelope.entries = Object.fromEntries(retained);
  writeEnvelope(envelope);
}

export function readNavigationState(
  surface: NavigationSurface,
  pathname: string
): NavigationState | null {
  const key = stateKey(surface, pathname);
  if (!key) return null;
  const storedState = readEnvelope().entries[key];
  if (!storedState) return null;
  return sanitizeState(storedState);
}

export function restoreNavigationScroll(
  element: HTMLElement,
  surface: NavigationSurface,
  pathname: string
): () => void {
  const state = readNavigationState(surface, pathname);
  if (!state) return () => undefined;
  const frame = window.requestAnimationFrame(() => {
    element.scrollTo({ top: state.scrollTop, behavior: 'auto' });
  });
  return () => window.cancelAnimationFrame(frame);
}

export function installManualHistoryScrollRestoration(): () => void {
  if (typeof window === 'undefined' || !('scrollRestoration' in window.history)) {
    return () => undefined;
  }
  const previous = window.history.scrollRestoration;
  window.history.scrollRestoration = 'manual';
  return () => {
    window.history.scrollRestoration = previous;
  };
}

export function clearNavigationState(surface?: NavigationSurface): void {
  const envelope = readEnvelope();
  envelope.entries = surface
    ? Object.fromEntries(
        Object.entries(envelope.entries).filter(
          ([key]) => !key.startsWith(`${surface}:`)
        )
      )
    : {};
  writeEnvelope(envelope);
}
