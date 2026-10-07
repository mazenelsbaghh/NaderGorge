import 'models.dart';

/// Money stays in integer piastres; percentages may contain a fraction.
int discountedAmount(int base, num percent) {
  if (base < 0 || !percent.isFinite || percent < 0 || percent > 100) {
    throw const CenterException('المبلغ ونسبة الخصم غير صالحين.');
  }
  // Keep existing integer snapshots bit-for-bit identical, including half cents.
  if (percent is int) return (base * (100 - percent) + 50) ~/ 100;
  return (base * (100 - percent) / 100).round();
}

/// Returns the full-precision percentage for a requested net piastre amount.
num discountPercentForAmount(int base, int target) {
  if (base <= 0 || target < 0 || target > base) {
    throw const CenterException(
      'اختر مبلغًا من صفر إلى السعر الأصلي غير الصفري.',
    );
  }
  final percent = (base - target) * 100 / base;
  final num normalized = percent == percent.round() ? percent.round() : percent;
  if (discountedAmount(base, normalized) != target) {
    throw const CenterException('تعذر حساب نسبة خصم تطابق المبلغ المطلوب.');
  }
  return normalized;
}
