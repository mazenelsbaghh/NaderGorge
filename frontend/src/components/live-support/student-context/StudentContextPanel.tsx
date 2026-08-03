'use client';

import { useEffect, useState } from 'react';
import { Search, UserRound, Wallet, MonitorSmartphone, BookOpenCheck, Trophy, StickyNote, ChevronDown, ChevronUp, AlertCircle, RefreshCw, History, MessageSquareText } from 'lucide-react';
import { liveSupportService, type LiveSupportConversation, type LiveSupportMessage, type LiveSupportStudentContextSectionKey, type LiveSupportStudentContextSections, type LiveSupportStudentSearchResult, type LiveSupportStudentSupportHistory } from '@/services/live-support-service';
import { StudentActionsPanel } from './StudentActionsPanel';
import { getEducationStageLabel, getGradeLevelLabel } from '@/lib/academic-labels';
import { formatCairoDateTime } from '@/lib/cairo-time';

export function StudentContextPanel({ conversation, onConversationChange }: { conversation: LiveSupportConversation; onConversationChange: (value: LiveSupportConversation) => void }) {
  const [sections, setSections] = useState<Partial<LiveSupportStudentContextSections>>({});
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<LiveSupportStudentSearchResult[]>([]);
  const [error, setError] = useState('');
  const [loadingSection, setLoadingSection] = useState<LiveSupportStudentContextSectionKey>();
  const [sectionErrors, setSectionErrors] = useState<Partial<Record<LiveSupportStudentContextSectionKey, string>>>({});
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});

  useEffect(() => {
    setSections({});
    setResults([]);
    setError('');
    setLoadingSection(undefined);
    setSectionErrors({});
    setExpandedSections({});
  }, [conversation.id, conversation.linkedStudentUserId]);

  async function loadSection<K extends LiveSupportStudentContextSectionKey>(section: K) {
    if (!conversation.linkedStudentUserId) return;
    setLoadingSection(section);
    setSectionErrors(current => ({ ...current, [section]: undefined }));
    try {
      const sectionData = await liveSupportService.getStudentContextSection(conversation.id, section);
      setSections(current => ({ ...current, [section]: sectionData }));
    } catch {
      setSectionErrors(current => ({ ...current, [section]: 'تعذر تحميل هذا القسم. حاول مرة أخرى.' }));
    } finally {
      setLoadingSection(undefined);
    }
  }

  function refreshExpandedSections() {
    (Object.keys(expandedSections) as LiveSupportStudentContextSectionKey[])
      .filter(section => expandedSections[section])
      .forEach(section => void loadSection(section));
  }

  async function refreshConversationAfterAction() {
    if (conversation.linkedStudentUserId) {
      refreshExpandedSections();
      return;
    }

    try {
      const bootstrap = await liveSupportService.getStaffBootstrap();
      const updatedConversation = bootstrap.conversations.find((item) => item.id === conversation.id);
      if (updatedConversation) onConversationChange(updatedConversation);
    } catch {
      setError('تم تنفيذ الإجراء، لكن تعذر تحديث بيانات المحادثة. أعد فتحها من القائمة.');
    }
  }

  async function search() {
    if (query.trim().length < 3) return;
    try {
      setResults(await liveSupportService.searchStudents(conversation.id, query.trim()));
      setError('');
    } catch {
      setError('اكتب اسمًا أو هاتفًا أو كودًا صحيحًا.');
    }
  }

  async function changeLink(studentUserId: string | null) {
    const reason = window.prompt(studentUserId ? 'اكتب سبب ربط هذا الطالب بالمحادثة' : 'اكتب سبب إلغاء ربط الطالب');
    if (!reason?.trim()) return;
    try {
      const updated = await liveSupportService.changeStudentLink(conversation.id, studentUserId, reason, conversation.version);
      onConversationChange(updated);
      setResults([]);
      if (!studentUserId) setSections({});
    } catch (cause) {
      alert((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تغيير الربط.');
    }
  }

  const toggleSection = (key: LiveSupportStudentContextSectionKey) => {
    const nextExpanded = !expandedSections[key];
    setExpandedSections({ ...expandedSections, [key]: nextExpanded });
    if (nextExpanded && !sections[key] && loadingSection !== key) {
      void loadSection(key);
    }
  };

  const renderSectionHeader = (key: LiveSupportStudentContextSectionKey, title: string, Icon: typeof UserRound) => {
    const expanded = expandedSections[key];
    return (
      <button
        onClick={() => toggleSection(key)}
        className="flex w-full items-center justify-between rounded-xl border border-slate-200 bg-white p-3 text-right hover:bg-slate-50 transition-colors"
      >
        <span className="flex items-center gap-2 text-sm font-bold text-slate-900">
          <Icon size={16} className="text-slate-500" />
          {title}
        </span>
        {expanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
      </button>
    );
  };

  const renderSkeleton = () => (
    <div className="space-y-2 p-2">
      <div className="h-4 w-3/4 animate-pulse rounded bg-slate-200" />
      <div className="h-4 w-1/2 animate-pulse rounded bg-slate-200" />
      <div className="h-4 w-5/6 animate-pulse rounded bg-slate-200" />
    </div>
  );

  if (!conversation.linkedStudentUserId) {
    return (
      <aside className="space-y-4 border-t border-slate-200 bg-slate-50 p-4 xl:border-r xl:border-t-0">
        <div>
          <div className="mb-4">
            <h2 className="font-bold text-slate-900">ربط طالب يدويًا</h2>
            <p className="mt-1 text-xs leading-5 text-slate-500">لا يتم اقتراح حساب من رقم الزائر تلقائيًا. ابحث ثم أكّد الربط.</p>
          </div>
          <div className="flex gap-2">
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && void search()}
              placeholder="الاسم، الهاتف، أو الكود"
              className="h-10 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 text-sm"
            />
            <button onClick={() => void search()} aria-label="بحث" className="grid size-10 place-items-center rounded-xl bg-slate-900 text-white">
              <Search size={17} />
            </button>
          </div>
          {error && <p className="mt-2 text-xs text-red-600">{error}</p>}
          <div className="mt-3 space-y-2">
            {results.map((student) => (
              <button key={student.userId} onClick={() => void changeLink(student.userId)} className="w-full rounded-xl border border-slate-200 bg-white p-3 text-right hover:border-cyan-600">
                <p className="text-sm font-semibold text-slate-900">{student.fullName}</p>
                <p className="mt-1 text-xs text-slate-500">{student.maskedPhone}{student.studentCode ? ` · ${student.studentCode}` : ''}</p>
              </button>
            ))}
          </div>
        </div>
        <StudentActionsPanel conversationId={conversation.id} hasStudent={false} onCompleted={() => void refreshConversationAfterAction()} />
      </aside>
    );
  }

  return (
    <aside className="max-h-[620px] space-y-3 overflow-y-auto border-t border-slate-200 bg-slate-50 p-4 xl:border-r xl:border-t-0">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-bold text-slate-900">بيانات الطالب</h2>
        <div className="flex gap-2">
          {conversation.linkedStudentUserId && (
            <button onClick={refreshExpandedSections} disabled={Boolean(loadingSection)} title="تحديث" className="p-1 text-slate-500 hover:text-slate-800">
              <RefreshCw size={15} className={loadingSection ? 'animate-spin' : ''} />
            </button>
          )}
          <button onClick={() => void changeLink(null)} className="text-xs font-semibold text-red-600">إلغاء الربط</button>
        </div>
      </div>

      <div className="space-y-2">
        <StudentSupportHistory conversation={conversation} />

        {/* Basic Info Section */}
        <div className="space-y-1">
          {renderSectionHeader('basic', 'الملف الشخصي', UserRound)}
          {expandedSections['basic'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loadingSection === 'basic' && renderSkeleton()}
              {sectionErrors.basic && <SectionError message={sectionErrors.basic} onRetry={() => void loadSection('basic')} />}
              {sections.basic && (
                <>
                  <p className="font-bold text-slate-900 text-sm mb-1">{sections.basic.fullName}</p>
                  <p>الهاتف: {sections.basic.phoneNumber}</p>
                  <p>كود الطالب: {sections.basic.studentCode || 'بدون كود'}</p>
                  <p>الحالة: {sections.basic.isActive ? 'نشط' : 'موقوف'}</p>
                  <p>المرحلة: {getEducationStageLabel(sections.basic.educationStage)} · {getGradeLevelLabel(sections.basic.gradeLevel)}</p>
                  <p>المحافظة: {sections.basic.governorate || 'غير محددة'}</p>
                  <p>المدرسة: {sections.basic.schoolName || 'غير محددة'}</p>
                </>
              )}
            </div>
          )}
        </div>

        {/* Metrics Section */}
        <div className="space-y-1">
          {renderSectionHeader('metrics', 'المؤشرات المالية والتعليمية', Wallet)}
          {expandedSections['metrics'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3">
              {loadingSection === 'metrics' && renderSkeleton()}
              {sectionErrors.metrics && <SectionError message={sectionErrors.metrics} onRetry={() => void loadSection('metrics')} />}
              {sections.metrics && (
                <div className="grid grid-cols-2 gap-2">
                  <Metric icon={Wallet} label="الرصيد الحالي" value={formatMetric(sections.metrics.balance, ' ج.م')} />
                  <Metric icon={Trophy} label="نقاط الطالب" value={formatMetric(sections.metrics.points)} />
                  <Metric icon={BookOpenCheck} label="محاولات الامتحانات" value={formatMetric(sections.metrics.examAttempts)} />
                  <Metric icon={MonitorSmartphone} label="الأجهزة المسجلة" value={formatMetric(sections.metrics.devicesCount)} />
                </div>
              )}
            </div>
          )}
        </div>

        {/* Study History Section */}
        <div className="space-y-1">
          {renderSectionHeader('study', 'الدراسة والمتابعة', BookOpenCheck)}
          {expandedSections['study'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loadingSection === 'study' && renderSkeleton()}
              {sectionErrors.study && <SectionError message={sectionErrors.study} onRetry={() => void loadSection('study')} />}
              {sections.study && (
                <>
                  <p>الباقات والاشتراكات النشطة: {sections.study.activeGrants}</p>
                  <p>سجلات مشاهدة الفيديوهات: {sections.study.watchEvents}</p>
                  <p>تسليمات الواجبات: {sections.study.homeworkSubmissions}</p>
                </>
              )}
            </div>
          )}
        </div>

        {/* Devices Section */}
        <div className="space-y-1">
          {renderSectionHeader('devices', 'الأجهزة المتصلة', MonitorSmartphone)}
          {expandedSections['devices'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loadingSection === 'devices' && renderSkeleton()}
              {sectionErrors.devices && <SectionError message={sectionErrors.devices} onRetry={() => void loadSection('devices')} />}
              {sections.devices && (
                sections.devices.devices.length ? (
                  <div className="space-y-2">
                    {sections.devices.devices.map((device) => (
                      <div key={device.id} className="border-b border-slate-100 pb-1 last:border-0 last:pb-0">
                        <p className="font-semibold text-slate-800">{device.name || 'جهاز'}</p>
                        <p className="text-[10px] text-slate-500">
                          نظام: {device.os || 'غير معروف'} · متصفح: {device.browser || 'غير معروف'} · آخر ظهور: {formatCairoDateTime(device.lastUsedAt, { dateStyle: 'short' })}
                        </p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-slate-400 text-center py-2">لا توجد أجهزة نشطة حالياً</p>
                )
              )}
            </div>
          )}
        </div>

        {/* Notes Section */}
        <div className="space-y-1">
          {renderSectionHeader('notes', 'ملاحظات الموظفين', StickyNote)}
          {expandedSections['notes'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loadingSection === 'notes' && renderSkeleton()}
              {sectionErrors.notes && <SectionError message={sectionErrors.notes} onRetry={() => void loadSection('notes')} />}
              {sections.notes && (
                sections.notes.notes.length ? (
                  <div className="space-y-2">
                    {sections.notes.notes.map((note) => (
                      <div key={note.id} className="border-b border-slate-100 pb-1 last:border-0 last:pb-0">
                        <p className="text-slate-800">
                          {note.isPinned ? '📌 ' : ''}
                          {note.content}
                        </p>
                        <p className="text-[10px] text-slate-400 mt-1">
                          {formatCairoDateTime(note.createdAt)}
                        </p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-slate-400 text-center py-2">لا توجد ملاحظات على هذا الطالب</p>
                )
              )}
            </div>
          )}
        </div>

        {/* CRM Section */}
        <div className="space-y-1">
          {renderSectionHeader('crm', 'إدارة العلاقات CRM', UserRound)}
          {expandedSections['crm'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loadingSection === 'crm' && renderSkeleton()}
              {sectionErrors.crm && <SectionError message={sectionErrors.crm} onRetry={() => void loadSection('crm')} />}
              {sections.crm && (
                <>
                  <p>حالة العميل: {sections.crm.status || 'غير مسند'}</p>
                  <p>الأولوية الحالية: {sections.crm.priority || 'بدون أولوية'}</p>
                </>
              )}
            </div>
          )}
        </div>
      </div>

      <StudentActionsPanel
        conversationId={conversation.id}
        hasStudent
        onCompleted={refreshExpandedSections}
      />
    </aside>
  );
}

function StudentSupportHistory({ conversation }: { conversation: LiveSupportConversation }) {
  const [items, setItems] = useState<LiveSupportStudentSupportHistory[]>([]);
  const [selectedHistory, setSelectedHistory] = useState<LiveSupportStudentSupportHistory>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!conversation.linkedStudentUserId) return;
    const controller = new AbortController();
    setLoading(true);
    setError('');
    setItems([]);
    setSelectedHistory(undefined);
    setMessages([]);
    liveSupportService.getStudentSupportHistory(conversation.id, controller.signal)
      .then(setItems)
      .catch((cause) => { if (!isAbortError(cause)) setError('تعذر تحميل سجل دعم الطالب.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [conversation.id, conversation.linkedStudentUserId]);

  async function openHistory(item: LiveSupportStudentSupportHistory) {
    if (selectedHistory?.conversationId === item.conversationId) {
      setSelectedHistory(undefined);
      setMessages([]);
      return;
    }
    setSelectedHistory(item);
    setMessages([]);
    setMessagesLoading(true);
    setError('');
    try {
      setMessages(await liveSupportService.getStudentHistoryMessages(conversation.id, item.conversationId));
    } catch {
      setError('تعذر تحميل رسائل هذه المحادثة.');
    } finally {
      setMessagesLoading(false);
    }
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white" aria-label="سجل دعم الطالب">
      <div className="flex items-start gap-2 border-b border-slate-100 px-3 py-3">
        <History size={17} className="mt-0.5 shrink-0 text-cyan-700" />
        <div>
          <h3 className="text-sm font-bold text-slate-900">سجل دعم الطالب</h3>
          <p className="mt-0.5 text-[11px] leading-4 text-slate-500">كل المحادثات السابقة، بما فيها المغلقة، والإجراءات المسجلة فيها.</p>
        </div>
      </div>
      <div className="max-h-52 divide-y divide-slate-100 overflow-y-auto">
        {loading && <div className="space-y-2 p-3"><div className="h-4 w-3/4 animate-pulse rounded bg-slate-200" /><div className="h-4 w-1/2 animate-pulse rounded bg-slate-200" /></div>}
        {!loading && !error && items.length === 0 && <p className="p-3 text-center text-xs text-slate-500">لا توجد محادثات سابقة لهذا الطالب.</p>}
        {items.map((item) => <button key={item.conversationId} type="button" onClick={() => void openHistory(item)} aria-expanded={selectedHistory?.conversationId === item.conversationId} className={`w-full px-3 py-2.5 text-right transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-700/30 ${selectedHistory?.conversationId === item.conversationId ? 'bg-cyan-50/70' : ''}`}>
          <span className="flex items-center justify-between gap-2"><span className="min-w-0 truncate text-xs font-bold text-slate-800">{item.subject || 'محادثة دعم'}</span><span className={`shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold ${historyStatusClass(item.status)}`}>{historyStatusLabel(item.status)}</span></span>
          <span className="mt-1 flex items-center justify-between gap-2 text-[10px] text-slate-500"><span>{formatCairoDateTime(item.lastActivityAt)}</span><span>{item.messageCount} رسالة{item.lastEventType ? ` · ${historyEventLabel(item.lastEventType)}` : ''}</span></span>
          {item.lastMessagePreview && <span className="mt-1 block truncate text-[11px] text-slate-600">{item.lastMessagePreview}</span>}
        </button>)}
      </div>
      {error && <div role="alert" className="p-3 text-xs text-red-700">{error}</div>}
      {selectedHistory && <div className="border-t border-slate-100 bg-slate-50 p-3">
        <div className="mb-2 flex items-center gap-1.5 text-xs font-bold text-slate-800"><MessageSquareText size={14} />تفاصيل: {selectedHistory.subject || 'محادثة دعم'}</div>
        {selectedHistory.activities.length > 0 && <ol className="mb-3 space-y-1 border-b border-slate-200 pb-3 text-[11px] text-slate-600">{selectedHistory.activities.map((activity, index) => <li key={`${activity.at}-${index}`} className="flex items-center justify-between gap-2"><span>{historyEventLabel(activity.type)}</span><time>{formatCairoDateTime(activity.at)}</time></li>)}</ol>}
        <div className="max-h-64 space-y-2 overflow-y-auto" aria-live="polite">
          {messagesLoading && <p className="text-xs text-slate-500">جارٍ تحميل الرسائل…</p>}
          {!messagesLoading && messages.length === 0 && <p className="text-xs text-slate-500">لا توجد رسائل في هذه المحادثة.</p>}
          {messages.map((message) => <article key={message.id} dir="auto" className={`rounded-lg px-2.5 py-2 text-xs ${['Staff', 'Admin', 'AI', 'System'].includes(message.senderType) ? 'mr-4 bg-slate-200 text-slate-800' : 'ml-4 bg-white text-slate-800'}`}><p className="whitespace-pre-wrap break-words">{message.content}</p><p className="mt-1 text-[10px] text-slate-500">{historySenderLabel(message.senderType)} · {formatCairoDateTime(message.sentAt)}</p></article>)}
        </div>
      </div>}
    </section>
  );
}

function historyStatusLabel(status: LiveSupportStudentSupportHistory['status']) {
  return ({ Waiting: 'بانتظار الدعم', Assigned: 'مسندة', Active: 'نشطة', Closed: 'مغلقة', Abandoned: 'منتهية' } as const)[status];
}

function historyStatusClass(status: LiveSupportStudentSupportHistory['status']) {
  return status === 'Closed' || status === 'Abandoned' ? 'bg-slate-100 text-slate-600' : status === 'Active' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800';
}

function historyEventLabel(eventType: string) {
  return ({
    ConversationCreated: 'تم فتح المحادثة', QueueEntered: 'دخلت قائمة الانتظار', Assigned: 'تم إسناد المحادثة', FirstStaffResponse: 'تم الرد لأول مرة',
    TransferRequested: 'طُلب التحويل', Transferred: 'تم التحويل', StaffDisconnected: 'انقطع الموظف', StaffReconnected: 'عاد الموظف',
    StudentLinked: 'تم ربط الطالب', StudentUnlinked: 'ألغي ربط الطالب', StudentLinkReplaced: 'تم تغيير الطالب المرتبط',
    ActionRequested: 'طُلب إجراء على الحساب', ActionSucceeded: 'تم الإجراء على الحساب', ActionFailed: 'لم يكتمل الإجراء',
    Closed: 'تم الإغلاق', Abandoned: 'أنهى الطالب المحادثة', RatingSubmitted: 'أضاف الطالب تقييمًا', AdminIntervened: 'تدخلت الإدارة',
    AIActionProposed: 'اقترح المساعد إجراءً', AIActionSucceeded: 'نفذ المساعد إجراءً', AIActionFailed: 'لم يكتمل إجراء المساعد',
    AIHandoffCompleted: 'تم تحويلها للدعم البشري', AIResolved: 'حلّها المساعد', AIAutoClosed: 'أغلقها المساعد تلقائيًا',
  } as Record<string, string>)[eventType] ?? 'نشاط في المحادثة';
}

function historySenderLabel(senderType: LiveSupportMessage['senderType']) {
  return ({ Student: 'الطالب', Guest: 'الزائر', Staff: 'الدعم', Admin: 'الإدارة', System: 'النظام', AI: 'المساعد الذكي' } as const)[senderType];
}

function isAbortError(cause: unknown) {
  return (typeof DOMException !== 'undefined' && cause instanceof DOMException && cause.name === 'AbortError')
    || (typeof cause === 'object' && cause !== null && 'code' in cause && (cause as { code?: string }).code === 'ERR_CANCELED');
}

function SectionError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return <div role="alert" className="rounded-xl bg-red-50 p-3 text-xs text-red-700"><span className="flex items-center gap-2"><AlertCircle size={14} />{message}</span><button onClick={onRetry} className="mt-2 font-bold underline">إعادة المحاولة</button></div>;
}

function Metric({ icon: Icon, label, value }: { icon: typeof UserRound; label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <Icon size={16} className="text-cyan-700" />
      <p className="mt-2 text-xs text-slate-500">{label}</p>
      <p className="font-bold text-slate-900">{value}</p>
    </div>
  );
}

function formatMetric(value: number | null | undefined, suffix = ''): string {
  return value == null ? '—' : `${value}${suffix}`;
}
