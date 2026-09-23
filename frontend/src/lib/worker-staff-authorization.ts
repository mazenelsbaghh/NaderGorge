const PRIVILEGED_ROLES = new Set(['Admin', 'Teacher']);

type CurrentSessionResponse = {
  data?: { user?: { roles?: string[]; permissions?: string[] } };
};

export function staffSessionApiUrl(
  nodeEnv: string | undefined,
  publicApiUrl: string | undefined,
  internalApiUrl: string | undefined,
): string | undefined {
  const apiUrl = nodeEnv === 'production'
    ? publicApiUrl
    : internalApiUrl || publicApiUrl || 'http://backend:5245/api';
  if (!apiUrl || (nodeEnv === 'production' && !apiUrl.startsWith('https://'))) return undefined;
  return apiUrl.replace(/\/$/, '');
}

export async function validateStaffAuthorization(
  authorization: string | null,
  apiUrl: string | undefined,
  fetchSession: typeof fetch,
) {
  if (!authorization?.startsWith('Bearer ')) {
    return { ok: false as const, status: 401, error: 'Authentication required' };
  }
  if (!apiUrl) {
    return { ok: false as const, status: 503, error: 'Authentication service unavailable' };
  }

  try {
    const response = await fetchSession(`${apiUrl}/auth/session`, {
      headers: { Authorization: authorization },
      cache: 'no-store',
      redirect: 'error',
      signal: AbortSignal.timeout(5_000),
    });
    if (!response.ok) {
      return { ok: false as const, status: 401, error: 'Authentication required' };
    }

    const session = (await response.json()) as CurrentSessionResponse;
    const user = session.data?.user;
    const isStaff = user?.roles?.some(role => PRIVILEGED_ROLES.has(role))
      || user?.permissions?.some(permission => permission.toLowerCase() === 'content.manage');
    if (!isStaff) {
      return { ok: false as const, status: 403, error: 'Content management permission required' };
    }
    return { ok: true as const };
  } catch (error) {
    console.error('[worker-proxy] Failed to validate staff authorization:', error);
    return { ok: false as const, status: 503, error: 'Authentication service unavailable' };
  }
}
