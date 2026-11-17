import type { AssessmentDefinition, RevisionPolicy } from '@/services/assessment-revision-service';
import type { OcrQuestionDraft } from './OcrQuestionImport';

export interface AssessmentAuthoringDraft {
  definition: AssessmentDefinition;
  policy: RevisionPolicy;
  subjectId: string;
  ocrQuestions: OcrQuestionDraft[];
}

export interface StoredAssessmentDraft {
  baseDefinition: string;
  draft: AssessmentAuthoringDraft;
}

export function readAssessmentDraft(key: string): StoredAssessmentDraft | null {
  try {
    const raw = window.sessionStorage.getItem(key);
    return raw ? JSON.parse(raw) as StoredAssessmentDraft : null;
  } catch {
    return null;
  }
}

export function writeAssessmentDraft(key: string, draft: StoredAssessmentDraft | null): boolean {
  try {
    if (draft) window.sessionStorage.setItem(key, JSON.stringify(draft));
    else window.sessionStorage.removeItem(key);
    return true;
  } catch {
    return false;
  }
}
