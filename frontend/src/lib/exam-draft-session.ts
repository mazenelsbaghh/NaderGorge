import type { ExamDraftDto, SaveExamDraftRequest } from '../services/exam-service';

export type DraftStatus = 'loading' | 'pending' | 'saving' | 'saved' | 'offline' | 'conflict' | 'closed';
export interface DraftCache {
  answers: Record<string, string>;
  confirmedAnswers: Record<string, string>;
  version: number;
}
export interface DraftView {
  answers: Record<string, string>;
  ready: boolean;
  status: DraftStatus;
  savedAt?: string | null;
  message?: string;
}
interface DraftConnection {
  read(): Promise<ExamDraftDto>;
  write(request: SaveExamDraftRequest): Promise<ExamDraftDto>;
  mutationId(): string;
}
interface DraftCallbacks {
  changed(view: DraftView): void;
  cache(cache: DraftCache): void;
}

export function sameExamAnswers(left: Record<string, string>, right: Record<string, string>) {
  return Object.keys(left).length === Object.keys(right).length
    && Object.entries(left).every(([question, answer]) => right[question] === answer);
}

export class ExamDraftSession {
  private view: DraftView = { answers: {}, ready: false, status: 'loading' };
  private confirmed: ExamDraftDto = { version: 0, answers: {}, isClosed: false };
  private conflicting?: ExamDraftDto;
  private pending?: SaveExamDraftRequest;
  private writing?: Promise<number>;
  private finished = false;

  private connection: DraftConnection;
  private callbacks: DraftCallbacks;
  constructor(connection: DraftConnection, callbacks: DraftCallbacks) {
    this.connection = connection;
    this.callbacks = callbacks;
  }

  async load(cached: DraftCache | null) {
    try {
      const server = await this.connection.read();
      this.confirmed = server;
      const dirty = cached && !sameExamAnswers(cached.answers, cached.confirmedAnswers);
      const conflict = dirty && cached.version !== server.version && !sameExamAnswers(cached.answers, server.answers);
      this.conflicting = conflict ? server : undefined;
      const answers = dirty && !server.isClosed ? cached.answers : server.answers;
      this.view = { answers, ready: true, status: server.isClosed ? 'closed' : conflict ? 'conflict'
        : sameExamAnswers(answers, server.answers) ? 'saved' : 'pending', savedAt: server.savedAt };
      this.publish();
    } catch (error) {
      this.view = { ...this.view, status: 'offline', message: this.errorMessage(error) };
      this.callbacks.changed(this.view);
      throw error;
    }
  }

  edit(question: string, answer: string) {
    if (!this.view.ready || this.finished || this.view.status === 'closed') return;
    const answers = { ...this.view.answers };
    if (answer) answers[question] = answer;
    else delete answers[question];
    this.view = { ...this.view, answers, status: this.conflicting ? 'conflict' : 'pending', message: undefined };
    this.publish();
  }

  refresh() {
    const cached = { answers: this.view.answers, confirmedAnswers: this.confirmed.answers, version: this.confirmed.version };
    this.view = { ...this.view, ready: false, status: 'loading' };
    this.callbacks.changed(this.view);
    return this.load(cached);
  }

  flush(): Promise<number> {
    if (this.writing) return this.writing;
    this.writing = this.saveUntilCurrent().finally(() => { this.writing = undefined; });
    return this.writing;
  }

  private async saveUntilCurrent(): Promise<number> {
    if (!this.view.ready || this.conflicting || this.finished || this.view.status === 'closed')
      throw new Error('راجع حالة حفظ الإجابات قبل التسليم.');
    while (this.confirmed.version === 0 || !sameExamAnswers(this.view.answers, this.confirmed.answers) || this.pending) {
      this.pending ??= { expectedVersion: this.confirmed.version, mutationId: this.connection.mutationId(), answers: { ...this.view.answers } };
      this.view = { ...this.view, status: 'saving', message: undefined };
      this.callbacks.changed(this.view);
      try {
        this.confirmed = await this.connection.write(this.pending);
        this.pending = undefined;
        this.view = { ...this.view, savedAt: this.confirmed.savedAt };
        this.publish();
      } catch (error) {
        this.recordFailure(error);
        throw error;
      }
      if (this.finished) return this.confirmed.version;
    }
    this.view = { ...this.view, status: 'saved', message: undefined };
    this.publish();
    return this.confirmed.version;
  }

  private recordFailure(error: unknown) {
    const response = (error as { response?: { status?: number; data?: { data?: ExamDraftDto; errors?: string[] } } }).response;
    this.conflicting = response?.status === 409 ? response.data?.data : undefined;
    const permanent = response?.status !== undefined && response.status >= 400 && response.status < 500
      && ![408, 429].includes(response.status);
    this.view = { ...this.view, status: this.conflicting ? 'conflict' : permanent ? 'closed' : 'offline', message: this.errorMessage(error) };
    this.publish();
  }

  useServerAnswers() {
    if (!this.conflicting) return;
    this.confirmed = this.conflicting;
    this.conflicting = undefined;
    this.pending = undefined;
    this.view = { ...this.view, answers: this.confirmed.answers, savedAt: this.confirmed.savedAt, status: 'saved', message: undefined };
    this.publish();
  }

  keepPageAnswers() {
    if (!this.conflicting) return;
    this.confirmed = this.conflicting;
    this.conflicting = undefined;
    this.pending = undefined;
    this.view = { ...this.view, status: 'pending', message: undefined };
    this.publish();
  }

  finish() { this.finished = true; }

  private publish() {
    if (this.finished) return;
    // Retain the old base during a conflict so reopening cannot silently replace the server copy.
    this.callbacks.cache({ answers: this.view.answers, confirmedAnswers: this.confirmed.answers,
      version: this.conflicting ? -1 : this.confirmed.version });
    this.callbacks.changed({ ...this.view, answers: { ...this.view.answers } });
  }

  private errorMessage(error: unknown) {
    return (error as { response?: { data?: { message?: string } } }).response?.data?.message
      ?? 'تعذر تأكيد حفظ الإجابات على السيرفر. احتفظ بهذه الصفحة؛ سنعيد المحاولة عند عودة الاتصال.';
  }
}
