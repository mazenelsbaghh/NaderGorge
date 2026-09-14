import type {
  AdminRechargeMatchDiagnosisDto,
  AdminRechargeRequestDto,
  RechargeMatchDiagnosisCode,
} from '@/services/wallet-service';

export type RechargeMatchDiagnosisTone = 'teal' | 'amber' | 'rose' | 'neutral';

export interface RechargeMatchDiagnosisPresentation {
  code: RechargeMatchDiagnosisCode | 'Unavailable' | 'Unknown';
  tone: RechargeMatchDiagnosisTone;
  title: string;
  detail: string;
}

type DiagnosisCopy = Omit<RechargeMatchDiagnosisPresentation, 'code'>;
type DiagnosisCopyBuilder = (
  request: AdminRechargeRequestDto,
  diagnosis: AdminRechargeMatchDiagnosisDto,
) => DiagnosisCopy;

const formatAmount = (amount?: number | null) =>
  amount == null
    ? 'مبلغ غير معروف'
    : `${amount.toLocaleString('ar-EG-u-nu-latn')} ج.م`;

const formatRechargeTimeDifference = (minutes: number) => {
  const absoluteMinutes = Math.abs(Math.round(minutes));
  const hours = Math.floor(absoluteMinutes / 60);
  const remainingMinutes = absoluteMinutes % 60;
  const parts: string[] = [];
  if (hours > 0) parts.push(`${hours.toLocaleString('ar-EG-u-nu-latn')} س`);
  if (remainingMinutes > 0 || hours === 0) {
    parts.push(`${remainingMinutes.toLocaleString('ar-EG-u-nu-latn')} د`);
  }
  return parts.join(' و');
};

const diagnosisCopy = {
  AwaitingEvidence: (request) => {
    const missing = [
      !request.screenshotUrl ? 'صورة الإثبات' : null,
      !request.senderPhoneNumber ? 'رقم المحول' : null,
    ].filter(Boolean).join(' و');
    return {
      tone: 'neutral',
      title: 'بيانات الطلب غير مكتملة',
      detail: `بانتظار ${missing || 'استكمال بيانات التحويل'} قبل بدء المطابقة.`,
    };
  },
  MissingTeacherScope: () => ({
    tone: 'rose',
    title: 'نطاق الرصيد غير محدد',
    detail: 'الطلب غير مرتبط بمدرس، لذلك لا يمكن إضافة الرصيد آليًا.',
  }),
  EligibleWaiting: () => ({
    tone: 'teal',
    title: 'مؤهل للمطابقة الآلية',
    detail: 'الرقم والمبلغ والوقت متطابقة. يعيد النظام الفحص كل 10 دقائق.',
  }),
  MultipleExactSms: (_request, diagnosis) => ({
    tone: 'amber',
    title: 'أكثر من رسالة مطابقة',
    detail: `وجدنا ${diagnosis.exactSmsCount.toLocaleString('ar-EG-u-nu-latn')} رسائل بنفس الرقم والمبلغ داخل المدة. اختر الصحيحة يدويًا.`,
  }),
  CompetingPendingRequests: (_request, diagnosis) => ({
    tone: 'rose',
    title: 'أكثر من طلب لنفس التحويل',
    detail: `الرسالة تطابق ${diagnosis.competingRequestCount.toLocaleString('ar-EG-u-nu-latn')} طلبات معلقة، لذلك توقف القبول الآلي.`,
  }),
  SmsClaimedByAnotherRequest: () => ({
    tone: 'rose',
    title: 'التحويل مرتبط بطلب آخر',
    detail: 'الرقم والمبلغ والوقت متطابقة، لكن رسالة التحويل استُخدمت بالفعل.',
  }),
  OutsideWindow: (_request, diagnosis) => {
    const offset = diagnosis.candidate?.timeOffsetMinutes ?? 0;
    const direction = offset < 0 ? 'قبل الوقت المعتمد للمطابقة' : 'بعد الوقت المعتمد للمطابقة';
    return {
      tone: 'amber',
      title: 'مطابق لكن خارج المدة',
      detail: `وصلت الرسالة ${direction} بـ${formatRechargeTimeDifference(offset)}. المسموح ساعتان قبل أو بعد.`,
    };
  },
  AmountMismatch: (request, diagnosis) => ({
    tone: 'amber',
    title: 'المبلغ مختلف',
    detail: `الرقم والوقت متطابقان، لكن الطلب ${formatAmount(request.amount)} والرسالة ${formatAmount(diagnosis.candidate?.amount)}.`,
  }),
  PhoneMismatch: (_request, diagnosis) => {
    const candidate = diagnosis.candidate;
    if (candidate?.hasSingleDigitMismatchPattern) {
      const before = candidate.matchingDigitsBeforeMismatch.toLocaleString('ar-EG-u-nu-latn');
      const after = candidate.matchingDigitsAfterMismatch.toLocaleString('ar-EG-u-nu-latn');
      return {
        tone: 'amber',
        title: 'رقم قريب — خطأ محتمل في رقم واحد',
        detail: `المبلغ والوقت متطابقان. يوجد ${before} أرقام صحيحة، ثم رقم مختلف، ثم ${after} أرقام صحيحة. يحتاج تأكيدًا يدويًا.`,
      };
    }

    return {
      tone: 'amber',
      title: 'رقم المحول قريب لكنه غير مطابق',
      detail: `المبلغ والوقت متطابقان، وأقرب رقم يشارك ${candidate?.matchingDigits.toLocaleString('ar-EG-u-nu-latn') ?? '0'} أرقام متتابعة. يحتاج تأكيدًا يدويًا.`,
    };
  },
  NoCandidate: () => ({
    tone: 'neutral',
    title: 'لا توجد رسالة مطابقة حتى الآن',
    detail: 'إذا كان هاتف المحفظة متصلًا ومحدّثًا وصلاحية الرسائل مفعلة، فسيحاول مزامنة الرسائل الحديثة تلقائيًا. هذه الحالة لا تثبت وصول التحويل؛ راجع الرسالة والمحفظة يدويًا.',
  }),
} satisfies Record<RechargeMatchDiagnosisCode, DiagnosisCopyBuilder>;

export const describeRechargeMatchDiagnosis = (
  request: AdminRechargeRequestDto,
): RechargeMatchDiagnosisPresentation | null => {
  const diagnosis = request.matchDiagnosis;
  if (!diagnosis) {
    const isPending = request.status === 0
      || (typeof request.status === 'string' && request.status.toLowerCase() === 'pending');
    return isPending
      ? {
        code: 'Unavailable',
        tone: 'neutral',
        title: 'التشخيص غير متاح مؤقتًا',
        detail: 'بانتظار وصول بيانات المطابقة من الخادم. راجع الطلب يدويًا حتى يتم التحديث.',
      }
      : null;
  }

  const builder = (diagnosisCopy as Partial<Record<string, DiagnosisCopyBuilder>>)[String(diagnosis.code)];
  if (!builder) {
    return {
      code: 'Unknown',
      tone: 'neutral',
      title: 'حالة مطابقة جديدة',
      detail: 'تعذر شرح هذه الحالة في النسخة الحالية. حدّث الصفحة أو راجع الطلب يدويًا.',
    };
  }

  return { code: diagnosis.code, ...builder(request, diagnosis) };
};
