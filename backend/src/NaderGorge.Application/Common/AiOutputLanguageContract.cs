using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Common;

internal static class AiOutputLanguageContract
{
    public static string ToWorkerCode(AiOutputLanguage language) => language switch
    {
        AiOutputLanguage.Auto => "auto",
        AiOutputLanguage.Arabic => "ar",
        AiOutputLanguage.English => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported AI output language.")
    };
}
