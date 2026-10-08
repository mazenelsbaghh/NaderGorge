import type { Express, RequestHandler } from 'express';
import { Queue, Worker, UnrecoverableError } from 'bullmq';
import { redisConnectionOptions } from '../config/redis.js';
import { editMimEpisode, episodeExists, episodeFile, parseEpisodeEdit, type MimEpisodeEdit } from '../services/mimEpisodeEditor.js';
import { logError } from '../logging.js';

const queueName = 'mim-episode-editing';
export function installMimEpisodeRoutes(app: Express, guard: RequestHandler) {
  const queue = new Queue<MimEpisodeEdit>(queueName, { connection: redisConnectionOptions() });
  const worker = new Worker<MimEpisodeEdit>(queueName, async job => {
    try { await editMimEpisode(parseEpisodeEdit(job.data), percent => job.updateProgress(percent)); }
    catch (error) {
      if (error instanceof Error && /^(INVALID_MIM_|MIM_SCENE_MUST|MIM_EPISODE_TOO)/.test(error.message))
        throw new UnrecoverableError(error.message);
      throw error;
    }
  }, { connection: redisConnectionOptions(), concurrency: 1 });
  worker.on('error', () => logError('mim-episode-editor', 'Episode editing worker is unavailable.'));

  const status = async (id: string) => {
    if (await episodeExists(id)) return { state: 'completed', progress: 100 };
    const job = await queue.getJob(id);
    if (!job) return { state: 'not_started', progress: 0 };
    const state = await job.getState();
    return { state: state === 'active' ? 'running' : state === 'failed' ? 'failed' : state === 'completed' ? 'not_started' : 'queued',
      progress: typeof job.progress === 'number' ? job.progress : 0,
      error: state === 'failed' ? job.failedReason === 'MIM_SCENE_MUST_BE_30_SECONDS'
        ? 'فيديو واحد على الأقل مدته مختلفة عن ٣٠ ثانية. راجعه قبل تجميع الحلقة.'
        : 'تعذر إكمال المونتاج. راجع الفيديوهات وحدّث الحالة قبل إعادة المحاولة.' : null };
  };
  app.post('/internal/mim-studio/episodes', guard, async (req, res) => {
    let edit: MimEpisodeEdit;
    try { edit = parseEpisodeEdit(req.body); }
    catch { return res.status(400).json({ error: 'INVALID_MIM_EPISODE' }); }
    if (!await episodeExists(edit.id)) {
      const previous = await queue.getJob(edit.id);
      const previousState = await previous?.getState();
      if (previous && previousState === 'failed') await previous.retry('failed');
      else if (!previous || previousState === 'completed') {
        if (previous) await previous.remove();
        await queue.add('assemble', edit, { jobId: edit.id, attempts: 1,
        removeOnComplete: { age: 7 * 86400, count: 1000 }, removeOnFail: { age: 14 * 86400, count: 500 } });
      }
    }
    return res.json(await status(edit.id));
  });
  app.get('/internal/mim-studio/episodes/:id', guard, async (req, res) => {
    try { episodeFile(String(req.params.id)); }
    catch { return res.status(400).json({ error: 'INVALID_MIM_EPISODE_ID' }); }
    return res.json(await status(String(req.params.id)));
  });
  app.get('/internal/mim-studio/episodes/:id/file', guard, async (req, res) => {
    const id = String(req.params.id);
    try { episodeFile(id); }
    catch { return res.status(400).end(); }
    if (!await episodeExists(id)) return res.status(404).end();
    return res.sendFile(episodeFile(id));
  });
}
