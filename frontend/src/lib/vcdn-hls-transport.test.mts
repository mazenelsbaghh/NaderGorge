import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { test } from 'node:test';
import { deflateSync } from 'node:zlib';

const require = createRequire(import.meta.url);
const transport = require('../../public/vendor/vcdn/vcdn-hls-transport.js') as {
  decodeSegment(payload: ArrayBuffer, options?: { expectedLength?: number; encrypted?: boolean }): Promise<ArrayBuffer>;
  resourceUrl(candidate: string, source: string): string | null;
};

function pngChunk(type: string, payload: Buffer = Buffer.alloc(0)): Buffer {
  const chunk = Buffer.alloc(payload.length + 12);
  chunk.writeUInt32BE(payload.length); chunk.write(type, 4, 'ascii'); payload.copy(chunk, 8);
  return chunk;
}

function packetPayload(): Buffer {
  const payload = Buffer.alloc(376);
  payload[0] = payload[188] = 0x47;
  return payload;
}

const pngHeader = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
function arrayBuffer(buffer: Buffer): ArrayBuffer { return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength) as ArrayBuffer; }

test('PNG tail and RGB pixel carriers both produce the complete TS payload, preserving zero bytes', async () => {
  const payload = packetPayload();
  const tailCarrier = Buffer.concat([pngHeader, pngChunk('IEND'), Buffer.from([255, 255]), payload]);
  const header = Buffer.alloc(13); header.writeUInt32BE(126); header.writeUInt32BE(1, 4); header[8] = 8; header[9] = 2;
  const pixels = Buffer.concat([Buffer.from([0]), payload, Buffer.alloc(2)]);
  const pixelCarrier = Buffer.concat([pngHeader, pngChunk('IHDR', header), pngChunk('IDAT', deflateSync(pixels)), pngChunk('IEND')]);
  for (const carrier of [tailCarrier, pixelCarrier]) assert.deepEqual(Buffer.from(await transport.decodeSegment(arrayBuffer(carrier))), payload);
});

test('AES carrier padding is removed without interpreting ciphertext as MPEG-TS', async () => {
  const ciphertext = Buffer.alloc(32, 0x23);
  const carrier = Buffer.concat([pngHeader, pngChunk('IEND'), Buffer.from([255, 255, 255]), ciphertext]);
  assert.deepEqual(Buffer.from(await transport.decodeSegment(arrayBuffer(carrier), { encrypted: true })), ciphertext);
});

function audioAndVideoPackets(): Buffer {
  const payload = Buffer.alloc(188 * 4, 255);
  Buffer.from([71, 64, 0, 16, 0, 0, 176, 13, 0, 1, 193, 0, 0, 0, 1, 240, 0, 0, 0, 0, 0]).copy(payload);
  Buffer.from([71, 80, 0, 16, 0, 2, 176, 23, 0, 1, 193, 0, 0, 225, 0, 240, 0,
    27, 225, 0, 240, 0, 15, 225, 1, 240, 0, 0, 0, 0, 0]).copy(payload, 188);
  for (const [packet, pid] of [[2, 256], [3, 257]]) {
    Buffer.from([71, 64 | (pid >> 8), pid & 255, 16]).copy(payload, packet * 188);
    Buffer.from([255, 241, 0, 0, 1, 0, 0, 0, 255, 241, 0, 0, 1, 0, 0, 0]).copy(payload, packet * 188 + 8);
  }
  return payload;
}

test('AAC correction is confined to the declared audio PID and never changes matching H.264 bytes', async () => {
  const payload = audioAndVideoPackets();
  const unchanged = Buffer.from(await transport.decodeSegment(arrayBuffer(payload), { encrypted: true }));
  assert.deepEqual(unchanged, payload);
  const decoded = Buffer.from(await transport.decodeSegment(arrayBuffer(payload)));
  assert.equal(decoded[188 * 3 + 10], 64);
  assert.deepEqual(decoded.subarray(188 * 2, 188 * 3), payload.subarray(188 * 2, 188 * 3));
});

test('missing or incomplete stream tables leave suspected ADTS headers untouched', async () => {
  const payload = audioAndVideoPackets();
  for (const candidate of [payload.subarray(188 * 2), Buffer.from(payload)]) {
    if (candidate.length === payload.length) candidate[188 + 7] = 255;
    assert.deepEqual(Buffer.from(await transport.decodeSegment(arrayBuffer(candidate))), candidate);
  }
});

test('truncated carrier and excessive dimensions fail instead of passing PNG into the media decoder', async () => {
  const header = Buffer.alloc(13); header.writeUInt32BE(0xffffffff); header.writeUInt32BE(0xffffffff, 4); header[8] = 8; header[9] = 2;
  for (const carrier of [Buffer.concat([pngHeader, Buffer.from([0, 0, 0, 50])]), Buffer.concat([pngHeader, pngChunk('IHDR', header), pngChunk('IEND')])]) {
    await assert.rejects(transport.decodeSegment(arrayBuffer(carrier)), /VCDN/);
  }
});

test('resource scope renews only this video and leaves opaque VCDN edge segments intact', () => {
  const source = 'https://cdn.vcdn.me/stream/11111111-1111-4111-8111-111111111111/providers/p1/master.m3u8?token=fresh';
  const rendition = source.replace('master.m3u8?token=fresh', '720p/playlist.m3u8?token=old').replace('cdn.vcdn.me', 'embed.vcdn.me');
  assert.equal(transport.resourceUrl(rendition, source), rendition.replace('token=old', 'token=fresh'));
  const segment = 'https://edge.vcdn.me/seg/0123456789abcdefghijklmnop';
  assert.equal(transport.resourceUrl(segment, source), segment);
  for (const invalid of [source.replace('11111111-', '22222222-'), source.replace('cdn.vcdn.me', 'attacker.test'), segment + '?redirect=foreign', segment.replace('https:', 'http:')]) {
    assert.equal(transport.resourceUrl(invalid, source), null);
  }
});
