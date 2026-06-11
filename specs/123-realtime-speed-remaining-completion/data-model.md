# Data Model: Real-time Speed Remaining Completion

This document outlines the schema modifications required for performance logging.

## 1. WebVitalsMetric Entity

A new entity `WebVitalsMetric` will be added to `NaderGorge.Domain` under `Entities/` to store Web Vitals metrics reported by the frontend.

### Schema Definition (C#)

```csharp
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class WebVitalsMetric : BaseEntity
{
    public string MetricName { get; set; } = string.Empty; // LCP, CLS, INP, FID, FCP, TTFB
    public double Value { get; set; }
    public string Rating { get; set; } = string.Empty; // good, needs-improvement, poor
    public string PageUrl { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}
```

---

## 2. Infrastructure & Database Configuration

- Update `IAppDbContext` to include `DbSet<WebVitalsMetric> WebVitalsMetrics { get; }`.
- Update `AppDbContext` to implement the DbSet and configure the entity mapping.

### Entity Configuration in DbContext

```csharp
modelBuilder.Entity<WebVitalsMetric>(e =>
{
    e.HasKey(x => x.Id);
    e.Property(x => x.MetricName).HasMaxLength(32).IsRequired();
    e.Property(x => x.Rating).HasMaxLength(32).IsRequired();
    e.Property(x => x.PageUrl).HasMaxLength(512).IsRequired();
    e.Property(x => x.UserAgent).HasMaxLength(512).IsRequired();
    e.Property(x => x.CreatedAt).IsRequired();
});
```

---

## 3. Database Migration Plan

An Entity Framework Core migration must be generated to create the table:
- **Migration Name**: `AddWebVitalsMetricsTable`
- **Command**: `make migrate-add NAME=AddWebVitalsMetricsTable`
