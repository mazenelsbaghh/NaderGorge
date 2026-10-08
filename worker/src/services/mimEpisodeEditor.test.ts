import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { concatenateMimScenes, probeMimVideo, renderMimScene } from './mimEpisodeEditor.js';
import { isPublicMediaAddress, publicMediaUrl } from './mimVideoDownload.js';

const exec = promisify(execFile);
test('editing accepts only HTTPS media and public addresses', () => {
  for (const url of ['http://cdn.example/movie.mp4', 'file:///tmp/movie.mp4', 'https://user:pass@cdn.example/movie.mp4', 'https://cdn.example:8443/movie.mp4'])
    assert.throws(() => publicMediaUrl(url));
  for (const address of ['127.0.0.1', '10.1.2.3', '169.254.169.254', '192.168.1.2', '100.64.0.1', '::1', '::ffff:127.0.0.1', 'fe80::1', '2001:db8::1'])
    assert.equal(isPublicMediaAddress(address), false, address);
  for (const address of ['8.8.8.8', '2606:4700:4700::1111']) assert.equal(isPublicMediaAddress(address), true, address);
});

test('real montage preserves 30 seconds per scene, fades at the join, and includes audio', { timeout: 180_000 }, async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'mim-edit-test-'));
  try {
    const source = path.join(directory, 'source.mp4');
    await exec('ffmpeg', ['-v', 'error', '-f', 'lavfi', '-i', 'color=c=white:s=160x90:r=30:d=30',
      '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000:duration=30', '-c:v', 'libx264', '-preset', 'ultrafast', '-c:a', 'aac', '-t', '30', source]);
    const clips = [path.join(directory, 'scene-0.mp4'), path.join(directory, 'scene-1.mp4')];
    for (let i = 0; i < clips.length; i++) await renderMimScene(source, clips[i]!, i, clips.length);
    const final = path.join(directory, 'episode.mp4');
    await concatenateMimScenes(clips, final);
    const media = await probeMimVideo(final);
    assert.ok(Math.abs(media.duration - 60) < 0.15);
    assert.equal(media.hasAudio, true);
    const pixel = async (time: string) => {
      const output = await exec('ffmpeg', ['-v', 'error', '-ss', time, '-i', final, '-frames:v', '1', '-vf', 'scale=1:1', '-pix_fmt', 'gray', '-f', 'rawvideo', 'pipe:1'], { encoding: 'buffer' });
      return output.stdout[0]!;
    };
    assert.ok(await pixel('15') > 220);
    assert.ok(await pixel('29.96') < 40);
    assert.ok(await pixel('30.02') < 40);
    assert.ok(await pixel('45') > 220);
    const short = path.join(directory, 'short.mp4');
    await exec('ffmpeg', ['-v', 'error', '-i', source, '-t', '2', '-c', 'copy', short]);
    await assert.rejects(renderMimScene(short, path.join(directory, 'invalid.mp4'), 0, 1), /MIM_SCENE_MUST_BE_30_SECONDS/);
  } finally { await fs.rm(directory, { recursive: true, force: true }); }
});
