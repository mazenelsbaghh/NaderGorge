import assert from 'node:assert/strict';
import test from 'node:test';
import { createLiveSupportRecording } from './live-support-recording.ts';

test('staff recordings use the actual container for the upload MIME type and filename', async () => {
  const cases = [
    { extension: 'webm', declaredType: 'audio/mp4', bytes: [0x1a, 0x45, 0xdf, 0xa3, 0x9f] },
    { extension: 'ogg', declaredType: '', bytes: Array.from(Buffer.from('OggS\x00\x02')) },
    { extension: 'mp4', declaredType: 'audio/webm;codecs=opus', bytes: Array.from(Buffer.from('\x00\x00\x00\x20ftypisom')) },
  ];

  for (const { extension, declaredType, bytes } of cases) {
    const audio = Uint8Array.from(bytes);
    const chunks = [new Blob([audio.slice(0, 5)], { type: declaredType }), new Blob([audio.slice(5)])];
    const file = await createLiveSupportRecording(chunks);

    assert.ok(file);
    assert.equal(file.type, `audio/${extension}`);
    assert.ok(file.name.endsWith(`.${extension}`));
    assert.deepEqual(new Uint8Array(await file.arrayBuffer()), audio);
  }
});

test('missing and headerless recordings are rejected before upload', async () => {
  assert.equal(await createLiveSupportRecording([]), undefined);
  assert.equal(await createLiveSupportRecording([new Blob(['\x1f\x43\xb6\x75'], { type: 'audio/webm' })]), undefined);
});
