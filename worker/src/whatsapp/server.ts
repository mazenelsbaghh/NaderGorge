import 'dotenv/config';
import { timingSafeEqual } from 'node:crypto';
import express from 'express';
import { BufferJSON, downloadMediaMessage, normalizeMessageContent, type WAMessage } from '@whiskeysockets/baileys';
import { pino } from 'pino';
import { Pool } from 'pg';
import { databasePoolConfig } from '../config/database.js';
import { BaileysStateStore } from './store.js';
import { BaileysSessions, contactJid } from './sessions.js';

const token = process.env.BAILEYS_API_KEY ?? '';
const authKey = process.env.BAILEYS_AUTH_KEY ?? '';
const backendUrl = process.env.BACKEND_API_URL ?? '';
if (token.length < 32 || !authKey || !/^https?:\/\//.test(backendUrl))
  throw new Error('Configure BAILEYS_API_KEY, BAILEYS_AUTH_KEY and BACKEND_API_URL before starting the bridge.');
const databaseConfig = databasePoolConfig();
if (process.env.BAILEYS_DATABASE_HOST) {
  const databaseConnection = new URL(databaseConfig.connectionString!);
  databaseConnection.hostname = process.env.BAILEYS_DATABASE_HOST;
  databaseConfig.connectionString = databaseConnection.toString();
}
const pool = new Pool({ ...databaseConfig, options: '-c timezone=UTC' });
const store = new BaileysStateStore(pool, authKey);
const sessions = new BaileysSessions(pool, store);
const lock = await pool.connect();
const claimed = await lock.query<{ acquired: boolean }>('SELECT pg_try_advisory_lock(169092026) AS acquired');
if (!claimed.rows[0]?.acquired) {
  lock.release(); await pool.end();
  throw new Error('A Baileys bridge already owns the sessions.');
}

const app = express();
app.disable('x-powered-by');
app.use((request, response, next) => {
  const supplied = Buffer.from(request.get('X-Baileys-Token') ?? '');
  const expected = Buffer.from(token);
  if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) { response.sendStatus(401); return; }
  response.setHeader('Cache-Control', 'no-store'); next();
});
app.use(express.json({ limit: '24mb' }));
app.get('/health', (_request, response) => response.json({ ok: true }));
app.param('id', (request, response, next, id: string) => {
  if (!/^massar-support-[a-f0-9]{32}$/.test(id)) { response.sendStatus(400); return; }
  next();
});
app.post('/sessions', async (request, response) => {
  const id = request.body?.sessionId;
  if (typeof id !== 'string' || !/^massar-support-[a-f0-9]{32}$/.test(id)) { response.sendStatus(400); return; }
  await sessions.start(id); response.json({ created: true });
});
app.get('/sessions/:id', (request, response) => {
  const state = sessions.state(request.params.id);
  if (!state) { response.sendStatus(404); return; }
  response.json(state);
});
app.post('/sessions/:id/connect', async (request, response) => response.json(await sessions.connection(request.params.id)));
app.delete('/sessions/:id', async (request, response) => { await sessions.logout(request.params.id); response.json({ disconnected: true }); });
app.post('/sessions/:id/block', async (request, response) => {
  const status: unknown = request.body?.status;
  if (status !== 'block' && status !== 'unblock') { response.sendStatus(400); return; }
  const socket = sessions.socket(request.params.id);
  const jid = contactJid(request.body?.number);
  await socket.updateBlockStatus(jid, status);
  const blocked = (await socket.fetchBlocklist()).includes(jid);
  if (blocked !== (status === 'block')) { response.sendStatus(409); return; }
  response.json({ blocked });
});
app.post('/sessions/:id/text', async (request, response) => {
  const text: unknown = request.body?.text;
  if (typeof text !== 'string' || text.length < 1 || text.length > 4000) { response.sendStatus(400); return; }
  const message = await sessions.socket(request.params.id).sendMessage(contactJid(request.body?.number), { text });
  response.json({ key: message?.key });
});
app.post('/sessions/:id/audio', async (request, response) => {
  const mimetype: unknown = request.body?.mimetype;
  if (typeof mimetype !== 'string' || !['audio/mpeg', 'audio/ogg', 'audio/ogg; codecs=opus', 'audio/mp4'].includes(mimetype)) { response.sendStatus(400); return; }
  const audio = mediaBytes(request.body?.audio);
  const message = await sessions.socket(request.params.id).sendMessage(contactJid(request.body?.number), { audio, mimetype, ptt: mimetype.includes('ogg') });
  response.json({ key: message?.key });
});
app.post('/sessions/:id/media', async (request, response) => {
  if (request.body?.mediatype !== 'image') { response.sendStatus(400); return; }
  const image = mediaBytes(request.body?.media);
  const caption = typeof request.body?.caption === 'string' ? request.body.caption.slice(0, 4000) : '';
  const message = await sessions.socket(request.params.id).sendMessage(contactJid(request.body?.number), { image, caption });
  response.json({ key: message?.key });
});
app.post('/sessions/:id/download', async (request, response) => {
  const message = JSON.parse(JSON.stringify(request.body?.message), BufferJSON.reviver) as WAMessage;
  if (!message?.key || !message.message) { response.sendStatus(400); return; }
  const socket = sessions.socket(request.params.id);
  const stream = await downloadMediaMessage(message, 'stream', {}, { logger: pino({ level: 'silent' }), reuploadRequest: socket.updateMediaMessage });
  let size = 0;
  const chunks: Buffer[] = [];
  for await (const chunk of stream) {
    size += chunk.length;
    if (size > 10 * 1024 * 1024) { stream.destroy(); response.sendStatus(413); return; }
    chunks.push(Buffer.from(chunk));
  }
  const content = normalizeMessageContent(message.message);
  const media = content?.imageMessage ?? content?.audioMessage ?? content?.documentMessage ?? content?.videoMessage;
  const mimetype = media?.mimetype ?? 'application/octet-stream';
  const extension = mimetype.includes('pdf') ? '.pdf' : mimetype.includes('png') ? '.png' : mimetype.includes('image') ? '.jpg' : mimetype.includes('audio') ? '.ogg' : '.mp4';
  response.json({ base64: Buffer.concat(chunks).toString('base64'), mimetype, fileName: `whatsapp-${message.key.id}${extension}` });
});
app.use((_error: unknown, _request: express.Request, response: express.Response, _next: express.NextFunction) => {
  // Provider errors can contain session credentials and message contents.
  response.status(502).json({ code: 'BAILEYS_REQUEST_FAILED' });
});

