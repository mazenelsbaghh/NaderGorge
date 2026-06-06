# Data Model: Roles & Settings Schema Updates

## 1. Role Table Extension

We modify the existing `Role` entity (`roles` table) to add a text column that stores a serialized JSON list of permissions.

```csharp
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public RoleType Type { get; set; }
    
    // New Property
    public string? PermissionsJson { get; set; } = "[]"; // Serialized JSON array: ["users.manage", "content.manage"]

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
```

### EF Core Mapping Configuration
In `AppDbContext.OnModelCreating`:
```csharp
modelBuilder.Entity<Role>(e =>
{
    e.ToTable("roles");
    e.HasKey(r => r.Id);
    e.HasIndex(r => r.Name).IsUnique();
    e.Property(r => r.Name).HasMaxLength(50).IsRequired();
    e.Property(r => r.PermissionsJson).HasMaxLength(4000).HasDefaultValue("[]");
});
```

## 2. PlatformSettings Table Keys

New dynamic settings key-value entries will be inserted/updated in the existing `PlatformSettings` table:

| Key | Default Value | Description |
|-----|---------------|-------------|
| `PlatformName` | `منصة نادر جورج` | Name of the platform displayed on student pages |
| `SupportPhoneNumber` | `01000000000` | Contact phone for student support |
| `SupportWhatsAppUrl` | `https://wa.me/201000000000` | Support WhatsApp link |
| `YouTubeChannelUrl` | `https://youtube.com` | Official YouTube channel |
| `TelegramChannelUrl` | `https://t.me` | Official Telegram channel |
| `MaxActiveDevicesPerStudent` | `2` | Number of concurrent devices allowed for logins |
| `EnableWatermark` | `true` | Toggles floating watermarks on videos |
| `WatermarkOpacity` | `0.15` | Opacity percentage of floating video watermark |
| `MaintenanceMode` | `false` | Block student dashboard paths |
| `MaintenanceMessage` | `المنصة في أعمال الصيانة حالياً، سنعود قريباً.` | Message displayed to blocked students |
