(function (root) {
  'use strict';
  const maxPngBytes = 32 * 1024 * 1024;

  function pngChunks(bytes) {
    const chunks = [];
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    let offset = 8;
    while (offset + 12 <= bytes.length) {
      const length = view.getUint32(offset);
      if (length > maxPngBytes || offset + length + 12 > bytes.length) throw new Error('Truncated VCDN PNG');
      const type = String.fromCharCode(...bytes.subarray(offset + 4, offset + 8));
      chunks.push({ type, body: bytes.subarray(offset + 8, offset + 8 + length) });
      offset += length + 12;
      if (type === 'IEND') return { chunks, tail: bytes.subarray(offset) };
    }
    throw new Error('VCDN PNG missing IEND');
  }

  async function inflateRows(compressed, expectedLength) {
    const reader = new Blob([compressed]).stream().pipeThrough(new DecompressionStream('deflate')).getReader();
    const rows = new Uint8Array(expectedLength);
    let offset = 0;
    try {
      for (;;) {
        const chunk = await reader.read();
        if (chunk.done) break;
        if (offset + chunk.value.length > rows.length) throw new Error('VCDN PNG exceeds declared dimensions');
        rows.set(chunk.value, offset); offset += chunk.value.length;
      }
      if (offset !== rows.length) throw new Error('VCDN PNG inflate length mismatch');
      return rows;
    } finally { await reader.cancel(); }
  }

  function filterPredictor(filter, left, above, upperLeft) {
    if (filter === 0) return 0;
    if (filter === 1) return left;
    if (filter === 2) return above;
    if (filter === 3) return Math.floor((left + above) / 2);
    if (filter !== 4) throw new Error('Unsupported VCDN PNG filter');
    const prediction = left + above - upperLeft;
    const distances = [Math.abs(prediction - left), Math.abs(prediction - above), Math.abs(prediction - upperLeft)];
    return distances[0] <= distances[1] && distances[0] <= distances[2] ? left : distances[1] <= distances[2] ? above : upperLeft;
  }

  function unfilterRows(rows, width, height) {
    const stride = width * 3;
    const pixels = new Uint8Array(stride * height);
    for (let row = 0; row < height; row++) {
      const filter = rows[row * (stride + 1)];
      for (let column = 0; column < stride; column++) {
        const position = row * stride + column;
        const left = column >= 3 ? pixels[position - 3] : 0;
        const above = row > 0 ? pixels[position - stride] : 0;
        const upperLeft = row > 0 && column >= 3 ? pixels[position - stride - 3] : 0;
        pixels[position] = rows[row * (stride + 1) + column + 1] + filterPredictor(filter, left, above, upperLeft);
      }
    }
    return pixels;
  }

  async function pixelPayload(chunks) {
    const header = chunks.find(chunk => chunk.type === 'IHDR')?.body;
    if (!header || header.length !== 13 || header[8] !== 8 || header[9] !== 2 || header[12] !== 0) throw new Error('VCDN pixel carrier requires non-interlaced RGB8');
    const view = new DataView(header.buffer, header.byteOffset, header.byteLength);
    const width = view.getUint32(0), height = view.getUint32(4);
    const expectedLength = (width * 3 + 1) * height;
    if (!width || !height || expectedLength > maxPngBytes) throw new Error('Invalid VCDN PNG dimensions');
    const parts = chunks.filter(chunk => chunk.type === 'IDAT').map(chunk => chunk.body);
    const compressed = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0));
    let offset = 0;
    for (const part of parts) { compressed.set(part, offset); offset += part.length; }
    return unfilterRows(await inflateRows(compressed, expectedLength), width, height);
  }

  function normalizeTransportStream(bytes, expectedLength) {
    if (expectedLength !== undefined) {
      if (!Number.isSafeInteger(expectedLength) || expectedLength <= 0 || expectedLength > bytes.length) throw new Error('Invalid VCDN TS length');
      bytes = bytes.subarray(0, expectedLength);
    } else {
      let packetEnd = 0;
      while (packetEnd + 188 <= bytes.length && bytes[packetEnd] === 0x47) packetEnd += 188;
      bytes = bytes.subarray(0, packetEnd);
    }
    if (!bytes.length || bytes.length % 188 !== 0 || bytes[0] !== 0x47) throw new Error('Invalid VCDN transport stream');
    return bytes;
  }

  function transportSection(bytes, packet, tableId) {
    if (!(bytes[packet + 1] & 64) || !(bytes[packet + 3] & 16)) return null;
    let start = packet + 4;
    if (bytes[packet + 3] & 32) start += 1 + bytes[start];
    if (start >= packet + 188) return null;
    start += 1 + bytes[start];
    if (start + 3 > packet + 188 || bytes[start] !== tableId) return null;
    const end = start + 3 + ((bytes[start + 1] & 15) << 8) + bytes[start + 2];
    // An incomplete PSI table must never permit a byte correction in an unknown elementary stream.
    return end <= packet + 188 ? bytes.subarray(start, end) : null;
  }

  function programMapPids(bytes) {
    const programPids = new Set();
    for (let packet = 0; packet + 188 <= bytes.length; packet += 188) {
      if (((bytes[packet + 1] & 31) << 8 | bytes[packet + 2]) !== 0) continue;
      const table = transportSection(bytes, packet, 0);
      if (!table) continue;
      for (let entry = 8; entry + 4 <= table.length - 4; entry += 4) {
        if (table[entry] || table[entry + 1]) programPids.add((table[entry + 2] & 31) << 8 | table[entry + 3]);
      }
    }
    return programPids;
  }

  function aacPacketPids(bytes) {
    const programPids = programMapPids(bytes), audioPids = new Set();
    for (let packet = 0; packet + 188 <= bytes.length; packet += 188) {
      if (!programPids.has((bytes[packet + 1] & 31) << 8 | bytes[packet + 2])) continue;
      const table = transportSection(bytes, packet, 2);
      if (!table || table.length < 16) continue;
      for (let entry = 12 + ((table[10] & 15) << 8 | table[11]); entry + 5 <= table.length - 4;) {
        if (table[entry] === 15) audioPids.add((table[entry + 1] & 31) << 8 | table[entry + 2]);
        entry += 5 + ((table[entry + 3] & 15) << 8 | table[entry + 4]);
      }
    }
    return audioPids;
  }

  function repairProviderAudio(bytes) {
    const audioPids = aacPacketPids(bytes);
    // The provider's whole-buffer ADTS scan can match H.264 bytes and corrupt a video frame.
    for (let offset = 0; offset + 7 < bytes.length; offset++) {
      if (bytes[offset] !== 255 || (bytes[offset + 1] & 246) !== 240) continue;
      const packet = Math.floor(offset / 188) * 188;
      if (!audioPids.has((bytes[packet + 1] & 31) << 8 | bytes[packet + 2]) || offset + 7 >= packet + 188) continue;
      const frameLength = ((bytes[offset + 3] & 3) << 11) | (bytes[offset + 4] << 3) | (bytes[offset + 5] >> 5);
      const next = offset + frameLength;
      if (frameLength < 7 || frameLength > 8192 || next + 1 >= bytes.length || bytes[next] !== 255 || (bytes[next + 1] & 246) !== 240) continue;
      if ((bytes[offset + 2] & 192) === 0) bytes[offset + 2] |= 64;
      offset = next - 1;
    }
    return bytes;
  }

  async function decodeSegment(payload, options = {}) {
    const bytes = new Uint8Array(payload);
    if (bytes.length < 8 || ![137, 80, 78, 71, 13, 10, 26, 10].every((octet, index) => bytes[index] === octet)) {
      return !options.encrypted && bytes[0] === 0x47 ? repairProviderAudio(bytes).buffer : payload;
    }
    const image = pngChunks(bytes);
    let tail = image.tail;
    if (options.encrypted) {
      if (!tail.length) throw new Error('Encrypted VCDN carrier missing ciphertext');
      for (let padding = 0; padding < 16; padding++) {
        if (padding > 0 && tail[padding - 1] !== 255) break;
        if ((tail.length - padding) % 16 === 0) return tail.slice(padding).buffer;
      }
      throw new Error('Invalid VCDN ciphertext alignment');
    }
    let padding = 0;
    while (padding < tail.length && tail[padding] === 255) padding++;
    const decoded = tail[padding] === 0x47 ? tail.subarray(padding) : await pixelPayload(image.chunks);
    return repairProviderAudio(normalizeTransportStream(decoded, options.expectedLength)).slice().buffer;
  }

  function resourceUrl(candidate, source) {
    const url = new URL(candidate), master = new URL(source);
    if (url.protocol !== 'https:' || url.port || url.username || url.password || url.hash) return null;
    if (url.hostname === 'edge.vcdn.me') return /^\/seg\/[A-Za-z0-9_-]{16,2048}$/.test(url.pathname) && !url.search ? url.href : null;
    if (!['cdn.vcdn.me', 'embed.vcdn.me'].includes(url.hostname)) return null;
    const rootPath = master.pathname.slice(0, master.pathname.lastIndexOf('/') + 1);
    const suffix = url.pathname.slice(rootPath.length);
    if (!url.pathname.startsWith(rootPath) || !/^(?:[A-Za-z0-9_-]+\/)*(?:[A-Za-z0-9_-]+\.m3u8|[A-Za-z0-9_-]+\.key)$/.test(suffix)) return null;
    url.search = master.search;
    return url.href;
  }

  function rememberLengths(text, playlistUrl, lengths) {
    const lines = text.split(/\r?\n/);
    let expectedLength;
    for (const line of lines) {
      const marker = line.trim().match(/^#\s*TTAM-TS-LEN:\s*(\d+)\s*$/i);
      if (marker) expectedLength = Number(marker[1]);
      else if (line.trim() && !line.startsWith('#')) {
        if (expectedLength !== undefined) lengths.set(new URL(line.trim(), playlistUrl).href, expectedLength);
        expectedLength = undefined;
      }
    }
  }

  function create() {
    const lengths = new Map();
    function callbacks(context, original) {
      return { ...original, onSuccess(response, stats, loadedContext, networkDetails) {
        if (typeof response.data === 'string') rememberLengths(response.data, context.url, lengths);
        if (!context.frag || context.keyInfo) return original.onSuccess(response, stats, loadedContext, networkDetails);
        void decodeSegment(response.data, { encrypted: context.frag.encrypted, expectedLength: lengths.get(context.url) })
          .then(decoded => original.onSuccess({ ...response, data: decoded }, stats, loadedContext, networkDetails))
          .catch(error => original.onError({ code: 422, text: error.message }, loadedContext, networkDetails, stats));
      } };
    }
    return { callbacks, resourceUrl };
  }

  const transport = { create, decodeSegment, resourceUrl };
  if (typeof module === 'object' && module.exports) module.exports = transport;
  else root.MassarVcdnTransport = transport;
})(typeof window === 'object' ? window : globalThis);
