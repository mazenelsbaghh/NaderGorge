export type ActionField = { key: string; label: string; type: 'text' | 'number' | 'password' | 'checkbox' | 'datetime' | 'select'; required?: boolean; options?: string[] };

export const studentActionFields: Record<string, ActionField[]> = {
  'student.profile.update': [{ key: 'fullName', label: 'الاسم', type: 'text' }, { key: 'phone', label: 'الهاتف', type: 'text' }, { key: 'governorate', label: 'المحافظة', type: 'text' }, { key: 'schoolName', label: 'المدرسة', type: 'text' }],
  'student.password.reset': [{ key: 'newPassword', label: 'كلمة السر الجديدة', type: 'password', required: true }],
  'student.account.status.set': [{ key: 'isActive', label: 'الحساب نشط', type: 'checkbox' }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.note.add': [{ key: 'content', label: 'الملاحظة', type: 'text', required: true }, { key: 'isPinned', label: 'تثبيت', type: 'checkbox' }],
  'student.note.delete': [{ key: 'noteId', label: 'معرّف الملاحظة', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.device.disconnect': [{ key: 'deviceId', label: 'معرّف الجهاز', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.devices.disconnect-all': [{ key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.package.cancel': [{ key: 'accessGrantId', label: 'معرّف الباقة', type: 'text', required: true }, { key: 'refundBalance', label: 'رد القيمة للرصيد', type: 'checkbox' }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.balance.adjust': [{ key: 'amount', label: 'المبلغ (+ أو -)', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.gamification.adjust': [{ key: 'points', label: 'النقاط (+ أو -)', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.video.override.add': [{ key: 'videoId', label: 'معرّف الفيديو', type: 'text', required: true }, { key: 'addedViews', label: 'مشاهدات إضافية', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch.reset': [{ key: 'lessonVideoId', label: 'معرّف الفيديو', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch.count.set': [{ key: 'lessonVideoId', label: 'معرّف الفيديو', type: 'text', required: true }, { key: 'newWatchCount', label: 'العدد الجديد', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch-request.approve': [{ key: 'requestId', label: 'معرّف الطلب', type: 'text', required: true }, { key: 'addedViews', label: 'المشاهدات المضافة', type: 'number' }, { key: 'reason', label: 'ملاحظة', type: 'text' }],
  'student.watch-request.reject': [{ key: 'requestId', label: 'معرّف الطلب', type: 'text', required: true }, { key: 'reason', label: 'سبب الرفض', type: 'text', required: true }],
  'student.lesson.unlock': [{ key: 'lessonId', label: 'معرّف الدرس', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.crm.assign': [{ key: 'assignedAgentId', label: 'معرّف الموظف (اختياري)', type: 'text' }, { key: 'priority', label: 'الأولوية', type: 'select', required: true, options: ['Low', 'Medium', 'High', 'Urgent'] }, { key: 'notes', label: 'ملاحظات', type: 'text' }],
  'student.crm.call.add': [{ key: 'outcome', label: 'نتيجة المكالمة', type: 'select', required: true, options: ['NoAnswer', 'Answered', 'FollowUp', 'Closed'] }, { key: 'notes', label: 'ملاحظات', type: 'text' }, { key: 'nextFollowUpDate', label: 'المتابعة القادمة', type: 'datetime' }],
  'student.create-and-link': [{ key: 'fullName', label: 'الاسم', type: 'text', required: true }, { key: 'phoneNumber', label: 'الهاتف', type: 'text', required: true }, { key: 'password', label: 'كلمة السر', type: 'password', required: true }, { key: 'reason', label: 'سبب الإنشاء والربط', type: 'text', required: true }],
};
