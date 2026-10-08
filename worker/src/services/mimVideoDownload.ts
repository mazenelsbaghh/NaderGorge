import { lookup } from 'node:dns';
import { lookup as lookupAsync } from 'node:dns/promises';
import { BlockList, isIP } from 'node:net';
import { createWriteStream } from 'node:fs';
import { Transform } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { Agent, request } from 'undici';

const denied = new BlockList();
for (const [address, prefix] of [
  ['0.0.0.0', 8], ['10.0.0.0', 8], ['100.64.0.0', 10], ['127.0.0.0', 8], ['169.254.0.0', 16],
  ['172.16.0.0', 12], ['192.0.0.0', 24], ['192.0.2.0', 24], ['192.168.0.0', 16],
  ['198.18.0.0', 15], ['198.51.100.0', 24], ['203.0.113.0', 24], ['224.0.0.0', 4], ['240.0.0.0', 4],
] as const) denied.addSubnet(address, prefix, 'ipv4');
for (const [address, prefix] of [['2001::', 32], ['2001:db8::', 32], ['2002::', 16]] as const)
  denied.addSubnet(address, prefix, 'ipv6');

export function isPublicMediaAddress(address: string) {
  const family = isIP(address);
  if (family === 4) return !denied.check(address, 'ipv4');
  return family === 6 && /^[23][0-9a-f]{3}:/i.test(address) && !denied.check(address, 'ipv6');
}

export function publicMediaUrl(raw: string) {
  const url = new URL(raw);
  if (url.protocol !== 'https:' || url.username || url.password || (url.port && url.port !== '443') || raw.length > 6000)
    throw new Error('INVALID_MIM_MEDIA_URL');
  return url;
}

async function pinnedMediaAgent(url: URL) {
  const hostname = url.hostname.replace(/^\[|\]$/g, '');
  const addresses = await lookupAsync(hostname, { all: true });
  if (!addresses.length || addresses.some(record => !isPublicMediaAddress(record.address)))
    throw new Error('INVALID_MIM_MEDIA_ADDRESS');
  const pinned = addresses.find(record => record.family === 4) ?? addresses[0]!;
  // Pin the vetted address while preserving the URL hostname for TLS verification.
  return new Agent({ connect: { lookup: (_hostname, options, callback) => lookup(pinned.address, options, callback) } });
}

export async function downloadMimVideo(raw: string, destination: string) {
  let url = publicMediaUrl(raw);
  const signal = AbortSignal.timeout(180_000);
  for (let redirects = 0; redirects <= 3; redirects++) {
    const agent = await pinnedMediaAgent(url);
    try {
      const response = await request(url, { dispatcher: agent, signal, headersTimeout: 30_000, bodyTimeout: 30_000 });
      if ([301, 302, 303, 307, 308].includes(response.statusCode)) {
        response.body.destroy();
        const location = response.headers.location;
        if (redirects === 3 || typeof location !== 'string') throw new Error('INVALID_MIM_MEDIA_REDIRECT');
        url = publicMediaUrl(new URL(location, url).toString());
        continue;
      }
      if (response.statusCode !== 200) { response.body.destroy(); throw new Error('MIM_MEDIA_DOWNLOAD_FAILED'); }
      let bytes = 0;
      const limit = new Transform({ transform(chunk: Buffer, _encoding, callback) {
        bytes += chunk.length;
        callback(bytes > 150 * 1024 * 1024 ? new Error('MIM_MEDIA_TOO_LARGE') : null, chunk);
      } });
      await pipeline(response.body, limit, createWriteStream(destination, { flags: 'wx', mode: 0o600 }));
      return bytes;
    } finally { await agent.close(); }
  }
  throw new Error('INVALID_MIM_MEDIA_REDIRECT');
}