function mediaBytes(encoded: unknown): Buffer {
  if (typeof encoded !== 'string' || encoded.length > 23_000_000 || !/^[A-Za-z0-9+/]*={0,2}$/.test(encoded)) throw new Error('Invalid media');
  const bytes = Buffer.from(encoded, 'base64');
  if (bytes.length === 0 || bytes.length > 16 * 1024 * 1024) throw new Error('Invalid media');
  return bytes;
}

let closing = false;
let delivering = false;
const server = app.listen(Number(process.env.BAILEYS_PORT ?? 3002), process.env.BAILEYS_HOST ?? '127.0.0.1');
async function shutdown(): Promise<void> {
  if (closing) return;
  closing = true; clearInterval(heartbeat); clearInterval(callbacks); sessions.close();
  server.close(); lock.release(true); await pool.end();
}
lock.on('error', () => { void shutdown(); });
const heartbeat = setInterval(() => { void lock.query('SELECT 1').catch(() => shutdown()); }, 5000);
const callbacks = setInterval(() => {
  if (delivering || closing) return;
  delivering = true;
  void store.deliverCallbacks(`${backendUrl.replace(/\/$/, '')}/api/live-support/baileys/webhook`, token)
    .catch(() => console.error('[Baileys] Callback delivery paused; encrypted messages retained.'))
    .finally(() => { delivering = false; });
}, 2000);
process.once('SIGTERM', () => { void shutdown(); });
process.once('SIGINT', () => { void shutdown(); });
await sessions.restore();
