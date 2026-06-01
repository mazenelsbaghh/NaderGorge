# Data Model: Google Drive Custom Player

*No new database tables or columns are required for this feature.*

The existing `LessonVideo` table will continue to store Google Drive videos with `provider = 'google_drive'` and `provider_video_id` holding the extracted Google Drive File ID.

```javascript
{
  provider: 'google_drive',
  providerVideoId: '1ZXxx-yYYzz...' // The Google Drive File ID
}
```
