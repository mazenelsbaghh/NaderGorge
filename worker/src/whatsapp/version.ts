import { fetchLatestWaWebVersion, type WAVersion } from '@whiskeysockets/baileys';

export async function currentWhatsAppVersion(): Promise<WAVersion> {
  const resolved = await fetchLatestWaWebVersion({ signal: AbortSignal.timeout(8_000) });
  // The bundled revision can be rejected with 405 before WhatsApp issues a QR.
  if (!resolved.isLatest) throw new Error('Could not resolve the current WhatsApp Web version.');
  return resolved.version;
}
