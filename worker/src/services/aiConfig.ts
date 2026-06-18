export type AIPrimaryProvider = 'vertex' | 'developer';

export interface AIConfig {
  primaryProvider: AIPrimaryProvider;
  project?: string | undefined;
  location?: string | undefined;
  temporaryBucket?: string | undefined;
  temporaryPrefix: string;
  textModel: string;
  imageModel: string;
  fallbackApiKey?: string | undefined;
  developerApiKey?: string | undefined;
}

function optional(name: string) {
  const environmentValue = process.env[name]?.trim();
  return environmentValue || undefined;
}

function required(name: string) {
  const environmentValue = optional(name);
  if (!environmentValue) throw new Error(`[AI config] Required setting ${name} is missing.`);
  return environmentValue;
}

function normalizePrefix(prefixValue: string | undefined) {
  const prefix = (prefixValue || 'ai-analysis').replace(/^\/+|\/+$/g, '');
  if (!prefix || prefix.split('/').includes('..')) {
    throw new Error('[AI config] AI_TEMP_GCS_PREFIX must be a safe object prefix.');
  }
  return prefix;
}

export function readAIConfig(): AIConfig {
  const configuredProvider = optional('AI_PRIMARY_PROVIDER') || 'vertex';
  if (configuredProvider !== 'vertex' && configuredProvider !== 'developer') {
    throw new Error('[AI config] AI_PRIMARY_PROVIDER must be vertex or developer.');
  }

  const fallbackApiKey = optional('GEMINI_FALLBACK_API_KEY');
  const developerPrimaryApiKey = optional('GEMINI_API_KEY') || fallbackApiKey;
  const config: AIConfig = {
    primaryProvider: configuredProvider,
    temporaryPrefix: normalizePrefix(optional('AI_TEMP_GCS_PREFIX')),
    textModel: optional('AI_TEXT_MODEL') || 'gemini-2.5-flash',
    imageModel: optional('AI_IMAGE_MODEL') || 'gemini-3-pro-image-preview',
    fallbackApiKey,
    developerApiKey: configuredProvider === 'vertex' ? fallbackApiKey : developerPrimaryApiKey,
  };

  if (configuredProvider === 'vertex') {
    config.project = required('GOOGLE_CLOUD_PROJECT');
    config.location = required('GOOGLE_CLOUD_LOCATION');
    config.temporaryBucket = required('AI_TEMP_GCS_BUCKET');
  } else if (!config.developerApiKey) {
    throw new Error('[AI config] Developer primary mode requires GEMINI_API_KEY or GEMINI_FALLBACK_API_KEY.');
  }

  return config;
}
