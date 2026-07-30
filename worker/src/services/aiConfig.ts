export interface AIConfig {
  primaryProvider: 'developer';
  textModel: string;
  imageModel: string;
  developerApiKey: string;
}

function optional(name: string) {
  const value = process.env[name]?.trim();
  return value || undefined;
}

export function readAIConfig(): AIConfig {
  const developerApiKey = optional('GEMINI_API_KEY');
  if (!developerApiKey) throw new Error('[AI config] GEMINI_API_KEY is required.');
  return {
    primaryProvider: 'developer',
    developerApiKey,
    textModel: optional('AI_TEXT_MODEL') || 'gemini-flash-latest',
    imageModel: optional('AI_IMAGE_MODEL') || 'gemini-3-pro-image-preview',
  };
}
