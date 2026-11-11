namespace NaderGorge.Domain.Entities.Assistant;

public sealed class AssistantAppearancePreference
{
    public Guid UserId { get; set; }
    public string LightPaletteId { get; set; } = string.Empty;
    public string DarkPaletteId { get; set; } = string.Empty;
    public string CurrentMode { get; set; } = string.Empty;
}
