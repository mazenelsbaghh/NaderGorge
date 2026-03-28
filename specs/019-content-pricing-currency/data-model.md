# Content Pricing Data Model

## Modified Entities

### Term

```csharp
public class Term : BaseEntity
{
    // ... existing fields ...
    public decimal Price { get; set; } // Added field
}
```

### ContentSection

```csharp
public class ContentSection : BaseEntity
{
    // ... existing fields ...
    public decimal Price { get; set; } // Added field
}
```

### Lesson

```csharp
public class Lesson : BaseEntity
{
    // ... existing fields ...
    public decimal Price { get; set; } // Added field
}
```

## Validations

- `Price` must be a valid non-negative decimal (`>= 0`).
- No cascading business logic required on creation; price only comes into play during code-redemption processes.
