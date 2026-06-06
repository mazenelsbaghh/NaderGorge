type PersistedStorageType = 'local' | 'session';

type PersistedAuthPayload = {
  accessToken: string;
  user: unknown;
};

const AUTH_KEYS = {
  accessToken: 'accessToken',
  refreshToken: 'refreshToken',
  user: 'user',
} as const;

function getStorage(type: PersistedStorageType): Storage | null {
  if (typeof window === 'undefined') return null;
  return type === 'local' ? window.localStorage : window.sessionStorage;
}

function clearStorage(storage: Storage | null) {
  if (!storage) return;
  storage.removeItem(AUTH_KEYS.accessToken);
  storage.removeItem(AUTH_KEYS.refreshToken);
  storage.removeItem(AUTH_KEYS.user);
}

function hasAnyAuthKey(storage: Storage | null) {
  if (!storage) return false;
  return [AUTH_KEYS.accessToken, AUTH_KEYS.refreshToken, AUTH_KEYS.user].some((key) =>
    storage.getItem(key) !== null
  );
}

function readRawPayload(storage: Storage | null) {
  if (!storage) return null;

  const accessToken = storage.getItem(AUTH_KEYS.accessToken);
  const user = storage.getItem(AUTH_KEYS.user);

  if (!accessToken || !user) {
    // Self-heal partial auth state so stale access tokens do not keep triggering refresh attempts.
    if (hasAnyAuthKey(storage)) {
      clearStorage(storage);
    }
    return null;
  }

  storage.removeItem(AUTH_KEYS.refreshToken);
  return { accessToken, user };
}

function getPreferredStorage(): Storage | null {
  return readRawPayload(getStorage('local'))
    ? getStorage('local')
    : readRawPayload(getStorage('session'))
      ? getStorage('session')
      : null;
}

export function persistAuthSession(
  payload: PersistedAuthPayload,
  rememberMe: boolean
) {
  const storage = getStorage(rememberMe ? 'local' : 'session');
  clearStoredAuth();

  if (!storage) return;

  storage.setItem(AUTH_KEYS.accessToken, payload.accessToken);
  storage.setItem(AUTH_KEYS.user, JSON.stringify(payload.user));
}

export function clearStoredAuth() {
  clearStorage(getStorage('local'));
  clearStorage(getStorage('session'));
}

export function readStoredAuth(): (PersistedAuthPayload & { storage: PersistedStorageType }) | null {
  const localStoragePayload = readRawPayload(getStorage('local'));
  if (localStoragePayload) {
    try {
      return {
        ...localStoragePayload,
        user: JSON.parse(localStoragePayload.user),
        storage: 'local',
      };
    } catch {
      clearStorage(getStorage('local'));
    }
  }

  const sessionStoragePayload = readRawPayload(getStorage('session'));
  if (sessionStoragePayload) {
    try {
      return {
        ...sessionStoragePayload,
        user: JSON.parse(sessionStoragePayload.user),
        storage: 'session',
      };
    } catch {
      clearStorage(getStorage('session'));
    }
  }

  return null;
}

export function getStoredAccessToken() {
  return readStoredAuth()?.accessToken ?? null;
}

export function replaceStoredTokens(accessToken: string) {
  const storage = getPreferredStorage();
  if (!storage) return;

  storage.setItem(AUTH_KEYS.accessToken, accessToken);
  storage.removeItem(AUTH_KEYS.refreshToken);
}

export function updateStoredUser(user: unknown) {
  const storage = getPreferredStorage();
  if (!storage) return;
  storage.setItem(AUTH_KEYS.user, JSON.stringify(user));
}
