// These authored lessons are isolated preview fixtures, never generation output.
export const lessons = [
  {
    id: 'rocks',
    title: 'رحلة الصخر',
    subtitle: 'من الجبل إلى قاع البحر',
    story:
      'اختفت قطع من متحف الأرض، والجسر اتفكك والآلة وقفت. ساعدني نرجّع القطع، ونفتح قاعة الاكتشاف من جديد!',
    missions: [
      {
        title: 'جسر التحوّل',
        concept: 'التجوية والتعرية والترسيب',
        type: 'order',
        icon: '◎',
        reward: 'بوصلة الرحلة',
        instruction:
          'رتّب رحلة الفتات الصخري. اختار الخطوة الأولى، ثم اللي بعدها، علشان نركّب الجسر.',
        items: [
          'يتفتت الصخر بالتجوية',
          'ينتقل الفتات بالتعرية',
          'يستقر الفتات بالترسيب',
        ],
        hints: [
          'قبل ما ننقل حاجة، لازم تكون اتفتّتت.',
          'ابدأ بالتجوية، ثم النقل، وأخيرًا الاستقرار.',
        ],
        recap: [
          'التجوية تفتت الصخر أو تحلله في مكانه.',
          'التعرية تنقل نواتج التجوية بالماء أو الرياح أو الجليد.',
          'الترسيب يحدث عندما تقل قدرة عامل النقل على حمل الفتات.',
        ],
        time: '02:10 – 03:05',
        success:
          'الجسر رجع متصل! ميم يقدر يوصل لآلة التحوّل، والبوصلة بقت في حقيبتك.',
      },
      {
        title: 'آلة الأسباب',
        concept: 'تكوّن الصخور',
        type: 'match',
        icon: '⚙',
        reward: 'ترس التحوّل',
        instruction: 'وصّل كل سبب بنتيجته. كل وصلة صحيحة تشغّل جزء من الآلة.',
        pairs: [
          ['تبريد الصهارة وتصلّبها', 'تكوّن صخر ناري'],
          ['تماسك وتلاحم الرواسب', 'تكوّن صخر رسوبي'],
          ['حرارة وضغط دون انصهار', 'تكوّن صخر متحوّل'],
        ],
        hints: [
          'الصخر الناري يبدأ من مادة منصهرة.',
          'الرواسب تصبح رسوبية؛ والحرارة والضغط دون انصهار ينتجان صخرًا متحوّلًا.',
        ],
        recap: [
          'الصهارة عندما تبرد وتتصلب تكوّن صخورًا نارية.',
          'الرواسب قد تتماسك وتتلاحم لتكوّن صخورًا رسوبية.',
          'الحرارة والضغط يغيّران الصخر دون صهره، فتتكوّن صخور متحوّلة.',
        ],
        time: '05:20 – 06:40',
        success:
          'الآلة اشتغلت والنور رجع! جمعت ترس التحوّل، وباقي نرتّب المعرض.',
      },
      {
        title: 'معرض الصخور',
        concept: 'تصنيف الصخور',
        type: 'sort',
        icon: '◈',
        reward: 'بلورة المستكشف',
        instruction: 'اختار نوع كل عيّنة علشان ترجع لمكانها الصحيح في المعرض.',
        categories: ['ناري', 'رسوبي', 'متحوّل'],
        items: ['الجرانيت', 'الحجر الرملي', 'الرخام'],
        answers: [0, 1, 2],
        hints: [
          'الجرانيت يتكوّن بتبريد الصهارة ببطء.',
          'الحجر الرملي رسوبي، والرخام ينتج من تحوّل الحجر الجيري.',
        ],
        recap: [
          'الجرانيت مثال للصخور النارية الجوفية.',
          'الحجر الرملي مثال للصخور الرسوبية الفتاتية.',
          'الرخام صخر متحوّل، أصله الحجر الجيري.',
        ],
        time: '08:15 – 09:20',
        success:
          'العينات رجعت للمعرض! معاك البلورة، وباب القاعة الأخيرة جاهز لتحدّي الاكتشاف.',
      },
    ],
    final: [
      {
        type: 'order',
        concept: 'رحلة الفتات',
        instruction: 'أعد بناء رحلة حبة رمل لتشغيل المفتاح الأول.',
        items: [
          'تفتت صخر الجبل',
          'حمل النهر للفتات',
          'استقرار الرمل عند المصب',
        ],
      },
      {
        type: 'match',
        concept: 'السبب والنتيجة',
        instruction: 'وصّل التحوّل بنتيجته لتشغيل المفتاح الثاني.',
        pairs: [
          ['صهارة تبرد', 'صخر ناري'],
          ['رواسب تتلاحم', 'صخر رسوبي'],
        ],
      },
      {
        type: 'sort',
        concept: 'تطبيق التصنيف',
        instruction: 'حط العينات في مكانها لتفتح القاعة.',
        categories: ['ناري', 'رسوبي', 'متحوّل'],
        items: ['الرخام', 'الجرانيت', 'الحجر الرملي'],
        answers: [2, 0, 1],
      },
    ],
  },
  {
    id: 'water',
    title: 'رحلة قطرة',
    subtitle: 'الماء في حركة مستمرة',
    story:
      'وصلنا لجناح المياه! هنرجّع مسار القطرة، ونشغّل آلة المطر، ونجمع قطع جديدة تكمّل مجموعتك.',
    missions: [
      {
        title: 'جسر السحاب',
        concept: 'مراحل دورة الماء',
        type: 'order',
        icon: '◎',
        reward: 'بوصلة السحاب',
        instruction: 'ابدأ بماء البحر ورتّب رحلته حتى ينزل مطرًا.',
        items: [
          'يتبخر ماء البحر',
          'يتكاثف البخار في السحب',
          'تهطل قطرات المطر',
        ],
        hints: [
          'حرارة الشمس تبدأ الرحلة.',
          'يتبخر الماء، ثم يتكاثف البخار، ثم يحدث الهطول.',
        ],
        recap: [
          'التبخر يحوّل الماء السائل إلى بخار.',
          'تبريد البخار يسمح بتكاثفه إلى قطرات دقيقة.',
          'الهطول يعيد الماء من السحب إلى سطح الأرض.',
        ],
        time: '01:00 – 02:15',
        success: 'جسر السحاب اتصل، وبوصلة جديدة انضمت لمقتنياتك!',
      },
      {
        title: 'آلة المطر',
        concept: 'تغيّر حالة الماء',
        type: 'match',
        icon: '⚙',
        reward: 'ترس المطر',
        instruction: 'وصّل كل تغير بنتيجته علشان تشغّل آلة المطر.',
        pairs: [
          ['تسخين الماء وتبخره', 'بخار ماء'],
          ['تكاثف بخار الماء', 'قطرات سائلة'],
          ['تجمّد الماء', 'جليد'],
        ],
        hints: [
          'فكّر في حالة الماء بعد كل تغيّر.',
          'التبخر ينتج بخارًا، والتكاثف سائلًا، والتجمّد جليدًا.',
        ],
        recap: [
          'التبخر انتقال من السائل إلى الغاز.',
          'التكاثف انتقال من الغاز إلى السائل.',
          'التجمد انتقال من السائل إلى الصلب.',
        ],
        time: '03:10 – 04:00',
        success: 'آلة المطر اشتغلت! استعد لترتيب معرض حالات الماء.',
      },
      {
        title: 'معرض الماء',
        concept: 'حالات الماء',
        type: 'sort',
        icon: '◈',
        reward: 'بلورة القطرة',
        instruction: 'صنّف كل مثال حسب حالته.',
        categories: ['صلب', 'سائل', 'غاز'],
        items: ['مكعب ثلج', 'ماء النهر', 'بخار الماء غير المرئي'],
        answers: [0, 1, 2],
        hints: [
          'مكعب الثلج له شكل ثابت.',
          'ماء النهر سائل، وبخار الماء غاز غير مرئي.',
        ],
        recap: [
          'الثلج ماء في الحالة الصلبة.',
          'ماء النهر في الحالة السائلة.',
          'بخار الماء غاز غير مرئي؛ السحب تحتوي قطرات أو بلورات دقيقة.',
        ],
        time: '06:00 – 07:05',
        success: 'المعرض اكتمل! قطع الجناحين محفوظة مع بعض في حقيبتك.',
      },
    ],
    final: [
      {
        type: 'order',
        concept: 'مسار القطرة',
        instruction: 'رتّب رحلة القطرة لتشغيل المفتاح الأول.',
        items: ['تبخر من سطح البحر', 'تكاثف في السحاب', 'هطول على الأرض'],
      },
      {
        type: 'match',
        concept: 'تغيّر الحالة',
        instruction: 'شغّل المفتاح الثاني بالوصلات الصحيحة.',
        pairs: [
          ['التجمّد', 'سائل إلى صلب'],
          ['التكاثف', 'غاز إلى سائل'],
        ],
      },
      {
        type: 'sort',
        concept: 'حالات الماء',
        instruction: 'صنّف العينات لفتح القاعة.',
        categories: ['صلب', 'سائل', 'غاز'],
        items: ['بخار الماء', 'ثلج', 'قطرة مطر'],
        answers: [2, 0, 1],
      },
    ],
  },
];

