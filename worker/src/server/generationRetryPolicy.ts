export const DIRECT_GENERATION_RETRY_DISABLED = 'DIRECT_GENERATION_RETRY_DISABLED';

export function directGenerationRetryDenied(logicalJobId: string) {
  return {
    statusCode: 409,
    body: {
      id: logicalJobId,
      success: false,
      code: DIRECT_GENERATION_RETRY_DISABLED,
      message: 'Direct retry is disabled. Start a new AI generation run through the backend.',
    },
  } as const;
}
