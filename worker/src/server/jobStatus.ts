const SAFE_FAILURE_MESSAGES = new Set([
  'تعذر تحليل فيديو YouTube مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
  'عاد مزود تحليل YouTube باستجابة فارغة. ستتم إعادة المحاولة تلقائيًا.',
  'إعداد مزود الذكاء الاصطناعي لا يسمح بتحليل روابط YouTube.',
  'تعذر قراءة فيديو YouTube. تأكد أنه عام ومتاح وليس خاصًا أو غير مدرج.',
  'تعذر إكمال تحليل الفيديو مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
  'رفض مزود الذكاء الاصطناعي طلب تحليل الفيديو. راجع إتاحة الفيديو وإعدادات المزود.',
  'تعذر الوصول إلى مصدر الفيديو. تأكد أن الفيديو متاح وأن الرابط صحيح.',
  'تعذر تنزيل الفيديو مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
  'تعذر تنزيل الفيديو من مزود المحتوى مؤقتًا. ستتم إعادة المحاولة تلقائيًا.',
  'انتهى تنزيل الفيديو دون إنتاج ملف صوتي. ستتم إعادة المحاولة تلقائيًا.',
  'تم تنزيل الفيديو لكن تعذر تجهيز مساره الصوتي.',
  'حدث خطأ داخلي أثناء تحليل الفيديو. تواصل مع الدعم قبل إعادة المحاولة.',
]);

const GENERIC_FAILURE_MESSAGE = 'تعذر إكمال المهمة. أعد المحاولة أو تواصل مع الدعم.';

export function publicFailedJobReason(failedReason: unknown) {
  const candidate = typeof failedReason === 'string' ? failedReason.trim() : '';
  return SAFE_FAILURE_MESSAGES.has(candidate) ? candidate : GENERIC_FAILURE_MESSAGE;
}

export function publicJobFailureReason(failedReason: unknown, state: string) {
  if (state !== 'failed') return null;
  return publicFailedJobReason(failedReason);
}
