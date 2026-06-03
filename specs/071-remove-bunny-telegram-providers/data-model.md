# Data Model: Removing Bunny and Telegram Video Providers

There are no new schemas or entities introduced by this feature. 

The `LessonVideo` entity's `Provider` column will be constrained to only accept:
- `"youtube"`
- `"vk"`

## Migrations
We will create a database migration that updates existing data:
```sql
UPDATE lesson_videos 
SET "Provider" = 'YouTube', "ProviderVideoId" = '2LfJcOt7Zhs'
WHERE LOWER("Provider") = 'bunny' OR LOWER("Provider") = 'telegram';
```
This ensures no invalid provider configurations remain in the database.