export function newLessonProgress() {
  return {
    completed: 0,
    finalStep: 0,
    mistakes: [0, 0, 0],
    hints: [0, 0, 0],
    finalMistakes: 0,
  };
}

export function readProfile(storage, key) {
  const empty = { version: 1, lessons: {}, badge: '' };
  const raw = storage.getItem(key);
  if (!raw) return empty;
  const saved = JSON.parse(raw);
  if (
    saved?.version !== 1 ||
    typeof saved.lessons !== 'object' ||
    !saved.lessons
  )
    throw new Error('Invalid preview save');
  for (const lesson of lessons) {
    const value = saved.lessons[lesson.id];
    if (!value) continue;
    const counter = (n) => Number.isSafeInteger(n) && n >= 0;
    if (
      !counter(value.completed) ||
      value.completed > 4 ||
      !counter(value.finalStep) ||
      value.finalStep > 3 ||
      !counter(value.finalMistakes) ||
      ![value.mistakes, value.hints].every(
        (a) => Array.isArray(a) && a.length === 3 && a.every(counter)
      ) ||
      (value.completed < 3 && value.finalStep !== 0) ||
      (value.completed === 4) !== (value.finalStep === 3)
    )
      throw new Error('Invalid preview progress');
  }
  return {
    version: 1,
    lessons: saved.lessons,
    badge: typeof saved.badge === 'string' ? saved.badge : '',
  };
}

export function earnedItems(profile) {
  return lessons.flatMap((lesson) =>
    lesson.missions
      .slice(0, Math.min(profile.lessons[lesson.id]?.completed ?? 0, 3))
      .map((mission, index) => ({
        id: `${lesson.id}:${index}`,
        title: mission.reward,
        icon: mission.icon,
        lesson: lesson.title,
      }))
  );
}
