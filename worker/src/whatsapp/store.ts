import { createCipheriv, createDecipheriv, randomBytes, randomUUID } from 'node:crypto';
import { BufferJSON, initAuthCreds, proto, type AuthenticationCreds, type AuthenticationState, type SignalDataTypeMap } from '@whiskeysockets/baileys';
import type { Pool } from 'pg';

export class BaileysStateStore {
  private readonly key: Buffer;
  private readonly writes = new Map<string, Promise<void>>();
  constructor(private readonly pool: Pool, encodedKey: string) {
    this.key = Buffer.from(encodedKey, 'base64');
    if (this.key.length !== 32) throw new Error('BAILEYS_AUTH_KEY must be a base64-encoded 32-byte key.');
  }

  encrypt(value: unknown, context: string): string {
    const nonce = randomBytes(12);
    const cipher = createCipheriv('aes-256-gcm', this.key, nonce);
    cipher.setAAD(Buffer.from(context));
    const bytes = Buffer.concat([cipher.update(JSON.stringify(value, BufferJSON.replacer)), cipher.final()]);
    return Buffer.concat([nonce, cipher.getAuthTag(), bytes]).toString('base64');
  }

  decrypt<T>(value: string, context: string): T {
    const bytes = Buffer.from(value, 'base64');
    const decipher = createDecipheriv('aes-256-gcm', this.key, bytes.subarray(0, 12));
    decipher.setAAD(Buffer.from(context));
    decipher.setAuthTag(bytes.subarray(12, 28));
    return JSON.parse(Buffer.concat([decipher.update(bytes.subarray(28)), decipher.final()]).toString(), BufferJSON.reviver) as T;
  }

  private async read<T>(accountId: string, key: string): Promise<T | undefined> {
    await this.writes.get(accountId);
    const result = await this.pool.query<{ Ciphertext: string }>(
      'SELECT "Ciphertext" FROM live_support_baileys_auth WHERE "AccountId"=$1 AND "Key"=$2', [accountId, key]);
    return result.rows[0] ? this.decrypt<T>(result.rows[0].Ciphertext, `${accountId}:${key}`) : undefined;
  }

  private persist(accountId: string, entries: [string, unknown][]): Promise<void> {
    const snapshot = entries.map(([key, value]) => [key, value == null ? null : this.encrypt(value, `${accountId}:${key}`)] as const);
    const previous = this.writes.get(accountId) ?? Promise.resolve();
    const pending = previous.catch(() => undefined).then(async () => {
      const client = await this.pool.connect();
      try {
        await client.query('BEGIN');
        for (const [key, encrypted] of snapshot) {
          if (encrypted === null) await client.query('DELETE FROM live_support_baileys_auth WHERE "AccountId"=$1 AND "Key"=$2', [accountId, key]);
          else await client.query(`INSERT INTO live_support_baileys_auth ("Id","AccountId","Key","Ciphertext","CreatedAt")
            VALUES ($1,$2,$3,$4,NOW()) ON CONFLICT ("AccountId","Key") DO UPDATE SET "Ciphertext"=EXCLUDED."Ciphertext","UpdatedAt"=NOW()`,
          [randomUUID(), accountId, key, encrypted]);
        }
        await client.query('COMMIT');
      } catch (error) { await client.query('ROLLBACK'); throw error; }
      finally { client.release(); }
    });
    this.writes.set(accountId, pending);
    return pending.finally(() => { if (this.writes.get(accountId) === pending) this.writes.delete(accountId); });
  }

  async auth(accountId: string): Promise<{ state: AuthenticationState; saveCreds: () => Promise<void> }> {
    const creds = await this.read<AuthenticationCreds>(accountId, 'creds') ?? initAuthCreds();
    const state: AuthenticationState = {
      creds,
      keys: {
        get: async <T extends keyof SignalDataTypeMap>(type: T, ids: string[]) => {
          const values: { [id: string]: SignalDataTypeMap[T] } = {};
          for (const id of ids) {
            const value = await this.read<SignalDataTypeMap[T]>(accountId, `${type}:${id}`);
            if (value !== undefined) values[id] = type === 'app-state-sync-key'
              ? proto.Message.AppStateSyncKeyData.fromObject(value as unknown as Record<string, unknown>) as unknown as SignalDataTypeMap[T] : value;
          }
          return values;
        },
        set: async values => {
          const updates: [string, unknown][] = [];
          for (const [type, entries] of Object.entries(values))
            for (const [id, value] of Object.entries(entries ?? {})) updates.push([`${type}:${id}`, value]);
          await this.persist(accountId, updates);
        },
      },
    };
    return { state, saveCreds: () => this.persist(accountId, [['creds', creds]]) };
  }

  async clear(accountId: string): Promise<void> {
    await this.writes.get(accountId);
    await this.pool.query('DELETE FROM live_support_baileys_auth WHERE "AccountId"=$1', [accountId]);
  }

  async enqueue(accountId: string, payload: unknown): Promise<void> {
    const id = randomUUID();
    await this.pool.query(`INSERT INTO live_support_baileys_callbacks ("Id","AccountId","Ciphertext","Attempts","NextAttemptAt","CreatedAt")
      VALUES ($1,$2,$3,0,NOW(),NOW())`, [id, accountId, this.encrypt(payload, `callback:${id}`)]);
  }

  async deliverCallbacks(url: string, token: string): Promise<void> {
    const result = await this.pool.query<{ Id: string; Ciphertext: string }>(
      'SELECT "Id","Ciphertext" FROM live_support_baileys_callbacks WHERE "NextAttemptAt"<=NOW() ORDER BY "CreatedAt" LIMIT 20');
    for (const row of result.rows) {
      try {
        const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Baileys-Token': token },
          body: JSON.stringify(this.decrypt(row.Ciphertext, `callback:${row.Id}`), BufferJSON.replacer), signal: AbortSignal.timeout(20_000) });
        await response.body?.cancel();
        if (!response.ok) throw new Error('Callback rejected');
        await this.pool.query('DELETE FROM live_support_baileys_callbacks WHERE "Id"=$1', [row.Id]);
      } catch {
        // Retain the encrypted payload until the backend acknowledges it; message IDs deduplicate retries.
        await this.pool.query(`UPDATE live_support_baileys_callbacks SET "Attempts"="Attempts"+1,
          "NextAttemptAt"=NOW()+INTERVAL '30 seconds' WHERE "Id"=$1`, [row.Id]);
      }
    }
  }
}
