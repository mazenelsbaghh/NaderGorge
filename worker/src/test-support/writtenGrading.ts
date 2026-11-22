import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { TestContext } from 'node:test';

export function gradingRuntime(context: TestContext, scenario: { met: number; total: number; answer: string; feedback: string; open?: boolean }) {
  const directory = mkdtempSync(path.join(os.tmpdir(), 'written-grading-test-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  const criteria = Array.from({ length: scenario.total }, (_, index) => ({ meaning: `Required meaning ${index}`, weight: 1 }));
  return {
    config: { primaryProvider: 'developer' as const, developerApiKey: 'test', textModel: 'test', imageModel: 'test' },
    gradingRubricDirectory: directory,
    developer: { models: { generateContent: async (request: { config: { responseSchema: { properties: Record<string, unknown> } } }) => ({ text: JSON.stringify(
      request.config.responseSchema.properties.openCriteria
        ? { openCriteria: scenario.open ? criteria : [] }
        : request.config.responseSchema.properties.criteria
        ? { criteria }
        : { judgments: Array.from({ length: scenario.total }, (_, index) => ({ criterionId: `c${index + 1}`, met: index < scenario.met, evidence: index < scenario.met ? scenario.answer : '' })), feedback: scenario.feedback },
    ) }) } } as never,
  };
}
