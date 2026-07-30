// Arab nationalities list — used in student registration form
// 22 Arab League member nationalities

export const ARAB_NATIONALITIES = [
  'مصري',
  'سعودي',
  'إماراتي',
  'كويتي',
  'قطري',
  'بحريني',
  'عُماني',
  'يمني',
  'أردني',
  'لبناني',
  'سوري',
  'عراقي',
  'فلسطيني',
  'ليبي',
  'تونسي',
  'جزائري',
  'مغربي',
  'موريتاني',
  'سوداني',
  'صومالي',
  'جيبوتي',
  'جزر القمر',
] as const;

export type ArabNationality = (typeof ARAB_NATIONALITIES)[number];
