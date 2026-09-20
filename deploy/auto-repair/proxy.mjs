// Dedicated CONNECT proxy: DNS is resolved and checked before opening the socket.
import http from 'node:http';
import net from 'node:net';
import { lookup } from 'node:dns/promises';

const allowed = new Set(['api.openai.com', 'chatgpt.com', 'auth.openai.com']);
const blocked = new net.BlockList();
for (const [address, prefix] of [['0.0.0.0', 8], ['10.0.0.0', 8], ['100.64.0.0', 10],
  ['127.0.0.0', 8], ['169.254.0.0', 16], ['172.16.0.0', 12], ['192.168.0.0', 16],
  ['192.0.0.0', 24], ['198.18.0.0', 15], ['224.0.0.0', 3]]) blocked.addSubnet(address, prefix);

const server = http.createServer((_, response) => response.writeHead(403).end());
server.on('connect', async (request, client, head) => {
  client.on('error', () => client.destroy());
  client.setTimeout(120_000, () => client.destroy());
  if (!allowed.has(request.url?.split(':')[0]) || !/^[a-z.]+:443$/.test(request.url ?? '')) {
    client.end('HTTP/1.1 403 Forbidden\r\n\r\n');
    return;
  }
  try {
    const answers = await lookup(request.url.split(':')[0], { all: true, family: 4 });
    if (!answers.length || answers.some(({ address }) => blocked.check(address))) {
      client.end('HTTP/1.1 403 Forbidden\r\n\r\n');
      return;
    }
    if (client.destroyed) return;
    const upstream = net.connect({ host: answers[0].address, port: 443 });
    upstream.setTimeout(120_000, () => upstream.destroy());
    upstream.on('error', () => client.destroy());
    client.on('close', () => upstream.destroy());
    upstream.on('close', () => client.destroy());
    upstream.on('connect', () => {
      client.write('HTTP/1.1 200 Connection Established\r\n\r\n');
      if (head.length) upstream.write(head);
      client.pipe(upstream);
      upstream.pipe(client);
    });
  } catch {
    client.end('HTTP/1.1 502 Bad Gateway\r\n\r\n');
  }
});
server.maxConnections = 64;
server.headersTimeout = 10_000;
server.listen(3128, '0.0.0.0');
