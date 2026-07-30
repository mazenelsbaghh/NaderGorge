import crypto from 'crypto';
import path from 'path';
import { Storage, type Bucket } from '@google-cloud/storage';
import type { AIConfig } from './aiConfig.js';
import { classifyAIError } from './aiErrors.js';

export interface TemporaryAnalysisObject {
  uri: string;
  objectName: string;
  generation?: string;
}

export class TemporaryAudioStorage {
  private readonly bucket: Bucket;

  constructor(
    private readonly config: AIConfig,
    storage = new Storage(config.project ? { projectId: config.project } : {}),
  ) {
    if (!config.temporaryBucket) throw new Error('[AI storage] AI_TEMP_GCS_BUCKET is required.');
    this.bucket = storage.bucket(config.temporaryBucket);
  }

  async validateAccess() {
    let metadata;
    try {
      [metadata] = await this.bucket.getMetadata();
    } catch (error) {
      const failure = classifyAIError(error);
      throw new Error(`[AI storage] Temporary bucket access failed (${failure.category}${failure.status ? `:${failure.status}` : ''}).`);
    }
    const rules = metadata.lifecycle?.rule || [];
    const hasDeleteSafetyNet = rules.some((rule) =>
      rule.action?.type === 'Delete' && typeof rule.condition?.age === 'number' && rule.condition.age <= 1,
    );
    if (!hasDeleteSafetyNet) {
      throw new Error('[AI storage] Required 24-hour delete lifecycle rule is missing.');
    }
  }

  async upload(audioPath: string, correlationId = 'job'): Promise<TemporaryAnalysisObject> {
    const safeCorrelation = crypto.createHash('sha256').update(correlationId).digest('hex').slice(0, 16);
    const objectName = `${this.config.temporaryPrefix}/${safeCorrelation}/${crypto.randomUUID()}${path.extname(audioPath) || '.mp3'}`;
    const [file] = await this.bucket.upload(audioPath, {
      destination: objectName,
      metadata: { contentType: 'audio/mpeg', cacheControl: 'no-store' },
      resumable: true,
    });
    let metadata;
    try {
      [metadata] = await file.getMetadata();
    } catch (error) {
      try {
        await this.bucket.file(objectName).delete({ ignoreNotFound: true });
      } catch {
        console.error('[AI storage] Metadata lookup and immediate orphan cleanup both failed; bucket lifecycle remains the safety net.');
      }
      const failure = classifyAIError(error);
      throw new Error(`[AI storage] Uploaded object metadata lookup failed (${failure.category}${failure.status ? `:${failure.status}` : ''}).`);
    }
    return {
      uri: `gs://${this.bucket.name}/${objectName}`,
      objectName,
      ...(metadata.generation ? { generation: String(metadata.generation) } : {}),
    };
  }

  async delete(temporaryObject: TemporaryAnalysisObject) {
    try {
      await this.bucket.file(
        temporaryObject.objectName,
        temporaryObject.generation ? { generation: temporaryObject.generation } : undefined,
      ).delete({ ignoreNotFound: true });
    } catch (error) {
      const failure = classifyAIError(error);
      console.error('[AI storage] Temporary object cleanup failed; bucket lifecycle remains the safety net.', {
        category: failure.category,
        status: failure.status,
      });
    }
  }
}
