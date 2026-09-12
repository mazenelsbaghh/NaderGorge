import makeWASocket, { DisconnectReason, normalizeMessageContent, jidNormalizedUser, type WASocket, type WAMessage } from '@whiskeysockets/baileys';
import { pino } from 'pino';
import QRCode from 'qrcode';
import type { Pool } from 'pg';
import { BaileysStateStore } from './store.js';

const logger = pino({ level: 'silent' });
type Session = { accountId: string; socket: WASocket; state: string; qr?: string; qrExpiresAt?: number; stopped: boolean; retries: number };

export class BaileysSessions {
  private readonly sessions = new Map<string, Session>();
  private readonly starting = new Map<string, Promise<Session>>();
  private closed = false;
  constructor(private readonly pool: Pool, private readonly store: BaileysStateStore) {}

  async restore(): Promise<void> {
    const accounts = await this.pool.query<{ InstanceName: string }>(`SELECT "InstanceName" FROM live_support_whatsapp_accounts
      WHERE "IsEnabled"=TRUE AND "Status"<>'Created'`);
    for (const account of accounts.rows) {
      try { await this.start(account.InstanceName); }
      catch { console.error('[Baileys] A saved session could not be restored; reconnect it from settings.'); }
    }
  }

  async start(sessionId: string): Promise<Session> {
    if (this.closed) throw new Error('Bridge stopped');
    const existing = this.sessions.get(sessionId);
    if (existing && !existing.stopped && existing.state !== 'close') return existing;
    const pending = this.starting.get(sessionId);
    if (pending) return pending;
    const started = this.open(sessionId);
    this.starting.set(sessionId, started);
    try { return await started; } finally { this.starting.delete(sessionId); }
  }

  private async open(sessionId: string): Promise<Session> {
    const accounts = await this.pool.query<{ Id: string }>('SELECT "Id" FROM live_support_whatsapp_accounts WHERE "InstanceName"=$1', [sessionId]);
    const account = accounts.rows[0];
    if (!account) throw new Error('Unknown account');
    const auth = await this.store.auth(account.Id);
    const socket = makeWASocket({ auth: auth.state, logger, markOnlineOnConnect: false,
      syncFullHistory: false, shouldSyncHistoryMessage: () => false,
      shouldIgnoreJid: jid => jid.endsWith('@g.us') || jid.endsWith('@broadcast') || jid.endsWith('@newsletter') });
    const session: Session = { accountId: account.Id, socket, state: 'connecting', stopped: false, retries: this.sessions.get(sessionId)?.retries ?? 0 };
    this.sessions.set(sessionId, session);
    socket.ev.process(async events => {
      try {
        if (events['creds.update']) await auth.saveCreds();
        const update = events['connection.update'];
        if (update) {
          if (update.qr) {
            session.qr = await QRCode.toDataURL(update.qr, { width: 280, margin: 2 });
            session.qrExpiresAt = Date.now() + 30_000;
          }
          if (update.connection) await this.connectionChanged(sessionId, session, update.connection, update.lastDisconnect?.error);
        }
        for (const message of events['messages.upsert']?.messages ?? []) await this.receive(sessionId, session, message);
        for (const receipt of events['messages.update'] ?? []) {
          if (receipt.key.fromMe && receipt.key.id && receipt.update.status != null)
            await this.store.enqueue(session.accountId, { sessionId, event: 'receipt', data: {
              id: receipt.key.id, status: receipt.update.status, timestamp: Math.floor(Date.now() / 1000) } });
        }
      } catch {
        session.stopped = true;
        session.state = 'close';
        delete session.qr; delete session.qrExpiresAt;
        socket.end(new Error('Session persistence failed'));
        console.error('[Baileys] Session paused after persistence failure.');
      }
    });
    return session;
  }

