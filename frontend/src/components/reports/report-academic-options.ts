export const gradeChoices: Array<[number, number, string]> = [
  [0, 0, 'الأول الثانوي'], [0, 1, 'الثاني الثانوي'], [0, 31, 'الثالث الثانوي'],
  [1, 2, 'الأول بكالوريا'], [1, 3, 'الثاني بكالوريا'],
  ...Array.from({ length: 6 }, (_, i): [number, number, string] => [2, i + 10, `الصف ${i + 1} الابتدائي`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [3, i + 20, `الصف ${i + 1} الإعدادي`]),
  ...Array.from({ length: 6 }, (_, i): [number, number, string] => [4, i + 40, `الصف ${i + 1} الابتدائي الأزهري`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [4, i + 50, `الصف ${i + 1} الإعدادي الأزهري`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [4, i + 60, `الصف ${i + 1} الثانوي الأزهري`]),
  ...Array.from({ length: 12 }, (_, i): [number, number, string] => [5, i + 70, `Grade ${i + 1}`]),
];
