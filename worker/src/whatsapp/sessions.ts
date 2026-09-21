import makeWASocket, { DisconnectReason, normalizeMessageContent, jidNormalizedUser, type WASocket, type WAMessage } from '@whiskeysockets/baileys';
import { pino } from 'pino';
import QRCode from 'qrcode';
import type { Pool } from 'pg';
import { BaileysStateStore } from './store.js';
import { currentWhatsAppVersion } from './version.js';

const logger = pino({ level: 'silent' });
type Session = { accountId: string; socket: WASocket; state: string; qr?: string; qrExpiresAt?: number; stopped: boolean; retries: number; lastProgressAt: number };
export type ConnectionSnapshot = { instance: { state: string }; base64?: string; qrExpiresAt?: number };

export class BaileysSessions {
  private readonly sessions = new Map<string, Session>();
  private readonly starting = new Map<string, Promise<Session>>();
  private readonly reconnectTimers = new Map<string, NodeJS.Timeout>();
  private closed = false;
  constructor(private readonly pool: Pool, private readonly store: BaileysStateStore) {}

  async restore(): Promise<void> {
    const accounts = await this.pool.query<{ InstanceName: string }>(`SELECT "InstanceName" FROM live_support_whatsapp_accounts
      WHERE "IsEnabled"=TRUE AND "Status"<>'Created'`);
    for (const account of accounts.rows) {
      try { await this.start(account.InstanceName); }
      catch (error) {
        this.logFailure('restore', error);
        this.scheduleReconnect(account.InstanceName, 0);
      }
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
    const version = await currentWhatsAppVersion();
    const auth = await this.store.auth(account.Id);
    const socket = makeWASocket({ version, auth: auth.state, logger, markOnlineOnConnect: false,
      syncFullHistory: false, shouldSyncHistoryMessage: () => false,
      shouldIgnoreJid: jid => jid.endsWith('@g.us') || jid.endsWith('@broadcast') || jid.endsWith('@newsletter') });
    const session: Session = { accountId: account.Id, socket, state: 'connecting', stopped: false,
      retries: this.sessions.get(sessionId)?.retries ?? 0, lastProgressAt: Date.now() };
    this.sessions.set(sessionId, session);
    socket.ev.process(async events => {
      if (events['creds.update']) {
        try { await auth.saveCreds(); }
        catch (error) { this.recoverSession(sessionId, session, error, 'credentials'); return; }
      }
      const update = events['connection.update'];
      if (update) {
        try {
          if (update.qr) {
            session.qr = await QRCode.toDataURL(update.qr, { width: 280, margin: 2 });
            session.qrExpiresAt = Date.now() + 30_000;
            session.lastProgressAt = Date.now();
          }
          if (update.connection) {
            session.lastProgressAt = Date.now();
            await this.connectionChanged(sessionId, session, update.connection, update.lastDisconnect?.error);
          }
        }
        catch (error) { this.recoverSession(sessionId, session, error, 'connection'); return; }
      }
      for (const message of events['messages.upsert']?.messages ?? []) {
        try { await this.receive(sessionId, session, message); }
        catch (error) { this.logFailure('inbound-message', error); }
      }
      for (const receipt of events['messages.update'] ?? []) {
        try {
          if (receipt.key.fromMe && receipt.key.id && receipt.update.status != null)
            await this.store.enqueue(session.accountId, { sessionId, event: 'receipt', data: {
              id: receipt.key.id, status: receipt.update.status, timestamp: Math.floor(Date.now() / 1000) } });
        }
        catch (error) { this.logFailure('message-receipt', error); }
      }
    });
    return session;
  }

  private async connectionChanged(sessionId: string, session: Session, state: string, error: unknown): Promise<void> {
    if (state === 'open') {
      session.retries = 0;
      this.clearReconnect(sessionId);
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
    console.error(`[Baileys] connection closed status=${status ?? 'unknown'}.`);
    if (status === DisconnectReason.loggedOut || status === DisconnectReason.badSession || status === DisconnectReason.forbidden) {
      session.stopped = true;
      await this.store.clear(session.accountId);
      return;
    }
    this.scheduleReconnect(sessionId, session.retries++, session);
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

  async connection(sessionId: string): Promise<ConnectionSnapshot> {
    const session = await this.start(sessionId);
    const deadline = Date.now() + 15_000;
    while ((!session.qr || (session.qrExpiresAt ?? 0) <= Date.now())
      && session.state === 'connecting' && Date.now() < deadline)
      await new Promise(resolve => setTimeout(resolve, 200));
    return connectionSnapshot(session.state, session.qr, session.qrExpiresAt);
  }

  state(sessionId: string): ConnectionSnapshot | undefined {
    const session = this.sessions.get(sessionId);
    if (session && session.state === 'connecting' && (session.qrExpiresAt ?? 0) <= Date.now() &&
        Date.now() - session.lastProgressAt > 45_000)
      this.recoverSession(sessionId, session, new Error('Pairing stalled'), 'pairing-timeout');
    return session ? connectionSnapshot(session.state, session.qr, session.qrExpiresAt) : undefined;
  }

  socket(sessionId: string): WASocket {
    const session = this.sessions.get(sessionId);
    if (!session || session.stopped || session.state !== 'open') throw new Error('Session disconnected');
    return session.socket;
  }

  async logout(sessionId: string): Promise<void> {
    this.clearReconnect(sessionId);
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
    for (const timer of this.reconnectTimers.values()) clearTimeout(timer);
    this.reconnectTimers.clear();
    for (const session of this.sessions.values()) { session.stopped = true; session.socket.end(new Error('Bridge stopped')); }
  }

  private recoverSession(sessionId: string, session: Session, error: unknown, operation: string): void {
    if (session.stopped || this.closed || this.sessions.get(sessionId) !== session) return;
    session.state = 'close';
    delete session.qr; delete session.qrExpiresAt;
    this.logFailure(operation, error);
    session.socket.end(new Error('Session recovery requested'));
    this.scheduleReconnect(sessionId, session.retries++, session);
  }

  private scheduleReconnect(sessionId: string, retries: number, expectedSession?: Session): void {
    if (this.closed || this.reconnectTimers.has(sessionId)) return;
    const delay = Math.min(30_000, 1000 * 2 ** Math.min(retries, 5));
    const timer = setTimeout(() => {
      this.reconnectTimers.delete(sessionId);
      if (this.closed || expectedSession?.stopped || (expectedSession && this.sessions.get(sessionId) !== expectedSession)) return;
      void this.start(sessionId).catch(error => {
        this.logFailure('reconnect', error);
        this.scheduleReconnect(sessionId, retries + 1);
      });
    }, delay);
    timer.unref();
    this.reconnectTimers.set(sessionId, timer);
  }

  private clearReconnect(sessionId: string): void {
    const timer = this.reconnectTimers.get(sessionId);
    if (timer) clearTimeout(timer);
    this.reconnectTimers.delete(sessionId);
  }

  private logFailure(operation: string, error: unknown): void {
    const kind = error instanceof Error ? error.name : typeof error;
    console.error(`[Baileys] ${operation} failed kind=${kind}.`);
  }
}

export function connectionSnapshot(
  state: string,
  qr?: string,
  qrExpiresAt?: number,
  now = Date.now(),
): ConnectionSnapshot {
  if (typeof qr === 'string' && typeof qrExpiresAt === 'number' && qrExpiresAt > now)
    return { instance: { state }, base64: qr, qrExpiresAt };
  return { instance: { state } };
}

export function contactJid(number: unknown): string {
  if (typeof number !== 'string' || !/^\d{7,15}$/.test(number)) throw new Error('Invalid recipient');
  return jidNormalizedUser(`${number}@s.whatsapp.net`);
}
