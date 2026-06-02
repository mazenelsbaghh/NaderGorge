import { resolveMediaUrl } from '@/utils/resolve-media-url';

export interface AvatarOption {
  slug: string;
  name: string;
  category: 'math' | 'science' | 'history' | 'art' | 'egypt';
  imageUrl: string;
  info: string;
}

const RAW_AVATAR_LIST: AvatarOption[] = [
  {
    slug: 'einstein',
    name: 'أينشتاين',
    category: 'science',
    imageUrl: '/uploads/avatars/einstein.png',
    info: 'عالم فيزيائي شهير، صاحب نظرية النسبية ومن أذكى علماء التاريخ.'
  },
  {
    slug: 'ibn-sina',
    name: 'ابن سينا',
    category: 'science',
    imageUrl: '/uploads/avatars/ibn-sina.png',
    info: 'الملقب بـ "الشيخ الرئيس"، طبيب وفيلسوف مسلم صاحب كتاب "القانون في الطب".'
  },
  {
    slug: 'curie',
    name: 'ماري كوري',
    category: 'science',
    imageUrl: '/uploads/avatars/curie.png',
    info: 'أول امرأة تحصل على نوبل، واكتشفت الراديوم والبولونيوم.'
  },
  {
    slug: 'newton',
    name: 'نيوتن',
    category: 'science',
    imageUrl: '/uploads/avatars/newton.png',
    info: 'مكتشف الجاذبية الأرضية وقوانين الحركة التي أسست للفيزياء الحديثة.'
  },
  {
    slug: 'khwarizmi',
    name: 'الخوارزمي',
    category: 'math',
    imageUrl: '/uploads/avatars/khwarizmi.png',
    info: 'مؤسس علم الجبر ومخترع الخوارزميات التي هي أساس البرمجة والكمبيوتر.'
  },
  {
    slug: 'cleopatra',
    name: 'كليوباترا',
    category: 'history',
    imageUrl: '/uploads/avatars/cleopatra.png',
    info: 'ملكة مصرية قوية وذكية، حكمت مصر في العصر البطلمي باقتدار وسحر العالم.'
  },
  {
    slug: 'davinci',
    name: 'دا فينشي',
    category: 'art',
    imageUrl: '/uploads/avatars/davinci.png',
    info: 'عبقري عصر النهضة الرسام والنحات والمخترع وصاحب لوحة الموناليزا.'
  },
  {
    slug: 'pythagoras',
    name: 'فيثاغورس',
    category: 'math',
    imageUrl: '/uploads/avatars/pythagoras.png',
    info: 'عالم رياضيات وفيلسوف يوناني شهير، صاحب نظرية فيثاغورس للمثلثات.'
  },
  {
    slug: 'hypatia',
    name: 'هيباتيا',
    category: 'math',
    imageUrl: '/uploads/avatars/hypatia.png',
    info: 'عالمة رياضيات وفيلسوفة فلكية مصرية قديمة في الإسكندرية.'
  },
  {
    slug: 'tesla',
    name: 'نيكولا تسلا',
    category: 'science',
    imageUrl: '/uploads/avatars/tesla.png',
    info: 'مخترع نظام التيار المتردد والكهرباء الحديثة، وصاحب رؤى تكنولوجية عبقرية.'
  },
  {
    slug: 'lovelace',
    name: 'آدا لوفليس',
    category: 'math',
    imageUrl: '/uploads/avatars/lovelace.png',
    info: 'عالمة رياضيات إنجليزية وتعتبر أول مبرمجة كمبيوتر في التاريخ.'
  },
  {
    slug: 'galileo',
    name: 'جاليليو',
    category: 'science',
    imageUrl: '/uploads/avatars/galileo.png',
    info: 'عالم فلك دافع عن مركزية الشمس ومهد لعلم الفلك الحديث بتلسكوبه.'
  },
  {
    slug: 'muhammad-ali',
    name: 'محمد علي باشا',
    category: 'egypt',
    imageUrl: '/uploads/avatars/muhammad-ali.png',
    info: 'مؤسس مصر الحديثة وباني نهضتها الزراعية والعسكرية والتعليمية.'
  },
  {
    slug: 'nasser',
    name: 'جمال عبد الناصر',
    category: 'egypt',
    imageUrl: '/uploads/avatars/nasser.png',
    info: 'زعيم مصري وقائد ثورة يوليو، قام بتأميم قناة السويس وبناء السد العالي.'
  },
  {
    slug: 'sadat',
    name: 'أنور السادات',
    category: 'egypt',
    imageUrl: '/uploads/avatars/sadat.png',
    info: 'بطل الحرب والسلام، قاد نصر أكتوبر 1973 وحقق معاهدة السلام التاريخية.'
  },
  {
    slug: 'saad-zaghloul',
    name: 'سعد زغلول',
    category: 'egypt',
    imageUrl: '/uploads/avatars/saad-zaghloul.png',
    info: 'زعيم الأمة وقائد ثورة 1919 التاريخية التي طالبت باستقلال مصر.'
  },
  {
    slug: 'zewail',
    name: 'أحمد زويل',
    category: 'egypt',
    imageUrl: '/uploads/avatars/zewail.png',
    info: 'العالم المصري الحائز على نوبل في الكيمياء لاختراعه كاميرا الفيمتو ثانية.'
  },
  {
    slug: 'naguib-mahfouz',
    name: 'نجيب محفوظ',
    category: 'egypt',
    imageUrl: '/uploads/avatars/naguib-mahfouz.png',
    info: 'أول كاتب عربي يفوز بنوبل للآداب، وصاحب الثلاثية وروايات الحارة المصرية.'
  }
];

export const AVATAR_LIST: AvatarOption[] = RAW_AVATAR_LIST.map((avatar) => ({
  ...avatar,
  imageUrl: resolveMediaUrl(avatar.imageUrl),
}));
