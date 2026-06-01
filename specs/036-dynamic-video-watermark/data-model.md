# Data Model: Dynamic Video Watermark

## Modified Entities & DTOs

No database schema changes or EF Core migrations are required for this feature. The student's name and phone number already exist in the `User` or `StudentProfile` entities.

### 1. `IVideoEncryptionService` Payload Schema
The `_encryption.EncryptVideoInfo` AES-256 payload schema needs to be updated to include student identity data so the server-side proxy `/api/video/embed` can securely read it.

```json
{
  "Provider": "youtube|telegram",
  "VideoId": "12345",
  "StudentName": "Mazen Elsbagh",
  "StudentPhone": "0100000000"
}
```

### 2. Next.js Decrypted Type
In `frontend/src/app/api/video/embed/route.ts`, the decrypted token parsing interface must be updated:
```typescript
interface DecryptedToken {
  Provider: string;
  VideoId: string;
  StudentName: string;
  StudentPhone: string;
}
```
