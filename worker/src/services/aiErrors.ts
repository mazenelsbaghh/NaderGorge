export type AIErrorCategory = 'balance-exhausted' | 'quota-exhausted' | 'authentication' | 'permission' | 'validation' | 'not-found' | 'provider' | 'implementation';

type ProviderErrorShape = {
  status?: unknown;
  code?: unknown;
  message?: unknown;
  error?: { status?: unknown; code?: unknown; message?: unknown };
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
  const candidates = [shaped.code, shaped.status, shaped.error?.status, shaped.error?.code];
  return candidates.find((statusCandidate): statusCandidate is string => typeof statusCandidate === 'string');
}

export function classifyAIError(error: unknown): { category: AIErrorCategory; status?: number } {
  const status = numericStatus(error);
  const rpcStatus = structuredStatus(error)?.toUpperCase();
  const classifiedError = (category: AIErrorCategory) => status === undefined ? { category } : { category, status };
  if (status === 402 || rpcStatus === 'PAYMENT_REQUIRED' || depletedPrepayment(error, status, rpcStatus))
    return classifiedError('balance-exhausted');
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

function depletedPrepayment(error: unknown, status?: number, rpcStatus?: string) {
  if (status !== 429 && rpcStatus !== 'RESOURCE_EXHAUSTED') return false;
  const shaped = error as ProviderErrorShape;
  return [shaped.message, shaped.error?.message].some(message => typeof message === 'string'
    && /(?:prepay(?:ment|paid)?\s+credits?\s+(?:are\s+)?depleted|prepaid\s+(?:credit\s+)?balance\s+(?:is\s+)?(?:depleted|exhausted)|insufficient\s+(?:prepaid\s+)?credits?)/i.test(message));
}
