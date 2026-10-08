import fs from 'node:fs/promises';
import path from 'node:path';
import { resolveWithin, sharedStorageRoot } from '../config/storage.js';
import { execFileWithTimeout } from './workerFetch.js';
import { downloadMimVideo, publicMediaUrl } from './mimVideoDownload.js';

export interface MimEpisodeEdit { id: string; urls: string[] }
const editorRoot = path.join(sharedStorageRoot, 'private', 'mim-episodes');
export function episodeFile(id: string) {
  if (!/^[a-f0-9]{64}$/.test(id)) throw new Error('INVALID_MIM_EPISODE_ID');
  return resolveWithin(editorRoot, `${id}.mp4`);
}
export function parseEpisodeEdit(input: unknown): MimEpisodeEdit {
  const edit = input as MimEpisodeEdit;
  if (!edit || !Array.isArray(edit.urls) || edit.urls.length < 1 || edit.urls.length > 20 ||
    edit.urls.some(url => typeof url !== 'string')) throw new Error('INVALID_MIM_EPISODE');
  episodeFile(edit.id);
  for (const url of edit.urls) publicMediaUrl(url);
  return edit;
}
export async function episodeExists(id: string) {
  try { return (await fs.stat(episodeFile(id))).size > 0; }
  catch (error) { if ((error as NodeJS.ErrnoException).code === 'ENOENT') return false; throw error; }
}
export async function probeMimVideo(file: string) {
  const probe = await execFileWithTimeout('ffprobe', ['-v', 'error', '-protocol_whitelist', 'file,pipe', '-show_entries', 'format=duration:stream=codec_type', '-of', 'json', file], 30_000);
  const media = JSON.parse(probe.stdout) as { format: { duration: string }; streams: { codec_type: string }[] };
  const duration = Number(media.format?.duration);
  if (!Number.isFinite(duration) || !media.streams?.some(stream => stream.codec_type === 'video')) throw new Error('INVALID_MIM_MEDIA');
  return { duration, hasAudio: media.streams.some(stream => stream.codec_type === 'audio') };
}
export function sceneFadeFilters(index: number, count: number) {
  const fades = [index > 0 ? 'fade=t=in:st=0:d=0.5' : '', index < count - 1 ? 'fade=t=out:st=29.5:d=0.5' : ''].filter(Boolean);
  return ['scale=1920:1080:force_original_aspect_ratio=decrease', 'pad=1920:1080:(ow-iw)/2:(oh-ih)/2',
    'setsar=1', 'fps=30', 'tpad=stop_mode=clone:stop_duration=1', 'trim=duration=30', 'setpts=PTS-STARTPTS', ...fades].join(',');
}
export async function renderMimScene(source: string, destination: string, index: number, count: number) {
  const probe = await probeMimVideo(source);
  if (Math.abs(probe.duration - 30) > 0.5) throw new Error('MIM_SCENE_MUST_BE_30_SECONDS');
  const inputs = ['-protocol_whitelist', 'file,pipe', '-i', source, ...(!probe.hasAudio ? ['-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo'] : [])];
  await execFileWithTimeout('ffmpeg', ['-hide_banner', '-loglevel', 'error', ...inputs,
    '-map', '0:v:0', '-map', probe.hasAudio ? '0:a:0' : '1:a:0', '-vf', sceneFadeFilters(index, count),
    '-af', 'aresample=48000,apad,atrim=duration=30,asetpts=PTS-STARTPTS', '-t', '30',
    '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '21', '-pix_fmt', 'yuv420p', '-threads', '2',
    '-c:a', 'aac', '-ac', '2', '-ar', '48000', '-b:a', '160k', '-map_metadata', '-1', '-y', destination], 600_000);
}
export async function concatenateMimScenes(clips: string[], destination: string) {
  const manifest = path.join(path.dirname(destination), 'concat.txt');
  await fs.writeFile(manifest, clips.map(file => `file '${path.basename(file)}'`).join('\n'), { mode: 0o600 });
  await execFileWithTimeout('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'concat', '-safe', '1', '-i', manifest,
    '-c', 'copy', '-movflags', '+faststart', '-map_metadata', '-1', '-y', destination], 600_000);
  const probe = await probeMimVideo(destination);
  if (Math.abs(probe.duration - clips.length * 30) > 0.15) throw new Error('INVALID_MIM_EPISODE_DURATION');
}
export async function editMimEpisode(edit: MimEpisodeEdit, progress: (percent: number) => Promise<void>) {
  if (await episodeExists(edit.id)) return;
  await fs.mkdir(editorRoot, { recursive: true });
  const working = await fs.mkdtemp(path.join(editorRoot, '.editing-'));
  const clips: string[] = [];
  let bytes = 0;
  try {
    for (let index = 0; index < edit.urls.length; index++) {
      const source = path.join(working, `source-${index}.mp4`);
      bytes += await downloadMimVideo(edit.urls[index]!, source);
      if (bytes > 1024 * 1024 * 1024) throw new Error('MIM_EPISODE_TOO_LARGE');
      const clip = path.join(working, `scene-${index}.mp4`);
      await renderMimScene(source, clip, index, edit.urls.length);
      await fs.rm(source); clips.push(clip);
      await progress(Math.round((index + 1) / edit.urls.length * 90));
    }
    const destination = path.join(working, 'episode.mp4');
    await concatenateMimScenes(clips, destination);
    await fs.chmod(destination, 0o640);
    await fs.rename(destination, episodeFile(edit.id));
    await progress(100);
  } finally { await fs.rm(working, { recursive: true, force: true }); }
}
