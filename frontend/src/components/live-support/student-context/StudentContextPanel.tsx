'use client';

import { useEffect, useState } from 'react';
import { Search, UserRound, Wallet, MonitorSmartphone, BookOpenCheck, Trophy, StickyNote } from 'lucide-react';
import { liveSupportService, type LiveSupportConversation, type LiveSupportStudentContext, type LiveSupportStudentSearchResult } from '@/services/live-support-service';
import { StudentActionsPanel } from './StudentActionsPanel';

export function StudentContextPanel({ conversation, onConversationChange }: { conversation: LiveSupportConversation; onConversationChange: (value: LiveSupportConversation) => void }) {
  const [context, setContext] = useState<LiveSupportStudentContext>();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<LiveSupportStudentSearchResult[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    setContext(undefined); setResults([]); setError('');
    if (conversation.linkedStudentUserId) void liveSupportService.getStudentContext(conversation.id).then(setContext).catch(() => setError('تعذر تحميل بيانات الطالب.'));
  }, [conversation.id, conversation.linkedStudentUserId]);

  async function search() {
    if (query.trim().length < 3) return;
    try { setResults(await liveSupportService.searchStudents(conversation.id, query.trim())); setError(''); }
    catch { setError('اكتب اسمًا أو هاتفًا أو كودًا صحيحًا.'); }
  }

  async function changeLink(studentUserId: string | null) {
    const reason = window.prompt(studentUserId ? 'اكتب سبب ربط هذا الطالب بالمحادثة' : 'اكتب سبب إلغاء ربط الطالب');
    if (!reason?.trim()) return;
    const updated = await liveSupportService.changeStudentLink(conversation.id, studentUserId, reason, conversation.version);
    onConversationChange(updated); setResults([]); if (!studentUserId) setContext(undefined);
  }

  if (!conversation.linkedStudentUserId) return <aside className="space-y-4 border-t border-slate-200 bg-slate-50 p-4 xl:border-r xl:border-t-0"><div><div className="mb-4"><h2 className="font-bold text-slate-900">ربط طالب يدويًا</h2><p className="mt-1 text-xs leading-5 text-slate-500">لا يتم اقتراح حساب من رقم الزائر تلقائيًا. ابحث ثم أكّد الربط.</p></div><div className="flex gap-2"><input value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={(event) => event.key === 'Enter' && void search()} placeholder="الاسم، الهاتف، أو الكود" className="h-10 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 text-sm"/><button onClick={() => void search()} aria-label="بحث" className="grid size-10 place-items-center rounded-xl bg-slate-900 text-white"><Search size={17}/></button></div>{error && <p className="mt-2 text-xs text-red-600">{error}</p>}<div className="mt-3 space-y-2">{results.map((student) => <button key={student.userId} onClick={() => void changeLink(student.userId)} className="w-full rounded-xl border border-slate-200 bg-white p-3 text-right hover:border-cyan-600"><p className="text-sm font-semibold text-slate-900">{student.fullName}</p><p className="mt-1 text-xs text-slate-500">{student.maskedPhone}{student.studentCode ? ` · ${student.studentCode}` : ''}</p></button>)}</div></div><StudentActionsPanel conversationId={conversation.id} hasStudent={false} onCompleted={() => window.location.reload()}/></aside>;

  return <aside className="max-h-[620px] space-y-3 overflow-y-auto border-t border-slate-200 bg-slate-50 p-4 xl:border-r xl:border-t-0"><div className="mb-3 flex items-center justify-between"><h2 className="font-bold text-slate-900">بيانات الطالب</h2><button onClick={() => void changeLink(null)} className="text-xs font-semibold text-red-600">إلغاء الربط</button></div>{!context ? <p className="text-sm text-slate-500">جارٍ تحميل البيانات…</p> : <div className="space-y-3"><Card icon={UserRound} title={context.fullName}><p>{context.phoneNumber}</p><p>{context.studentCode || 'بدون كود'} · {context.isActive ? 'نشط' : 'موقوف'}</p><p>{context.gradeLevel || 'المرحلة غير مكتملة'}{context.schoolName ? ` · ${context.schoolName}` : ''}</p></Card><div className="grid grid-cols-2 gap-2"><Metric icon={Wallet} label="الرصيد" value={`${context.balance} ج.م`}/><Metric icon={Trophy} label="النقاط" value={String(context.points)}/><Metric icon={BookOpenCheck} label="الامتحانات" value={String(context.examAttempts)}/><Metric icon={MonitorSmartphone} label="الأجهزة" value={String(context.devices.length)}/></div><Card icon={BookOpenCheck} title="الدراسة والمتابعة"><p>صلاحيات/باقات: {context.grants.length}</p><p>سجلات مشاهدة: {context.watchEvents}</p><p>واجبات: {context.homeworkSubmissions}</p></Card><Card icon={MonitorSmartphone} title="الأجهزة">{context.devices.length ? context.devices.map((device) => <p key={device.id}>{device.type || 'جهاز'} · {device.os || 'نظام غير معروف'} · {device.isActive ? 'نشط' : 'غير نشط'}</p>) : <p>لا توجد أجهزة</p>}</Card><Card icon={StickyNote} title="الملاحظات">{context.notes.length ? context.notes.map((note) => <p key={note.id}>{note.isPinned ? '📌 ' : ''}{note.content}</p>) : <p>لا توجد ملاحظات</p>}</Card><Card icon={UserRound} title="CRM"><p>{context.crmStatus || 'غير مسند'} · {context.crmPriority || 'بدون أولوية'}</p></Card><StudentActionsPanel conversationId={conversation.id} hasStudent onCompleted={() => void liveSupportService.getStudentContext(conversation.id).then(setContext)}/></div>}</aside>;
}

function Card({ icon: Icon, title, children }: { icon: typeof UserRound; title: string; children: React.ReactNode }) { return <section className="rounded-2xl border border-slate-200 bg-white p-3"><h3 className="mb-2 flex items-center gap-2 text-sm font-bold text-slate-900"><Icon size={16}/>{title}</h3><div className="space-y-1 text-xs leading-5 text-slate-600">{children}</div></section>; }
function Metric({ icon: Icon, label, value }: { icon: typeof UserRound; label: string; value: string }) { return <div className="rounded-xl border border-slate-200 bg-white p-3"><Icon size={16} className="text-cyan-700"/><p className="mt-2 text-xs text-slate-500">{label}</p><p className="font-bold text-slate-900">{value}</p></div>; }
