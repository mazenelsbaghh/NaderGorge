import crypto from 'crypto';
import type { NextFunction, Request, Response } from 'express';

const UNSAFE_SECRETS = new Set([
  'change_me',
  'changeme',
  'CHANGE_ME',
  'CHANGE_ME_IN_LOCAL_ENV',
  'AIzaSyB...'
]);

export function isUnsafeSecret(value: string | undefined, minLength = 24) {
  if (!value || value.trim().length < minLength) return true;
  return UNSAFE_SECRETS.has(value.trim());
}

export function requireStrongSecret(name: string, minLength = 32) {
  const value = process.env[name];
  if (isUnsafeSecret(value, minLength)) {
    throw new Error(`${name} is missing, weak, or uses an unsafe placeholder.`);
  }
  return value!;
}

export function validateWorkerSecurityConfig() {
  requireStrongSecret('API_CALLBACK_SECRET');
  requireStrongSecret('WORKER_ADMIN_TOKEN');
}

export function requireWorkerAdminToken(req: Request, res: Response, next: NextFunction) {
  const configuredToken = process.env.WORKER_ADMIN_TOKEN;
  const authHeader = req.header('authorization') || '';
  const suppliedToken = authHeader.startsWith('Bearer ') ? authHeader.slice(7) : '';

  if (isUnsafeSecret(configuredToken, 32) || !fixedTimeEquals(suppliedToken, configuredToken!)) {
    return res.status(401).json({ error: 'Unauthorized' });
  }

  return next();
}

function fixedTimeEquals(left: string, right: string) {
  const leftBuffer = Buffer.from(left);
  const rightBuffer = Buffer.from(right);
  return leftBuffer.length === rightBuffer.length && crypto.timingSafeEqual(leftBuffer, rightBuffer);
}
