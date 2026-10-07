import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import JSZip from 'jszip';
import { createEpisodeArchive } from './archive.ts';
import { characterReferences, type MimDocument } from './contract.ts';

const episode: MimDocument = JSON.parse(readFileSync(new URL('./prepared-episode.json', import.meta.url), 'utf8'));

test('export contains both original image files and the latest edited storyboard', async context => {
  const images = characterReferences.map(reference => readFileSync(new URL(`../../../public${reference.path}`, import.meta.url)));
  context.mock.method(globalThis, 'fetch', async (path: string) => {
    const index = characterReferences.findIndex(reference => reference.path === path);
    assert.ok(index >= 0);
    return new Response(new Uint8Array(images[index]));
  });
  const document = structuredClone(episode);
  document.scenes[0].shots[0].dialogue = 'ميم: «إيه الحكاية الجديدة دي؟»';
  const archive = await JSZip.loadAsync(await createEpisodeArchive(document));
  for (const [index, reference] of characterReferences.entries()) {
    assert.deepEqual(await archive.file(reference.fileName)!.async('nodebuffer'), images[index]);
  }
  assert.match(await archive.file('episode-script.txt')!.async('string'), /إيه الحكاية الجديدة دي/);
  for (let index = 0; index < 4; index++) {
    const prompt = await archive.file(`scene-${index + 1}-prompt.txt`)!.async('string');
    for (const reference of characterReferences) assert.ok(prompt.includes(reference.fileName));
    if (index === 0) assert.ok(prompt.includes(document.scenes[0].shots[0].dialogue));
  }
});

test('a missing or non-image sheet rejects the whole package', async context => {
  const fetchMock = context.mock.method(globalThis, 'fetch', async () => new Response('', { status: 404 }));
  await assert.rejects(createEpisodeArchive(episode), /تعذر تحميل شيت/);
  fetchMock.mock.mockImplementation(async () => new Response('<html>Not an image</html>'));
  await assert.rejects(createEpisodeArchive(episode), /غير صالح/);
});
