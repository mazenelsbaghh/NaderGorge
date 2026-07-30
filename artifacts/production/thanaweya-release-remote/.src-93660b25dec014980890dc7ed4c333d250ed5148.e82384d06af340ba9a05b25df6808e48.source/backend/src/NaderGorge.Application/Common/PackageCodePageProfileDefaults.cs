namespace NaderGorge.Application.Common;

public sealed record PackageCodePageProfileDefaultContent(
    string HeroEyebrow,
    string HeroTitle,
    string HeroDescription,
    string OfferTitle,
    string OfferDescription,
    string ActivationTitle,
    string ActivationDescription,
    string SupportTitle,
    string SupportDescription,
    string ThemeAccentKey
);

public static class PackageCodePageProfileDefaults
{
    public static readonly string[] ThemeAccentKeys =
    [
        "default-gold",
        "physics-gold",
        "emerald-accent",
        "ocean-accent",
    ];

    public static PackageCodePageProfileDefaultContent Build(string packageName, string packageDescription, decimal packagePrice)
    {
        var safePackageName = string.IsNullOrWhiteSpace(packageName) ? "هذه الباقة" : packageName.Trim();
        var safeDescription = string.IsNullOrWhiteSpace(packageDescription)
            ? "فعّل الكود الخاص بهذه الباقة لتظهر داخل حسابك وتبدأ الدراسة مباشرة."
            : packageDescription.Trim();

        return new PackageCodePageProfileDefaultContent(
            HeroEyebrow: "تفعيل الوصول",
            HeroTitle: $"فعّل {safePackageName}",
            HeroDescription: $"{safeDescription} ستجد هنا كل ما تحتاجه لتفعيل الوصول بشكل واضح وسريع.",
            OfferTitle: "ماذا ستحصل بعد التفعيل؟",
            OfferDescription: packagePrice > 0
                ? $"بمجرد نجاح التفعيل ستتم إضافة {safePackageName} إلى حسابك مع كل محتواها وسعرها الحالي {packagePrice:0.##} جنيها."
                : $"بمجرد نجاح التفعيل ستتم إضافة {safePackageName} إلى حسابك وتبدأ الدراسة فورًا.",
            ActivationTitle: "أدخل كود التفعيل",
            ActivationDescription: "اكتب الكود كما وصلك وسيتم التحقق منه وربط الوصول بحسابك الحالي مباشرة.",
            SupportTitle: "تحتاج مساعدة؟",
            SupportDescription: "إذا واجهت مشكلة في الكود أو ظهرت بيانات ناقصة، أكمل المطلوب ثم أعد المحاولة أو تواصل مع الإدارة.",
            ThemeAccentKey: "default-gold"
        );
    }
}
