namespace NaderGorge.Application.Features.Student;

public record StudentThemePaletteOptionDto(
    string Id,
    string Name,
    string Mode,
    string PreviewAccent
);

public record StudentThemePreferencesDto(
    string CurrentMode,
    string SelectedLightPaletteId,
    string SelectedDarkPaletteId,
    string DefaultLightPaletteId,
    string DefaultDarkPaletteId,
    IReadOnlyList<StudentThemePaletteOptionDto> AvailableLightPalettes,
    IReadOnlyList<StudentThemePaletteOptionDto> AvailableDarkPalettes
);

public static class StudentThemeCatalog
{
    public const string DefaultLightPaletteId = "scholar-light";
    public const string DefaultDarkPaletteId = "scholar-dark";

    private static readonly IReadOnlyList<StudentThemePaletteOptionDto> LightPalettes =
    [
        new("scholar-light", "ذهبي أكاديمي", "light", "#c79b46"),
        new("oasis-light", "واحة هادئة", "light", "#2d8f7b"),
        new("ruby-light", "نحاس وردي", "light", "#a35352"),
        new("blossom-light", "زهر الربيع", "light", "#e83e8c"),
        new("winter-sky-light", "سماء شتوية", "light", "#64748b"),
    ];

    private static readonly IReadOnlyList<StudentThemePaletteOptionDto> DarkPalettes =
    [
        new("scholar-dark", "ذهبي ليلي", "dark", "#c5a059"),
        new("midnight-teal", "تركواز ليلي", "dark", "#4bb5a6"),
        new("ember-dark", "عنبر دافئ", "dark", "#d17f49"),
        new("rainy-night", "ليلة ممطرة", "dark", "#94a3b8"),
    ];

    public static IReadOnlyList<StudentThemePaletteOptionDto> GetLightPalettes() => LightPalettes;

    public static IReadOnlyList<StudentThemePaletteOptionDto> GetDarkPalettes() => DarkPalettes;

    public static bool IsValidLightPalette(string paletteId)
        => LightPalettes.Any(p => string.Equals(p.Id, paletteId, StringComparison.Ordinal));

    public static bool IsValidDarkPalette(string paletteId)
        => DarkPalettes.Any(p => string.Equals(p.Id, paletteId, StringComparison.Ordinal));

    public static StudentThemePreferencesDto BuildPreferences(string? lightPaletteId, string? darkPaletteId, string? currentMode)
    {
        var resolvedLight = IsValidLightPalette(lightPaletteId ?? string.Empty)
            ? lightPaletteId!
            : DefaultLightPaletteId;
        var resolvedDark = IsValidDarkPalette(darkPaletteId ?? string.Empty)
            ? darkPaletteId!
            : DefaultDarkPaletteId;
        var resolvedMode = string.Equals(currentMode, "dark", StringComparison.OrdinalIgnoreCase)
            ? "dark"
            : "light";

        return new StudentThemePreferencesDto(
            CurrentMode: resolvedMode,
            SelectedLightPaletteId: resolvedLight,
            SelectedDarkPaletteId: resolvedDark,
            DefaultLightPaletteId: DefaultLightPaletteId,
            DefaultDarkPaletteId: DefaultDarkPaletteId,
            AvailableLightPalettes: GetLightPalettes(),
            AvailableDarkPalettes: GetDarkPalettes()
        );
    }
}