  private async connectionChanged(sessionId: string, session: Session, state: string, error: unknown): Promise<void> {
    if (state === 'open') {
      session.retries = 0;
      const phone = jidNormalizedUser(session.socket.user?.id ?? '').split('@')[0];
      const pinned = await this.pool.query(`UPDATE live_support_whatsapp_accounts SET "PhoneNumber"=$2
        WHERE "Id"=$1 AND ("PhoneNumber" IS NULL OR "PhoneNumber"=$2) RETURNING "Id"`, [session.accountId, phone]);
      if (!phone || pinned.rowCount !== 1) {
        session.stopped = true;
        await session.socket.logout();
        await this.store.clear(session.accountId);
        session.state = 'close';
        await this.pool.query(`UPDATE live_support_whatsapp_accounts SET "Status"='NumberChanged' WHERE "Id"=$1`, [session.accountId]);
        return;
      }
    }
    session.state = state;
    if (state === 'open' || state === 'close') { delete session.qr; delete session.qrExpiresAt; }
    await this.store.enqueue(session.accountId, { sessionId, event: 'connection', data: { state, wuid: session.socket.user?.id } });
    if (state !== 'close' || session.stopped || this.closed) return;
    const status = (error as { output?: { statusCode?: number } } | undefined)?.output?.statusCode;
    if (status === DisconnectReason.loggedOut || status === DisconnectReason.badSession || status === DisconnectReason.forbidden) {
      session.stopped = true;
      await this.store.clear(session.accountId);
      return;
    }
    const delay = Math.min(30_000, 1000 * 2 ** session.retries++);
    setTimeout(() => {
      if (!this.closed && !session.stopped && this.sessions.get(sessionId) === session)
        void this.start(sessionId).catch(() => console.error('[Baileys] Reconnection failed.'));
    }, delay).unref();
  }

  private async receive(sessionId: string, session: Session, message: WAMessage): Promise<void> {
    const jid = message.key.remoteJid;
    if (message.key.fromMe || !message.message || !jid || jid.endsWith('@g.us') || jid.endsWith('@broadcast') || jid.endsWith('@newsletter')) return;
    if (jid.endsWith('@lid') && !message.key.remoteJidAlt) {
      const phoneJid = await session.socket.signalRepository.lidMapping.getPNForLID(jid);
      if (phoneJid) message.key.remoteJidAlt = phoneJid;
    }
    const content = normalizeMessageContent(message.message);
    await this.store.enqueue(session.accountId, { sessionId, event: 'message', data: {
      ...message, message: content, messageTimestamp: message.messageTimestamp?.toString() } });
  }

  async connection(sessionId: string): Promise<{ instance: { state: string }; base64?: string }> {
    const session = await this.start(sessionId);
    const deadline = Date.now() + 15_000;
    while (!session.qr && session.state === 'connecting' && Date.now() < deadline)
      await new Promise(resolve => setTimeout(resolve, 200));
    return { instance: { state: session.state }, ...(session.qr && (session.qrExpiresAt ?? 0) > Date.now() ? { base64: session.qr } : {}) };
  }

  state(sessionId: string): { instance: { state: string } } | undefined {
    const session = this.sessions.get(sessionId);
    return session ? { instance: { state: session.state } } : undefined;
  }

  socket(sessionId: string): WASocket {
    const session = this.sessions.get(sessionId);
    if (!session || session.stopped || session.state !== 'open') throw new Error('Session disconnected');
    return session.socket;
  }

  async logout(sessionId: string): Promise<void> {
    const session = this.sessions.get(sessionId);
    if (!session) throw new Error('Unknown session');
    if (session.stopped && session.state === 'close') return;
    session.stopped = true;
    try { await session.socket.logout(); }
    catch (error) { session.stopped = false; throw error; }
    await this.store.clear(session.accountId);
    session.state = 'close';
    delete session.qr; delete session.qrExpiresAt;
  }

  close(): void {
    this.closed = true;
    for (const session of this.sessions.values()) { session.stopped = true; session.socket.end(new Error('Bridge stopped')); }
  }
}

export function contactJid(number: unknown): string {
  if (typeof number !== 'string' || !/^\d{7,15}$/.test(number)) throw new Error('Invalid recipient');
  return jidNormalizedUser(`${number}@s.whatsapp.net`);
}
