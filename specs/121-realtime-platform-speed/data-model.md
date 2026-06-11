# Data Model: Real-time Platform Speed & Sync

## Database Schema (PostgreSQL)

### Entity: `OutboxEvent`
Stores events generated during business transactions, waiting to be sent via SignalR.

```sql
CREATE TABLE "OutboxEvents" (
    "Id" UUID PRIMARY KEY,
    "Type" VARCHAR(100) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "TargetGroup" VARCHAR(150) NULL,
    "TargetUserId" VARCHAR(150) NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "ProcessedAt" TIMESTAMP WITH TIME ZONE NULL,
    "RetryCount" INTEGER NOT NULL DEFAULT 0,
    "LastError" TEXT NULL
);

-- Indexes for performance
CREATE INDEX "IX_OutboxEvents_ProcessedAt_CreatedAt" ON "OutboxEvents" ("ProcessedAt", "CreatedAt") 
WHERE "ProcessedAt" IS NULL;
```

### Entity: `IdempotentRequest`
If database fallback is used for idempotency (or for audit logging), we define this schema. Otherwise, Redis holds the keys with a 10-minute TTL. For reliability, we will design a Redis-centric layout with an optional DB logging table.

#### Redis Layout for Idempotency:
- **Key Format**: `idempotency:{userId}:{idempotencyKey}`
- **Type**: Hash or String JSON.
- **Fields**:
  - `status`: "processing" | "completed"
  - `statusCode`: HTTP status code (e.g., 200, 201, 400)
  - `responseBody`: JSON response payload
- **TTL**: 600 seconds (10 minutes).

---

## Entity Relationships (EF Core mappings)

- `OutboxEvent` is mapped in `AppDbContext` via `EntityTypeConfiguration<OutboxEvent>`.
- It is a standalone table with no hard foreign keys to ensure event publishing doesn't fail due to cascade deletes or reference locks.
