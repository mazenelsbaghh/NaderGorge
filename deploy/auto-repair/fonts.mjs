// Cache actual font assets at image-build time; verification serves them on loopback only.
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';

const directory = '/opt/cache/fonts';
const stylesheetUrl = 'https://fonts.googleapis.com/css2?family=Tajawal:wght@400;500;700;800;900&display=swap';
const port = 17788;

async function prepare() {
  await mkdir(directory, { recursive: true });
  const response = await fetch(stylesheetUrl, {
    headers: { 'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36' },
    signal: AbortSignal.timeout(30000),
  });
  if (!response.ok) throw new Error('Font stylesheet download failed');
  let css = await response.text();
  const urls = [...new Set([...css.matchAll(/url\((https:[^)]+)\)/g)].map(match => match[1]))];
  if (!urls.length) throw new Error('Font stylesheet contains no assets');
  for (const url of urls) {
    const parsed = new URL(url);
    if (parsed.hostname !== 'fonts.gstatic.com' || !parsed.pathname.endsWith('.woff2')) throw new Error('Unexpected font source');
    const asset = await fetch(url, { signal: AbortSignal.timeout(30000) });
    if (!asset.ok) throw new Error('Font asset download failed');
    const bytes = Buffer.from(await asset.arrayBuffer());
    if (bytes.subarray(0, 4).toString() !== 'wOF2') throw new Error('Invalid WOFF2 asset');
    const name = createHash('sha256').update(bytes).digest('hex') + '.woff2';
    await writeFile(directory + '/' + name, bytes);
    css = css.replaceAll(url, `http://127.0.0.1:${port}/${name}`);
  }
  await writeFile(directory + '/responses.cjs', 'module.exports = ' + JSON.stringify({ [stylesheetUrl]: css }) + ';\n');
  console.log('Cached actual Tajawal styles and ' + urls.length + ' font assets');
}

if (process.argv[2] === '--prepare') {
  await prepare();
} else if (process.argv[2] === '--serve') {
  createServer(async (request, response) => {
    if (request.method !== 'GET' || !/^\/[a-f0-9]{64}\.woff2$/.test(request.url || '')) {
      response.writeHead(404).end();
      return;
    }
    try {
      const bytes = await readFile(directory + request.url);
      response.writeHead(200, { 'Content-Type': 'font/woff2', 'Content-Length': bytes.length }).end(bytes);
    } catch {
      response.writeHead(404).end();
    }
  }).listen(port, '127.0.0.1');
} else {
  throw new Error('Choose --prepare during image build or --serve during verification');
}
