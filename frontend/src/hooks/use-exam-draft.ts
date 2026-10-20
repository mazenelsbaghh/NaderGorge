'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { examService, type ActiveExamAttemptDto } from '@/services/exam-service';
import { ExamDraftSession, type DraftCache, type DraftView } from '@/lib/exam-draft-session';
import { createClientId } from '@/lib/client-id';

function readAnswers(encoded: string | null): Record<string, string> {
  if (!encoded) return {};
  const parsed: unknown = JSON.parse(encoded);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)
    || Object.values(parsed).some(answer => typeof answer !== 'string')) throw new SyntaxError('Invalid exam draft');
  return parsed as Record<string, string>;
}

function readCache(key: string, assigned: Set<string>): DraftCache | null {
  const encoded = localStorage.getItem('exam_draft_sync_' + key);
  const legacy = readAnswers(localStorage.getItem('exam_answers_' + key));
  const keepAssigned = (answers: Record<string, string>) => Object.fromEntries(Object.entries(answers).filter(([id]) => assigned.has(id)));
  if (!encoded) return { answers: keepAssigned(legacy), confirmedAnswers: {}, version: 0 };
  const cached = JSON.parse(encoded) as DraftCache;
  if (!Number.isSafeInteger(cached.version)) throw new SyntaxError('Invalid exam draft version');
  return { version: cached.version, answers: keepAssigned(readAnswers(JSON.stringify(cached.answers))),
    confirmedAnswers: keepAssigned(readAnswers(JSON.stringify(cached.confirmedAnswers))) };
}

export function useExamDraft(examId: string, attempt: ActiveExamAttemptDto) {
  const [view, setView] = useState<DraftView>({ answers: {}, ready: false, status: 'loading' });
  const [localWarning, setLocalWarning] = useState(false);
  const session = useRef<ExamDraftSession | null>(null);
  const retry = useRef<() => void>(() => {});
  const key = `${attempt.attemptId}${attempt.revisionId ? `_${attempt.revisionId}` : ''}`;
  const assignedIds = attempt.questions.map(question => question.id).join(',');

  useEffect(() => {
    let active = true;
    let restored = false;
    setView({ answers: {}, ready: false, status: 'loading' });
    let cached: DraftCache | null = null;
    try { cached = readCache(key, new Set(assignedIds.split(','))); }
    catch (error) {
      if (!(error instanceof DOMException) && !(error instanceof SyntaxError)) throw error;
      setLocalWarning(true);
    }
    const current = new ExamDraftSession({
      read: async () => (await examService.getDraft(examId, attempt.attemptId, attempt.revisionId)).data.data,
      write: async draft => (await examService.saveDraft(examId, attempt.attemptId, { ...draft, revisionId: attempt.revisionId })).data.data,
      mutationId: createClientId,
    }, {
      changed: next => { if (active) { restored = next.ready; setView(next); } },
      cache: next => {
        if (!active) return;
        cached = next;
        try {
          localStorage.setItem('exam_draft_sync_' + key, JSON.stringify(next));
          localStorage.setItem('exam_answers_' + key, JSON.stringify(next.answers));
        } catch (error) {
          if (!(error instanceof DOMException)) throw error;
          setLocalWarning(true);
        }
      },
    });
    session.current = current;
    // Session publishes the failure state; background retries must not reject into the page.
    const run = () => { void (restored ? current.flush() : current.load(cached)).catch(() => {}); };
    retry.current = run;
    run();
    window.addEventListener('online', run);
    const visibility = () => { if (document.visibilityState === 'hidden' && restored) run(); };
    document.addEventListener('visibilitychange', visibility);
    return () => {
      active = false;
      current.finish();
      window.removeEventListener('online', run);
      document.removeEventListener('visibilitychange', visibility);
    };
  }, [examId, attempt.attemptId, attempt.revisionId, key, assignedIds]);

  useEffect(() => {
    if (!['pending', 'offline'].includes(view.status)) return;
    if (view.status === 'offline') {
      const timer = window.setInterval(() => retry.current(), 5000);
      return () => window.clearInterval(timer);
    }
    const timer = window.setTimeout(() => retry.current(), 800);
    return () => window.clearTimeout(timer);
  }, [view.status]);

  useEffect(() => {
    if (!['pending', 'saving', 'offline', 'conflict'].includes(view.status)) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [view.status]);

  const finish = useCallback(() => {
    session.current?.finish();
    try {
      localStorage.removeItem('exam_answers_' + key);
      localStorage.removeItem('exam_draft_sync_' + key);
      localStorage.removeItem('exam_audio_answers_' + key);
    } catch (error) { if (!(error instanceof DOMException)) throw error; }
  }, [key]);

  return { ...view, localWarning, finish,
    edit: (question: string, answer: string) => session.current?.edit(question, answer),
    flush: () => session.current!.flush(), retry: () => retry.current(),
    refresh: () => session.current!.refresh(),
    useServerAnswers: () => session.current?.useServerAnswers(), keepPageAnswers: () => session.current?.keepPageAnswers(),
  };
}
