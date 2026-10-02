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
    string? AvatarSlug,
    string DefaultLightPaletteId,
    string DefaultDarkPaletteId,
    IReadOnlyList<StudentThemePaletteOptionDto> AvailableLightPalettes,
    IReadOnlyList<StudentThemePaletteOptionDto> AvailableDarkPalettes
);

public static class StudentThemeCatalog
{
    public const string DefaultLightPaletteId = "massar-light";
    public const string DefaultDarkPaletteId = "massar-dark";

    private static readonly IReadOnlyList<StudentThemePaletteOptionDto> LightPalettes =
    [
        new("massar-light", "مسار نهاري", "light", "#0A1D3D"),
        new("scholar-light", "رمادي هادئ", "light", "#475569"),
        new("oasis-light", "واحة هادئة", "light", "#1e6d5f"),
        new("ruby-light", "نحاس وردي", "light", "#904847"),
        new("blossom-light", "زهر الربيع", "light", "#ad1457"),
        new("winter-sky-light", "سماء شتوية", "light", "#475569"),
    ];

    private static readonly IReadOnlyList<StudentThemePaletteOptionDto> DarkPalettes =
    [
        new("massar-dark", "مسار ليلي", "dark", "#26a8a2"),
        new("scholar-dark", "رمادي ليلي", "dark", "#94a3b8"),
        new("midnight-teal", "تركواز ليلي", "dark", "#58c8b8"),
        new("ember-dark", "عنبر دافئ", "dark", "#e59a5d"),
        new("rainy-night", "أزرق ليلي", "dark", "#8cb6ed"),
    ];

    public static IReadOnlyList<StudentThemePaletteOptionDto> GetLightPalettes() => LightPalettes;

    public static IReadOnlyList<StudentThemePaletteOptionDto> GetDarkPalettes() => DarkPalettes;

    public static bool IsValidLightPalette(string paletteId)
        => LightPalettes.Any(p => string.Equals(p.Id, paletteId, StringComparison.Ordinal));

    public static bool IsValidDarkPalette(string paletteId)
        => DarkPalettes.Any(p => string.Equals(p.Id, paletteId, StringComparison.Ordinal));

    public static StudentThemePreferencesDto BuildPreferences(
        string? lightPaletteId,
        string? darkPaletteId,
        string? currentMode,
        string? avatarSlug)
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
            AvatarSlug: avatarSlug,
            DefaultLightPaletteId: DefaultLightPaletteId,
            DefaultDarkPaletteId: DefaultDarkPaletteId,
            AvailableLightPalettes: GetLightPalettes(),
            AvailableDarkPalettes: GetDarkPalettes()
        );
    }
}
