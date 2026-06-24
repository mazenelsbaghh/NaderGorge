# Data Model & DB Schema

This document details the database schema changes for the parent tracking system using Entity Framework Core and PostgreSQL.

## Entity Modifications

### 1. `StudentProfile` Entity

We modify the existing `StudentProfiles` table to hold the tracking code and modal acknowledgement state.

| Column | Type | Nullable | Index | Constraints / Default | Description |
|--------|------|----------|-------|-----------------------|-------------|
| `ParentTrackingCode` | `VARCHAR(6)` | Yes | Yes (Unique) | Uppercase, Alphanumeric | 6-character unique code to link parent. |
| `HasSeenTrackingCodePopup` | `BOOLEAN` | No | No | Default `false` | Tracks if student acknowledged the code popup. |

#### EF Core Configuration Mapping:
```csharp
builder.Entity<StudentProfile>()
    .HasIndex(s => s.ParentTrackingCode)
    .IsUnique();

builder.Entity<StudentProfile>()
    .Property(s => s.HasSeenTrackingCodePopup)
    .HasDefaultValue(false);
```

---

## New Entities

### 2. `ParentDeviceToken` Entity

A new table `ParentDeviceTokens` will store Firebase Cloud Messaging (FCM) tokens for parents associated with specific students.

| Column | Type | Nullable | Index | Key | Constraints / Default | Description |
|--------|------|----------|-------|-----|-----------------------|-------------|
| `Id` | `UUID` | No | Yes | Primary Key | - | Unique record identifier. |
| `StudentId` | `UUID` | No | Yes | Foreign Key | References `StudentProfiles(Id)` | The student being tracked. |
| `DeviceToken` | `TEXT` | No | No | - | - | FCM token of the parent device. |
| `Platform` | `VARCHAR(20)`| No | No | - | "android" or "ios" | Target mobile platform. |
| `CreatedAt` | `TIMESTAMPTZ`| No | No | - | Default `UtcNow` | When the device was linked. |

#### EF Core Configuration Mapping:
```csharp
public class ParentDeviceTokenConfiguration : IEntityTypeConfiguration<ParentDeviceToken>
{
    public void Configure(EntityTypeBuilder<ParentDeviceToken> builder)
    {
        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.DeviceToken)
            .IsRequired();
            
        builder.Property(t => t.Platform)
            .HasMaxLength(20)
            .IsRequired();
            
        builder.HasOne(t => t.Student)
            .WithMany()
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasIndex(t => t.StudentId);
    }
}
```
