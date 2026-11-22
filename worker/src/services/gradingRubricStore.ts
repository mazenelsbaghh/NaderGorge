import { promises as fs } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import path from 'node:path';
import { sharedStorageRoot } from '../config/storage.js';
import { parseRubric, WRITTEN_GRADING_POLICY_VERSION, type WrittenGradingRubric } from './writtenGradingContract.js';

export interface GradingSource { questionText: string; expectedAnswer: string }
export const gradingRubricDirectory = process.env.SHARED_STORAGE_ROOT
  ? path.join(sharedStorageRoot, 'private', `grading-rubrics-v${WRITTEN_GRADING_POLICY_VERSION}`)
  : path.resolve('.tmp', `grading-rubrics-v${WRITTEN_GRADING_POLICY_VERSION}`);

function hasCode(error: unknown, code: string) { return error instanceof Error && 'code' in error && error.code === code; }

export class GradingRubricStore {
  constructor(private readonly directory: string) {}

  async resolve(source: GradingSource, generate: () => Promise<string | undefined>): Promise<WrittenGradingRubric> {
    const fingerprint = createHash('sha256').update(JSON.stringify({ version: WRITTEN_GRADING_POLICY_VERSION, questionText: source.questionText, expectedAnswer: source.expectedAnswer })).digest('hex');
    const filename = path.join(this.directory, `${fingerprint}.json`);
    const existing = await this.read(filename, fingerprint);
    if (existing) return existing;
    const rubric = parseRubric(await generate(), fingerprint);
    await this.publish(filename, rubric);
    const published = await this.read(filename, fingerprint);
    if (!published) throw new Error('Published grading rubric is unavailable.');
    return published;
  }

  private async read(filename: string, fingerprint: string): Promise<WrittenGradingRubric | undefined> {
    let metadata;
    try { metadata = await fs.lstat(filename); }
    catch (error) { if (hasCode(error, 'ENOENT')) return undefined; throw error; }
    if (!metadata.isFile() || metadata.size > 65536) throw new Error('Invalid saved grading rubric.');
    const stored = JSON.parse(await fs.readFile(filename, 'utf8')) as WrittenGradingRubric;
    if (stored.version !== WRITTEN_GRADING_POLICY_VERSION || stored.fingerprint !== fingerprint) throw new Error('Grading rubric identity mismatch.');
    const canonical = parseRubric(JSON.stringify(stored), fingerprint);
    if (stored.criteria.some((criterion, index) => criterion.id !== canonical.criteria[index]?.id)) throw new Error('Invalid saved grading rubric identifiers.');
    return canonical;
  }

  private async publish(filename: string, rubric: WrittenGradingRubric) {
    await fs.mkdir(this.directory, { recursive: true, mode: 0o770 });
    const temporary = `${filename}.${randomUUID()}.tmp`;
    try {
      const file = await fs.open(temporary, 'wx', 0o640);
      try { await file.writeFile(JSON.stringify(rubric)); await file.sync(); }
      finally { await file.close(); }
      // First complete publish wins across processes/nodes; rename would overwrite another worker's rubric.
      try { await fs.link(temporary, filename); }
      catch (error) { if (!hasCode(error, 'EEXIST')) throw error; }
      const directory = await fs.open(this.directory, 'r');
      try { await directory.sync(); } finally { await directory.close(); }
    } finally { await fs.rm(temporary, { force: true }); }
  }
}
