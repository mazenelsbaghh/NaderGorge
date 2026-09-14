import { connect, type ClientHttp2Session, type IncomingHttpHeaders } from 'node:http2';
import { createPrivateKey, createSign } from 'node:crypto';
import fs from 'node:fs';

export interface ApnsNotificationPayload {
  title: string;
  body: string;
  studentId: string;
  category: string;
}

export interface ApnsTokenResult {
  token: string;
  success: boolean;
  statusCode?: number;
  reason?: string;
}

export interface ApnsSendResult {
  successCount: number;
  failureCount: number;
  results: ApnsTokenResult[];
}

interface ApnsConfiguration {
  keyId: string;
  teamId: string;
  topic: string;
  privateKey: string;
  host: string;
}

let cachedJwt: { keyId: string; teamId: string; token: string; expiresAt: number } | undefined;

function environmentValue(name: string): string | undefined {
  const value = process.env[name]?.trim();
  return value ? value : undefined;
}

function readPrivateKey(): string | undefined {
  const inlineKey = environmentValue('APNS_PRIVATE_KEY');
  if (inlineKey) {
    return inlineKey.replace(/\\n/g, '\n');
  }

  const keyPath = environmentValue('APNS_PRIVATE_KEY_PATH');
  if (!keyPath) {
    return undefined;
  }

  try {
    return fs.readFileSync(keyPath, 'utf8');
  } catch (error) {
    console.warn(`[APNs] Unable to read APNS_PRIVATE_KEY_PATH (${keyPath}):`, error);
    return undefined;
  }
}

function configuration(): ApnsConfiguration | undefined {
  const keyId = environmentValue('APNS_KEY_ID');
  const teamId = environmentValue('APNS_TEAM_ID');
  const privateKey = readPrivateKey();

  if (!keyId || !teamId || !privateKey) {
    return undefined;
  }

  const environment = (environmentValue('APNS_ENVIRONMENT') || 'development').toLowerCase();
  return {
    keyId,
    teamId,
    privateKey,
    topic: environmentValue('APNS_TOPIC') || environmentValue('APNS_BUNDLE_ID') || 'net.massaracademy.parent',
    host: environment === 'production' ? 'api.push.apple.com' : 'api.development.push.apple.com'
  };
}

function base64Url(value: string | Buffer): string {
  return Buffer.from(value).toString('base64url');
}

function authorizationToken(config: ApnsConfiguration): string {
  const now = Math.floor(Date.now() / 1000);
  if (cachedJwt && cachedJwt.keyId === config.keyId && cachedJwt.teamId === config.teamId && cachedJwt.expiresAt > now + 60) {
    return cachedJwt.token;
  }

  const header = base64Url(JSON.stringify({ alg: 'ES256', kid: config.keyId }));
  const payload = base64Url(JSON.stringify({ iss: config.teamId, iat: now }));
  const signingInput = `${header}.${payload}`;
  const signer = createSign('sha256');
  signer.update(signingInput);
  signer.end();
  const signature = signer.sign({
    key: createPrivateKey(config.privateKey),
    dsaEncoding: 'ieee-p1363'
  });
  const token = `${signingInput}.${base64Url(signature)}`;
  cachedJwt = { keyId: config.keyId, teamId: config.teamId, token, expiresAt: now + 50 * 60 };
  return token;
}

function sendOne(
  session: ClientHttp2Session,
  config: ApnsConfiguration,
  authorization: string,
  token: string,
  payload: ApnsNotificationPayload
): Promise<ApnsTokenResult> {
  return new Promise((resolve) => {
    const request = session.request({
      ':method': 'POST',
      ':path': `/3/device/${encodeURIComponent(token)}`,
      authorization: `bearer ${authorization}`,
      'apns-topic': config.topic,
      'apns-push-type': 'alert',
      'apns-priority': '10',
      'content-type': 'application/json'
    });

    let statusCode: number | undefined;
    let responseBody = '';
    request.setEncoding('utf8');
    request.on('response', (headers: IncomingHttpHeaders) => {
      const rawStatus = headers[':status'];
      statusCode = rawStatus === undefined ? undefined : Number(rawStatus);
    });
    request.on('data', (chunk: string) => {
      responseBody += chunk;
    });
    request.on('end', () => {
      let reason: string | undefined;
      if (responseBody) {
        try {
          const parsed = JSON.parse(responseBody) as { reason?: unknown };
          if (typeof parsed.reason === 'string') {
            reason = parsed.reason;
          }
        } catch {
          reason = responseBody.slice(0, 200);
        }
      }
      resolve({ token, success: statusCode === 200, ...(statusCode === undefined ? {} : { statusCode }), ...(reason ? { reason } : {}) });
    });
    request.on('error', (error: Error) => {
      resolve({ token, success: false, reason: error.message });
    });
    request.end(JSON.stringify({
      aps: {
        alert: { title: payload.title, body: payload.body },
        sound: 'default'
      },
      studentId: payload.studentId,
      category: payload.category
    }));
  });
}

async function openSession(config: ApnsConfiguration): Promise<ClientHttp2Session> {
  const session = connect(`https://${config.host}:443`);
  try {
    await new Promise<void>((resolve, reject) => {
      const onConnect = () => {
        cleanup();
        resolve();
      };
      const onError = (error: Error) => {
        cleanup();
        reject(error);
      };
      const cleanup = () => {
        session.off('connect', onConnect);
        session.off('error', onError);
      };
      session.once('connect', onConnect);
      session.once('error', onError);
    });
    return session;
  } catch (error) {
    session.destroy();
    throw error;
  }
}

export const apnsProvider = {
  isConfigured(): boolean {
    return configuration() !== undefined;
  },

  async sendMany(tokens: string[], payload: ApnsNotificationPayload): Promise<ApnsSendResult> {
    const config = configuration();
    if (!config) {
      throw new Error('APNs is not configured. Set APNS_KEY_ID, APNS_TEAM_ID, and APNS_PRIVATE_KEY (or APNS_PRIVATE_KEY_PATH).');
    }

    const uniqueTokens = [...new Set(tokens.filter(Boolean))];
    if (uniqueTokens.length === 0) {
      return { successCount: 0, failureCount: 0, results: [] };
    }

    const session = await openSession(config);
    try {
      const authorization = authorizationToken(config);
      const results: ApnsTokenResult[] = [];
      for (const token of uniqueTokens) {
        results.push(await sendOne(session, config, authorization, token, payload));
      }
      return {
        successCount: results.filter((result) => result.success).length,
        failureCount: results.filter((result) => !result.success).length,
        results
      };
    } finally {
      session.close();
    }
  }
};
