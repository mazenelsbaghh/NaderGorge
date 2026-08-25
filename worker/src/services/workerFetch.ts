import child_process from 'child_process';

export type ExternalFailureCategory =
  | 'timeout'
  | 'network'
  | 'rejected'
  | 'response-too-large'
  | 'provider'
  | 'conversion'
  | 'cancelled'
  | 'implementation';

export class WorkerExternalError extends Error {
  constructor(
    public readonly category: ExternalFailureCategory,
    public readonly retryable: boolean,
    public readonly remediation: string,
    message?: string,
  ) {
    super(message ?? remediation);
    this.name = 'WorkerExternalError';
  }
}

const URL_PATTERN = /\bhttps?:\/\/[^\s]+/gi;
const SECRET_PATTERN = /(token|secret|password|key)=([^&\s]+)/gi;

export function redactExternalText(value: unknown) {
  const text = String(value || '')
    .replace(URL_PATTERN, '[redacted-url]')
    .replace(SECRET_PATTERN, '$1=[redacted]');
  return text.length > 500 ? `${text.slice(0, 500)}...` : text;
}

export function classifyExternalFailure(error: unknown, fallback: ExternalFailureCategory = 'implementation') {
  if (error instanceof WorkerExternalError) return error;
  if (error instanceof DOMException && error.name === 'AbortError') {
    return new WorkerExternalError('timeout', true, 'External operation timed out.');
  }
  if (error instanceof Error && /timeout|aborted/i.test(error.message)) {
    return new WorkerExternalError('timeout', true, 'External operation timed out.', redactExternalText(error.message));
  }
  return new WorkerExternalError(fallback, fallback !== 'implementation', `External operation failed (${fallback}).`, redactExternalText(error instanceof Error ? error.message : error));
}

function responseTooLarge() {
  return new WorkerExternalError(
    'response-too-large',
    false,
    'External response exceeded the allowed size.',
  );
}

function joinResponseChunks(chunks: Uint8Array[], byteLength: number) {
  const body = new Uint8Array(byteLength);
  let offset = 0;
  for (const chunk of chunks) {
    body.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return body;
}

async function collectBoundedBody(
  body: ReadableStream<Uint8Array>,
  maxResponseBytes: number,
  controller: AbortController,
) {
  const reader = body.getReader();
  const chunks: Uint8Array[] = [];
  let byteLength = 0;
  try {
    for (;;) {
      const next = await reader.read();
      if (next.done) break;
      byteLength += next.value.byteLength;
      if (byteLength > maxResponseBytes) {
        controller.abort();
        throw responseTooLarge();
      }
      chunks.push(next.value);
    }
  } finally {
    reader.releaseLock();
  }

  return joinResponseChunks(chunks, byteLength);
}

async function readBoundedResponseBody(
  response: Response,
  maxResponseBytes: number,
  controller: AbortController,
) {
  if (!response.body) return response;
  const body = await collectBoundedBody(response.body, maxResponseBytes, controller);
  return new Response(body.byteLength === 0 ? null : body, {
    status: response.status,
    statusText: response.statusText,
    headers: response.headers,
  });
}

export async function fetchWithTimeout(
  url: string,
  init: RequestInit & { timeoutMs?: number; maxResponseBytes?: number; operation?: string } = {},
) {
  const timeoutMs = init.timeoutMs ?? Number.parseInt(process.env.WORKER_FETCH_TIMEOUT_MS || '10000', 10);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  const { timeoutMs: _timeout, maxResponseBytes, operation: _operation, ...requestInit } = init;
  try {
    const response = await fetch(url, { ...requestInit, signal: controller.signal });
    if (maxResponseBytes) {
      const declared = Number(response.headers.get('content-length'));
      if (Number.isFinite(declared) && declared > maxResponseBytes) {
        controller.abort();
        throw responseTooLarge();
      }
      return await readBoundedResponseBody(response, maxResponseBytes, controller);
    }
    return response;
  } catch (error) {
    throw classifyExternalFailure(error, 'network');
  } finally {
    clearTimeout(timer);
  }
}

export function execFileWithTimeout(
  file: string,
  args: string[],
  timeoutMs = Number.parseInt(process.env.WORKER_EXEC_TIMEOUT_MS || '600000', 10),
): Promise<{ stdout: string; stderr: string }> {
  return new Promise((resolve, reject) => {
    const child = child_process.execFile(file, args, { timeout: timeoutMs }, (err, stdout, stderr) => {
      if (err) {
        const message = redactExternalText(stderr || stdout || err.message);
        const timedOut = Boolean((err as NodeJS.ErrnoException & { killed?: boolean }).killed);
        reject(new WorkerExternalError(timedOut ? 'timeout' : 'conversion', true, timedOut ? 'External process timed out.' : 'External process failed.', message));
      } else {
        resolve({ stdout, stderr });
      }
    });
    child.on('error', error => reject(classifyExternalFailure(error, 'conversion')));
  });
}
