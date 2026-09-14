import {
  GRADE_LEVEL_LABELS,
  GRADES_BY_STAGE,
  STAGE_OPTIONS,
  STUDY_TRACK_LABELS,
} from '@/lib/academic-labels';

type ActionFieldOption = { value: string; label: string };
export type ActionField = { key: string; label: string; type: 'text' | 'number' | 'password' | 'checkbox' | 'date' | 'datetime' | 'select'; required?: boolean; options?: ActionFieldOption[] };

const gradeOptions = Object.values(GRADES_BY_STAGE)
  .flatMap((groups) => groups)
  .flatMap((group) => group.grades)
  .filter((grade, index, grades) => grades.findIndex((candidate) => candidate.value === grade.value) === index);

const studyTrackOptions = Object.entries(STUDY_TRACK_LABELS).map(([value, label]) => ({ value, label }));

export const studentActionFields: Record<string, ActionField[]> = {
  'student.profile.update': [
    { key: 'fullName', label: 'الاسم الكامل', type: 'text' },
    { key: 'phone', label: 'رقم الهاتف', type: 'text' },
    { key: 'secondaryPhone', label: 'هاتف الطالب الإضافي', type: 'text' },
    { key: 'studentCode', label: 'كود الطالب', type: 'text' },
    { key: 'nationality', label: 'الجنسية', type: 'text' },
    { key: 'dateOfBirth', label: 'تاريخ ميلاد الطالب', type: 'date' },
    { key: 'gender', label: 'النوع', type: 'select', options: [{ value: 'Male', label: 'ذكر' }, { value: 'Female', label: 'أنثى' }] },
    { key: 'parentPhone', label: 'هاتف الأب / ولي الأمر', type: 'text' },
    { key: 'secondaryParentPhone', label: 'هاتف ولي أمر إضافي', type: 'text' },
    { key: 'motherPhone', label: 'هاتف الأم', type: 'text' },
    { key: 'fatherDateOfBirth', label: 'تاريخ ميلاد الأب', type: 'date' },
    { key: 'motherDateOfBirth', label: 'تاريخ ميلاد الأم', type: 'date' },
    { key: 'isFatherAlive', label: 'الأب على قيد الحياة', type: 'checkbox' },
    { key: 'isMotherAlive', label: 'الأم على قيد الحياة', type: 'checkbox' },
    { key: 'educationStage', label: 'المرحلة الدراسية', type: 'select', options: STAGE_OPTIONS },
    { key: 'gradeLevel', label: 'الصف الدراسي', type: 'select', options: gradeOptions.map((grade) => ({ value: grade.value, label: GRADE_LEVEL_LABELS[grade.value] ?? grade.label })) },
    { key: 'studyTrack', label: 'الشعبة / التخصص', type: 'select', options: studyTrackOptions },
    { key: 'schoolType', label: 'نوع المدرسة', type: 'select', options: [{ value: 'Government', label: 'حكومية' }, { value: 'Language', label: 'لغات' }, { value: 'Experimental', label: 'تجريبية' }, { value: 'Private', label: 'خاصة' }, { value: 'Azhari', label: 'أزهرية' }, { value: 'American', label: 'أمريكية' }] },
    { key: 'schoolName', label: 'اسم المدرسة', type: 'text' },
    { key: 'governorate', label: 'المحافظة', type: 'text' },
    { key: 'district', label: 'المنطقة / الحي', type: 'text' },
    { key: 'address', label: 'العنوان التفصيلي', type: 'text' },
  ],
  'student.password.reset': [{ key: 'newPassword', label: 'كلمة السر الجديدة', type: 'password', required: true }],
  'student.account.status.set': [{ key: 'isActive', label: 'الحساب نشط', type: 'checkbox' }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.note.add': [{ key: 'content', label: 'الملاحظة', type: 'text', required: true }, { key: 'isPinned', label: 'تثبيت', type: 'checkbox' }],
  'student.note.delete': [{ key: 'noteId', label: 'الملاحظة', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.device.disconnect': [{ key: 'deviceId', label: 'الجهاز', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.devices.disconnect-all': [{ key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.package.cancel': [{ key: 'accessGrantId', label: 'الباقة أو الاشتراك', type: 'text', required: true }, { key: 'refundBalance', label: 'رد القيمة للرصيد', type: 'checkbox' }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.balance.adjust': [{ key: 'amount', label: 'المبلغ (+ أو -)', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.gamification.adjust': [{ key: 'points', label: 'النقاط (+ أو -)', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.video.override.add': [{ key: 'videoId', label: 'الفيديو المشترك فيه', type: 'text', required: true }, { key: 'addedViews', label: 'مشاهدات إضافية', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch.reset': [{ key: 'lessonVideoId', label: 'الفيديو المشترك فيه', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch.count.set': [{ key: 'lessonVideoId', label: 'الفيديو المشترك فيه', type: 'text', required: true }, { key: 'newWatchCount', label: 'العدد الجديد', type: 'number', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.watch-request.approve': [{ key: 'requestId', label: 'طلب المشاهدة', type: 'text', required: true }, { key: 'addedViews', label: 'المشاهدات المضافة', type: 'number' }, { key: 'reason', label: 'ملاحظة', type: 'text' }],
  'student.watch-request.reject': [{ key: 'requestId', label: 'طلب المشاهدة', type: 'text', required: true }, { key: 'reason', label: 'سبب الرفض', type: 'text', required: true }],
  'student.lesson.unlock': [{ key: 'lessonId', label: 'الدرس', type: 'text', required: true }, { key: 'reason', label: 'السبب', type: 'text', required: true }],
  'student.crm.assign': [{ key: 'assignedAgentId', label: 'موظف CRM (اختياري)', type: 'text' }, { key: 'priority', label: 'الأولوية', type: 'select', required: true, options: [{ value: 'Low', label: 'منخفضة' }, { value: 'Medium', label: 'متوسطة' }, { value: 'High', label: 'عالية' }, { value: 'Urgent', label: 'عاجلة' }] }, { key: 'notes', label: 'ملاحظات', type: 'text' }],
  'student.crm.call.add': [{ key: 'outcome', label: 'نتيجة المكالمة', type: 'select', required: true, options: [{ value: 'NoAnswer', label: 'لا يوجد رد' }, { value: 'Answered', label: 'تم الرد' }, { value: 'FollowUp', label: 'يحتاج متابعة' }, { value: 'Closed', label: 'مغلقة' }] }, { key: 'notes', label: 'ملاحظات', type: 'text' }, { key: 'nextFollowUpDate', label: 'المتابعة القادمة', type: 'datetime' }],
  'student.create-and-link': [{ key: 'fullName', label: 'الاسم', type: 'text', required: true }, { key: 'phoneNumber', label: 'الهاتف', type: 'text', required: true }, { key: 'password', label: 'كلمة السر', type: 'password', required: true }, { key: 'reason', label: 'سبب الإنشاء والربط', type: 'text', required: true }],
};
