'use client';

import { useEffect, useState } from 'react';
import { Search, UserRound, Wallet, MonitorSmartphone, BookOpenCheck, Trophy, StickyNote, ChevronDown, ChevronUp, AlertCircle, RefreshCw } from 'lucide-react';
import { liveSupportService, type LiveSupportConversation, type LiveSupportStudentContext, type LiveSupportStudentSearchResult } from '@/services/live-support-service';
import { StudentActionsPanel } from './StudentActionsPanel';

export function StudentContextPanel({ conversation, onConversationChange }: { conversation: LiveSupportConversation; onConversationChange: (value: LiveSupportConversation) => void }) {
  const [context, setContext] = useState<LiveSupportStudentContext>();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<LiveSupportStudentSearchResult[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});

  useEffect(() => {
    setContext(undefined);
    setResults([]);
    setError('');
    setLoading(false);
    setExpandedSections({});
  }, [conversation.id, conversation.linkedStudentUserId]);

  async function loadContext() {
    if (!conversation.linkedStudentUserId) return;
    setLoading(true);
    setError('');
    try {
      const data = await liveSupportService.getStudentContext(conversation.id);
      setContext(data);
    } catch {
      setError('تعذر تحميل بيانات الطالب. حاول مرة أخرى.');
    } finally {
      setLoading(false);
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
      if (!studentUserId) setContext(undefined);
    } catch (cause) {
      alert((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تغيير الربط.');
    }
  }

  const toggleSection = (key: string) => {
    const nextExpanded = !expandedSections[key];
    setExpandedSections({ ...expandedSections, [key]: nextExpanded });
    if (nextExpanded && !context && !loading) {
      void loadContext();
    }
  };

  const renderSectionHeader = (key: string, title: string, Icon: typeof UserRound) => {
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
        <StudentActionsPanel conversationId={conversation.id} hasStudent={false} onCompleted={() => window.location.reload()} />
      </aside>
    );
  }

  return (
    <aside className="max-h-[620px] space-y-3 overflow-y-auto border-t border-slate-200 bg-slate-50 p-4 xl:border-r xl:border-t-0">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-bold text-slate-900">بيانات الطالب</h2>
        <div className="flex gap-2">
          {conversation.linkedStudentUserId && (
            <button onClick={loadContext} disabled={loading} title="تحديث" className="p-1 text-slate-500 hover:text-slate-800">
              <RefreshCw size={15} className={loading ? 'animate-spin' : ''} />
            </button>
          )}
          <button onClick={() => void changeLink(null)} className="text-xs font-semibold text-red-600">إلغاء الربط</button>
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-red-100 bg-red-50 p-3 text-red-700">
          <div className="flex items-center gap-2 text-xs font-bold">
            <AlertCircle size={14} />
            {error}
          </div>
          <button onClick={loadContext} className="mt-2 text-xs font-bold underline">إعادة المحاولة</button>
        </div>
      )}

      <div className="space-y-2">
        {/* Basic Info Section */}
        <div className="space-y-1">
          {renderSectionHeader('basic', 'الملف الشخصي', UserRound)}
          {expandedSections['basic'] && (
            <div className="rounded-xl border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-600">
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                <>
                  <p className="font-bold text-slate-900 text-sm mb-1">{context.fullName}</p>
                  <p>الهاتف: {context.phoneNumber}</p>
                  <p>كود الطالب: {context.studentCode || 'بدون كود'}</p>
                  <p>الحالة: {context.isActive ? 'نشط' : 'موقوف'}</p>
                  <p>المرحلة: {context.educationStage || 'غير محددة'} · {context.gradeLevel || 'غير محددة'}</p>
                  <p>المحافظة: {context.governorate || 'غير محددة'}</p>
                  <p>المدرسة: {context.schoolName || 'غير محددة'}</p>
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
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-xs text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                <div className="grid grid-cols-2 gap-2">
                  <Metric icon={Wallet} label="الرصيد الحالي" value={`${context.balance} ج.م`} />
                  <Metric icon={Trophy} label="نقاط الطالب" value={String(context.points)} />
                  <Metric icon={BookOpenCheck} label="محاولات الامتحانات" value={String(context.examAttempts)} />
                  <Metric icon={MonitorSmartphone} label="الأجهزة المسجلة" value={String(context.devices.length)} />
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
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                <>
                  <p>الباقات والاشتراكات النشطة: {context.grants.length}</p>
                  <p>سجلات مشاهدة الفيديوهات: {context.watchEvents}</p>
                  <p>تسليمات الواجبات: {context.homeworkSubmissions}</p>
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
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                context.devices.length ? (
                  <div className="space-y-2">
                    {context.devices.map((device) => (
                      <div key={device.id} className="border-b border-slate-100 pb-1 last:border-0 last:pb-0">
                        <p className="font-semibold text-slate-800">{device.name || 'جهاز'}</p>
                        <p className="text-[10px] text-slate-500">
                          نظام: {device.os || 'غير معروف'} · متصفح: {device.browser || 'غير معروف'} · آخر ظهور: {new Date(device.lastUsedAt).toLocaleDateString('ar-EG')}
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
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                context.notes.length ? (
                  <div className="space-y-2">
                    {context.notes.map((note) => (
                      <div key={note.id} className="border-b border-slate-100 pb-1 last:border-0 last:pb-0">
                        <p className="text-slate-800">
                          {note.isPinned ? '📌 ' : ''}
                          {note.content}
                        </p>
                        <p className="text-[10px] text-slate-400 mt-1">
                          {new Date(note.createdAt).toLocaleString('ar-EG')}
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
              {loading && renderSkeleton()}
              {!loading && !context && <p className="text-slate-400">انقر لتوسيع وتحميل البيانات</p>}
              {!loading && context && (
                <>
                  <p>حالة العميل: {context.crmStatus || 'غير مسند'}</p>
                  <p>الأولوية الحالية: {context.crmPriority || 'بدون أولوية'}</p>
                </>
              )}
            </div>
          )}
        </div>
      </div>

      <StudentActionsPanel
        conversationId={conversation.id}
        hasStudent
        onCompleted={() => void liveSupportService.getStudentContext(conversation.id).then(setContext)}
      />
    </aside>
  );
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
