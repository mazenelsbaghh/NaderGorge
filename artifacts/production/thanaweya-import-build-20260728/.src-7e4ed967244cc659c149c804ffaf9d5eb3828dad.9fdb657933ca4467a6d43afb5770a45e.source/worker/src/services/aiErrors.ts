export type AIErrorCategory = 'quota-exhausted' | 'authentication' | 'permission' | 'validation' | 'not-found' | 'provider' | 'implementation';

type ProviderErrorShape = {
  status?: unknown;
  code?: unknown;
  error?: { status?: unknown; code?: unknown };
};

function numericStatus(error: unknown) {
  if (!error || typeof error !== 'object') return undefined;
  const shaped = error as ProviderErrorShape;
  const candidates = [shaped.status, shaped.code, shaped.error?.code];
  return candidates.find((statusCandidate): statusCandidate is number => typeof statusCandidate === 'number');
}

function structuredStatus(error: unknown) {
  if (!error || typeof error !== 'object') return undefined;
  const shaped = error as ProviderErrorShape;
  const candidates = [shaped.code, shaped.status, shaped.error?.status];
  return candidates.find((statusCandidate): statusCandidate is string => typeof statusCandidate === 'string');
}

export function classifyAIError(error: unknown): { category: AIErrorCategory; status?: number } {
  const status = numericStatus(error);
  const rpcStatus = structuredStatus(error);
  const classifiedError = (category: AIErrorCategory) => status === undefined ? { category } : { category, status };
  if (status === 429 || rpcStatus === 'RESOURCE_EXHAUSTED') return classifiedError('quota-exhausted');
  if (status === 401 || rpcStatus === 'UNAUTHENTICATED') return classifiedError('authentication');
  if (status === 403 || rpcStatus === 'PERMISSION_DENIED') return classifiedError('permission');
  if (status === 400 || rpcStatus === 'INVALID_ARGUMENT' || rpcStatus === 'FAILED_PRECONDITION') return classifiedError('validation');
  if (status === 404 || rpcStatus === 'NOT_FOUND') return classifiedError('not-found');
  if (status !== undefined || rpcStatus) return classifiedError('provider');
  return { category: 'implementation' };
}

export function isQuotaExhausted(error: unknown) {
  return classifyAIError(error).category === 'quota-exhausted';
}
