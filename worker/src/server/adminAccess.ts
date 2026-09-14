import type { NextFunction, Request, Response } from 'express';
import { isUnsafeSecret, requireWorkerAdminToken } from '../security.js';
import { logWarn, logInfo } from '../logging.js';

const attempts = new Map<string, { count: number; resetAt: number }>();

export function isWorkerAdminEnabled() {
  if (process.env.WORKER_ADMIN_ENABLED === 'true') return true;
  if (process.env.WORKER_ADMIN_ENABLED === 'false') return false;
  return process.env.NODE_ENV !== 'production';
}

function sourceKey(req: Request) {
  return req.ip || req.socket.remoteAddress || 'unknown';
}

function rateLimited(req: Request) {
  const limit = Number.parseInt(process.env.WORKER_ADMIN_RATE_LIMIT_PER_MINUTE || '30', 10);
  const now = Date.now();
  const key = sourceKey(req);
  const current = attempts.get(key);
  if (!current || current.resetAt <= now) {
    attempts.set(key, { count: 1, resetAt: now + 60_000 });
    return false;
  }
  current.count += 1;
  return current.count > limit;
}

export function createWorkerAdminGuard() {
  return (req: Request, res: Response, next: NextFunction) => {
    if (!isWorkerAdminEnabled()) {
      logWarn('worker-admin', 'Admin surface denied because it is disabled.', { route: req.path, method: req.method, remoteAddress: sourceKey(req) });
      return res.status(404).json({ error: 'Not found' });
    }

    if (rateLimited(req)) {
      logWarn('worker-admin', 'Admin surface rate limited.', { route: req.path, method: req.method, remoteAddress: sourceKey(req) });
      return res.status(429).json({ error: 'Rate limited' });
    }

    const token = process.env.WORKER_ADMIN_TOKEN;
    if (isUnsafeSecret(token, 32)) {
      logWarn('worker-admin', 'Admin surface denied because token configuration is unsafe.', { route: req.path, method: req.method });
      return res.status(401).json({ error: 'Unauthorized' });
    }

    return requireWorkerAdminToken(req, res, () => {
      logInfo('worker-admin', 'Admin surface authorized.', { route: req.path, method: req.method, remoteAddress: sourceKey(req) });
      next();
    });
  };
}
