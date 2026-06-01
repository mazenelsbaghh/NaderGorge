# Data Model Changes: Student & Teacher AI-Generated Avatars

## Entity Modifications

### StudentProfile (NaderGorge.Domain.Entities)

We add a nullable string property `AvatarSlug` to the existing `StudentProfile` class:

```csharp
public class StudentProfile : BaseEntity
{
    // ... other properties ...

    public string? AvatarSlug { get; set; }
}
```

## Migration Impact

- **Table**: `StudentProfiles`
- **New Column**: `AvatarSlug` (Type: `text`, Nullable: `true`)
- **Action**: Add migration `AddStudentAvatarSlug` and run database update.
