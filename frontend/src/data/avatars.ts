import { resolveMediaUrl } from '@/utils/resolve-media-url';

export interface AvatarOption {
  slug: string;
  name: string;
  category: 'football' | 'science' | 'music' | 'acting';
  imageUrl: string;
  info: string;
}

const RAW_AVATAR_LIST: AvatarOption[] = [
  {
    slug: 'mohamed-salah',
    name: 'محمد صلاح',
    category: 'football',
    imageUrl: '/uploads/avatars/mohamed-salah.webp',
    info: 'ذاكر قبل الامتحان، عشان ما تدخلش اللجنة عامل ضغط عالي على المدرس.'
  },
  {
    slug: 'messi',
    name: 'ميسي',
    category: 'football',
    imageUrl: '/uploads/avatars/messi.webp',
    info: 'لو السؤال طويل، عدّي منه براحة، مش لازم تراوغ الورقة كلها.'
  },
  {
    slug: 'ronaldo',
    name: 'كريستيانو',
    category: 'football',
    imageUrl: '/uploads/avatars/ronaldo.webp',
    info: 'كرر الحل كتير، الفورمة مش بتيجي من أول “أنا هبدأ بكرة”.'
  },
  {
    slug: 'aboutrika',
    name: 'أبو تريكة',
    category: 'football',
    imageUrl: '/uploads/avatars/aboutrika.webp',
    info: 'ذاكر بنية صافية، بس النية لوحدها مش هتفتح الكتاب.'
  },
  {
    slug: 'mbappe',
    name: 'مبابي',
    category: 'football',
    imageUrl: '/uploads/avatars/mbappe.webp',
    info: 'خلّص بسرعة، بس ما تبقاش سريع لدرجة إن إجابتك تسبق تفكيرك.'
  },
  {
    slug: 'pele',
    name: 'بيليه',
    category: 'football',
    imageUrl: '/uploads/avatars/pele.webp',
    info: 'ابدأ بدري، ليلة الامتحان دي مش معسكر إعداد، دي طوارئ.'
  },
  {
    slug: 'einstein',
    name: 'أينشتاين',
    category: 'science',
    imageUrl: '/uploads/avatars/einstein.webp',
    info: 'لو مش فاهم، اسأل، العبقري مش اللي ساكت ومسرّح في المروحة.'
  },
  {
    slug: 'newton',
    name: 'نيوتن',
    category: 'science',
    imageUrl: '/uploads/avatars/newton.webp',
    info: 'لما المعلومة تقع عليك، اكتبها، مش تستنى تفاحة تانية.'
  },
  {
    slug: 'khwarizmi',
    name: 'الخوارزمي',
    category: 'science',
    imageUrl: '/uploads/avatars/khwarizmi.webp',
    info: 'قسم المنهج، عشان دماغك ما تعملش فورمات لوحدها.'
  },
  {
    slug: 'ibn-sina',
    name: 'ابن سينا',
    category: 'science',
    imageUrl: '/uploads/avatars/ibn-sina.webp',
    info: 'نام شوية، المخ لو سخن هيطلب دكتور، والدكتور أنا مش فاضي.'
  },
  {
    slug: 'curie',
    name: 'ماري كوري',
    category: 'science',
    imageUrl: '/uploads/avatars/curie.webp',
    info: 'جرّب تحل، التجارب مش معمولة للمعمل بس ولا للأكل المحروق.'
  },
  {
    slug: 'zewail',
    name: 'أحمد زويل',
    category: 'science',
    imageUrl: '/uploads/avatars/zewail.webp',
    info: 'الوقت بيطير، الحقه قبل ما يقولك “أنا نازل أجيب حاجة وراجع”.'
  },
  {
    slug: 'amr-diab',
    name: 'عمرو دياب',
    category: 'music',
    imageUrl: '/uploads/avatars/amr-diab.webp',
    info: 'اعمل بلاي ليست مذاكرة، بس بلاش كل شوية تقلبها حفلة الساحل.'
  },
  {
    slug: 'tamer-hosny',
    name: 'تامر حسني',
    category: 'music',
    imageUrl: '/uploads/avatars/tamer-hosny.webp',
    info: 'حفّز نفسك، بس التحفيز من غير مذاكرة اسمه إعلان مش نجاح.'
  },
  {
    slug: 'sherine',
    name: 'شيرين',
    category: 'music',
    imageUrl: '/uploads/avatars/sherine.webp',
    info: 'لو المادة نكدية، افتح لها الكتاب، مش البلاي ليست الحزينة.'
  },
  {
    slug: 'angham',
    name: 'أنغام',
    category: 'music',
    imageUrl: '/uploads/avatars/angham.webp',
    info: 'ذاكر بهدوء، الصريخ على السؤال مش هيخليه يحل نفسه.'
  },
  {
    slug: 'om-kalthoum',
    name: 'أم كلثوم',
    category: 'music',
    imageUrl: '/uploads/avatars/om-kalthoum.webp',
    info: 'قسم المذاكرة فقرات، بس متخليش أول درس “أنت عمري”.'
  },
  {
    slug: 'abdel-halim',
    name: 'عبد الحليم',
    category: 'music',
    imageUrl: '/uploads/avatars/abdel-halim.webp',
    info: 'لو غلطت، ما تغنيش “جانا الهوى”، امسح وكمل.'
  },
  {
    slug: 'adel-emam',
    name: 'عادل إمام',
    category: 'acting',
    imageUrl: '/uploads/avatars/adel-emam.webp',
    info: 'ادخل بثقة، بس لو مذاكرتش بلاش تعمل فيها “أنا فاهم كل حاجة”.'
  },
  {
    slug: 'ahmed-helmy',
    name: 'أحمد حلمي',
    category: 'acting',
    imageUrl: '/uploads/avatars/ahmed-helmy.webp',
    info: 'افتكر المعلومة بقصة، بس متخليش القصة أطول من المنهج.'
  },
  {
    slug: 'mohamed-henedy',
    name: 'محمد هنيدي',
    category: 'acting',
    imageUrl: '/uploads/avatars/mohamed-henedy.webp',
    info: 'خد بريك، بس لو البريك طول يبقى أنت فتحت فرع كافيه.'
  },
  {
    slug: 'karim-abdelaziz',
    name: 'كريم عبد العزيز',
    category: 'acting',
    imageUrl: '/uploads/avatars/karim-abdelaziz.webp',
    info: 'الامتحان مهمة، ادخل هادي، واقفل باب التوتر بالمفتاح.'
  },
  {
    slug: 'ahmed-mekky',
    name: 'أحمد مكي',
    category: 'acting',
    imageUrl: '/uploads/avatars/ahmed-mekky.webp',
    info: 'لو الدرس كبير أوي، قطعه صغير أوي، وهتحله جامد أوي.'
  },
  {
    slug: 'donia-samir-ghanem',
    name: 'دنيا سمير غانم',
    category: 'acting',
    imageUrl: '/uploads/avatars/donia-samir-ghanem.webp',
    info: 'ذاكر بأكتر من طريقة، بس بلاش تحول الدرس لبرنامج مواهب.'
  },
  {
    slug: 'samir-ghanem',
    name: 'سمير غانم',
    category: 'acting',
    imageUrl: '/uploads/avatars/samir-ghanem.webp',
    info: 'اضحك شوية، بس خلي إجابتك هي اللي تعمل الشو في الآخر.'
  }
];

export const AVATAR_LIST: AvatarOption[] = RAW_AVATAR_LIST.map((avatar) => ({
  ...avatar,
  imageUrl: resolveMediaUrl(avatar.imageUrl),
}));
