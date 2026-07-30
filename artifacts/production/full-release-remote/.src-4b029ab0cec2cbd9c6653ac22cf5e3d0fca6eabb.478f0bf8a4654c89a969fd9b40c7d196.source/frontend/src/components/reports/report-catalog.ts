import {
  Activity,
  BadgeDollarSign,
  BookOpenText,
  ClipboardCheck,
  Coins,
  GraduationCap,
  Headphones,
  KeyRound,
  MessageSquareText,
  ShieldCheck,
  UserRoundCheck,
  Users,
  WalletCards,
  type LucideIcon,
} from 'lucide-react';

export interface ReportFieldDefinition {
  key: string;
  label: string;
  kind: 'text' | 'number' | 'date' | 'select';
  valueType?: 'text' | 'number' | 'date' | 'boolean';
  options?: Array<{ value: string; label: string }>;
  operators?: string[];
}

export interface ReportDomainDefinition {
  id: string;
  label: string;
  description: string;
  icon: LucideIcon;
  adminOnly?: boolean;
  fields: ReportFieldDefinition[];
  defaultColumns?: string[];
}

const yesNo = [
  { value: 'true', label: 'نعم' },
  { value: 'false', label: 'لا' },
];

const sharedStudentFields: ReportFieldDefinition[] = [
  { key: 'studentName', label: 'اسم الطالب', kind: 'text' },
  { key: 'studentCode', label: 'كود الطالب', kind: 'text' },
  { key: 'phone', label: 'رقم الهاتف', kind: 'text' },
  { key: 'grade', label: 'الصف الدراسي', kind: 'select' },
  { key: 'governorate', label: 'المحافظة', kind: 'select' },
  { key: 'teacherId', label: 'المدرس', kind: 'select' },
  { key: 'courseId', label: 'الكورس', kind: 'select' },
  { key: 'packageId', label: 'الباقة', kind: 'select' },
  { key: 'termId', label: 'الترم', kind: 'select' },
  { key: 'registeredAt', label: 'تاريخ التسجيل', kind: 'date' },
  { key: 'lastLoginAt', label: 'آخر دخول', kind: 'date' },
];

export const reportCatalog: ReportDomainDefinition[] = [
  { id: 'student-journey', label: 'رحلة الطالب', description: 'الشراء والحضور والمشاهدة والامتحانات والواجبات في تقرير واحد.', icon: Activity, fields: [...sharedStudentFields, { key: 'packageName', label: 'الكورس', kind: 'select' }, { key: 'purchaseStatus', label: 'حالة الشراء', kind: 'select' }, { key: 'attendanceStatus', label: 'الحضور', kind: 'select' }, { key: 'videoStatus', label: 'المشاهدة', kind: 'select' }, { key: 'examStatus', label: 'الامتحانات', kind: 'select' }, { key: 'homeworkStatus', label: 'الواجبات', kind: 'select' }, { key: 'lastActivityAt', label: 'آخر نشاط', kind: 'date' }], defaultColumns: ['studentName', 'packageName', 'purchaseStatus', 'attendanceStatus', 'videoStatus', 'examStatus', 'homeworkStatus', 'lastActivityAt'] },
  { id: 'students', label: 'الطلاب', description: 'التسجيل والنشاط والحالة والبيانات الأكاديمية.', icon: Users, fields: [...sharedStudentFields, { key: 'isActive', label: 'الحساب نشط', kind: 'select', options: yesNo }, { key: 'balance', label: 'الرصيد', kind: 'number' }] },
  { id: 'purchases', label: 'الشراء والمبيعات', description: 'من اشترى ومن لم يشترِ ومصدر كل عملية.', icon: BadgeDollarSign, fields: [...sharedStudentFields, { key: 'purchaseStatus', label: 'حالة الشراء', kind: 'select', options: [{ value: 'purchased', label: 'اشترى' }, { value: 'notPurchased', label: 'لم يشترِ' }, { value: 'expired', label: 'انتهت الصلاحية' }, { value: 'gift', label: 'هدية' }] }, { key: 'paymentMethod', label: 'طريقة الدفع', kind: 'select' }, { key: 'amount', label: 'قيمة الشراء', kind: 'number' }, { key: 'purchasedAt', label: 'تاريخ الشراء', kind: 'date' }] },
  { id: 'codes', label: 'الأكواد', description: 'المُصدر والمباع والمستخدم والمنتهي والمتبقي.', icon: KeyRound, fields: [...sharedStudentFields, { key: 'code', label: 'الكود', kind: 'text' }, { key: 'codeStatus', label: 'حالة الكود', kind: 'select' }, { key: 'codeType', label: 'نوع الكود', kind: 'select' }, { key: 'usedAt', label: 'وقت الاستخدام', kind: 'date' }] },
  { id: 'balance-recharge', label: 'الرصيد والشحن', description: 'الشحن والاستخدام والإثبات والمطابقة.', icon: WalletCards, fields: [...sharedStudentFields, { key: 'transactionStatus', label: 'حالة العملية', kind: 'select' }, { key: 'senderPhone', label: 'رقم المحول منه', kind: 'text' }, { key: 'amount', label: 'القيمة', kind: 'number' }, { key: 'hasProof', label: 'يوجد إثبات', kind: 'select', options: yesNo }, { key: 'createdAt', label: 'تاريخ العملية', kind: 'date' }] },
  { id: 'content', label: 'المحتوى', description: 'الكورسات والباقات والترمات والحصص والفيديوهات.', icon: BookOpenText, fields: [...sharedStudentFields, { key: 'lessonId', label: 'الحصة', kind: 'select' }, { key: 'videoId', label: 'الفيديو', kind: 'select' }, { key: 'published', label: 'منشور', kind: 'select', options: yesNo }] },
  { id: 'engagement', label: 'المشاهدة والتفاعل', description: 'البداية والإكمال والتوقف ووقت المشاهدة.', icon: Activity, fields: [...sharedStudentFields, { key: 'videoName', label: 'الفيديو', kind: 'text' }, { key: 'watchedSeconds', label: 'ثواني المشاهدة', kind: 'number' }, { key: 'watchCount', label: 'عدد المشاهدات', kind: 'number' }, { key: 'lastActivityAt', label: 'آخر نشاط', kind: 'date' }] },
  { id: 'assessments', label: 'الاختبارات والواجبات', description: 'الدرجات والنجاح والتسليم والأسئلة الصعبة.', icon: ClipboardCheck, fields: [...sharedStudentFields, { key: 'examId', label: 'الاختبار', kind: 'select' }, { key: 'homeworkId', label: 'الواجب', kind: 'select' }, { key: 'score', label: 'الدرجة', kind: 'number' }, { key: 'passed', label: 'ناجح', kind: 'select', options: yesNo }, { key: 'submittedAt', label: 'تاريخ التسليم', kind: 'date' }] },
  { id: 'teachers-finance', label: 'المدرسون والمالية', description: 'أداء المحتوى والطلاب والمبيعات والأرباح لكل مدرس.', icon: GraduationCap, adminOnly: true, fields: [{ key: 'teacherId', label: 'المدرس', kind: 'select' }, { key: 'courseId', label: 'الكورس', kind: 'select' }, { key: 'studentCount', label: 'عدد الطلاب', kind: 'number' }, { key: 'revenue', label: 'الإيراد', kind: 'number' }] },
  { id: 'staff', label: 'الاستاف', description: 'النشاط والطلبات والإنجاز ووقت الاستجابة.', icon: UserRoundCheck, adminOnly: true, fields: [{ key: 'employeeId', label: 'الموظف', kind: 'select' }, { key: 'role', label: 'الدور', kind: 'select' }, { key: 'actionType', label: 'نوع الإجراء', kind: 'select' }, { key: 'createdAt', label: 'التاريخ', kind: 'date' }] },
  { id: 'support', label: 'الدعم', description: 'المحادثات وأسباب التواصل وأوقات الحل.', icon: Headphones, adminOnly: true, fields: [{ key: 'supportUserId', label: 'موظف الدعم', kind: 'select' }, { key: 'status', label: 'الحالة', kind: 'select' }, { key: 'reason', label: 'سبب التواصل', kind: 'select' }, { key: 'responseMinutes', label: 'دقائق أول رد', kind: 'number' }, { key: 'createdAt', label: 'التاريخ', kind: 'date' }] },
  { id: 'comments-community', label: 'التعليقات والمجتمع', description: 'التفاعل والردود والمراجعة على المحتوى.', icon: MessageSquareText, fields: [...sharedStudentFields, { key: 'status', label: 'حالة التعليق', kind: 'select' }, { key: 'hasReply', label: 'تم الرد', kind: 'select', options: yesNo }, { key: 'createdAt', label: 'التاريخ', kind: 'date' }] },
  { id: 'parent-tracking', label: 'متابعة ولي الأمر', description: 'الأكواد وحالة الربط وآخر متابعة.', icon: ShieldCheck, adminOnly: true, fields: [...sharedStudentFields, { key: 'hasParentCode', label: 'لديه كود متابعة', kind: 'select', options: yesNo }, { key: 'lastParentVisitAt', label: 'آخر متابعة', kind: 'date' }] },
  { id: 'operations-security', label: 'التشغيل والأمان', description: 'الدخول والأجهزة وسجل الإجراءات الحساسة.', icon: Coins, adminOnly: true, fields: [{ key: 'actorId', label: 'منفذ الإجراء', kind: 'select' }, { key: 'action', label: 'الإجراء', kind: 'select' }, { key: 'entityType', label: 'نوع السجل', kind: 'select' }, { key: 'ipAddress', label: 'عنوان IP', kind: 'text' }, { key: 'createdAt', label: 'التاريخ', kind: 'date' }] },
];
